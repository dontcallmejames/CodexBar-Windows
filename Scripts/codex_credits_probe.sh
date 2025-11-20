#!/usr/bin/env bash
#
# Run `codex /status` inside tmux, capture the pane, and emit a small JSON blob:
# {
#   "ok": true,
#   "credits_remaining": 191.0,
#   "five_pct_left": 100,
#   "week_pct_left": 0,
#   "pane_preview": "..."
# }
#
# Env vars:
# - CODEXBAR_CODEX_BIN: override codex path (default: codex)
# - CODEXBAR_TMUX_BIN:  override tmux path  (default: tmux)
# - CODEXBAR_TIMEOUT:    seconds to wait before giving up (default: 30)
# - CODEXBAR_WORKDIR:    working dir for logs (default: $PWD)

set -Eeuo pipefail

exec 3>&1 4>&2

CODEX_BIN="${CODEXBAR_CODEX_BIN:-codex}"
TMUX_BIN="${CODEXBAR_TMUX_BIN:-tmux}"
TIMEOUT_SECS="${CODEXBAR_TIMEOUT:-30}"
WORKDIR="${CODEXBAR_WORKDIR:-$PWD}"

LABEL="cb-codex-credits-$$"
SESSION="credits"
CAPTURE_LINES=400
LOG_TAIL_BYTES=12288
LOG_DIR="$WORKDIR/cb-codex-credits-$LABEL"
mkdir -p "$LOG_DIR"
STDOUT_LOG="$LOG_DIR/script.stdout.log"
STDERR_LOG="$LOG_DIR/script.stderr.log"
PANE_FILE="$LOG_DIR/pane.txt"

exec 1>>"$STDOUT_LOG"
exec 2>>"$STDERR_LOG"

cleanup() { "$TMUX_BIN" -L "$LABEL" kill-server >/dev/null 2>&1 || true; }
trap cleanup EXIT

error_json() {
  local code="$1"; local hint="$2"; local pane="$3"; local out_tail="$4"; local err_tail="$5"
  printf '{"ok":false,"code":"%s","hint":"%s","pane_preview":"%s","stdout_tail_b64":"%s","stderr_tail_b64":"%s"}\n' \
    "$code" "$hint" "$pane" "$out_tail" "$err_tail" | tee /dev/fd/3
  exit 1
}

b64_tail() {
  local f="$1"; local limit="${2:-$LOG_TAIL_BYTES}"; if [ -f "$f" ]; then tail -c "$limit" "$f" | base64 | tr -d '\n'; fi;
}

pane_preview() {
  if [ -f "$PANE_FILE" ]; then
    tr -cd '\11\12\15\40-\176' < "$PANE_FILE" | tr '\n' ' ' | sed -E 's/[[:space:]]+/ /g' | head -c 400
  fi
}

capture_pane() {
  local target="$("$TMUX_BIN" -L "$LABEL" list-panes -a -F '#{session_name}:#{window_index}.#{pane_index}' 2>>"$STDERR_LOG" | head -n1)"
  if [ -n "$target" ]; then
    "$TMUX_BIN" -L "$LABEL" capture-pane -t "$target" -p -S -$CAPTURE_LINES -J 2>>"$STDERR_LOG" > "$PANE_FILE.tmp" || true
    head -n $CAPTURE_LINES "$PANE_FILE.tmp" > "$PANE_FILE" 2>>"$STDERR_LOG" || true
    rm -f "$PANE_FILE.tmp" 2>>"$STDERR_LOG" || true
  fi
}

if ! command -v "$TMUX_BIN" >/dev/null 2>&1; then
  error_json "tmux_not_found" "Install tmux (brew install tmux)" "" "" ""
fi
if ! command -v "$CODEX_BIN" >/dev/null 2>&1; then
  error_json "codex_not_found" "Install codex CLI" "" "" ""
fi

# Launch detached tmux running an interactive codex, then drive it with /status and /exit.
CMD="TERM=xterm-256color $CODEX_BIN"
"$TMUX_BIN" -L "$LABEL" new -d -s "$SESSION" "bash -lc '$CMD'" >>"$STDOUT_LOG" 2>>"$STDERR_LOG" || {
  error_json "tmux_launch_failed" "Could not start tmux session" "" "" ""
}

TARGET="$("$TMUX_BIN" -L "$LABEL" list-panes -a -F '#{session_name}:#{window_index}.#{pane_index}' 2>>"$STDERR_LOG" | head -n1)"
sleep 0.6
"$TMUX_BIN" -L "$LABEL" send-keys -t "$TARGET" "/status" Enter >>"$STDOUT_LOG" 2>>"$STDERR_LOG" || true
sleep 2
"$TMUX_BIN" -L "$LABEL" send-keys -t "$TARGET" "/exit" Enter >>"$STDOUT_LOG" 2>>"$STDERR_LOG" || true

# Wait for command to finish or timeout.
elapsed=0
while "$TMUX_BIN" -L "$LABEL" has-session -t "$SESSION" >/dev/null 2>&1 && [ "$elapsed" -lt "$TIMEOUT_SECS" ]; do
  sleep 1
  elapsed=$((elapsed + 1))
  # If pane already dead, break early.
  if "$TMUX_BIN" -L "$LABEL" list-panes -F '#{pane_dead}' -t "$SESSION" 2>/dev/null | grep -q "1"; then
    break
  fi
done

capture_pane

PANE_TEXT=""
if [ -f "$PANE_FILE" ]; then
  PANE_TEXT="$(cat "$PANE_FILE")"
fi

credits_raw=$(printf '%s\n' "$PANE_TEXT" | sed -n 's/.*Credits:[[:space:]]*\\([0-9.,][0-9.,]*\\).*$/\\1/p' | head -n1)
five_pct=$(printf '%s\n' "$PANE_TEXT" | sed -n 's/.*5h limit:.*\\([0-9]\\+\\)% Left.*/\\1/ip' | head -n1)
week_pct=$(printf '%s\n' "$PANE_TEXT" | sed -n 's/.*Weekly limit:.*\\([0-9]\\+\\)% Left.*/\\1/ip' | head -n1)

if [ -z "$credits_raw" ]; then
  error_json "parse_failed" "Could not find credits in codex /status output" "$(pane_preview)" "$(b64_tail "$STDOUT_LOG")" "$(b64_tail "$STDERR_LOG")"
fi

credits_clean="${credits_raw//,/}"
printf '{"ok":true,"credits_remaining":%s,"five_pct_left":%s,"week_pct_left":%s,"pane_preview":"%s"}\n' \
  "$credits_clean" "${five_pct:-null}" "${week_pct:-null}" "$(pane_preview)" | tee /dev/fd/3
