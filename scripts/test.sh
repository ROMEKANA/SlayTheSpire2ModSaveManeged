#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet run --project tests/UnifiedVanillaSave.Tests -c Release
dotnet build tools/SaveAudit -c Release
dotnet run --project tests/UnifiedVanillaSave.HarmonyTests -c Release
DOTNET_TieredCompilation=0 dotnet tests/UnifiedVanillaSave.HarmonyTests/bin/Release/net9.0/UnifiedVanillaSave.HarmonyTests.dll
