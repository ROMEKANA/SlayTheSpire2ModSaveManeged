#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet run --project tests/UnifiedVanillaSave.Tests -c Release
dotnet build tools/SaveAudit -c Release
