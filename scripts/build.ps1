$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    dotnet restore EditorAddons.sln --locked-mode -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
    dotnet build EditorAddons.sln -c Release --no-restore -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    # dist is generated output; remove stale modules before packaging.
    if (Test-Path dist) { Remove-Item dist -Recurse -Force }
    New-Item -ItemType Directory -Force dist/LICENSES, dist/docs | Out-Null
    Copy-Item src/EditorAddons/bin/Release/net10.0/osu.Game.Rulesets.EditorAddons.dll dist/
    Copy-Item INSTALL.md dist/README.md
    Copy-Item LICENSE.md, THIRD_PARTY_NOTICES.md, CHANGELOG.md dist/
    Copy-Item LICENSES/* dist/LICENSES/
    Copy-Item docs/USER-GUIDE.fr.md dist/docs/
    $name = 'osu.Game.Rulesets.EditorAddons.dll'
    $hash = (Get-FileHash (Join-Path 'dist' $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name" | Set-Content dist/SHA256SUMS.txt -Encoding ascii
} finally { Pop-Location }
