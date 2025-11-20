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
        var timeout: TimeInterval = 8.0
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

        let masterHandle = FileHandle(fileDescriptor: master, closeOnDealloc: true)
        let slaveHandle = FileHandle(fileDescriptor: slave, closeOnDealloc: true)

        let proc = Process()
        proc.executableURL = URL(fileURLWithPath: resolved)
        proc.standardInput = slaveHandle
        proc.standardOutput = slaveHandle
        proc.standardError = slaveHandle

        try proc.run()

        if let data = script.data(using: .utf8) {
            try masterHandle.write(contentsOf: data)
        }

        let deadline = Date().addingTimeInterval(options.timeout)
        var buffer = Data()
        while Date() < deadline {
            let chunk = try masterHandle.read(upToCount: 8192) ?? Data()
            buffer.append(chunk)
            if chunk.isEmpty { break }
            // heuristic: stop early if we saw two prompts
            if buffer.contains("/status".data(using: .utf8)!) || buffer.contains("/usage".data(using: .utf8)!) {
                if buffer.contains("Credits".data(using: .utf8)!) || buffer.contains("% left".data(using: .utf8)!) {
                    break
                }
            }
            usleep(120_000) // 120ms
        }

        // try to exit gracefully
        try? masterHandle.write(contentsOf: "/exit\n".data(using: .utf8)!)
        proc.terminate()
        proc.waitUntilExit()

        guard let text = String(data: buffer, encoding: .utf8), !text.isEmpty else {
            throw Error.timedOut
        }

        return Result(text: text)
    }

    static func which(_ tool: String) -> String? {
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
