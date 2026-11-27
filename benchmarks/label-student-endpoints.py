"""Create source-derived Spring endpoint labels without consulting PACE output."""
import argparse
import collections
import hashlib
import json
from pathlib import Path
import re
import subprocess
import tempfile

# Reuse only the pinned Git tree materializer. No parser, matcher or prediction from PACE.
from run import HERE, LOCAL, materialize, write_json

ID = re.compile(r"student-[0-9]{2,}")


def key(row):
    return row["verb"], re.sub(r"\{[^}]+\}", "{}", row["route"])


def labels(root, entry):
    output = subprocess.check_output(["java", str(HERE / "ReadSpringMappings.java"), str(root)], text=True)
    items = []
    for line in output.splitlines():
        verb, route, file, handler_line, prefix_line, route_line, owner, method = line.split("\t")
        items.append({"repositoryId": entry["id"], "commit": entry["commit"], "verb": verb, "route": route,
                      "expectedEntry": {"file": file, "line": int(handler_line)},
                      "evidence": {"type": "literal_spring_source_annotation", "controller": owner, "method": method,
                                   "prefixLine": int(prefix_line) or None, "mappingLine": int(route_line)},
                      "reviewStatus": "source_derived_pending_second_review"})
    frequency = collections.Counter(map(key, items))
    return [row for row in items if frequency[key(row)] == 1], sum(n for n in frequency.values() if n > 1)


def sample(items, max_cases, seed):
    by_controller = collections.defaultdict(list)
    for item in items:
        by_controller[item["expectedEntry"]["file"]].append(item)
    def order(value):
        return hashlib.sha256((seed + ":" + value).encode()).hexdigest()
    groups = sorted(by_controller, key=order)
    for filename in groups:
        by_controller[filename].sort(key=lambda row: order(row["verb"] + " " + row["route"]))
    selected = []
    while len(selected) < max_cases and groups:
        next_round = []
        for filename in groups:
            if len(selected) == max_cases:
                break
            selected.append(by_controller[filename].pop())
            if by_controller[filename]:
                next_round.append(filename)
        groups = next_round
    return selected


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("lock", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--per-repo", type=int, default=40)
    args = parser.parse_args()
    if args.per_repo < 1:
        parser.error("--per-repo must be positive")
    if args.output.exists():
        parser.error("Output already exists; choose a new path to preserve the previous labels.")
    entries = json.loads(args.lock.read_text())["entries"]
    seed = "pace-spring-source-v1"
    cases, inventory = [], []
    with tempfile.TemporaryDirectory(prefix="pace-labels-") as directory:
        for entry in entries:
            identifier = entry["id"]
            if not ID.fullmatch(identifier):
                raise ValueError("Invalid student ID")
            dest = Path(directory) / identifier
            dest.mkdir()
            materialize(LOCAL / "git-cache" / (identifier + ".git"), entry["commit"], entry["tree_scope"], dest)
            eligible, ambiguous = labels(dest, entry)
            selected = sample(eligible, args.per_repo, seed)
            for index, row in enumerate(selected, 1):
                row["id"] = f"{identifier}-endpoint-{index:03}"
            cases.extend(selected)
            inventory.append({"repositoryId": identifier, "eligible": len(eligible), "ambiguous": ambiguous,
                              "selected": len(selected)})
            print(inventory[-1])
    write_json(args.output, {"schemaVersion": 1, "seed": seed, "inventory": inventory, "cases": cases})


if __name__ == "__main__":
    main()
