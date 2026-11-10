#!/usr/bin/env bash
# Switches the running Linux Tammi to a build of this checkout, e.g. to try a branch in the real app.
#
# It builds the Linux host, then starts a detached launcher that waits for the running Tammi to exit and starts
# this checkout's build in its place. Closing Tammi is left to you, because it ends every agent running in it
# (including the one that ran this script). Tammi warns about running agents when you close it.
#
# Usage: scripts/switch-linux-instance.sh [--stop]
#   --stop  Also ask the running Tammi to exit now (SIGTERM). This skips its close warning and ends its agents.

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
bin_dir="$root/src/Deosrc.Tammi.Linux/bin/Debug/net10.0"
binary="$bin_dir/Deosrc.Tammi.Linux"
log="${TMPDIR:-/tmp}/tammi-switch.log"

stop=false
[[ "${1:-}" == "--stop" ]] && stop=true

dotnet build "$root/Deosrc.Tammi.Linux.slnf" --nologo -v q

# Only one Tammi runs at a time (a second launch just signals the first), so there's at most one to replace.
running_pid="$(pgrep -x Deosrc.Tammi.Linux | head -n 1 || true)"

if [[ -n "$running_pid" ]]; then
  echo "Tammi is running (pid $running_pid). The build starts once it exits."
  $stop && kill -TERM "$running_pid"
else
  echo "Tammi isn't running. Starting the build now."
fi

# setsid detaches the launcher from this shell, so it outlives the agent that ran this script. The hook token
# belongs to the running Tammi, so the new instance must not inherit it. The new instance's output goes to $log.
setsid nohup env -u TAMMI_AGENT_HOOK_TOKEN bash -c '
  pid="$1"; bin_dir="$2"; binary="$3"
  while [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; do sleep 1; done
  cd "$bin_dir" && exec "$binary"
' _ "$running_pid" "$bin_dir" "$binary" >"$log" 2>&1 < /dev/null &
disown

echo "Output from the new instance: $log"
