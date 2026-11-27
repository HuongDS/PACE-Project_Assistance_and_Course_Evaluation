"""Pinned, independent research benchmarks for Spring endpoints and DI calls."""
from collections import Counter
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import stat
import subprocess
from zipfile import ZipFile

AUTO_MD5 = "460e47f4108b2b8e663376de467bd08c"
MICRO_LABELS = "63653e09177b6d757eada4521f8bace8a8c20766"
MICRO_SOURCES = "bc21234b64f8cf55a181fe8d86a3632fe1234eec"
JASMINE = "79ad187a6f7ca07cfc6b874b05f5972ba9d64132"
VERBS = {"get", "post", "put", "patch", "delete", "options", "head", "trace"}
AUTO_PROJECTS = {
    "proxyprint-kitchen": ("proxyprint",), "ocvn": ("ocvn",), "Ur-Codebin-API": ("ur-codebin",),
    "ohsome-api": ("ohsome",), "catwatch": ("catwatch",), "quartz-manager": ("quartz",),
    "cwa-verification-server": ("cwa-internal", "cwa-external"),
}


def git(path, *args):
    return subprocess.check_output(["git", "-C", str(path), *args], text=True).strip()


def normalize(route):
    # Use one rule on gold and predictions. Preserve case and parameter constraints.
    route = re.sub(r"\{[^{}:]+(?=[:}])", "{param", route)
    return "/" + route.strip("/")


def auto_oas(upstream, destination):
    archive = upstream / "auto-oas.zip"
    sha256 = hashlib.sha256()
    md5 = hashlib.md5()  # Match the publisher's archive checksum.
    with archive.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            md5.update(chunk)
            sha256.update(chunk)
    if md5.hexdigest() != AUTO_MD5:
        raise ValueError("AutoOAS archive checksum differs from Zenodo v2.")
    prefix = "AutoOAS replication package/dataset/"
    scopes = []
    with ZipFile(archive) as package:
        for project, specs in AUTO_PROJECTS.items():
            identifier = "auto-" + project.lower()
            root = destination / identifier
            root.mkdir()
            source_prefix = prefix + "web-service-projects/" + project + "/"
            for item in package.infolist():
                if item.is_dir() or not item.filename.startswith(source_prefix):
                    continue
                relative = PurePosixPath(item.filename[len(source_prefix):])
                if relative.is_absolute() or ".." in relative.parts or stat.S_ISLNK(item.external_attr >> 16):
                    continue
                target = root.joinpath(*relative.parts)
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(package.read(item))
            if not any(root.rglob("*.java")):
                raise ValueError(f"AutoOAS source missing for {identifier}")
            labels = set()
            for spec in specs:
                data = json.loads(package.read(prefix + f"GT-plus specs/spring/{spec}.json"))
                labels.update((verb.upper(), normalize(path)) for path, operations in data["paths"].items()
                              for verb in operations if verb in VERBS)
            scopes.append({"id": identifier, "dataset": "auto_oas", "source": str(root),
                           "pin": sha256.hexdigest(), "gold": sorted(labels), "specs": list(specs)})
    return scopes


def microsecend(upstream, destination, materialize):
    labels, sources = (upstream / name for name in ("microsecend", "microsecend-sources"))
    for repo, expected in ((labels, MICRO_LABELS), (sources, MICRO_SOURCES)):
        if git(repo, "rev-parse", "HEAD") != expected or git(repo, "status", "--porcelain"):
            raise ValueError("microSecEnD requires clean pinned artifact checkouts")
    scopes = []
    catalog = json.loads((labels / "dataset/dataset.json").read_text())
    modules = {line.split()[3].removeprefix("EMSE2025SAR-").lower(): (line.split()[2], line.split()[3])
               for line in git(sources, "ls-tree", "-d", MICRO_SOURCES).splitlines() if line.startswith("160000 ")}
    for name, item in sorted(catalog.items()):
        identifier = "micro-" + name.lower()
        sha, path = modules[item["slug"].split("/")[-1].lower()]
        root = destination / identifier
        root.mkdir()
        materialize(sources / path, sha, "", root)
        model = json.loads((labels / "dataset" / name / (name + ".json")).read_text())
        expected = {(service["name"], normalize(endpoint)) for service in model["services"]
                    for endpoint in service.get("tagged_values", {}).get("Endpoints", [])
                    if isinstance(endpoint, str) and endpoint.startswith("/")}
        scopes.append({"id": identifier, "dataset": "microsecend", "source": str(root), "pin": sha,
                       "gold": sorted({path for _, path in expected}), "labeled_service_paths": len(expected),
                       "labels_commit": MICRO_LABELS, "source_manifest_commit": MICRO_SOURCES})
    return scopes


def jasmine(upstream, destination, materialize):
    source = upstream / "jasmine"
    if git(source, "rev-parse", "HEAD") != JASMINE or git(source, "status", "--porcelain"):
        raise ValueError("Jasmine requires the clean pinned upstream checkout")
    root = destination / "jasmine"
    root.mkdir()
    materialize(source, JASMINE, "", root)
    # Direct calls verified from the published tagged controllers and local bean definitions.
    # Group repeated call sites into method pairs for comparability with B0.
    cases = [
        (f"{scope}{index}", f"/{scope}DI/{scope}{index}", f"{scope.title()}DIController", concrete, "print")
        for scope, services in (
            ("singleton", ("SingletonDIServiceAImpl", "SingletonDIServiceBImpl", "SingletonDIServiceBImpl")),
            ("prototype", ("PrototypeDIServiceCImpl", "PrototypeDIServiceDImpl", "PrototypeDIServiceDImpl")),
        )
        for index, concrete in enumerate(services, 1)
    ]
    # The tagged annotation and XML aspects specify possible advice reachability.
    # It is distinct from a direct edge from the REST controller.
    advice = [(f"aop-{index}", "GET", f"/aopTest/aop{index}", "AOPAspectA", method)
              for index in (1, 2) for method in ("aroundMethod", "doBefore", "doAfter")]
    advice += [("aop-4", "ANY", "/aopTest/aop4", "AopXmlAspect", method)
               for method in ("doBefore", "doAfter")]
    return {"id": "jasmine", "dataset": "jasmine", "source": str(root), "pin": JASMINE, "gold": cases, "advice": advice}


def prepare(upstream, destination, materialize):
    scopes = auto_oas(upstream, destination) + microsecend(upstream, destination, materialize)
    jasmine_scope = jasmine(upstream, destination, materialize)
    scopes.append(jasmine_scope)
    input_data = {"repositories": [{"id": item["id"], "commit": item["pin"], "root": item["source"]}
                                   for item in scopes],
                  "cases": [{"id": name, "repositoryId": "jasmine", "verb": "ANY", "route": route}
                            for name, route, *_ in jasmine_scope["gold"]]}
    for name, verb, route in sorted({tuple(check[:3]) for check in jasmine_scope["advice"]}):
        input_data["cases"].append({"id": name, "repositoryId": "jasmine", "verb": verb, "route": route})
    protocol = [{key: value for key, value in item.items() if key != "source"} for item in scopes]
    return input_data, protocol


def summarize(raw, protocol):
    rows, summary = [], {}
    for repo in raw:
        oracle = next(x for x in protocol if x["id"] == repo["id"])
        dataset = oracle["dataset"]
        counts = summary.setdefault(dataset, Counter())
        counts["projects"] += 1
        counts["analyzed"] += repo["status"] == "ok"
        if dataset == "jasmine":
            by_case = {x["caseId"]: x for x in repo.get("gold", [])}
            for case, route, caller, target, method in oracle["gold"]:
                result = by_case.get(case, {})
                graph_nodes = {(x["location"]["file"], x["location"]["line"]): x for x in result.get("nodes") or []}
                def node(location):
                    return graph_nodes.get((location["file"], location["line"]), {})
                found = any(node(edge["from"]).get("className") == caller and
                            node(edge["to"]).get("className") == target and node(edge["to"]).get("methodName") == method
                            for edge in result.get("edges") or [] if edge["edgeType"] == "Call")
                rows.append({"dataset": dataset, "project": repo["id"], "case": case,
                             "expected": f"{caller} → {target}.{method}", "status": "found" if found else "missing"})
                counts["required_calls"] += 1
                counts["found_calls"] += found
            for case, _, _, target, method in oracle["advice"]:
                found = any(node["className"] == target and node["methodName"] == method
                            for node in by_case.get(case, {}).get("nodes") or [])
                rows.append({"dataset": dataset, "project": repo["id"], "case": case,
                             "expected": f"advice reachable: {target}.{method}", "status": "found" if found else "missing"})
                counts["required_advice_methods"] += 1
                counts["found_advice_methods"] += found
            continue
        observed_routes = {(item["verb"], normalize(item["route"])) for item in repo.get("discovery", [])
                           if item.get("verb") and item.get("route")}
        if dataset == "auto_oas":
            observed = {(verb, path) for verb, path in observed_routes if verb.lower() in VERBS}
            expected = {tuple(item) for item in oracle["gold"]}
        else:
            # The released annotations carry path, not verb or an unambiguous controller ID.
            observed = {path for _, path in observed_routes}
            expected = set(oracle["gold"])
        for item in sorted(expected | observed):
            status = "matched" if item in expected and item in observed else "missing" if item in expected else "unmatched_observation"
            rows.append({"dataset": dataset, "project": repo["id"], "case": "",
                         "expected": " ".join(item) if isinstance(item, tuple) else item, "status": status})
            counts[status] += 1
        counts["gold_keys"] += len(expected)
        counts["reported_keys"] += len(observed)
    return {key: dict(value) for key, value in summary.items()}, rows
