#!/usr/bin/env bash
# Allows only release/* and hotfix/* branches to open pull requests into main (TD-87).
# BASE_REF and HEAD_REF come from the environment and are only compared, never evaluated.
set -euo pipefail

base="${BASE_REF:-}"
head="${HEAD_REF:-}"

if [[ -z "$base" || -z "$head" ]]; then
  echo "::error::BASE_REF and HEAD_REF must be set to the pull request's base and head branches." >&2
  exit 1
fi

if [[ "$base" != "main" ]]; then
  echo "OK: any branch may open a pull request into '$base'."
  exit 0
fi

case "$head" in
  release/* | hotfix/*)
    echo "OK: '$head' may open a pull request into main."
    ;;
  *)
    echo "::error::Only release/* and hotfix/* branches may open pull requests into main; target develop with '$head' instead." >&2
    exit 1
    ;;
esac
