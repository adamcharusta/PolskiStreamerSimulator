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
    echo "FAIL $name: base='$base' head='$head' exited $got, want $want"
    failures=$((failures + 1))
  fi
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

if [[ -e "$work/pwned" ]]; then
  echo "FAIL injection: the branch name was executed"
  failures=$((failures + 1))
fi
rm -rf "$work"

if [[ $failures -ne 0 ]]; then
  echo "$failures case(s) failed"
  exit 1
fi
echo "All check-pr-source cases passed"
