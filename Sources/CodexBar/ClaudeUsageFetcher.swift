import Foundation

struct ClaudeUsageSnapshot {
    let primary: RateWindow
    let secondary: RateWindow
    let updatedAt: Date
    let accountEmail: String?
    let accountOrganization: String?
}

enum ClaudeUsageError: LocalizedError {
    case claudeNotInstalled
    case tmuxNotInstalled
    case parseFailed(String)
    case scriptFailed(Int32, String)

    var errorDescription: String? {
        switch self {
        case .claudeNotInstalled:
            "Claude CLI is not installed. Install it from https://docs.claude.ai/claude-code."
        case .tmuxNotInstalled:
            "tmux is required to probe Claude usage. Install via Homebrew: brew install tmux"
        case .parseFailed(let details):
            "Could not parse Claude usage: \(details)"
        case .scriptFailed(let code, let output):
            "Claude usage probe failed (exit \(code)). Output: \(output)"
        }
    }
}

struct ClaudeUsageFetcher: Sendable {
    private let environment: [String: String]

    init(environment: [String: String] = ProcessInfo.processInfo.environment) {
        self.environment = environment
    }

    func detectVersion() -> String? {
        guard let path = Self.which("claude") else { return nil }
        return Self.readString(cmd: path, args: ["--version"])?.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    func debugRawProbe(model: String = "sonnet") async -> String {
        do {
            let result = try await self.runProbe(model: model)
            return result.output
        } catch {
            return "Probe failed: \(error)"
        }
    }

    func loadLatestUsage(model: String = "sonnet") async throws -> ClaudeUsageSnapshot {
        guard let claudePath = Self.which("claude") else { throw ClaudeUsageError.claudeNotInstalled }
        guard let tmuxPath = Self.which("tmux") else { throw ClaudeUsageError.tmuxNotInstalled }

        let result = try await self.runProbe(model: model, claudePath: claudePath, tmuxPath: tmuxPath)
        guard result.status == 0 else {
            throw ClaudeUsageError.scriptFailed(result.status, result.output.trimmingCharacters(in: .whitespacesAndNewlines))
        }

        return try Self.parse(output: result.output)
    }

    // MARK: - Parsing helpers

    static func parse(json: Data) -> ClaudeUsageSnapshot? {
        guard let output = String(data: json, encoding: .utf8) else { return nil }
        return try? Self.parse(output: output)
    }

    private static func parse(output: String) throws -> ClaudeUsageSnapshot {
        guard
            let data = output.data(using: .utf8),
            let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any]
        else {
            throw ClaudeUsageError.parseFailed(output.prefix(500).description)
        }

        if let ok = obj["ok"] as? Bool, !ok {
            let hint = obj["hint"] as? String ?? (obj["pane_preview"] as? String ?? "")
            throw ClaudeUsageError.parseFailed(hint)
        }

        func makeWindow(_ dict: [String: Any]?) -> RateWindow? {
            guard let dict else { return nil }
            let pct = (dict["pct_used"] as? NSNumber)?.doubleValue ?? 0
            let resetText = dict["resets"] as? String
            return RateWindow(
                usedPercent: pct,
                windowMinutes: nil,
                resetsAt: Self.parseReset(text: resetText),
                resetDescription: resetText)
        }

        guard
            let session = makeWindow(obj["session_5h"] as? [String: Any]),
            let weekAll = makeWindow(obj["week_all_models"] as? [String: Any])
        else {
            throw ClaudeUsageError.parseFailed("missing session/weekly data")
        }

        let rawEmail = (obj["account_email"] as? String)?.trimmingCharacters(in: .whitespacesAndNewlines)
        let email = (rawEmail?.isEmpty ?? true) ? nil : rawEmail
        let rawOrg = (obj["account_org"] as? String)?.trimmingCharacters(in: .whitespacesAndNewlines)
        let org = (rawOrg?.isEmpty ?? true) ? nil : rawOrg
        return ClaudeUsageSnapshot(
            primary: session,
            secondary: weekAll,
            updatedAt: Date(),
            accountEmail: email,
            accountOrganization: org)
    }

    private static func parseReset(text: String?) -> Date? {
        guard let text, !text.isEmpty else { return nil }
        let parts = text.split(separator: "(")
        let timePart = parts.first?.trimmingCharacters(in: .whitespaces)
        let tzPart = parts.count > 1 ? parts[1].replacingOccurrences(of: ")", with: "").trimmingCharacters(in: .whitespaces) : nil
        let tz = tzPart.flatMap(TimeZone.init(identifier:))
        let formats = ["ha", "h:mma", "MMM d 'at' ha", "MMM d 'at' h:mma"]
        for format in formats {
            let df = DateFormatter()
            df.locale = Locale(identifier: "en_US_POSIX")
            df.timeZone = tz ?? TimeZone.current
            df.dateFormat = format
            if let t = timePart, let date = df.date(from: t) { return date }
        }
        return nil
    }

    // MARK: - Process helpers

    private static func which(_ tool: String) -> String? {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/which")
        process.arguments = [tool]
        let pipe = Pipe()
        process.standardOutput = pipe
        try? process.run()
        process.waitUntilExit()
        guard process.terminationStatus == 0 else { return nil }
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        let path = String(decoding: data, as: UTF8.self).trimmingCharacters(in: .whitespacesAndNewlines)
        return path.isEmpty ? nil : path
    }

    private static func readString(cmd: String, args: [String]) -> String? {
        let task = Process()
        task.executableURL = URL(fileURLWithPath: cmd)
        task.arguments = args
        let pipe = Pipe()
        task.standardOutput = pipe
        try? task.run()
        task.waitUntilExit()
        guard task.terminationStatus == 0 else { return nil }
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        return String(decoding: data, as: UTF8.self)
    }

    private func runProbe(model: String, claudePath: String? = nil, tmuxPath: String? = nil) async throws -> (status: Int32, output: String) {
        let claudePath = claudePath ?? (Self.which("claude") ?? "")
        let tmuxPath = tmuxPath ?? (Self.which("tmux") ?? "")
        let scriptURL = try Self.writeProbeScript()
        let task = Process()
        task.executableURL = URL(fileURLWithPath: "/bin/bash")
        task.arguments = [scriptURL.path]
        var env = self.environment
        env["PATH"] = (env["PATH"] ?? "") + ":" + URL(fileURLWithPath: claudePath).deletingLastPathComponent().path
        env["CODEXBAR_CLAUDE_MODEL"] = model
        env["CODEXBAR_CLAUDE_BIN"] = claudePath
        env["CODEXBAR_TMUX_BIN"] = tmuxPath
        let workdir = FileManager.default.temporaryDirectory.appendingPathComponent("cb-claude-usage", isDirectory: true)
        try? FileManager.default.createDirectory(at: workdir, withIntermediateDirectories: true)
        env["CODEXBAR_WORKDIR"] = workdir.path
        if env["CODEXBAR_CLAUDE_TIMEOUT"] == nil { env["CODEXBAR_CLAUDE_TIMEOUT"] = "30" }
        if env["CODEXBAR_CLAUDE_SLEEP_BOOT"] == nil { env["CODEXBAR_CLAUDE_SLEEP_BOOT"] = "0.2" }
        if env["CODEXBAR_CLAUDE_SLEEP_AFTER"] == nil { env["CODEXBAR_CLAUDE_SLEEP_AFTER"] = "1.6" }
        if env["LC_ALL"] == nil { env["LC_ALL"] = "C" }
        task.environment = env

        let pipe = Pipe()
        task.standardOutput = pipe
        task.standardError = pipe

        try task.run()
        task.waitUntilExit()

        let outputData = pipe.fileHandleForReading.readDataToEndOfFile()
        let output = String(decoding: outputData, as: UTF8.self)
        return (task.terminationStatus, output)
    }

    private static func writeProbeScript() throws -> URL {
        let temp = FileManager.default.temporaryDirectory
            .appendingPathComponent("claude_usage_\(UUID().uuidString).sh")
        try script.write(to: temp, atomically: true, encoding: String.Encoding.utf8)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: temp.path)
        return temp
    }

    // Robust tmux probe emitting JSON always.
    private static let script = #"""
#!/usr/bin/env bash
set -Eeuo pipefail

exec 3>&1 4>&2

CLAUDE_BIN="${CODEXBAR_CLAUDE_BIN:-claude}"
TMUX_BIN="${CODEXBAR_TMUX_BIN:-tmux}"
MODEL="${CODEXBAR_CLAUDE_MODEL:-sonnet}"
TIMEOUT_SECS="${CODEXBAR_CLAUDE_TIMEOUT:-30}"
SLEEP_BOOT="${CODEXBAR_CLAUDE_SLEEP_BOOT:-0.2}"
SLEEP_AFTER_USAGE="${CODEXBAR_CLAUDE_SLEEP_AFTER:-1.6}"
WORKDIR="${CODEXBAR_WORKDIR:-$PWD}"

LABEL="cb-cc-$$"
SESSION="usage"
CAPTURE_LINES=400
LOG_TAIL_BYTES=12288
LOG_DIR="$WORKDIR/cb-claude-usage-$LABEL"
mkdir -p "$LOG_DIR"
STDOUT_LOG="$LOG_DIR/script.stdout.log"
STDERR_LOG="$LOG_DIR/script.stderr.log"
PANE_FILE="$LOG_DIR/pane.txt"

exec 1>>"$STDOUT_LOG"
exec 2>>"$STDERR_LOG"

cleanup() { "$TMUX_BIN" -L "$LABEL" kill-server >/dev/null 2>&1 || true; }
trap cleanup EXIT

b64_tail() {
  local f="$1"; local limit="${2:-$LOG_TAIL_BYTES}"; if [ -f "$f" ]; then tail -c "$limit" "$f" | base64 | tr -d '\n'; fi;
}

capture_pane() {
  "$TMUX_BIN" -L "$LABEL" capture-pane -t "$TARGET" -p -S -$CAPTURE_LINES -J 2>>"$STDERR_LOG" > "$PANE_FILE.tmp" || true
  head -n $CAPTURE_LINES "$PANE_FILE.tmp" > "$PANE_FILE" 2>>"$STDERR_LOG" || true
  rm -f "$PANE_FILE.tmp" 2>>"$STDERR_LOG" || true
}

pane_preview() {
  if [ -f "$PANE_FILE" ]; then
    tr -cd '\11\12\15\40-\176' < "$PANE_FILE" | tr '\n' ' ' | sed -E 's/[[:space:]]+/ /g' | head -c 400
  fi
}

error_json() {
  local code="$1"; local hint="$2"; local pane="$3"; local out_tail="$4"; local err_tail="$5"
  echo "{\"ok\":false,\"error\":\"$code\",\"hint\":\"$hint\",\"pane_preview\":\"$pane\",\"stdout_b64\":\"$out_tail\",\"stderr_b64\":\"$err_tail\"}" >&3
  exit 1
}

if ! command -v "$TMUX_BIN" >/dev/null 2>&1; then error_json "tmux_not_found" "Install tmux" "" "" ""; fi
if ! command -v "$CLAUDE_BIN" >/dev/null 2>&1; then error_json "claude_cli_not_found" "Install Claude CLI" "" "" ""; fi

mkdir -p "$WORKDIR"
"$TMUX_BIN" -L "$LABEL" new-session -d -s "$SESSION" "cd '$WORKDIR' && \"$CLAUDE_BIN\" --model $MODEL" >/dev/null 2>&1 || true
WIN_IDX=$("$TMUX_BIN" -L "$LABEL" list-windows -t "$SESSION" -F '#{window_index}' 2>>"$STDERR_LOG" | head -n1)
PANE_IDX=$("$TMUX_BIN" -L "$LABEL" list-panes -t "$SESSION:$WIN_IDX" -F '#{pane_index}' 2>>"$STDERR_LOG" | head -n1)
TARGET="$SESSION:$WIN_IDX.$PANE_IDX"
"$TMUX_BIN" -L "$LABEL" resize-pane -t "$TARGET" -x 120 -y 32 >/dev/null 2>&1 || true

iterations=0; max_iterations=$((TIMEOUT_SECS * 10 / 4)); booted=false
while [ $iterations -lt $max_iterations ]; do
  sleep "$SLEEP_BOOT"
  output=$("$TMUX_BIN" -L "$LABEL" capture-pane -t "$TARGET" -p -J 2>/dev/null || true)
  lower=$(echo "$output" | tr '[:upper:]' '[:lower:]')
  if echo "$lower" | grep -q "do you trust the files in this folder"; then "$TMUX_BIN" -L "$LABEL" send-keys -t "$SESSION:0.0" "1" Enter; sleep 1; continue; fi
  if echo "$lower" | grep -q "select a workspace"; then "$TMUX_BIN" -L "$LABEL" send-keys -t "$SESSION:0.0" Enter; sleep 1; continue; fi
  if echo "$lower" | grep -q "telemetry" && echo "$lower" | grep -q "(y/n)"; then "$TMUX_BIN" -L "$LABEL" send-keys -t "$SESSION:0.0" "n" Enter; sleep 1; continue; fi
  if echo "$lower" | grep -qE '(sign in|login|please run.*claude login)'; then capture_pane; error_json "auth_required" "Run: claude login" "$(pane_preview)" "$(b64_tail "$STDOUT_LOG")" "$(b64_tail "$STDERR_LOG")"; fi
  if echo "$lower" | grep -qiE '(claude code|tab to toggle|try )'; then booted=true; break; fi
  if [ $iterations -gt 5 ] && [ -n "$output" ]; then booted=true; break; fi
  iterations=$((iterations+1))
done

if [ "$booted" = false ]; then
  capture_pane
  error_json "tui_failed_to_boot" "TUI did not boot within ${TIMEOUT_SECS}s" "$(pane_preview)" "$(b64_tail "$STDOUT_LOG")" "$(b64_tail "$STDERR_LOG")"
fi

"$TMUX_BIN" -L "$LABEL" send-keys -t "$TARGET" "/" >/dev/null 2>&1; sleep 0.2
"$TMUX_BIN" -L "$LABEL" send-keys -t "$TARGET" "usage" >/dev/null 2>&1; sleep 0.3
"$TMUX_BIN" -L "$LABEL" send-keys -t "$TARGET" Enter >/dev/null 2>&1

tries=0; usage_output=""
while [ $tries -lt 6 ]; do
  sleep "$SLEEP_AFTER_USAGE"
  "$TMUX_BIN" -L "$LABEL" send-keys -t "$TARGET" Tab Tab Tab >/dev/null 2>&1
  sleep 0.2
  usage_output=$("$TMUX_BIN" -L "$LABEL" capture-pane -t "$TARGET" -p -S -200 -J 2>/dev/null || true)
  if echo "$usage_output" | grep -qi "current session"; then break; fi
  tries=$((tries+1))
done

capture_pane

"$TMUX_BIN" -L "$LABEL" send-keys -t "$TARGET" "/" >/dev/null 2>&1; sleep 0.2
"$TMUX_BIN" -L "$LABEL" send-keys -t "$TARGET" "status" >/dev/null 2>&1; sleep 0.3
"$TMUX_BIN" -L "$LABEL" send-keys -t "$TARGET" Enter >/dev/null 2>&1
sleep 0.6
status_output=$("$TMUX_BIN" -L "$LABEL" capture-pane -t "$TARGET" -p -S -120 -J 2>/dev/null || true)
status_clean=$(printf "%s" "$status_output" | perl -pe 's/\x1B\[[0-9;?]*[[:alpha:]]//g')
account_email=$(echo "$status_clean" | awk '/Email/ {print $0; exit}' | sed -E 's/.*Email[^A-Za-z0-9@+_.-]*//' | xargs)
if [ -z "$account_email" ]; then
  account_email=$(echo "$status_clean" | awk '/Welcome back/ {sub(/.*Welcome back[[:space:]]+/,""); sub(/!.*$/,""); print; exit}' | xargs)
fi
account_org=$(echo "$status_clean" | awk '/Organization/ {print $0; exit}' | sed -E 's/.*Organization[^A-Za-z0-9@+_.-]*//' | xargs)

parse_block() {
  local label="$1"
  local block=$(echo "$usage_output" | awk "/$label/{flag=1;next}/^$/{flag=0}flag")
  local pct=$(echo "$block" | grep -i "% used" | sed -E 's/.*[^0-9]([0-9]{1,3})% used.*/\1/' || echo "")
  local reset=$(echo "$block" | grep -i "Resets" | sed 's/.*Resets *//' | xargs || echo "")
  echo "$pct|$reset"
}

session_data=$(parse_block "Current session"); week_all_data=$(parse_block "Current week \(all models\)"); week_opus_data=$(parse_block "Current week \(Opus\)")

session_pct=${session_data%%|*}; session_reset=${session_data#*|}; week_all_pct=${week_all_data%%|*}; week_all_reset=${week_all_data#*|}

if [ -z "$session_pct" ] || [ -z "$week_all_pct" ]; then
  error_json "parsing_failed" "Failed to extract usage data from TUI" "$(pane_preview)" "$(b64_tail "$STDOUT_LOG")" "$(b64_tail "$STDERR_LOG")"
fi

if [ -n "$week_opus_data" ]; then
  opus_pct=${week_opus_data%%|*}; opus_reset=${week_opus_data#*|}; opus_json="{\"pct_used\": $opus_pct, \"resets\": \"$opus_reset\"}"
else
  opus_json=null
fi

echo "{\"ok\":true,\"session_5h\":{\"pct_used\":$session_pct,\"resets\":\"$session_reset\"},\"week_all_models\":{\"pct_used\":$week_all_pct,\"resets\":\"$week_all_reset\"},\"week_opus\":$opus_json,\"account_email\":\"$account_email\",\"account_org\":\"$account_org\",\"pane_preview\":\"$(pane_preview)\",\"stdout_b64\":\"$(b64_tail "$STDOUT_LOG")\",\"stderr_b64\":\"$(b64_tail "$STDERR_LOG")\"}" >&3
"""#
}
