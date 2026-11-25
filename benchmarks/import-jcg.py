"""Import Java cases and separate the executable source from the upstream oracle."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

REVISION = "4737aac8c2652acded1c4505961b6af52d06ceeb"
RESOURCE = "jcg_testcases/src/main/resources/java"
CASE = re.compile(r"^##\s+([^\n]+)\n\[//\]: # \((?:MAIN: [^\n]+|LIBRARY)\)\n(.*?)^\[//\]: # \(END\)", re.M | re.S)
FILE = re.compile(r"^```java\n//\s*([^\n]+\.java)\n(.*?)^```", re.M | re.S)
ANNOTATION = re.compile(r"@(DirectCall|IndirectCall)\s*\(((?:[^()\"]|\"[^\"\n]*\")*)\)", re.S)
TYPE = re.compile(r"\"L([^\"]+);\"")


def field(body, name, pattern):
    value = re.search(r"\b" + name + r"\s*=\s*(" + pattern + ")", body)
    return value[1] if value else ""


def run_git(root, *args):
    return subprocess.check_output(["git", "-C", root, *args])


def import_cases(root):
    # Read git objects so working-tree edits never change the pinned corpus.
    names = run_git(root, "ls-tree", "-r", "--name-only", REVISION, RESOURCE).decode().splitlines()
    cases = []
    for path in sorted(name for name in names if name.endswith(".md")):
        data = run_git(root, "show", f"{REVISION}:{path}").decode()
        for match in CASE.finditer(data):
            files, checks = [], []
            for file in FILE.finditer(match[2]):
                name, source = file.groups()
                for annotation in ANNOTATION.finditer(source):
                    kind, body = annotation.groups()
                    line = field(body, "line", r"-?\d+")
                    method = field(body, "name", r'"[^"]+"').strip('"')
                    targets = {}
                    for label in ("resolvedTargets", "prohibitedTargets"):
                        value = field(body, label, r'\{[^}]*\}|"[^"]+"')
                        targets[label] = [owner.replace("/", ".") for owner in TYPE.findall(value)]
                    checks.append(dict(file=name, kind=kind, line=int(line or -1), method=method,
                                       annotationLine=source.count("\n", 0, annotation.start()) + 1, **targets))
                # Preserve line positions for the scorer. Remove annotations and their repeatable wrappers.
                source = ANNOTATION.sub(lambda m: re.sub(r"[^\n]", " ", m[0]), source)
                source = re.sub(r"@(?:DirectCalls|IndirectCalls)\s*\(\{[\s,]*\}\s*\)",
                                lambda m: re.sub(r"[^\n]", " ", m[0]), source)
                files.append(dict(path=name, source=source))
            if files:
                cases.append(dict(id=f"{Path(path).stem}/{match[1].strip()}", upstreamPath=path,
                                  upstreamLine=data.count("\n", 0, match.start()) + 1, files=files, checks=checks))
    if not cases or len({case["id"] for case in cases}) != len(cases):
        raise ValueError("Empty corpus or duplicate case IDs")
    return cases


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("checkout")
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    cases = import_cases(args.checkout)
    dataset = json.dumps({"revision": REVISION, "cases": cases}, ensure_ascii=False, indent=2) + "\n"
    args.output.write_bytes(dataset.encode())
    print(f"{len(cases)} cases, {sum(len(case['checks']) for case in cases)} direct/indirect annotations")
    print(f"SHA256 {hashlib.sha256(dataset.encode()).hexdigest()}")
