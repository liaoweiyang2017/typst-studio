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
        let version = model.revision
        model.openFile(preface)
        try await Task.sleep(nanoseconds: 300_000_000)
        precondition(model.pdf === mainPDF && model.compileTargetURL == main && model.revision == version && !model.compiling, "Browsing a file must preserve the PDF and compile entry")
        print("PASS: browsing does not compile or change preview entry")

        model.openFile(preface); model.compile()
        try await wait { model.successfulRevision == model.revision }
        let started = Date(); model.openFile(main); model.compile()
        guard model.pdf === mainPDF else { print("FAIL: switching back discarded the existing PDF"); model.shutdownCompiler(); exit(1) }
        print(String(format: "PASS: cached preview restored synchronously in %.2f ms", Date().timeIntervalSince(started) * 1000))
        try await wait { model.successfulRevision == model.revision }
        // An inactive main document must still observe imported-file changes.
        model.openFile(preface); model.compile()
        try await wait { model.successfulRevision == model.revision }
        try "= Preface\nDependency changed".write(to: preface, atomically: false, encoding: .utf8)
        try await Task.sleep(nanoseconds: 700_000_000)
        model.openFile(main); model.compile()
        try await wait { model.pdf?.string?.contains("Dependency changed") == true }
        print("PASS: inactive document dependencies update its cached preview")
        try "#set text(font: )".write(to: main, atomically: true, encoding: .utf8)
        model.openFile(preface); model.compile(); model.openFile(main); model.compile()
        precondition(model.successfulRevision != model.revision)
        try await wait { !model.diagnostics.isEmpty && !model.compiling }
        print("PASS: disk edits invalidate cached export; errors preserve last good preview")
        for i in 0..<3 {
            let other = root.appendingPathComponent("other-\(i).typ")
            try "= Other \(i)".write(to: other, atomically: true, encoding: .utf8)
            model.openFile(other); model.compile()
            try await wait { model.successfulRevision == model.revision }
        }
        model.openFile(main); model.compile()
        precondition(model.successfulRevision != model.revision, "Evicted entry cannot mark the old preview current")
        try await wait { !model.compiling && !model.diagnostics.isEmpty }
        print("PASS: entry cache evicts beyond three sessions without certifying stale PDF")
        model.changed("= Saved main edit\n#include \"preface.typ\"")
        precondition(model.save())
        model.openFile(preface)
        model.changed("= Immediate included edit")
        try await wait { model.pdf?.string?.contains("Saved main edit") == true && model.pdf?.string?.contains("Immediate included edit") == true && !model.compiling }
        precondition(model.compileTargetURL == main, "Saving then immediately editing an include must keep the entry")
        model.dirty = false
        print("PASS: pending saved entry changes survive an immediate included edit")
        if let path = ProcessInfo.processInfo.environment["STUDIO_TEST_PROJECT"] {
            model.dirty = false; model.openProject(URL(fileURLWithPath: path))
            try await wait { (model.pdf?.pageCount ?? 0) > 100 && model.successfulRevision == model.revision }
            let entry = model.fileURL!, pdf = model.pdf!
            model.openFile(entry.deletingLastPathComponent().appendingPathComponent("preface.typ")); model.compile()
            try await wait { model.successfulRevision == model.revision }
            let start = Date(); model.openFile(entry); model.compile()
            precondition(model.pdf === pdf)
            print(String(format: "EMDoc cached switch: %.2f ms, %d pages", Date().timeIntervalSince(start)*1000, pdf.pageCount))
        }
    }
}
