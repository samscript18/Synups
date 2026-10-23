#!/usr/bin/env bash
# Disposable CI display only. A real window manager is required for minimize/focus behavior.
set -euo pipefail
: "${DISPLAY:?Run this helper inside an isolated Xvfb display}"
openbox --sm-disable > "${SYNUPS_X11_WM_LOG:-/tmp/synups-openbox.log}" 2>&1 &
synups_wm_pid=$!
cleanup() {
  local result=$?
  kill "$synups_wm_pid" 2>/dev/null || true
  wait "$synups_wm_pid" 2>/dev/null || true
  exit "$result"
}
trap cleanup EXIT
for synups_wm_attempt in {1..100}; do
  synups_wm_state=$(xprop -root _NET_SUPPORTING_WM_CHECK 2>/dev/null || true)
  if [[ "$synups_wm_state" == *"window id #"* ]]; then
    "$@"
    exit $?
  fi
  kill -0 "$synups_wm_pid"
  sleep 0.1
done
echo "Openbox did not establish the disposable X11 window-manager root property" >&2
exit 1
