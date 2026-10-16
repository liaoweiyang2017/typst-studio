import Foundation
@main struct CompilerPerformance {
    static func main() async throws {
        let root: URL, entry: URL, text: String
        if let path = ProcessInfo.processInfo.environment["STUDIO_TEST_PROJECT"] {
            root = URL(fileURLWithPath: path)
            entry = ProjectFiles.mainFile(in: ProjectFiles.index(root), root: root)!
            text = try String(contentsOf: entry, encoding: .utf8)
        } else {
            root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
            try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
            entry = root.appendingPathComponent("main.typ")
            text = (1...180).map { "= Chapter \($0)\n$ integral_0^1 x^2 dif x = 1/3 $\n" }.joined(separator: "#pagebreak()\n")
            try text.write(to: entry, atomically: true, encoding: .utf8)
        }
        let watch = WatchCompiler(), mapped = TinymistCompiler()
        defer { watch.shutdown(); mapped.shutdown(); if ProcessInfo.processInfo.environment["STUDIO_TEST_PROJECT"] == nil { try? FileManager.default.removeItem(at: root) } }
        for backend in ["watch", "tinymist"] {
            for revision in 1...4 {
                let start = Date()
                var completed = false
                let result: CompileResult = await withCheckedContinuation { continuation in
                    let request = WatchCompiler.Request(source: text + "\n// benchmark \(revision)\n", directory: entry.deletingLastPathComponent(), root: root, document: entry.path, revision: revision) { version, result in
                        if version == revision && !completed { completed = true; continuation.resume(returning: result) }
                    }
                    if backend == "watch" { watch.submit(request) } else { mapped.submit(request) }
                }
                precondition(result.success, result.diagnostics)
                print(String(format: "%@ revision %d: %.1f ms (%d PDF bytes)", backend, revision, Date().timeIntervalSince(start) * 1000, result.pdf?.count ?? 0))
            }
        }
    }
}
