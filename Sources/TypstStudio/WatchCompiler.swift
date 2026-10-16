import Foundation
import Darwin

/// A single serial owner for a long-lived Typst process. Edits are coalesced while
/// compilation is running so each published PDF belongs to one known revision.
final class WatchCompiler: @unchecked Sendable {
    struct Request {
        let source: String
        let directory: URL
        let root: URL
        let document: String
        let revision: Int
        let completion: (Int, CompileResult) -> Void
    }
    private let queue = DispatchQueue(label: "studio.typst.watch", qos: .userInitiated)
    private let key = DispatchSpecificKey<Bool>()
    private var process: Process?
    private var pipe: Pipe?
    private var input: URL?
    private var outputDirectory: URL?
    private var current: Request?
    private var pending: Request?
    private var busy = false
    private var sawCompileStart = false
    private var lastResult: CompileResult?
    private var generation = UUID()
    private var buffer = ""
    private var diagnostics = ""
    private var started = Date()
    private var finishWork: DispatchWorkItem?
    private var watchdog: DispatchWorkItem?
    init() { queue.setSpecific(key: key, value: true) }
    deinit { shutdown() }
    var processIdentifier: Int32? { queue.sync { process?.processIdentifier } }
    func submit(_ request: Request) {
        queue.async { [weak self] in
            guard let self else { return }
            if let old = self.current, old.document != request.document || old.root != request.root || old.directory != request.directory { self.stopProcess() }
            if self.busy { self.pending = request; return }
            if let old = self.current, old.source == request.source, let result = self.lastResult {
                self.current = request
                var cached = result; cached.fromCache = true
                request.completion(request.revision, cached)
            } else { self.write(request) }
        }
    }
    func shutdown() {
        if DispatchQueue.getSpecific(key: key) == true { stopProcess() }
        else { queue.sync { stopProcess() } }
    }
    private func write(_ request: Request) {
        do {
            if input == nil {
                input = request.directory.appendingPathComponent(".typst-studio-live-\(UUID().uuidString).typ")
                outputDirectory = FileManager.default.temporaryDirectory.appendingPathComponent("typst-studio-live-\(UUID().uuidString)")
                try FileManager.default.createDirectory(at: outputDirectory!, withIntermediateDirectories: true)
            }
            current = request; busy = true; sawCompileStart = false; started = Date(); diagnostics = ""
            // Keep the private shadow file inode stable. Atomic rename can lose
            // watch events in File Provider/iCloud directories. Originals are untouched.
            try request.source.write(to: input!, atomically: false, encoding: .utf8)
            armWatchdog()
            if process == nil { try startProcess(request) }
            // Typst reports completion before registering its new dependencies.
            // Re-notify boundedly if an edit landed in that registration window.
            let id = generation
            for delay in [0.15, 0.4, 1.0] {
                queue.asyncAfter(deadline: .now() + delay) { [weak self] in
                    guard let self, self.generation == id, self.busy, !self.sawCompileStart,
                          self.current?.revision == request.revision, let input = self.input else { return }
                    try? request.source.write(to: input, atomically: false, encoding: .utf8)
                }
            }
        } catch {
            request.completion(request.revision, CompileResult(pdf: nil, diagnostics: error.localizedDescription, elapsed: 0, success: false))
            stopProcess()
        }
    }
    private func startProcess(_ request: Request) throws {
        let p = Process(), stderr = Pipe(), id = generation
        p.executableURL = Compiler.executable; p.currentDirectoryURL = request.directory
        p.arguments = ["watch", "--diagnostic-format", "short", "--root", request.root.path, input!.path, outputDirectory!.appendingPathComponent("preview.pdf").path]
        var env = ProcessInfo.processInfo.environment; env["NO_COLOR"] = "1"; env["TERM"] = "dumb"
        p.environment = env; p.standardInput = FileHandle.nullDevice; p.standardOutput = FileHandle.nullDevice; p.standardError = stderr
        stderr.fileHandleForReading.readabilityHandler = { [weak self] handle in
            let data = handle.availableData
            guard !data.isEmpty else { return }
            self?.queue.async { [weak self] in
                guard let self, self.generation == id else { return }
                self.consume(String(decoding: data, as: UTF8.self))
            }
        }
        p.terminationHandler = { [weak self] _ in
            self?.queue.async { [weak self] in
                guard let self, self.generation == id, let request = self.pending ?? self.current else { return }
                let message = self.diagnostics.isEmpty ? "The bundled Typst compiler stopped. Edit or compile again to restart it." : self.diagnostics
                self.stopProcess()
                request.completion(request.revision, CompileResult(pdf: nil, diagnostics: message, elapsed: 0, success: false))
            }
        }
        process = p; pipe = stderr
        try p.run()
    }
    private func consume(_ chunk: String) {
        buffer += chunk
        while let end = buffer.firstIndex(of: "\n") {
            let line = String(buffer[..<end]).trimmingCharacters(in: .whitespacesAndNewlines)
            buffer.removeSubrange(...end)
            if line.contains("] compiling ...") {
                sawCompileStart = true
                if !busy { busy = true; started = Date(); diagnostics = ""; armWatchdog() }
            } else if line.contains("] compiled successfully") || line.contains("] compiled with warnings") { scheduleFinish(success: true) }
            else if line.contains("] compiled with errors") { scheduleFinish(success: false) }
            else if !line.isEmpty && !line.hasPrefix("watching ") && !line.hasPrefix("writing to ") {
                let cleaned = input.map { line.replacingOccurrences(of: $0.path, with: "<stdin>").replacingOccurrences(of: $0.lastPathComponent, with: "<stdin>") } ?? line
                diagnostics += cleaned + "\n"
            }
        }
        // Bound unexpected log output without retaining unlimited memory.
        if buffer.count > 65_536 { buffer = String(buffer.suffix(65_536)) }
        if diagnostics.count > 65_536 { diagnostics = String(diagnostics.suffix(65_536)) }
    }
    private func scheduleFinish(success: Bool) {
        finishWork?.cancel()
        let id = generation
        let work = DispatchWorkItem { [weak self] in
            guard let self, self.generation == id, let request = self.current else { return }
            self.watchdog?.cancel()
            let data = success ? self.outputDirectory.flatMap { try? Data(contentsOf: $0.appendingPathComponent("preview.pdf")) } : nil
            let result = CompileResult(pdf: data, diagnostics: self.diagnostics, elapsed: Date().timeIntervalSince(self.started), success: success && data != nil)
            self.busy = false; self.lastResult = result
            request.completion(request.revision, result)
            if let next = self.pending { self.pending = nil; self.write(next) }
        }
        finishWork = work
        // Typst prints diagnostic lines immediately after its completion status.
        queue.asyncAfter(deadline: .now() + 0.03, execute: work)
    }
    private func armWatchdog() {
        watchdog?.cancel()
        let id = generation
        let work = DispatchWorkItem { [weak self] in
            guard let self, self.generation == id, self.busy, let request = self.pending ?? self.current else { return }
            self.stopProcess()
            request.completion(request.revision, CompileResult(pdf: nil, diagnostics: "Compilation exceeded 60 seconds. Check your source, then compile again.", elapsed: 60, success: false))
        }
        watchdog = work; queue.asyncAfter(deadline: .now() + 60, execute: work)
    }
    private func stopProcess() {
        generation = UUID(); finishWork?.cancel(); watchdog?.cancel()
        pipe?.fileHandleForReading.readabilityHandler = nil
        if let process, process.isRunning {
            process.terminationHandler = nil; process.terminate()
            let deadline = Date().addingTimeInterval(0.3)
            while process.isRunning && Date() < deadline { Thread.sleep(forTimeInterval: 0.01) }
            if process.isRunning { kill(process.processIdentifier, SIGKILL) }
            // Foundation waitUntilExit can wait on the launching run loop.
            // Termination is already bounded above; never block UI shutdown here.
        }
        try? pipe?.fileHandleForReading.close()
        if let input { try? FileManager.default.removeItem(at: input) }
        if let outputDirectory { try? FileManager.default.removeItem(at: outputDirectory) }
        process = nil; pipe = nil; input = nil; outputDirectory = nil
        current = nil; pending = nil; lastResult = nil; busy = false; buffer = ""; diagnostics = ""
    }
}
