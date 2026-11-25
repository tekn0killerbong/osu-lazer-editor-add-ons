#!/usr/bin/env python3
"""Verify standalone EditorAddons discovery without preloading Harmony."""
from pathlib import Path
import os
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
dotnet = os.environ.get("DOTNET_EXECUTABLE", "dotnet")
for project in ("EditorAddons.Tests", "ModuleDiscovery"):
	subprocess.run([dotnet, "build", str(root / "tests" / project / (project + ".csproj")), "-c", "Release", "-m:1", "-p:RestoreLockedMode=true"], check=True)
discovery = root / "tests/ModuleDiscovery/bin/Release/net10.0/ModuleDiscovery.dll"
with tempfile.TemporaryDirectory(prefix="editor-addons-discovery-") as temporary:
	folder = Path(temporary) / "standalone"
	shutil.copytree(root / "tests/EditorAddons.Tests/bin/Release/net10.0", folder)
	(folder / "0Harmony.dll").unlink(missing_ok=True)
	module = folder / "osu.Game.Rulesets.EditorAddons.dll"
	subprocess.run([dotnet, str(discovery), str(module)], check=True)
