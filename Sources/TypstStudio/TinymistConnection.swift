import Foundation
import Darwin

/// One serial owner for the child process and Content-Length framed JSON-RPC.
final class TinymistConnection: @unchecked Sendable {
    let queue = DispatchQueue(label: "studio.tinymist", qos: .userInitiated)
    private var process: Process?
    private var input: FileHandle?
    private var output: FileHandle?
    private var buffer = Data()
    private var nextID = 0
    private var pending: [Int: (Result<Any, Error>) -> Void] = [:]
    var notification: ((String, [String: Any]) -> Void)?
    var terminated: (() -> Void)?
    var processIdentifier: Int32? { process?.processIdentifier }
    static var executable: URL {
        if Bundle.main.bundleURL.pathExtension == "app" { return Bundle.main.bundleURL.appendingPathComponent("Contents/Helpers/tinymist") }
        return URL(fileURLWithPath: ProcessInfo.processInfo.environment["TINYMIST_BIN"] ?? FileManager.default.currentDirectoryPath + "/tools/tinymist/target/release/tinymist")
    }
    func start(root: URL) throws {
        buffer.removeAll()
        let p = Process(), stdin = Pipe(), stdout = Pipe()
        p.executableURL = Self.executable; p.arguments = ["lsp"]; p.currentDirectoryURL = root
        p.standardInput = stdin; p.standardOutput = stdout; p.standardError = FileHandle.nullDevice
        input = stdin.fileHandleForWriting; output = stdout.fileHandleForReading
        output?.readabilityHandler = { [weak self] handle in
            let data = handle.availableData
            guard !data.isEmpty else { handle.readabilityHandler = nil; return }
            self?.queue.async { [weak self] in self?.consume(data) }
        }
        p.terminationHandler = { [weak self] _ in self?.queue.async { [weak self] in self?.failAll("Tinymist stopped unexpectedly."); self?.terminated?() } }
        try p.run(); process = p
    }
    func send(_ method: String, _ params: [String: Any]) { write(["jsonrpc": "2.0", "method": method, "params": params]) }
    func request(_ method: String, _ params: [String: Any], completion: @escaping (Result<Any, Error>) -> Void) {
        nextID += 1; let id = nextID; pending[id] = completion
        write(["jsonrpc": "2.0", "id": id, "method": method, "params": params])
        queue.asyncAfter(deadline: .now() + 60) { [weak self] in
            self?.pending.removeValue(forKey: id)?(.failure(Self.error("Tinymist request timed out.")))
        }
    }
    func command(_ command: String, _ arguments: [Any], completion: @escaping (Result<Any, Error>) -> Void) {
        request("workspace/executeCommand", ["command": command, "arguments": arguments], completion: completion)
    }
    private func write(_ object: [String: Any]) {
        do {
            let body = try JSONSerialization.data(withJSONObject: object)
            var data = Data("Content-Length: \(body.count)\r\n\r\n".utf8); data.append(body)
            try input?.write(contentsOf: data)
        } catch { failAll(error.localizedDescription) }
    }
    private func consume(_ data: Data) {
        buffer.append(data)
        while let boundary = buffer.range(of: Data("\r\n\r\n".utf8)) {
            guard let header = String(data: buffer[..<boundary.lowerBound], encoding: .utf8),
                  let lengthLine = header.components(separatedBy: "\r\n").first(where: { $0.lowercased().hasPrefix("content-length:") }),
                  let count = Int(lengthLine.split(separator: ":")[1].trimmingCharacters(in: .whitespaces)), count > 0, count <= 256 * 1024 * 1024 else { failAll("Invalid Tinymist frame."); buffer.removeAll(); return }
            let start = boundary.upperBound
            guard buffer.count - start >= count else { return }
            let body = buffer.subdata(in: start..<start + count); buffer.removeSubrange(0..<start + count)
            guard let message = try? JSONSerialization.jsonObject(with: body) as? [String: Any] else { continue }
            if let method = message["method"] as? String {
                let params = message["params"] as? [String: Any] ?? [:]
                notification?(method, params)
                if let id = message["id"] {
                    write(["jsonrpc": "2.0", "id": id, "result": method == "window/showDocument" ? ["success": true] : NSNull()])
                }
            } else if let id = message["id"] as? Int, let completion = pending.removeValue(forKey: id) {
                if let error = message["error"] as? [String: Any] { completion(.failure(Self.error(error["message"] as? String ?? "Tinymist request failed."))) }
                else { completion(.success(message["result"] ?? NSNull())) }
            }
        }
    }
    static func error(_ message: String) -> NSError { NSError(domain: "TypstStudio.Tinymist", code: 1, userInfo: [NSLocalizedDescriptionKey: message]) }
    private func failAll(_ message: String) {
        let callbacks = pending.values; pending.removeAll()
        callbacks.forEach { $0(.failure(Self.error(message))) }
    }
    func stop() {
        output?.readabilityHandler = nil; process?.terminationHandler = nil
        if let p = process, p.isRunning { p.terminate(); queue.asyncAfter(deadline: .now() + 1) { if p.isRunning { kill(p.processIdentifier, SIGKILL) } } }
        process = nil; try? input?.close(); input = nil; output = nil; buffer.removeAll()
        failAll("Tinymist session closed.")
    }
}
