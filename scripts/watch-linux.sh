#!/usr/bin/env bash
# Runs the Linux Tammi under `dotnet watch`, so C# and Razor edits apply to the running app without a restart.
# Edits hot reload can't apply (e.g. changing a method signature) restart the app instead. Press Ctrl+R in this
# terminal to force a restart.
#
# Only one Tammi runs at a time (a second launch just signals the first), so close any running Tammi first.
#
# Usage: scripts/watch-linux.sh [extra dotnet watch args]

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

exec dotnet watch run \
  --project "$root/src/Deosrc.Tammi.Linux/Deosrc.Tammi.Linux.csproj" \
  --launch-profile Linux \
  --configuration Debug \
  "$@"
