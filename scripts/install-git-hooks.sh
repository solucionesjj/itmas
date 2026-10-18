#!/bin/sh
# Enables the versioned git hooks of .githooks/ for this clone (SPEC-0007). Safe to run more than once.
# It neither reads nor needs .env. Once core.hooksPath is set, git stops running the hooks of .git/hooks/.
set -eu

root=$(cd "$(dirname "$0")/.." && pwd -P)

chmod u+x "$root/.githooks/commit-msg"

current=$(git -C "$root" config --local --get core.hooksPath || true)
if [ -n "$current" ] && [ "$current" != ".githooks" ]; then
  echo "Warning: core.hooksPath was '$current' and is replaced by '.githooks'." >&2
fi

git -C "$root" config --local core.hooksPath .githooks

echo "Git hooks enabled (core.hooksPath = .githooks)."
