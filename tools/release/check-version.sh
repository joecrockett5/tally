#!/usr/bin/env bash
# Prints the current version. Fails if the manifests disagree, the version isn't
# valid semver, or CHANGELOG.md has no (non-empty) section for it.
set -euo pipefail
cd "$(dirname "$0")/../.."

fail() { echo "::error::$1" >&2; exit 1; }

version=$(node -p "require('./web/package.json').version")
lock=$(node -p "require('./web/package-lock.json').version")
lock_root=$(node -p "require('./web/package-lock.json').packages[''].version")
api=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' src/Tally.Api/Tally.Api.csproj)

if [[ "$lock" != "$version" || "$lock_root" != "$version" || "$api" != "$version" ]]; then
  fail "Versions disagree: web/package.json=$version, web/package-lock.json=$lock/$lock_root, Tally.Api.csproj=$api. Fix with tools/release/set-version.sh."
fi

semver='^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z.-]+)?$'
[[ "$version" =~ $semver ]] || fail "Version '$version' is not valid semver (X.Y.Z or X.Y.Z-pre)."

[[ -n "$(tools/release/changelog-notes.sh "$version")" ]] \
  || fail "CHANGELOG.md has no non-empty '## [$version]' section."

echo "$version"
