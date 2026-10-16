import AppKit
import Foundation
import PDFKit

@main struct ProjectRegression {
    @MainActor static func waitUntil(_ condition: () -> Bool) async throws {
        for _ in 0..<600 { if condition() { return }; try await Task.sleep(nanoseconds: 50_000_000) }
        fatalError("Timed out waiting for project state")
    }
    @MainActor static func main() async throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("studio-regression-\(UUID().uuidString)").resolvingSymlinksInPath()
        try FileManager.default.createDirectory(at: root.appendingPathComponent("typst-source/sections"), withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let main = root.appendingPathComponent("typst-source/main.typ")
        let include = root.appendingPathComponent("typst-source/sections/part.typ")
        try "= Included chapter\nNested relative import works.".write(to: include, atomically: true, encoding: .utf8)
        try "#include \"sections/part.typ\"".write(to: main, atomically: true, encoding: .utf8)
        try "all:\n\techo test".write(to: root.appendingPathComponent("Makefile"), atomically: true, encoding: .utf8)
        try FileManager.default.createDirectory(at: root.appendingPathComponent("empty"), withIntermediateDirectories: true)
        let model = StudioModel(recoveryLocation: root.appendingPathComponent("recovery.json"))
        defer { model.shutdownCompiler() }
        precondition(model.showWelcome && model.fileURL == nil)
        model.openProject(root)
        try await waitUntil { model.fileURL == main && !model.compiling && model.pdf != nil }
        precondition(model.pdf?.string?.contains("Nested relative import works") == true)
        precondition(model.expandedDirectories.contains(main.deletingLastPathComponent().path))
        precondition(model.files.contains { $0.path == root.appendingPathComponent("Makefile").path })
        precondition(ProjectFiles.children(root).contains { $0.path == root.appendingPathComponent("empty").path })
        precondition(model.pdf?.outlineRoot?.numberOfChildren == 1)
        print("PASS: welcome, folder main selection, nested imports, auto compile, expanded parent, all file kinds, empty folders, PDF outline")
        let matches = ProjectSearch.find("Nested", caseSensitive: false, files: model.files, currentURL: model.fileURL, currentText: model.text)
        precondition(matches.contains { $0.url == include && $0.line == 2 })
        print("PASS: project search includes nested source")
        model.changed("#set text(font: )")
        try await waitUntil { !model.compiling && !model.diagnostics.isEmpty }
        precondition(model.successfulRevision != model.revision && model.pdf != nil)
        precondition(model.errorLine == 1)
        model.changed("= Recovered")
        try await waitUntil { model.successfulRevision == model.revision }
        precondition(model.diagnostics.isEmpty)
        print("PASS: failing edit retains preview, stale export blocked, recovery compile")
        let view = StudioPDFView(frame: NSRect(x: 0, y: 0, width: 700, height: 300))
        view.document = model.pdf; view.fitWidth()
        let scale = view.scaleFactor
        view.frame.size.height = 900; view.fitWidth()
        precondition(abs(view.scaleFactor - scale) < 0.01)
        view.frame.size.width = 900; view.fitWidth()
        precondition(view.scaleFactor > scale)
        print("PASS: fit width independent of height and responsive to pane width")
        let cancel = CompileCancellation()
        DispatchQueue.global().asyncAfter(deadline: .now() + 0.1) { cancel.cancel() }
        let cancelled = Compiler.compile("#while true {}", directory: root, root: root, cancellation: cancel)
        precondition(!cancelled.success && cancelled.elapsed < 2)
        print("PASS: obsolete compilation cancellation")
        let symbols = SymbolPalette.symbols.map { "$ " + $0.1 + " $" }.joined(separator: "\n\n")
        let result = Compiler.compile(symbols, directory: root, root: root)
        precondition(result.success, result.diagnostics)
        print("PASS: all symbol palette expressions compile")
        if let input = ProcessInfo.processInfo.environment["STUDIO_TEST_PROJECT"] {
            let project = URL(fileURLWithPath: input)
            model.dirty = false; model.openProject(project)
            try await waitUntil { model.fileURL?.lastPathComponent == "main.typ" && !model.compiling && model.pdf != nil }
            precondition((model.pdf?.pageCount ?? 0) > 100)
            print("PASS: real project auto-load and compile: \(model.pdf!.pageCount) pages, \(model.elapsed)s")
        }
    }
}
