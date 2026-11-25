"""Verify the exact Git commits in PACE's local research corpus lock."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys

GITHUB = re.compile(r"https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+")
SHA = re.compile(r"[0-9a-f]{40}")
ID = re.compile(r"student-[0-9]{2,}")
ENV = {**os.environ, "GIT_TERMINAL_PROMPT": "0", "GCM_INTERACTIVE": "Never"}


def git(repository, *arguments):
    return subprocess.run(
        ["git", "-c", "core.hooksPath=/dev/null", "-c", "protocol.file.allow=never",
         "-C", str(repository), *arguments],
        env=ENV, capture_output=True, text=True, timeout=90,
    )


def verify(entry, cache, offline):
    identifier, url, sha = (entry[key] for key in ("id", "repository", "commit"))
    if not (ID.fullmatch(identifier) and GITHUB.fullmatch(url) and SHA.fullmatch(sha)):
        raise ValueError("Invalid corpus entry")
    if any(part in {".", ".."} for part in url.split("/")[-2:]):
        raise ValueError("Invalid repository path")
    scope = entry.get("tree_scope", "")
    if scope and any(part in {"", ".", ".."} for part in scope.split("/")):
        raise ValueError("Invalid tree scope")

    repository = cache / (identifier + ".git")
    repository.mkdir(parents=True, exist_ok=True, mode=0o700)
    git(repository, "init", "--bare", "--quiet").check_returncode()
    commit = git(repository, "rev-parse", "--verify", sha + "^{commit}")
    if commit.returncode and not offline:
        fetched = git(repository, "fetch", "--no-tags", "--quiet", url + ".git", sha)
        if fetched.returncode:
            raise RuntimeError("Pinned commit unavailable")
        commit = git(repository, "rev-parse", "--verify", sha + "^{commit}")
    if commit.returncode or commit.stdout.strip() != sha:
        raise RuntimeError("Pinned commit unavailable")
    tree = git(repository, "cat-file", "-t", f"{sha}:{scope}" if scope else sha + "^{tree}")
    if tree.returncode or tree.stdout.strip() != "tree":
        raise RuntimeError("Pinned source tree unavailable")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("cache", type=Path)
    parser.add_argument("--offline", action="store_true")
    arguments = parser.parse_args()
    os.umask(0o077)
    entries = json.loads(arguments.manifest.read_text())["entries"]
    if not entries or len({entry["id"] for entry in entries}) != len(entries):
        parser.error("Manifest must contain unique entries")
    failures = 0
    for order, entry in enumerate(entries, 1):
        try:
            verify(entry, arguments.cache, arguments.offline)
        except (ValueError, OSError, RuntimeError, subprocess.SubprocessError) as error:
            print(f"Entry {order}: verification failed ({type(error).__name__}).")
            failures += 1
    print(f"Verified {len(entries) - failures}/{len(entries)} exact commit pins.")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
