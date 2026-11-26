#!/usr/bin/env bash
# Switches the running Linux Tammi to a build of this checkout, e.g. to try a branch in the real app.
#
# It starts a detached launcher that waits for the running Tammi to exit and then runs this checkout with `dotnet run`,
# or with `dotnet watch` (see watch-linux.sh) when --watch is given, so later C# and Razor edits hot reload into the new
# instance. Closing Tammi is left to you, because it ends every agent running in it (including the one that ran this
# script). Tammi warns about running agents when you close it.
#
# Usage: scripts/switch-linux-instance.sh [--stop] [--watch]
#   --stop   Also ask the running Tammi to exit now (SIGTERM). This skips its close warning and ends its agents.
#   --watch  Run the new instance under `dotnet watch` instead of `dotnet run`.

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
log="${TMPDIR:-/tmp}/tammi-switch.log"

stop=false
watch=false
for arg in "$@"; do
  case "$arg" in
    --stop) stop=true ;;
    --watch) watch=true ;;
    *)
      echo "Unknown argument: $arg" >&2
      echo "Usage: $0 [--stop] [--watch]" >&2
      exit 2
      ;;
  esac
done

if $watch; then
  mode="watch"
  runner="dotnet watch"
else
  mode="run"
  runner="dotnet run"
fi

# Fail now on a build error, rather than after the running Tammi has been closed. dotnet run and dotnet watch build
# again on start, which picks up anything changed meanwhile.
dotnet build "$root/Deosrc.Tammi.Linux.slnf" --nologo -v q

# Only one Tammi runs at a time (a second launch just signals the first), so there's at most one to replace.
# The kernel truncates process names to 15 characters, so that's what pgrep -x has to match.
process_name="Deosrc.Tammi.Linux"
running_pid="$(pgrep -x "${process_name:0:15}" | head -n 1 || true)"

if [[ -n "$running_pid" ]]; then
  echo "Tammi is running (pid $running_pid). The new instance starts with $runner once it exits."
  $stop && kill -TERM "$running_pid"
else
  echo "Tammi isn't running. Starting the new instance with $runner now."
fi

# setsid detaches the launcher from this shell, so it outlives the agent that ran this script. The hook token
# belongs to the running Tammi, so the new instance must not inherit it. The new instance's output goes to $log.
setsid nohup env -u TAMMI_AGENT_HOOK_TOKEN bash -c '
  pid="$1"; root="$2"; mode="$3"
  while [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; do sleep 1; done
  if [[ "$mode" == "watch" ]]; then
    exec "$root/scripts/watch-linux.sh" --non-interactive
  fi
  exec dotnet run \
    --project "$root/src/Deosrc.Tammi.Linux/Deosrc.Tammi.Linux.csproj" \
    --launch-profile Linux \
    --configuration Debug
' _ "$running_pid" "$root" "$mode" >"$log" 2>&1 < /dev/null &
disown

if $watch; then
  echo "Output from the watch and the new instance: $log (stop it by ending the dotnet watch process)"
else
  echo "Output from the new instance: $log"
fi
