#!/bin/sh
# Tests for the commit-msg hook and its installer (SPEC-0007, CA-8 and CA-9).
# Self-contained: temporary files and a temporary git repository; needs no network, Docker or .env.
set -eu

root=$(cd "$(dirname "$0")/.." && pwd)
hook="$root/.githooks/commit-msg"
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
trap 'exit 143' TERM HUP
trap 'exit 130' INT

# Isolate from the caller's git environment (this script may run inside a hook) and from global git config.
for var in $(git rev-parse --local-env-vars); do unset "$var"; done
unset GIT_CONFIG_PARAMETERS
export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1

checks=0
failures=0
rc=0

fail() {
  failures=$((failures + 1))
  printf 'FAIL: %s\n' "$1" >&2
}

# run_hook <raw message>: writes it verbatim to a file and leaves the hook's exit status in $rc.
run_hook() {
  printf '%b' "$1" > "$tmp/msg"
  rc=0
  sh "$hook" "$tmp/msg" 2> /dev/null || rc=$?
}

# check <accept|reject> <raw message>: accept means exit 0; reject means exit 1 (any other status is a hook failure).
check() {
  checks=$((checks + 1))
  run_hook "$2"
  case "$1:$rc" in
    accept:0 | reject:1) ;;
    *) fail "expected $1 (hook exit status $rc) for: $2" ;;
  esac
}

# --- Accepted: subjects of the real history on 2026-10-17 (frozen fixtures) -----------------------------------
while IFS= read -r subject; do
  check accept "$subject\n"
done <<'EOF'
SPEC-0000, SPEC-0001: application foundation and policy-based authorization
SPEC-0001: record manual validation of the Root bootstrap
SPEC-0002: OS inventory agent, device enrollment and inventory API
SPEC-0003: device registry with per-device enrollment codes and inventory viewer
SPEC-0004: public user manual kept up to date with every feature
SPEC-0005: approve SQL engine access audit spec and amend constitution to 2.0.0
SPEC-0005: SQL engine access audit (agent, API, screens and manual)
SPEC-0006: approve infrastructure agenda spec and propose ADR-0016
SPEC-0007: approve SDD tooling spec
EOF

# --- Accepted: other valid forms -------------------------------------------------------------------------------
check accept 'SPEC-0007: texto\n'
check accept 'ADR-0016: texto\n'
check accept 'SPEC-0001, ADR-0008: texto\n'
check accept 'minor: corregir errata\n'
check accept "Merge branch 'feature' into main\n"
check accept 'Revert "SPEC-0001: texto"\n'
check accept 'fixup! SPEC-0001: texto\n'
check accept 'squash! SPEC-0001: texto\n'
check accept 'amend! SPEC-0001: texto\n'
check accept '# comentario de git\n\nSPEC-0001: texto\n'
check accept '\n\nSPEC-0001: texto\n'
check accept '  \t \nSPEC-0001: texto\n'
check accept 'SPEC-0001: texto\r\n'
check accept 'SPEC-0001: asunto\n\ncuerpo libre sin prefijo\n'

# --- Rejected --------------------------------------------------------------------------------------------------
check reject 'update stuff\n'
check reject 'spec-0007: x\n'
check reject 'SPEC-7: x\n'
check reject 'SPEC-007: x\n'
check reject 'SPEC-00071: x\n'
check reject 'XSPEC-0001: x\n'
check reject '  SPEC-0001: x\n'
check reject 'SPEC-0007 x\n'
check reject 'SPEC-0007:\n'
check reject 'SPEC-0007:   \n'
check reject 'SPEC-0007,SPEC-0001: x\n'
check reject 'minor:\n'
check reject 'minor: \n'
check reject 'Minor: x\n'
check reject 'minor fix typo\n'
check reject 'Mergeable x\n'
check reject '\n'
check reject ''
check reject '  \t \n'
check reject '# solo comentarios\n'
check reject 'update stuff\n\nSPEC-0001: cuerpo\n'

# --- An inherited GREP_OPTIONS must not change the verdict --------------------------------------------------------
checks=$((checks + 1))
printf 'update stuff\n' > "$tmp/msg"
rc=0
GREP_OPTIONS=--invert-match sh "$hook" "$tmp/msg" 2> /dev/null || rc=$?
[ "$rc" = "1" ] || fail "GREP_OPTIONS changed the verdict of the hook (exit status $rc)"

# --- The message is data: it must never be evaluated ------------------------------------------------------------
check accept "SPEC-0001: \$(touch $tmp/pwned1) \`touch $tmp/pwned2\`\n"
check reject "update \$(touch $tmp/pwned3) \`touch $tmp/pwned4\`\n"
checks=$((checks + 1))
if ls "$tmp"/pwned* > /dev/null 2>&1; then fail 'the hook evaluated part of the message'; fi

# --- Installer (CA-9) and real commits in a temporary repository --------------------------------------------------
repo="$tmp/repo"
mkdir -p "$repo/scripts" "$repo/.githooks"
cp "$root/scripts/install-git-hooks.sh" "$repo/scripts/"
cp "$hook" "$repo/.githooks/commit-msg"
chmod -x "$repo/.githooks/commit-msg" # the installer must restore the executable bit
git -C "$repo" init -q

checks=$((checks + 1))
sh "$repo/scripts/install-git-hooks.sh" > /dev/null 2> "$tmp/warn1" || fail 'installer failed on the first run'
first=$(git -C "$repo" config --get core.hooksPath || true)
sh "$repo/scripts/install-git-hooks.sh" > /dev/null 2>&1 || fail 'installer failed on the second run'
second=$(git -C "$repo" config --get core.hooksPath || true)
[ "$first" = ".githooks" ] && [ "$second" = ".githooks" ] || fail "core.hooksPath is '$first' then '$second'"
[ -x "$repo/.githooks/commit-msg" ] || fail 'the installer did not make the hook executable'
[ ! -e "$repo/.env" ] || fail 'the installer created .env'
[ ! -s "$tmp/warn1" ] || fail 'the installer warned on a clean clone'

checks=$((checks + 1))
git -C "$repo" config --local core.hooksPath .husky
sh "$repo/scripts/install-git-hooks.sh" > /dev/null 2> "$tmp/warn2" || fail 'installer failed over a previous core.hooksPath'
grep -q '\.husky' "$tmp/warn2" || fail 'the installer did not warn about replacing a previous core.hooksPath'
[ "$(git -C "$repo" config --get core.hooksPath)" = ".githooks" ] || fail 'the installer did not replace the previous core.hooksPath'

git_commit() {
  git -C "$repo" -c user.name=Test -c user.email=test@example.invalid -c commit.gpgsign=false \
    commit -q --allow-empty -m "$1" > /dev/null 2>&1
}

checks=$((checks + 1))
if git_commit 'update stuff'; then fail 'git accepted a commit without SPEC/ADR/minor'; fi
checks=$((checks + 1))
git_commit 'SPEC-0001: texto' || fail 'git rejected a valid commit'
checks=$((checks + 1))
git_commit 'minor: corregir errata' || fail 'git rejected a minor commit'
checks=$((checks + 1))
commits=$(git -C "$repo" rev-list --count HEAD)
[ "$commits" = "2" ] || fail "expected 2 commits in the temporary repository, found $commits"

if [ "$failures" -eq 0 ]; then
  echo "OK: $checks checks passed."
else
  echo "FAILED: $failures failure(s) in $checks checks." >&2
  exit 1
fi
