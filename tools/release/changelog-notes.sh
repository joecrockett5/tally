#!/usr/bin/env bash
# Prints one version's section of a Keep a Changelog file: everything after its
# "## [X.Y.Z]" heading, up to the next "## [" heading or the link definitions.
# Usage: changelog-notes.sh X.Y.Z [CHANGELOG.md]
set -euo pipefail

awk -v heading="## [$1]" '
  /^## \[/ { if (found) exit; if (index($0, heading) == 1) { found = 1; next } }
  /^\[[^]]+\]: / { if (found) exit }
  found && (started || NF) { started = 1; print }
' "${2:-CHANGELOG.md}" | sed -e :a -e '/^[[:space:]]*$/{$d;N;ba' -e '}'
