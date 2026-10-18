import Foundation
import Darwin
struct CompileResult { var pdf: Data?; var diagnostics: String; var elapsed: Double; var success: Bool; var fromCache = false }
final class CompileCancellation: @unchecked Sendable {
    private let lock = NSLock()
    private var value = false
    func cancel() { lock.lock(); value = true; lock.unlock() }
    var isCancelled: Bool { lock.lock(); defer { lock.unlock() }; return value }
}
final class Compiler {
    static let executable: URL = {
        let bundled = Bundle.main.bundleURL.appendingPathComponent("Contents/Helpers/typst")
        // Installed apps must never silently depend on a system installation.
        if Bundle.main.bundleURL.pathExtension == "app" { return bundled }
        return URL(fileURLWithPath: ProcessInfo.processInfo.environment["TYPST_BIN"] ?? "/opt/homebrew/bin/typst")
    }()
    static func compile(_ text: String, directory: URL, root: URL, cancellation: CompileCancellation? = nil) -> CompileResult {
        let start = Date(), temp = FileManager.default.temporaryDirectory.appendingPathComponent("typst-studio-\(UUID().uuidString)")
        do {
            try FileManager.default.createDirectory(at: temp, withIntermediateDirectories: true)
            defer { try? FileManager.default.removeItem(at: temp) }
            let output = temp.appendingPathComponent("preview.pdf"), input = directory.appendingPathComponent(".typst-studio-\(UUID().uuidString).typ"), errors = temp.appendingPathComponent("errors")
            defer { try? FileManager.default.removeItem(at: input) }
            try Data(text.utf8).write(to: input, options: .atomic); FileManager.default.createFile(atPath: errors.path, contents: nil)
            let ih = try FileHandle(forReadingFrom: input), eh = try FileHandle(forWritingTo: errors)
            defer { try? ih.close(); try? eh.close() }
            let p = Process(); p.executableURL = executable; p.currentDirectoryURL = directory
            p.arguments = ["compile", "--root", root.path, "--diagnostic-format", "short", input.path, output.path]
            p.standardInput = ih; p.standardError = eh; p.standardOutput = FileHandle.nullDevice
            try p.run()
            let deadline = Date().addingTimeInterval(120)
            while p.isRunning && Date() < deadline && cancellation?.isCancelled != true { Thread.sleep(forTimeInterval: 0.025) }
            let timeout = p.isRunning
            if timeout {
                p.terminate()
                let shutdownDeadline = Date().addingTimeInterval(1)
                while p.isRunning && Date() < shutdownDeadline { Thread.sleep(forTimeInterval: 0.025) }
                if p.isRunning { kill(p.processIdentifier, SIGKILL) }
            }
            p.waitUntilExit()
            let data = try? Data(contentsOf: output)
            return CompileResult(pdf: data, diagnostics: (timeout ? "Compilation exceeded 120 seconds.\n" : "") + ((try? String(contentsOf: errors, encoding: .utf8)) ?? "").replacingOccurrences(of: input.path, with: "<stdin>").replacingOccurrences(of: input.lastPathComponent, with: "<stdin>"), elapsed: Date().timeIntervalSince(start), success: !timeout && p.terminationStatus == 0 && data != nil)
        } catch { return CompileResult(pdf: nil, diagnostics: error.localizedDescription, elapsed: Date().timeIntervalSince(start), success: false) }
    }
}
