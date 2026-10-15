#!/usr/bin/env bash
# Self-test for check-pr-source.sh: each case states the expected exit code for a base/head pair.
set -uo pipefail

script="$(cd "$(dirname "$0")" && pwd)/check-pr-source.sh"
work="$(mktemp -d)"
failures=0

expect() {
  local name=$1 want=$2 base=$3 head=$4 got
  (cd "$work" && BASE_REF="$base" HEAD_REF="$head" bash "$script" >/dev/null 2>&1)
  got=$?
  if [[ $got -ne $want ]]; then
    echo "FAIL $name: base='$base' head='$head' exited $got, want $want" >&2
    failures=$((failures + 1))
  fi
  return 0
}

# A rejection writes its ::error:: annotation to stderr and nothing to stdout.
expect_error_on_stderr() {
  local name=$1 base=$2 head=$3 out err
  out="$(cd "$work" && BASE_REF="$base" HEAD_REF="$head" bash "$script" 2>"$work/stderr")"
  err="$(cat "$work/stderr")"
  if [[ -n "$out" || "$err" != "::error::"* ]]; then
    echo "FAIL $name: stdout='$out' stderr='$err', want the error on stderr only" >&2
    failures=$((failures + 1))
  fi
  return 0
}

expect release-into-main 0 main release/1.0.0
expect hotfix-into-main 0 main hotfix/1.0.1-login
expect feature-into-main 1 main feature/m2a-delivery-foundation
expect bugfix-into-main 1 main bugfix/save-import
expect develop-into-main 1 main develop
expect bare-release-into-main 1 main release
expect lookalike-into-main 1 main xrelease/1.0.0
expect injection 1 main 'feature/$(touch pwned)'
expect feature-into-develop 0 develop feature/x
expect main-into-develop 0 develop main
expect release-into-develop 0 develop release/1.0.0
expect missing-head 1 main ''
expect_error_on_stderr feature-into-main-stderr main feature/x
expect_error_on_stderr missing-head-stderr main ''

if [[ -e "$work/pwned" ]]; then
  echo "FAIL injection: the branch name was executed" >&2
  failures=$((failures + 1))
fi
rm -rf "$work"

if [[ $failures -ne 0 ]]; then
  echo "$failures case(s) failed" >&2
  exit 1
fi
echo "All check-pr-source cases passed"
