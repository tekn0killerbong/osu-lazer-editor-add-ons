#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet restore EditorAddons.sln --locked-mode -m:1
dotnet build EditorAddons.sln -c Release --no-restore -m:1
# dist is generated output; recreate it so removed modules cannot be packaged.
rm -rf dist
mkdir -p dist/LICENSES dist/docs
cp src/EditorAddons/bin/Release/net10.0/osu.Game.Rulesets.EditorAddons.dll dist/
cp INSTALL.md dist/README.md
cp LICENSE.md THIRD_PARTY_NOTICES.md CHANGELOG.md dist/
cp LICENSES/* dist/LICENSES/
cp docs/USER-GUIDE.fr.md dist/docs/
(
  cd dist
  sha256sum osu.Game.Rulesets.EditorAddons.dll > SHA256SUMS.txt
)
