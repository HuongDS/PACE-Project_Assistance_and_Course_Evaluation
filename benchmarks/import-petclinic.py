"""Derive endpoint gold from pinned PetClinic OpenAPI and concrete controller overrides."""
import argparse
import json
import re
import subprocess
from pathlib import Path

REVISION = "220f4ba8e92cec9ad1faa03324723a3e9da63d95"
SPEC = "src/main/resources/openapi.yml"
CONTROLLERS = "src/main/java/org/springframework/samples/petclinic/rest/controller"


def extract(root):
    def git(*args):
        return subprocess.check_output(["git", "-C", root, *args]).decode()

    spec = git("show", f"{REVISION}:{SPEC}")
    paths = git("ls-tree", "-r", "--name-only", REVISION, CONTROLLERS).splitlines()
    methods = {}
    for path in (path for path in paths if path.endswith(".java")):
        source = git("show", f"{REVISION}:{path}")
        if not re.search(r'@RequestMapping\("/?api"\)', source):
            continue
        for match in re.finditer(r"(?m)^\s*public ResponseEntity[^\n]*?\s+(\w+)\(", source):
            name = match[1]
            if name in methods:
                raise ValueError(f"Non-unique implementation: {name}")
            methods[name] = (path, source.count("\n", 0, match.start(1)) + 1)

    # The pinned spec uses two-space path and four-space operation indentation.
    assert "paths:\n" in spec and re.search(r"(?m)^  - url: .*?/api/?\s*$", spec), "Unexpected base route"
    verb = route = None
    rows, excluded = [], []
    for line_number, line in enumerate(spec.splitlines(), 1):
        if match := re.fullmatch(r"  (/[^:]+):", line):
            route, verb = match[1], None
        if match := re.fullmatch(r"    (get|post|put|patch|delete|options|head):", line):
            verb = match[1].upper()
        if match := re.fullmatch(r"      operationId: (\w+)", line):
            if not route or not verb:
                raise ValueError(f"Unmapped operation at {SPEC}:{line_number}")
            name = match[1]
            if name not in methods:
                excluded.append({"method": name, "specLine": line_number, "reason": "No unique concrete override in scoped controllers"})
                continue
            path, line = methods[name]
            rows.append(dict(verb=verb, route="/api" + route, path=path, line=line,
                             method=name, specLine=line_number))
    keys = {(row["verb"], row["route"]) for row in rows}
    if len(rows) != len(keys) or not rows:
        raise ValueError("Empty endpoint gold or duplicate route/verb")
    return dict(revision=REVISION, spec=SPEC, endpoints=rows, excluded=excluded)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("checkout")
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    data = extract(args.checkout)
    args.output.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    print(f"{len(data['endpoints'])} endpoint labels; {len(data['excluded'])} exclusions")
