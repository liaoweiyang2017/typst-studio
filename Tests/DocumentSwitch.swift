import Foundation
import PDFKit
@main struct DocumentSwitch {
    @MainActor static func wait(_ condition: () -> Bool) async throws {
        for _ in 0..<400 { if condition() { return }; try await Task.sleep(nanoseconds: 50_000_000) }
        fatalError("Document did not finish compiling")
    }
    @MainActor static func main() async throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let main = root.appendingPathComponent("main.typ"), preface = root.appendingPathComponent("preface.typ")
        try "= Main\n#include \"preface.typ\"\n#pagebreak()\n= Second page".write(to: main, atomically: true, encoding: .utf8)
        try "= Preface\nOriginal preface".write(to: preface, atomically: true, encoding: .utf8)
        let model = StudioModel(recoveryLocation: root.appendingPathComponent("recovery.json"))
        defer { model.shutdownCompiler() }
        model.openProject(root)
        try await wait { model.pdf != nil && model.successfulRevision == model.revision }
        let mainPDF = model.pdf!
        model.openFile(preface)
        try await wait { model.successfulRevision == model.revision }
        let started = Date(); model.openFile(main)
        guard model.pdf === mainPDF else { print("FAIL: switching back discarded the existing PDF"); model.shutdownCompiler(); exit(1) }
        print(String(format: "PASS: cached preview restored synchronously in %.2f ms", Date().timeIntervalSince(started) * 1000))
        try await wait { model.successfulRevision == model.revision }
        // An inactive main document must still observe imported-file changes.
        model.openFile(preface)
        try await wait { model.successfulRevision == model.revision }
        try "= Preface\nDependency changed".write(to: preface, atomically: false, encoding: .utf8)
        try await Task.sleep(nanoseconds: 700_000_000)
        model.openFile(main)
        try await wait { model.pdf?.string?.contains("Dependency changed") == true }
        print("PASS: inactive document dependencies update its cached preview")
        try "#set text(font: )".write(to: main, atomically: true, encoding: .utf8)
        model.openFile(preface); model.openFile(main)
        precondition(model.successfulRevision != model.revision)
        try await wait { !model.diagnostics.isEmpty && !model.compiling }
        print("PASS: disk edits invalidate cached export; errors preserve last good preview")
        for i in 0..<3 {
            let other = root.appendingPathComponent("other-\(i).typ")
            try "= Other \(i)".write(to: other, atomically: true, encoding: .utf8)
            model.openFile(other)
            try await wait { model.successfulRevision == model.revision }
        }
        model.openFile(main)
        precondition(model.pdf == nil, "Least-recently-used session should have been evicted")
        try await wait { !model.compiling && !model.diagnostics.isEmpty }
        print("PASS: document cache evicts beyond three sessions")
        if let path = ProcessInfo.processInfo.environment["STUDIO_TEST_PROJECT"] {
            model.dirty = false; model.openProject(URL(fileURLWithPath: path))
            try await wait { (model.pdf?.pageCount ?? 0) > 100 && model.successfulRevision == model.revision }
            let entry = model.fileURL!, pdf = model.pdf!
            model.openFile(entry.deletingLastPathComponent().appendingPathComponent("preface.typ"))
            try await wait { model.successfulRevision == model.revision }
            let start = Date(); model.openFile(entry)
            precondition(model.pdf === pdf)
            print(String(format: "EMDoc cached switch: %.2f ms, %d pages", Date().timeIntervalSince(start)*1000, pdf.pageCount))
        }
    }
}
