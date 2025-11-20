import Foundation
import Darwin

/// Executes an interactive CLI inside a pseudo-terminal and returns all captured text.
/// Keeps it minimal so we can reuse for Codex and Claude without tmux.
struct TTYCommandRunner {
    struct Result {
        let text: String
    }

    struct Options {
        var rows: UInt16 = 50
        var cols: UInt16 = 160
        var timeout: TimeInterval = 20.0
        var extraArgs: [String] = []
    }

    enum Error: Swift.Error, LocalizedError {
        case binaryNotFound(String)
        case launchFailed(String)
        case timedOut

        var errorDescription: String? {
            switch self {
            case .binaryNotFound(let bin): "Binary not found on PATH: \(bin)"
            case .launchFailed(let msg): "Failed to launch process: \(msg)"
            case .timedOut: "PTY command timed out."
            }
        }
    }

    func run(binary: String, send script: String, options: Options = Options()) throws -> Result {
        guard let resolved = Self.which(binary) else { throw Error.binaryNotFound(binary) }

        var master: Int32 = -1
        var slave: Int32 = -1
        var term = termios()
        var win = winsize(ws_row: options.rows, ws_col: options.cols, ws_xpixel: 0, ws_ypixel: 0)
        guard openpty(&master, &slave, nil, &term, &win) == 0 else {
            throw Error.launchFailed("openpty failed")
        }
        // Make master non-blocking so read loops don't hang when no data is available.
        _ = fcntl(master, F_SETFL, O_NONBLOCK)

        let masterHandle = FileHandle(fileDescriptor: master, closeOnDealloc: true)
        let slaveHandle = FileHandle(fileDescriptor: slave, closeOnDealloc: true)

        let proc = Process()
        proc.executableURL = URL(fileURLWithPath: resolved)
        proc.arguments = options.extraArgs
        proc.standardInput = slaveHandle
        proc.standardOutput = slaveHandle
        proc.standardError = slaveHandle

        try proc.run()

        func send(_ text: String) throws {
            if let data = text.data(using: .utf8) {
                try masterHandle.write(contentsOf: data)
            }
        }

        let deadline = Date().addingTimeInterval(options.timeout)
        var buffer = Data()
        func readChunk() {
            var tmp = [UInt8](repeating: 0, count: 8192)
            let n = Darwin.read(master, &tmp, tmp.count)
            if n > 0 { buffer.append(contentsOf: tmp.prefix(n)) }
        }

        func containsSession() -> Bool {
            buffer.contains("Current session".data(using: .utf8)!)
        }

        func containsWeek() -> Bool {
            buffer.contains("Current week (all models)".data(using: .utf8)!)
        }

        if script == "/usage" {
            // Boot loop: wait for TUI to be ready and handle first-run prompts.
            let bootDeadline = Date().addingTimeInterval(4.0)
            while Date() < bootDeadline {
                readChunk()
                guard let text = String(data: buffer, encoding: .utf8) else { break }
                let lower = text.lowercased()

                if lower.contains("do you trust the files in this folder") {
                    try send("1\r"); buffer.removeAll(); usleep(300_000); continue
                }
                if lower.contains("select a workspace") {
                    try send("\r"); buffer.removeAll(); usleep(300_000); continue
                }
                if lower.contains("telemetry") && lower.contains("(y/n)") {
                    try send("n\r"); buffer.removeAll(); usleep(300_000); continue
                }
                if lower.contains("sign in") || lower.contains("login") || (lower.contains("please run") && lower.contains("claude login")) {
                    throw Error.launchFailed("Claude CLI requires login (`claude login`).")
                }
                if lower.contains("claude code") || lower.contains("tab to toggle") || lower.contains("try ") || !lower.isEmpty {
                    break
                }
                usleep(150_000)
            }

            // Send `/usage`: open the command palette, type "usage", then press Enter.
            // Claude sometimes drops the very first Enter when the system is busy, so we
            // keep retrying Enter later instead of spamming other navigation keys.
            usleep(800_000) // let the CLI finish booting before we start typing
            try send("/")
            usleep(200_000)
            try send("usage")
            usleep(250_000)
            try send("\r") // initial Enter to execute the command palette entry

            // Read until we see both session and weekly blocks. The CLI occasionally ignores Enter
            // when the host is under load, so we keep re-sending Enter at a sane cadence instead of
            // spraying tabs/escapes that can leave us in autocomplete.
            var gotSession = false
            var gotWeek = false
            var enterRetries = 0
            var lastEnter = Date()
            var resendUsageRetries = 0
            usleep(600_000) // allow usage view to render before detection
            while Date() < deadline {
                readChunk()
                if containsSession() { gotSession = true }
                if containsWeek() { gotWeek = true }
                if gotSession && gotWeek { break }

                // Re-press Enter roughly once per 1.5s until usage shows up or retries are exhausted.
                if Date().timeIntervalSince(lastEnter) >= 1.5 && enterRetries < 10 {
                    try? send("\r")
                    enterRetries += 1
                    lastEnter = Date()
                    usleep(120_000)
                    continue
                }

                // As a stronger nudge, re-send "/usage" + Enter a few times. This mirrors a human
                // re-typing the command when the palette ignored Enter because it was busy.
                if Date().timeIntervalSince(lastEnter) >= 3.0,
                   enterRetries >= 2,
                   resendUsageRetries < 3 {
                    try? send("/usage")
                    usleep(100_000)
                    try? send("\r")
                    resendUsageRetries += 1
                    enterRetries += 1
                    lastEnter = Date()
                    usleep(200_000)
                    continue
                }

                usleep(150_000)
            }
            // After usage appears, read a bit longer to capture percent lines.
            let settleDeadline = Date().addingTimeInterval(2.0)
            while Date() < settleDeadline {
                readChunk()
                usleep(80_000)
            }
        } else {
            // Generic behavior for other commands.
            usleep(400_000) // small boot grace
            try send(script)
            try send("\r")
            usleep(150_000)
            try send("\r")
            try send("\u{1b}")

            while Date() < deadline {
                readChunk()
                if containsSession() { break }
                usleep(120_000)
            }
        }

        // try to exit gracefully
        try? masterHandle.write(contentsOf: "/exit\n".data(using: .utf8)!)
        proc.terminate()
        let waitDeadline = Date().addingTimeInterval(2.0)
        while proc.isRunning && Date() < waitDeadline {
            usleep(100_000)
        }
        if proc.isRunning {
            kill(proc.processIdentifier, SIGKILL)
        }
        proc.waitUntilExit()

        guard let text = String(data: buffer, encoding: .utf8), !text.isEmpty else {
            throw Error.timedOut
        }

        return Result(text: text)
    }

    static func which(_ tool: String) -> String? {
        // First try system PATH
        if let path = runWhich(tool) { return path }
        // Fallback to common locations (Homebrew, local bins)
        let home = NSHomeDirectory()
        let candidates = [
            "/opt/homebrew/bin/\(tool)",
            "/usr/local/bin/\(tool)",
            "\(home)/.local/bin/\(tool)",
            "\(home)/bin/\(tool)"
        ]
        for c in candidates where FileManager.default.isExecutableFile(atPath: c) {
            return c
        }
        return nil
    }

    private static func runWhich(_ tool: String) -> String? {
        let proc = Process()
        proc.executableURL = URL(fileURLWithPath: "/usr/bin/which")
        proc.arguments = [tool]
        let pipe = Pipe()
        proc.standardOutput = pipe
        try? proc.run()
        proc.waitUntilExit()
        guard proc.terminationStatus == 0 else { return nil }
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        let path = String(decoding: data, as: UTF8.self).trimmingCharacters(in: .whitespacesAndNewlines)
        return path.isEmpty ? nil : path
    }
}
