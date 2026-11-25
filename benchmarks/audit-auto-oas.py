"""Re-score saved public-suite outputs against the AutoOAS GT+ workbook."""
import argparse
from hashlib import file_digest, sha256
from io import BytesIO
import json
from pathlib import Path
from zipfile import ZipFile

import pandas as pd

from research_suites import VERBS, normalize


MEMBER = "AutoOAS replication package/dataset/GT-plus.xlsx"
WORKBOOK_SHA256 = "1698eb7a851defa8e8bf224d908474077ec062c5366ac618194987145aeec87a"
SHEETS = {
    "auto-proxyprint-kitchen": ["ProxyPrint"],
    "auto-ocvn": ["OCVN"],
    "auto-ur-codebin-api": ["Ur-Codebin"],
    "auto-ohsome-api": ["ohsome"],
    "auto-catwatch": ["Catwatch"],
    "auto-quartz-manager": ["Quartz"],
    "auto-cwa-verification-server": ["cwa-external", "cwa-internal"],
}


def load(path):
    return json.loads(path.read_bytes())


def read_reference(workbook):
    names = [name for sheets in SHEETS.values() for name in sheets]
    tables = pd.read_excel(BytesIO(workbook), sheet_name=names, header=[0, 1])
    reference = {project: set() for project in SHEETS}
    for project, sheets in SHEETS.items():
        for name in sheets:
            methods = tables[name]["endpoint method"].dropna(how="all")
            for verb, path in methods[["method", "path"]].itertuples(index=False, name=None):
                if (not isinstance(verb, str) or verb.lower() not in VERBS
                        or not isinstance(path, str) or not path.startswith("/")):
                    raise ValueError(f"Unsupported label in {name}: {(verb, path)}")
                reference[project].add((verb.upper(), normalize(path)))
    return reference


def audit(package, runs):
    with package.open("rb") as stream:
        archive_sha = file_digest(stream, "sha256").hexdigest()
    with ZipFile(package) as archive:
        workbook = archive.read(MEMBER)
    if sha256(workbook).hexdigest() != WORKBOOK_SHA256:
        raise ValueError("Workbook differs from the audited AutoOAS v2 artifact")
    reference = read_reference(workbook)
    report = {
        "artifact": "https://zenodo.org/records/20962871",
        "archive_sha256": archive_sha,
        "member": MEMBER,
        "workbook_sha256": WORKBOOK_SHA256,
        "projects": {project: {"sheets": sheets, "stages": {}} for project, sheets in SHEETS.items()},
        "stages": {},
        "pairwise_changes": [],
    }
    previous = None
    for folder in runs:
        metadata = load(folder / "run.json")
        stage = metadata["stage"]
        rows = {row["id"]: row for row in load(folder / "research-raw.json")}
        if metadata["status"] != "ok" or stage in report["stages"]:
            raise ValueError(f"Expected one successful run per stage: {folder}")
        matches = set()
        stage_scores = []
        for project, expected in reference.items():
            row = rows[project]
            if row["status"] != "ok" or row["commit"] != archive_sha:
                raise ValueError(f"Different or failed AutoOAS snapshot: {folder}/{project}")
            observed = {(case["verb"].upper(), normalize(case["route"]))
                        for case in row["discovery"]
                        if case.get("route") and (case.get("verb") or "").lower() in VERBS}
            metrics = {
                "reference": len(expected),
                "matched": len(observed & expected),
                "missing": len(expected - observed),
                "reported": len(observed),
                "outside_reference": len(observed - expected),
            }
            report["projects"][project]["stages"][stage] = metrics
            stage_scores.append(metrics)
            matches.update((project, verb, path) for verb, path in observed & expected)
        report["stages"][stage] = {
            "run": folder.name,
            **{key: sum(score[key] for score in stage_scores) for key in stage_scores[0]},
        }
        if previous:
            previous_stage, prior = previous
            report["pairwise_changes"].append({
                "from": previous_stage,
                "to": stage,
                "gains": sorted(matches - prior),
                "losses": sorted(prior - matches),
            })
        previous = stage, matches
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", type=Path)
    parser.add_argument("runs", type=Path, nargs="+", help="Successful public run directories in stage order")
    arguments = parser.parse_args()
    print(json.dumps(audit(arguments.package, arguments.runs), indent=2))
