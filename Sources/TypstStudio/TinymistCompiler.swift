import Foundation

struct SourceLocation { let url: URL; let start: [Int]; let end: [Int] }
struct DocumentLocation { let page: Int; let x: Double; let y: Double }
enum MappedLocation { case document([DocumentLocation]); case source(SourceLocation) }

/// PDF export and source-span lookup share one incremental compiler world.
final class TinymistCompiler: @unchecked Sendable {
    typealias Request = WatchCompiler.Request
    private let connection = TinymistConnection()
    private var current: Request?
    private var pending: Request?
    private var ready = false, busy = false, stopped = false
    private var externalCompile = false
    private var documents: [String: String] = [:]
    private var versions: [String: Int] = [:]
    private var lastResult: CompileResult?
    private var lastPDF: Data?
    private var diagnostics: [String: String] = [:]
    private var socket: URLSessionWebSocketTask?
    private var socketSession: URLSession?
    private var previewReady = false
    private var previewStarting = false
    private var previewWaiters: [(Result<Void, Error>) -> Void] = []
    private var navigation: ((Result<MappedLocation, Error>) -> Void)?
    private var navigationID = UUID()
    var processIdentifier: Int32? { connection.processIdentifier }
    private var entry: String { current?.document == "untitled" ? current!.directory.appendingPathComponent(".typst-studio-untitled.typ").path : current!.document }
    func submit(_ request: Request) {
        connection.queue.async { [self] in
            guard !stopped else { return }
            pending = request
            if !ready && current == nil { current = request; start(request) }
            else if ready && !busy { drain() }
        }
    }
    private func start(_ request: Request) {
        do {
            try connection.start(root: request.root)
            connection.notification = { [weak self] method, params in self?.receive(method, params) }
            connection.terminated = { [weak self] in self?.fail("Tinymist stopped. Reopen the project to restart it.") }
            connection.request("initialize", ["processId": NSNull(), "rootUri": request.root.absoluteString,
                "capabilities": ["general": ["positionEncodings": ["utf-16"]]],
                "initializationOptions": ["exportPdf": "never", "rootPath": request.root.path, "compileStatus": "enable", "customizedShowDocument": true,
                    "preview": ["background": ["enabled": false], "browsing": ["enabled": false]]]]) { [weak self] result in
                guard let self else { return }
                switch result {
                case .failure(let error): self.fail(error.localizedDescription)
                case .success:
                    self.connection.send("initialized", [:]); self.ready = true
                    self.drain()
                }
            }
        } catch { fail(error.localizedDescription) }
    }
    private func update(_ path: String, source: String) {
        guard documents[path] != source else { return }
        let uri = URL(fileURLWithPath: path).absoluteString
        versions[path, default: 0] += 1
        if documents[path] == nil {
            connection.send("textDocument/didOpen", ["textDocument": ["uri": uri, "languageId": "typst", "version": versions[path]!, "text": source]])
        } else {
            connection.send("textDocument/didChange", ["textDocument": ["uri": uri, "version": versions[path]!], "contentChanges": [["text": source]]])
        }
        documents[path] = source
    }
    /// Keep the same entry when editing a source reached through an inverse jump.
    func updateIncludedFile(_ url: URL, source: String, revision: Int, completion: @escaping (Int, CompileResult) -> Void) {
        connection.queue.async { [self] in
            guard let old = current, ready else { return }
            if documents[url.path] != source { finishNavigation(.failure(TinymistConnection.error("Document changed. Try navigating again."))) }
            update(url.path, source: source)
            pending = Request(source: old.source, directory: old.directory, root: old.root, document: old.document, revision: revision, completion: completion)
            lastResult = nil
            if !busy { drain() }
        }
    }
    func closeIncludedFiles() {
        connection.queue.async { [self] in
            guard current != nil else { return }
            for path in Array(documents.keys) where path != entry {
                connection.send("textDocument/didClose", ["textDocument": ["uri": URL(fileURLWithPath: path).absoluteString]])
                documents.removeValue(forKey: path); versions.removeValue(forKey: path)
            }
            lastResult = nil
        }
    }
    private func drain() {
        guard let request = pending, !stopped else { return }; pending = nil
        let unchanged = current?.source == request.source
        current = request
        if unchanged, let result = lastResult {
            var cached = result; cached.fromCache = true; request.completion(request.revision, cached); return
        }
        finishNavigation(.failure(TinymistConnection.error("Document changed. Try navigating again.")))
        busy = true
        update(entry, source: request.source)
        connection.command("tinymist.pinMain", [entry]) { [weak self] _ in self?.export(request) }
    }
    private func export(_ request: Request, then: (() -> Void)? = nil) {
        guard !stopped else { return }
        busy = true; let started = Date()
        connection.command("tinymist.exportPdf", [entry, [:], ["write": false]]) { [weak self] result in
            guard let self, !self.stopped else { return }
            let data = (try? result.get() as? [String: Any])?["data"] as? String
            let pdf = data.flatMap { Data(base64Encoded: $0) }
            let error: String
            if case .failure(let reason) = result { error = reason.localizedDescription } else { error = "" }
            let details = self.diagnostics.values.filter { !$0.isEmpty }.sorted().joined(separator: "\n")
            let compiled = CompileResult(pdf: pdf, diagnostics: details.isEmpty ? error : details, elapsed: Date().timeIntervalSince(started), success: pdf != nil, fromCache: pdf != nil && pdf == self.lastPDF)
            if let pdf { self.lastPDF = pdf }
            self.lastResult = compiled; self.busy = false
            request.completion(request.revision, compiled)
            if self.pending != nil { self.drain() } else { then?() }
        }
    }
    private func receive(_ method: String, _ params: [String: Any]) {
        if method == "textDocument/publishDiagnostics", let uri = params["uri"] as? String {
            diagnostics[uri] = (params["diagnostics"] as? [[String: Any]] ?? []).map { d in
                let range = d["range"] as? [String: Any], start = range?["start"] as? [String: Int]
                return "\(URL(string: uri)?.path ?? uri):\((start?["line"] ?? 0) + 1):\((start?["character"] ?? 0) + 1): \(d["message"] as? String ?? "")"
            }.joined(separator: "\n")
        } else if method == "tinymist/compileStatus", let state = params["status"] as? String {
            if state == "compiling" {
                externalCompile = !busy
                if previewReady && !previewStarting { finishNavigation(.failure(TinymistConnection.error("Document changed. Try navigating again."))) }
            }
            else if externalCompile, !busy, let current, ready {
                // Status also carries word counts; only a compilation transition triggers export.
                externalCompile = false; lastResult = nil; export(current)
            }
        } else if method == "tinymist/preview/scrollSource" {
            if let path = params["filepath"] as? String, !path.isEmpty,
               let start = params["start"] as? [Int], let end = params["end"] as? [Int] {
                finishNavigation(.success(.source(.init(url: URL(fileURLWithPath: path), start: start, end: end))))
            } else { finishNavigation(.failure(TinymistConnection.error("No source span at this position. Try clicking a visible glyph."))) }
        }
    }
    private func ensurePreview(_ completion: @escaping (Result<Void, Error>) -> Void) {
        if previewReady { completion(.success(())); return }
        previewWaiters.append(completion)
        guard !previewStarting else { return }; previewStarting = true
        connection.command("tinymist.doStartPreview", [[entry, "--root=\(current!.root.path)", "--task-id=studio", "--data-plane-host=127.0.0.1:0", "--control-plane-host=127.0.0.1:0", "--no-open"]]) { [weak self] result in
            guard let self else { return }
            guard let value = try? result.get() as? [String: Any], let port = value["dataPlanePort"] as? Int else { self.finishPreview(.failure(TinymistConnection.error("Could not start the source map service."))); return }
            let session = URLSession(configuration: .ephemeral); self.socketSession = session
            let socket = session.webSocketTask(with: URL(string: "ws://127.0.0.1:\(port)/")!); self.socket = socket
            socket.maximumMessageSize = 128 * 1024 * 1024
            socket.resume(); self.readSocket(socket)
            Task { try? await socket.send(.string("current")) }
            self.connection.queue.asyncAfter(deadline: .now() + 15) { [weak self] in
                guard let self, !self.previewReady else { return }
                self.finishPreview(.failure(TinymistConnection.error("Source map preparation timed out.")))
            }
        }
    }
    private func readSocket(_ socket: URLSessionWebSocketTask) {
        Task { [weak self] in
            do {
                let message = try await socket.receive()
                self?.connection.queue.async { [weak self] in
                    guard let self, self.socket === socket else { return }
                    let data: Data
                    switch message { case .data(let d): data = d; case .string(let s): data = Data(s.utf8); @unknown default: return }
                    if data.starts(with: Data("jump,".utf8)), let text = String(data: data, encoding: .utf8) {
                        let positions = text.split(separator: ",").dropFirst().compactMap { part -> DocumentLocation? in
                            let fields = part.split(separator: " ")
                            guard fields.count == 3, let p = Int(fields[0]), let x = Double(fields[1]), let y = Double(fields[2]) else { return nil }
                            return DocumentLocation(page: p, x: x, y: y)
                        }
                        self.finishNavigation(.success(.document(positions)))
                    } else if !self.previewReady, !data.starts(with: Data("invert-colors".utf8)) { self.finishPreview(.success(())) }
                    self.readSocket(socket)
                }
            } catch { self?.connection.queue.async { [weak self] in self?.finishPreview(.failure(error)); self?.finishNavigation(.failure(error)) } }
        }
    }
    private func finishPreview(_ result: Result<Void, Error>) {
        if case .success = result { previewReady = true }
        previewStarting = false
        let waiters = previewWaiters; previewWaiters.removeAll(); waiters.forEach { $0(result) }
    }
    func forward(file: URL, line: Int, column: Int, completion: @escaping (Result<MappedLocation, Error>) -> Void) {
        navigate(completion) { [self] in
            connection.command("tinymist.scrollPreview", ["studio", ["event": "panelScrollTo", "filepath": file.path, "line": line, "character": column]]) { _ in }
        }
    }
    func inverse(page: Int, x: Double, y: Double, completion: @escaping (Result<MappedLocation, Error>) -> Void) {
        navigate(completion) { [self] in
            let object: [String: Any] = ["page_no": page, "x": x, "y": y]
            guard let data = try? JSONSerialization.data(withJSONObject: object), let text = String(data: data, encoding: .utf8), let socket else { return }
            Task { try? await socket.send(.string("src-point " + text)) }
        }
    }
    private func navigate(_ completion: @escaping (Result<MappedLocation, Error>) -> Void, action: @escaping () -> Void) {
        connection.queue.async { [self] in
            guard ready, !busy, navigation == nil, lastResult?.success == true else { completion(.failure(TinymistConnection.error("Wait for compilation to finish, then try again."))); return }
            navigation = completion; navigationID = UUID(); let id = navigationID
            let wasReady = previewReady
            ensurePreview { [weak self] result in
                guard let self, self.navigationID == id else { return }
                if case .failure(let e) = result { self.finishNavigation(.failure(e)) }
                else if !wasReady, let current = self.current { self.export(current, then: action) }
                else { action() }
            }
            connection.queue.asyncAfter(deadline: .now() + 20) { [weak self] in
                guard let self, self.navigationID == id else { return }
                self.finishNavigation(.failure(TinymistConnection.error("No source span at this position. Try clicking a visible glyph.")))
            }
        }
    }
    private func finishNavigation(_ result: Result<MappedLocation, Error>) { let callback = navigation; navigation = nil; navigationID = UUID(); callback?(result) }
    private func fail(_ error: String) {
        let request = pending ?? current
        ready = false; busy = false; pending = nil; current = nil; lastResult = nil; lastPDF = nil
        documents.removeAll(); versions.removeAll(); diagnostics.removeAll()
        socket?.cancel(with: .goingAway, reason: nil); socketSession?.invalidateAndCancel(); socket = nil
        previewReady = false; previewStarting = false
        finishPreview(.failure(TinymistConnection.error(error)))
        finishNavigation(.failure(TinymistConnection.error(error)))
        request?.completion(request!.revision, CompileResult(pdf: nil, diagnostics: error, elapsed: 0, success: false))
    }
    func shutdown() {
        connection.queue.async { [self] in
            stopped = true; pending = nil; current = nil
            socket?.cancel(with: .goingAway, reason: nil); socketSession?.invalidateAndCancel(); socket = nil
            finishNavigation(.failure(TinymistConnection.error("Session closed."))); connection.stop()
        }
    }
}
