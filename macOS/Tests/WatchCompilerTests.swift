import Foundation
import PDFKit

final class Results: @unchecked Sendable {
    private let condition = NSCondition()
    private var values: [(Int, CompileResult)] = []
    func append(_ revision: Int, _ result: CompileResult) { condition.lock(); values.append((revision, result)); condition.signal(); condition.unlock() }
    func take(_ revision: Int) -> CompileResult {
        condition.lock(); defer { condition.unlock() }
        let deadline = Date().addingTimeInterval(20)
        while true {
            if let i = values.firstIndex(where: { $0.0 == revision }) { return values.remove(at: i).1 }
            guard condition.wait(until: deadline) else { fatalError("No watch result for revision \(revision)") }
        }
    }
}
@main struct WatchCompilerTests {
    static func main() throws {
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: dir) }
        let compiler = WatchCompiler(), results = Results()
        defer { compiler.shutdown() }
        func submit(_ text: String, _ revision: Int, root: URL? = nil, directory: URL? = nil) {
            compiler.submit(.init(source: text, directory: directory ?? dir, root: root ?? dir, document: "test", revision: revision, completion: results.append))
        }
        try "= Dependency".write(to: dir.appendingPathComponent("part.typ"), atomically: true, encoding: .utf8)
        submit("#include \"part.typ\"", 1)
        precondition(results.take(1).success)
        let pid = compiler.processIdentifier
        try "= Updated dependency".write(to: dir.appendingPathComponent("part.typ"), atomically: true, encoding: .utf8)
        let external = results.take(1)
        precondition(external.pdf.flatMap { PDFDocument(data: $0)?.string }?.contains("Updated dependency") == true)
        submit("#set text(font: )", 2)
        let failure = results.take(2)
        precondition(!failure.success && failure.diagnostics.contains("expected expression"))
        submit("= Latest document", 3)
        precondition(results.take(3).success && pid == compiler.processIdentifier)
        print("PASS: one watch process, dependency edits, compiler errors and recovery")
        // Rapid requests must eventually publish the last revision, not stale output.
        for i in 4...15 { submit("= Revision \(i)", i) }
        let latest = results.take(15)
        precondition(latest.pdf.flatMap { PDFDocument(data: $0)?.string }?.contains("Revision 15") == true)
        print("PASS: coalesced edits publish latest revision")
        compiler.shutdown()
        if let project = ProcessInfo.processInfo.environment["STUDIO_TEST_PROJECT"] {
            let root = URL(fileURLWithPath: project), main = URL(fileURLWithPath: project).appendingPathComponent("typst-source/main.typ")
            let source = try String(contentsOf: main, encoding: .utf8)
            submit(source, 20, root: root, directory: main.deletingLastPathComponent())
            let cold = results.take(20); precondition(cold.success)
            let largePID = compiler.processIdentifier
            var times: [Double] = []
            for i in 21...23 {
                submit(source + "\n\nIncremental test \(i).", i, root: root, directory: main.deletingLastPathComponent())
                let warm = results.take(i); precondition(warm.success && compiler.processIdentifier == largePID)
                precondition((warm.pdf.flatMap { PDFDocument(data: $0)?.pageCount } ?? 0) >= 182)
                times.append(warm.elapsed)
            }
            print(String(format: "EMDoc cold %.3fs; incremental %@ seconds", cold.elapsed, times.map { String(format: "%.3f", $0) }.joined(separator: ", ")))
        }
    }
}
