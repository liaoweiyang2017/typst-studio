import Foundation
import AppKit
import PDFKit
@main struct SourceMappingTests {
    @MainActor static func wait(_ predicate: () -> Bool) async throws {
        for _ in 0..<600 { if predicate() { return }; try await Task.sleep(nanoseconds: 50_000_000) }
        fatalError("Source mapping timed out")
    }
    static func forward(_ engine: TinymistCompiler, file: URL, source: String, token: String) async throws -> DocumentLocation {
        let range = (source as NSString).range(of: token)
        let pos = SourceCoordinates.position(in: source, utf16Offset: range.location)
        let result = await withCheckedContinuation { cont in engine.forward(file: file, line: pos.line, column: pos.column) { cont.resume(returning: $0) } }
        guard case .document(let points) = try result.get(), let point = points.first else { fatalError("No mapped position for \(token)") }
        return point
    }
    @MainActor static func main() async throws {
        let unicode = "🙂e\u{301}中文 alpha"
        let alphaOffset = (unicode as NSString).range(of: "alpha").location
        let unicodePosition = SourceCoordinates.position(in: unicode, utf16Offset: alphaOffset)
        precondition(SourceCoordinates.range(in: unicode, start: [0, unicodePosition.column], end: [0, unicodePosition.column])?.location == alphaOffset)
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let main = root.appendingPathComponent("main.typ"), child = root.appendingPathComponent("child.typ")
        let mainText = "= Main\n$ alpha + sqrt(2) $\n$ frac(1, 2) + integral_0^1 x dif x + sum_(i=0)^3 i $\n#include \"child.typ\""
        let childText = "#pagebreak()\n= 中文章节\n中文 $ alpha + sqrt(2) $\n#rotate(20deg)[$ beta $]"
        try mainText.write(to: main, atomically: true, encoding: .utf8)
        try childText.write(to: child, atomically: true, encoding: .utf8)
        let engine = TinymistCompiler(); defer { engine.shutdown() }
        var delivered = false
        let compiled: CompileResult = await withCheckedContinuation { cont in engine.submit(.init(source: mainText, directory: root, root: root, document: main.path, revision: 1) { _, result in if !delivered { delivered = true; cont.resume(returning: result) } }) }
        precondition(compiled.success, compiled.diagnostics)
        let a = try await forward(engine, file: main, source: mainText, token: "alpha")
        for token in ["sqrt", "frac", "integral", "sum"] {
            let target = try await forward(engine, file: main, source: mainText, token: token)
            precondition(target.page == 1, "Wrong page for \(token)")
        }
        let b = try await forward(engine, file: child, source: childText, token: "alpha")
        precondition(a.page == 1 && b.page == 2, "Identical formulas must map to their own source file/page")
        let inverse = await withCheckedContinuation { cont in engine.inverse(page: b.page, x: b.x + 0.5, y: b.y - 1) { cont.resume(returning: $0) } }
        guard case .source(let location) = try inverse.get() else { fatalError("No inverse mapping") }
        precondition(location.url == child && location.start[0] == 2)
        let sourceRange = SourceCoordinates.range(in: childText, start: location.start, end: location.end)!
        precondition(sourceRange.location == (childText as NSString).range(of: "alpha").location, "Chinese prefix byte/UTF16 mismatch")
        let rotated = try await forward(engine, file: child, source: childText, token: "beta")
        precondition(rotated.page == 2)
        let rotationResult = await withCheckedContinuation { cont in engine.inverse(page: rotated.page, x: rotated.x + 0.5, y: rotated.y - 1) { cont.resume(returning: $0) } }
        guard case .source(let rotationSource) = try rotationResult.get() else { fatalError("Rotated formula did not map back") }
        precondition(rotationSource.url == child && rotationSource.start[0] == 3)
        let noTargetStart = Date()
        let blank = await withCheckedContinuation { cont in engine.inverse(page: 1, x: 0, y: 0) { cont.resume(returning: $0) } }
        if case .success = blank { fatalError("Blank page area must not guess a source target") }
        precondition(Date().timeIntervalSince(noTargetStart) < 2)
        print("PASS: exact identical-formula spans, math identifiers, cross-file mapping, Chinese scalar/UTF-16 positions and rotated frames")

        let interrupted = await withCheckedContinuation { continuation in
            engine.forward(file: main, line: 1, column: 2) { continuation.resume(returning: $0) }
            engine.submit(.init(source: mainText + "\n// New revision", directory: root, root: root, document: main.path, revision: 2) { _, _ in })
        }
        if case .success = interrupted { fatalError("A changed source revision must cancel old navigation") }
        print("PASS: source revision changes cancel pending navigation")
        let model = StudioModel(recoveryLocation: root.appendingPathComponent("recovery.json")); defer { model.shutdownCompiler() }
        model.openProject(root)
        try await wait { model.pdf?.pageCount == 2 && model.successfulRevision == model.revision }
        let editor = NSTextView(); editor.string = mainText
        editor.setSelectedRange((mainText as NSString).range(of: "alpha")); model.editor = editor
        let view = StudioPDFView(); view.document = model.pdf; model.preview = view
        model.jumpToPDF()
        try await wait { model.navigationStatus == "PDF location found" }
        let page = model.pdf!.page(at: 1)!, bounds = page.bounds(for: .mediaBox)
        model.jumpFromPDF(page: page, point: CGPoint(x: b.x + 0.5, y: bounds.maxY - b.y + 1))
        try await wait { model.fileURL == child && model.jumpRange != nil }
        precondition(model.pdf?.pageCount == 2, "Inverse navigation must preserve the main PDF")
        try await wait { !model.compiling && model.successfulRevision == model.revision }
        model.changed(childText + "\n#pagebreak()\n= Unsaved included edit")
        try await wait { model.pdf?.pageCount == 3 && model.successfulRevision == model.revision }
        let onDisk = try String(contentsOf: child, encoding: .utf8)
        precondition(onDisk == childText, "Unsaved included text must remain in memory")
        print("PASS: native model inverse jump preserves main PDF, included unsaved edits compile through the same entry without writing source")
    }
}
