#!/usr/bin/env bash
# Switches the running Linux Tammi to a build of this checkout, e.g. to try a branch in the real app.
#
# It starts a detached launcher that waits for the running Tammi to exit and then runs this checkout under
# `dotnet watch` (see watch-linux.sh), so later C# and Razor edits hot reload into the new instance. Closing Tammi is left to you, because it ends every agent running in it
# (including the one that ran this script). Tammi warns about running agents when you close it.
#
# Usage: scripts/switch-linux-instance.sh [--stop]
#   --stop  Also ask the running Tammi to exit now (SIGTERM). This skips its close warning and ends its agents.

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
log="${TMPDIR:-/tmp}/tammi-switch.log"

stop=false
[[ "${1:-}" == "--stop" ]] && stop=true

# Fail now on a build error, rather than after the running Tammi has been closed. dotnet watch builds again on start.
dotnet build "$root/Deosrc.Tammi.Linux.slnf" --nologo -v q

# Only one Tammi runs at a time (a second launch just signals the first), so there's at most one to replace.
# The kernel truncates process names to 15 characters, so that's what pgrep -x has to match.
process_name="Deosrc.Tammi.Linux"
running_pid="$(pgrep -x "${process_name:0:15}" | head -n 1 || true)"

if [[ -n "$running_pid" ]]; then
  echo "Tammi is running (pid $running_pid). The watch starts once it exits."
  $stop && kill -TERM "$running_pid"
else
  echo "Tammi isn't running. Starting the watch now."
fi

# setsid detaches the launcher from this shell, so it outlives the agent that ran this script. The hook token
# belongs to the running Tammi, so the new instance must not inherit it. The new instance's output goes to $log.
setsid nohup env -u TAMMI_AGENT_HOOK_TOKEN bash -c '
  pid="$1"; root="$2"
  while [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; do sleep 1; done
  exec "$root/scripts/watch-linux.sh" --non-interactive
' _ "$running_pid" "$root" >"$log" 2>&1 < /dev/null &
disown

echo "Output from the watch and the new instance: $log (stop it by ending the dotnet watch process)"
