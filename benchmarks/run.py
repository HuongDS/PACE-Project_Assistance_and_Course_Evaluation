"""Run PACE benchmarks offline and retain reproducible per-run artifacts."""
import argparse
import collections
import csv
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess
import sys
import tempfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
LOCAL = HERE / "local"
PRODUCTION = ("PACE.Application", "PACE.Infrastructure", "PACE.Domain", "PACE.API")
BASELINE_COMMIT = "22351f5b3cc0fdc6d17602dfbb35063d9b73b76f"
SHA = re.compile(r"[a-f0-9]{40}")


def command(*args, cwd=ROOT):
    return subprocess.check_output(args, cwd=cwd).decode().strip()


def digest(data):
    return hashlib.sha256(data).hexdigest()


def write_json(path, data):
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def git_source_hash(paths):
    names = subprocess.check_output(
        ["git", "ls-files", "-z", "-c", "-o", "--exclude-standard", "--", *paths], cwd=ROOT
    ).decode().split("\0")
    state = {name: digest((ROOT / name).read_bytes()) if (ROOT / name).is_file() else None
             for name in sorted(set(names) - {""})}
    return digest(json.dumps(state, sort_keys=True).encode())


def logged(args, log, env=None):
    log.write("$ " + subprocess.list2cmdline([str(item) for item in args]) + "\n")
    log.flush()
    with subprocess.Popen(args, cwd=ROOT, env=env, text=True, errors="replace",
                          stdout=subprocess.PIPE, stderr=subprocess.STDOUT) as process:
        for line in process.stdout:
            log.write(line)
            log.flush()
            print(line, end="", flush=True)
    if process.returncode:
        raise RuntimeError(f"{Path(args[0]).name} exited with code {process.returncode}")


def materialize(repo, commit, scope, destination):
    # Read tracked raw blobs. Git archive can honor export-ignore or export-subst.
    # Worktree checkout can run filters. Neither should change this benchmark's input.
    if not SHA.fullmatch(commit):
        raise ValueError("Invalid pinned commit")
    if scope and (scope.startswith("/") or any(part in {"", ".", ".."} for part in scope.split("/"))):
        raise ValueError("Invalid pinned directory")
    tree = f"{commit}:{scope}" if scope else f"{commit}^{{tree}}"
    git = ["git", "-C", str(repo)]
    if command(*git, "rev-parse", "--verify", commit + "^{commit}") != commit:
        raise ValueError("Unexpected snapshot revision")
    listing = subprocess.check_output([*git, "ls-tree", "-r", "-z", tree])
    blobs, skipped = [], 0
    for entry in listing.split(b"\0"):
        if not entry:
            continue
        info, name = entry.split(b"\t", 1)
        mode, kind, object_id = info.split()
        parts = os.fsdecode(name).split("/")
        if any(part.lower() in {"", ".", "..", ".git"} for part in parts) or "\\" in os.fsdecode(name):
            raise ValueError("Invalid path in snapshot")
        if kind != b"blob" or mode not in (b"100644", b"100755"):
            skipped += 1
            continue
        blobs.append((object_id, destination.joinpath(*parts)))
    with tempfile.TemporaryFile() as requests:
        requests.write(b"".join(object_id + b"\n" for object_id, _ in blobs))
        requests.seek(0)
        with subprocess.Popen([*git, "cat-file", "--batch"], stdin=requests, stdout=subprocess.PIPE) as process:
            for object_id, path in blobs:
                header = process.stdout.readline().split()
                if len(header) != 3 or header[:2] != [object_id, b"blob"]:
                    raise ValueError("Unexpected Git blob data")
                payload = process.stdout.read(int(header[2]))
                if len(payload) != int(header[2]) or process.stdout.read(1) != b"\n":
                    raise ValueError("Incomplete Git blob data")
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(payload)
            if process.wait():
                raise RuntimeError("Unable to read Git blobs")
    return {"tracked_files": len(blobs), "skipped_symlinks_or_submodules": skipped}


def table(path, rows, fields, delimiter=","):
    with path.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields, delimiter=delimiter, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)


def export_reports(output):
    summary = {}
    method_flow_path = output / "java-method-flow-raw.json"
    if method_flow_path.exists():
        rows = json.loads(method_flow_path.read_text())
        summary["java_method_flow"] = {
            "cases": len(rows),
            "exact": sum(row["correct"] for row in rows),
            "supported_cases": sum(row["expected"]["status"] == "Complete" for row in rows),
            "supported_exact": sum(row["correct"] and row["expected"]["status"] == "Complete" for row in rows),
            "unsupported_exact": sum(row["correct"] and row["expected"]["status"] == "Unsupported" for row in rows),
        }
    signature_path = output / "java-signatures-raw.json"
    if signature_path.exists():
        rows = json.loads(signature_path.read_text())["scores"]
        summary["java_signatures"] = {
            "cases": len(rows),
            "exact": sum(row["expected"] == row["observed"] for row in rows),
            "correct_targets": sum(len(set(row["expected"]) & set(row["observed"])) for row in rows),
            "missing_targets": sum(len(set(row["expected"]) - set(row["observed"])) for row in rows),
            "extra_targets": sum(len(set(row["observed"]) - set(row["expected"])) for row in rows),
        }
    jcg_path = output / "jcg-raw.json"
    if jcg_path.exists():
        rows = json.loads(jcg_path.read_text())
        eligible = [row for row in rows if row["status"] in {"scored", "parse-failure", "caller-missing"}]
        summary["jcg"] = {
            "annotation_rows": len(rows), "eligible_rows": len(eligible),
            "eligible_programs": len({row["case"] for row in eligible}),
            "status_counts": dict(collections.Counter(row["status"] for row in rows)),
            "required_found": sum(len(set(row["required"]) & set(row["observed"])) for row in eligible),
            "required_total": sum(len(row["required"]) for row in eligible),
            "prohibited_reported": sum(len(set(row["prohibited"]) & set(row["observed"])) for row in eligible),
            "prohibited_total": sum(len(row["prohibited"]) for row in eligible),
        }
    research_path = output / "research-raw.json"
    if research_path.exists():
        from research_suites import summarize
        raw = json.loads(research_path.read_text())
        research, rows = summarize(raw, json.loads((output / "research-oracles.json").read_text()))
        table(output / "research-cases.csv", rows, ["dataset", "project", "case", "expected", "status"])
        summary["research"] = research
        if any("javaMethodFlow" in repo for repo in raw):
            statuses = collections.Counter()
            reasons = collections.Counter()
            for repo in raw:
                for item in repo.get("javaMethodFlow", []):
                    statuses[item["status"]] += item["count"]
                    if item.get("reason"):
                        reasons[item["reason"]] += item["count"]
            summary["java_method_flow_coverage"] = {
                "analyzed_methods": sum(statuses.values()),
                "status_counts": dict(statuses),
                "reason_counts": dict(reasons),
            }
    write_json(output / "summary.json", summary)
    return summary


def compare_runs(previous_path, output, previous, summary):
    before = json.loads((previous_path / "summary.json").read_text())
    write_json(output / "comparison.json", {"previous_stage": previous["stage"], "before": before, "after": summary})

    def numbers(document, prefix=""):
        for key, value in document.items():
            label = f"{prefix}.{key}" if prefix else key
            if isinstance(value, dict):
                yield from numbers(value, label)
            elif isinstance(value, (int, float)) and not isinstance(value, bool):
                yield label, value

    old, new = dict(numbers(before)), dict(numbers(summary))
    rows = [{"metric": key, "before": old.get(key, 0), "after": new.get(key, 0),
             "delta": new.get(key, 0) - old.get(key, 0)} for key in sorted(old.keys() | new.keys())]
    table(output / "comparison.csv", rows, ["metric", "before", "after", "delta"])
    if (output / "jcg-raw.json").exists():
        jcg_delta = subprocess.check_output([sys.executable, str(HERE / "compare-jcg.py"),
            str(previous_path / "jcg-raw.json"), str(output / "jcg-raw.json")])
        (output / "jcg-delta.json").write_bytes(jcg_delta)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stage", required=True, help="B0, B1, B2, Final, etc.")
    parser.add_argument("--suite", choices=("public", "jcg", "research", "signatures", "java-method-flow"), default="public")
    parser.add_argument("--compare", type=Path, help="Previous run directory using identical input hashes")
    parser.add_argument("--no-restore", action="store_true", help="Use already restored NuGet dependencies")
    args = parser.parse_args()
    if not re.fullmatch(r"[A-Za-z0-9_-]+", args.stage):
        parser.error("Stage must contain only letters, digits, '-' or '_'")
    os.umask(0o077)
    started = datetime.now(timezone.utc)
    output = LOCAL / "runs" / (started.strftime("%Y%m%dT%H%M%S%fZ-") + args.stage)
    output.mkdir(parents=True)
    print(f"Run folder: {output}", flush=True)
    metadata = {"stage": args.stage, "suite": args.suite, "start_utc": started.isoformat(),
                "input_hashes": {}, "status": "failed"}
    result = 1
    with (output / "run.log").open("w", encoding="utf-8") as log, tempfile.TemporaryDirectory(prefix="pace-benchmark-") as temp:
        try:
            metadata.update({
                "pace_commit": command("git", "rev-parse", "HEAD"),
                "production_source_sha256": git_source_hash(PRODUCTION),
                "production_changes": bool(command("git", "status", "--porcelain", "--", *PRODUCTION)),
                "benchmark_harness_sha256": git_source_hash(("benchmarks", "PACE.Tests")),
                "dotnet_sdk": command("dotnet", "--version"),
                "python": platform.python_version(), "environment": platform.platform(),
            })
            if args.stage.upper() == "B0":
                baseline_diff = subprocess.run(
                    ["git", "diff", "--quiet", BASELINE_COMMIT, "--", *PRODUCTION], cwd=ROOT)
                if baseline_diff.returncode or metadata["production_changes"]:
                    raise ValueError("B0 requires production source identical to the original PACE commit.")
                metadata["baseline_reference"] = BASELINE_COMMIT
            metadata["status"] = "running"
            write_json(output / "run.json", metadata)
            env = os.environ.copy()
            env.pop("PACE_JAVA_CLASSPATH", None)
            java = shutil.which("java")
            metadata["java_version"] = subprocess.run(
                [java, "-version"], capture_output=True, text=True, check=True).stderr.splitlines()[0] if java else None
            metadata["java_classpath"] = "JDK only"
            if args.stage.upper() != "B0" and java is None:
                raise RuntimeError("Install JDK 17 or newer before running the candidate benchmark.")
            filters = []
            required = []
            if args.suite == "java-method-flow":
                fixture = ROOT / "PACE.Tests/JavaMethodFlowBenchmarkTests.cs"
                metadata["input_hashes"]["java_method_flow"] = digest(fixture.read_bytes())
                env.update(
                    PACE_JAVA_METHOD_FLOW_REPORT=str(output / "java-method-flow-raw.json"),
                    PACE_JAVA_METHOD_FLOW_BASELINE="1" if args.stage.upper() in {"B0", "B3"} else "0")
                filters.append("FullyQualifiedName~JavaMethodFlowBenchmarkTests")
                required.append("java-method-flow-raw.json")
            if args.suite == "signatures":
                fixture = HERE / "fixtures" / "java-signatures.json"
                metadata["input_hashes"]["java_signatures"] = digest(fixture.read_bytes())
                env.update(
                    PACE_JAVA_SIGNATURE_FIXTURE=str(fixture),
                    PACE_JAVA_SIGNATURE_REPORT=str(output / "java-signatures-raw.json"),
                    PACE_JAVA_SIGNATURE_BASELINE="1" if args.stage.upper() == "B0" else "0")
                filters.append("FullyQualifiedName~JavaSignatureBenchmarkTests")
                required.append("java-signatures-raw.json")
            if args.suite in ("public", "jcg"):
                jcg = Path(temp) / "jcg-input.json"
                logged([sys.executable, str(HERE / "import-jcg.py"), str(LOCAL / "jcg"), str(jcg)], log)
                metadata["input_hashes"]["jcg"] = digest(jcg.read_bytes())
                env.update(PACE_JCG_DATASET=str(jcg), PACE_JCG_REPORT=str(output / "jcg-raw.json"))
                filters.append("FullyQualifiedName~JcgBenchmarkTests")
                required.append("jcg-raw.json")

            if args.suite in ("public", "research"):
                from research_suites import prepare
                inputs, protocol = prepare(LOCAL / "upstream", Path(temp), materialize)
                oracle_path = output / "research-oracles.json"
                write_json(oracle_path, protocol)
                metadata["input_hashes"]["research_oracles"] = digest(oracle_path.read_bytes())
                input_path = Path(temp) / "research-input.json"
                write_json(input_path, inputs)
                env.update(PACE_OFFLINE_CORPUS_INPUT=str(input_path), PACE_OFFLINE_CORPUS_REPORT=str(output / "research-raw.json"))
                filters.append("FullyQualifiedName~OfflineCorpusBenchmarkTests")
                required.append("research-raw.json")

            previous = None
            if args.compare:
                previous = json.loads((args.compare / "run.json").read_text())
                if previous["status"] != "ok" or previous["input_hashes"] != metadata["input_hashes"]:
                    raise ValueError("Comparison requires a successful run with identical input hashes.")
            test = ["dotnet", "test", "PACE.Tests/PACE.Tests.csproj", "-m:1", "-p:UseSharedCompilation=false",
                    "--filter", "|".join(filters), "--logger", "console;verbosity=detailed", "--logger",
                    "trx;LogFileName=tests.trx", "--results-directory", str(output)]
            if args.no_restore:
                test.append("--no-restore")
            write_json(output / "run.json", metadata)
            logged(test, log, env)
            if any(not (output / name).exists() for name in required):
                raise RuntimeError("Benchmark completed without emitting all required raw reports")
            summary = export_reports(output)
            if previous is not None:
                compare_runs(args.compare, output, previous, summary)
            metadata["status"] = "ok"
            result = 0
        except (KeyError, OSError, RuntimeError, ValueError, subprocess.SubprocessError, KeyboardInterrupt) as error:
            metadata["status"] = "failed"
            metadata["error"] = str(error)
            log.write(f"ERROR: {error}\n")
            print(f"Benchmark failed: {error}", file=sys.stderr)
        finally:
            if not (output / "summary.json").exists():
                try:
                    export_reports(output)
                except (KeyError, OSError, ValueError) as error:
                    log.write(f"PARTIAL REPORT ERROR: {error}\n")
            metadata["finish_utc"] = datetime.now(timezone.utc).isoformat()
            write_json(output / "run.json", metadata)
    print(f"Artifacts: {output}", flush=True)
    return result


if __name__ == "__main__":
    sys.exit(main())
