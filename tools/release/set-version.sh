#!/usr/bin/env bash
# Sets the version in every manifest. Usage: set-version.sh X.Y.Z
set -euo pipefail
cd "$(dirname "$0")/../.."

version="${1:?Usage: set-version.sh X.Y.Z}"
(cd web && npm version "$version" --no-git-tag-version --allow-same-version >/dev/null)
perl -pi -e "s|<Version>.*</Version>|<Version>$version</Version>|" src/Tally.Api/Tally.Api.csproj
echo "Version set to $version. Add a '## [$version]' section to CHANGELOG.md."
