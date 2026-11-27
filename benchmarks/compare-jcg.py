"""Compare an analyzer against a frozen JCG projection using paired case resampling."""
import argparse
import json
import random
from collections import defaultdict
from pathlib import Path

MEASURED = {"scored", "parse-failure", "caller-missing"}


def load(path):
    rows = {}
    for row in json.loads(path.read_text()):
        key = tuple(row[field] for field in ("case", "file", "line", "method", "annotationLine"))
        if key in rows:
            raise ValueError(f"Duplicate annotation: {key}")
        rows[key] = row
    return rows


def hits(row, field):
    return len(set(row[field]) & set(row["observed"]))


def compare(base_path, candidate_path, samples):
    base, candidate = load(base_path), load(candidate_path)
    if base.keys() != candidate.keys():
        raise ValueError("Reports must have the same annotation IDs.")
    by_case = defaultdict(lambda: [0, 0, 0])
    negatives = [0, 0, 0]
    eligible = 0
    for key, first in base.items():
        second = candidate[key]
        for field in ("required", "prohibited"):
            if first[field] != second[field]:
                raise ValueError(f"Gold labels changed: {key}")
        if first["status"] not in MEASURED:
            continue
        if second["status"] not in MEASURED:
            raise ValueError(f"Baseline annotation became unscorable: {key}. Inspect the projection.")
        eligible += 1
        totals = by_case[first["case"]]
        totals[0] += len(first["required"])
        totals[1] += hits(first, "required")
        totals[2] += hits(second, "required")
        negatives[0] += len(first["prohibited"])
        negatives[1] += hits(first, "prohibited")
        negatives[2] += hits(second, "prohibited")

    cases = list(by_case.values())
    gold, old, new = [sum(column) for column in zip(*cases)]
    rng = random.Random(271828)
    deltas = []
    for _ in range(samples):
        reference, prior, current = [sum(column) for column in zip(*rng.choices(cases, k=len(cases)))]
        deltas.append(100 * (current - prior) / reference)
    deltas.sort()
    return {
        "scored_annotations": eligible,
        "cases": len(cases),
        "required_targets": {"total": gold, "baseline_found": old, "candidate_found": new},
        "prohibited_targets": {"total": negatives[0], "baseline_reported": negatives[1], "candidate_reported": negatives[2]},
        "required_target_recall_delta_pp": 100 * (new - old) / gold,
        "paired_case_bootstrap_95_percent_interval_pp": [
            deltas[int((samples - 1) * 0.025)], deltas[int((samples - 1) * 0.975)]
        ],
        "bootstrap_samples": samples,
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("baseline", type=Path)
    parser.add_argument("candidate", type=Path)
    parser.add_argument("--samples", type=int, default=10000)
    args = parser.parse_args()
    if args.samples < 1:
        parser.error("--samples must be positive")
    print(json.dumps(compare(args.baseline, args.candidate, args.samples), indent=2))
