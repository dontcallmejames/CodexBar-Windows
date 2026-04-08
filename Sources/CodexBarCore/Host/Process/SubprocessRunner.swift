#if canImport(Darwin)
import Darwin
#else
import Glibc
#endif
import Foundation

public enum SubprocessRunnerError: LocalizedError, Sendable {
    case binaryNotFound(String)
    case launchFailed(String)
    case timedOut(String)
    case nonZeroExit(code: Int32, stderr: String)

    public var errorDescription: String? {
        switch self {
        case let .binaryNotFound(binary):
            return "Missing CLI '\(binary)'. Install it and restart CodexBar."
        case let .launchFailed(details):
            return "Failed to launch process: \(details)"
        case let .timedOut(label):
            return "Command timed out: \(label)"
        case let .nonZeroExit(code, stderr):
            let trimmed = stderr.trimmingCharacters(in: .whitespacesAndNewlines)
            if trimmed.isEmpty {
                return "Command failed with exit code \(code)."
            }
            return "Command failed (\(code)): \(trimmed)"
        }
    }
}

public struct SubprocessResult: Sendable {
    public let stdout: String
    public let stderr: String
}

public enum SubprocessRunner {
    private static let log = CodexBarLog.logger(LogCategories.subprocess)

    /// Thread-safe flag for communicating between concurrent tasks (e.g. timeout → caller).
    private final class KillFlag: @unchecked Sendable {
        private let lock = NSLock()
        private var value = false

        func set() {
            self.lock.withLock { self.value = true }
        }

        var isSet: Bool {
            self.lock.withLock { self.value }
        }
    }

    // MARK: - Helpers to move blocking calls off the cooperative thread pool

    /// Runs `readDataToEndOfFile()` on a GCD thread so it does not block the Swift cooperative pool.
    private static func readDataOffPool(_ fileHandle: FileHandle) async -> Data {
        await withCheckedContinuation { continuation in
            DispatchQueue.global().async {
                let data = fileHandle.readDataToEndOfFile()
                continuation.resume(returning: data)
            }
        }
    }

    /// Runs `waitUntilExit()` on a GCD thread so it does not block the Swift cooperative pool.
    private static func waitForExitOffPool(_ process: Process) async -> Int32 {
        await withCheckedContinuation { continuation in
            DispatchQueue.global().async {
                process.waitUntilExit()
                continuation.resume(returning: process.terminationStatus)
            }
        }
    }

    /// Terminates a process and its process group, escalating from SIGTERM to SIGKILL.
    /// Returns `true` if the process was actually killed, `false` if it had already exited.
    @discardableResult
    private static func terminateProcess(_ process: Process, processGroup: pid_t?) -> Bool {
        guard process.isRunning else { return false }
        process.terminate()
        if let pgid = processGroup {
            kill(-pgid, SIGTERM)
        }
        let killDeadline = Date().addingTimeInterval(0.4)
        while process.isRunning, Date() < killDeadline {
            usleep(50000)
        }
        if process.isRunning {
            if let pgid = processGroup {
                kill(-pgid, SIGKILL)
            }
            kill(process.processIdentifier, SIGKILL)
        }
        return true
    }

    // MARK: - Public API

    public static func run(
        binary: String,
        arguments: [String],
        environment: [String: String],
        timeout: TimeInterval,
        label: String) async throws -> SubprocessResult
    {
        guard FileManager.default.isExecutableFile(atPath: binary) else {
            throw SubprocessRunnerError.binaryNotFound(binary)
        }

        let start = Date()
        let binaryName = URL(fileURLWithPath: binary).lastPathComponent
        self.log.debug(
            "Subprocess start",
            metadata: ["label": label, "binary": binaryName, "timeout": "\(timeout)"])

        let process = Process()
        process.executableURL = URL(fileURLWithPath: binary)
        process.arguments = arguments
        process.environment = environment

        let stdoutPipe = Pipe()
        let stderrPipe = Pipe()
        process.standardOutput = stdoutPipe
        process.standardError = stderrPipe
        process.standardInput = nil

        let stdoutTask = Task<Data, Never> {
            await self.readDataOffPool(stdoutPipe.fileHandleForReading)
        }
        let stderrTask = Task<Data, Never> {
            await self.readDataOffPool(stderrPipe.fileHandleForReading)
        }

        do {
            try process.run()
        } catch {
            stdoutTask.cancel()
            stderrTask.cancel()
            stdoutPipe.fileHandleForReading.closeFile()
            stderrPipe.fileHandleForReading.closeFile()
            throw SubprocessRunnerError.launchFailed(error.localizedDescription)
        }

        let pid = process.processIdentifier
        let processGroup: pid_t? = setpgid(pid, pid) == 0 ? pid : nil

        let exitCodeTask = Task<Int32, Never> {
            await self.waitForExitOffPool(process)
        }

        let killedByTimeout = KillFlag()

        do {
            let exitCode = try await withThrowingTaskGroup(of: Int32.self) { group in
                group.addTask { await exitCodeTask.value }
                group.addTask {
                    try await Task.sleep(for: .seconds(timeout))
                    // Kill the process BEFORE throwing so the exit-code task can complete
                    // and withThrowingTaskGroup can exit promptly. Only throw if we
                    // actually killed the process; if it already exited, let the exit
                    // code win the race naturally.
                    guard self.terminateProcess(process, processGroup: processGroup) else {
                        return await exitCodeTask.value
                    }
                    killedByTimeout.set()
                    throw SubprocessRunnerError.timedOut(label)
                }
                let code = try await group.next()!
                group.cancelAll()
                return code
            }

            // Race guard: our timeout task killed the process, but the exit code
            // arrived at group.next() before the .timedOut throw. Use the explicit
            // flag instead of wall-clock heuristics to avoid misclassifying processes
            // that crash or are killed externally.
            if killedByTimeout.isSet {
                let duration = Date().timeIntervalSince(start)
                self.log.warning(
                    "Subprocess timed out (race)",
                    metadata: [
                        "label": label,
                        "binary": binaryName,
                        "duration_ms": "\(Int(duration * 1000))",
                    ])
                stdoutTask.cancel()
                stderrTask.cancel()
                stdoutPipe.fileHandleForReading.closeFile()
                stderrPipe.fileHandleForReading.closeFile()
                throw SubprocessRunnerError.timedOut(label)
            }

            let stdoutData = await stdoutTask.value
            let stderrData = await stderrTask.value
            let stdout = String(data: stdoutData, encoding: .utf8) ?? ""
            let stderr = String(data: stderrData, encoding: .utf8) ?? ""

            if exitCode != 0 {
                let duration = Date().timeIntervalSince(start)
                self.log.warning(
                    "Subprocess failed",
                    metadata: [
                        "label": label,
                        "binary": binaryName,
                        "status": "\(exitCode)",
                        "duration_ms": "\(Int(duration * 1000))",
                    ])
                throw SubprocessRunnerError.nonZeroExit(code: exitCode, stderr: stderr)
            }

            let duration = Date().timeIntervalSince(start)
            self.log.debug(
                "Subprocess exit",
                metadata: [
                    "label": label,
                    "binary": binaryName,
                    "status": "\(exitCode)",
                    "duration_ms": "\(Int(duration * 1000))",
                ])
            return SubprocessResult(stdout: stdout, stderr: stderr)
        } catch {
            let duration = Date().timeIntervalSince(start)
            self.log.warning(
                "Subprocess error",
                metadata: [
                    "label": label,
                    "binary": binaryName,
                    "duration_ms": "\(Int(duration * 1000))",
                ])
            // Safety net: ensure the process is dead (may already be killed by timeout task).
            self.terminateProcess(process, processGroup: processGroup)
            exitCodeTask.cancel()
            stdoutTask.cancel()
            stderrTask.cancel()
            stdoutPipe.fileHandleForReading.closeFile()
            stderrPipe.fileHandleForReading.closeFile()
            throw error
        }
    }
}
