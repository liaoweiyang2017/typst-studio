import Foundation
import AppKit
import PDFKit
@main struct TextNavigationTests {
    @MainActor static func wait(_ predicate: () -> Bool) async throws {
        for _ in 0..<200 { if predicate() { return }; try await Task.sleep(nanoseconds: 50_000_000) }
        fatalError("Navigation timed out")
    }
    @MainActor static func main() async throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let unicode = "🙂 标题\n这是*跨行*\n定位测试 ﬁle"
        let ranges = NavigationText(unicode).matches("这是跨行定位测试 file")
        precondition(ranges.count == 1)
        precondition((unicode as NSString).substring(with: ranges[0]) == "这是*跨行*\n定位测试 ﬁle")
        precondition(NavigationText("Repeated phrase\nRepeated phrase").matches("Repeated phrase").count == 2)
        precondition(NavigationText("one").matches("on").isEmpty)
        let main = root.appendingPathComponent("main.typ"), child = root.appendingPathComponent("child.typ")
        let mainText = "= Navigation\n\nUnique forward target paragraph.\n\nRepeated phrase\n\nRepeated phrase\n\n#pagebreak()\n#include \"child.typ\""
        let childText = "= Included chapter\n\nA distinctive inverse navigation sentence."
        try mainText.write(to: main, atomically: true, encoding: .utf8)
        try childText.write(to: child, atomically: true, encoding: .utf8)
        let model = StudioModel(recoveryLocation: root.appendingPathComponent("recovery.json"))
        defer { model.shutdownCompiler() }
        model.openProject(root)
        try await wait { model.successfulRevision == model.revision && model.pdf != nil }
        let view = StudioPDFView(); view.document = model.pdf; model.preview = view
        let editor = NSTextView(); editor.string = model.text; model.editor = editor
        editor.setSelectedRange((model.text as NSString).range(of: "Unique forward target paragraph"))
        model.jumpToPDF()
        try await wait { model.navigationStatus == "PDF location found" }
        precondition(view.currentSelection?.string?.contains("Unique forward target") == true)
        editor.setSelectedRange((model.text as NSString).range(of: "Repeated phrase"))
        model.jumpToPDF()
        try await wait { model.navigationStatus == "Multiple matches. Select a longer phrase." }
        model.jumpFromPDF("distinctive inverse navigation sentence")
        try await wait { model.fileURL == child && model.jumpRange != nil }
        precondition((model.text as NSString).substring(with: model.jumpRange!) == "distinctive inverse navigation sentence")
        // Changing source immediately after a request must invalidate its eventual result.
        model.jumpRange = nil; model.jumpFromPDF("distinctive inverse navigation sentence")
        model.changed(model.text + "\nMore text.")
        try await Task.sleep(nanoseconds: 300_000_000)
        precondition(model.jumpRange == nil)
        print("PASS: UTF-16/Chinese/ligature mapping, wrapped markup, ambiguity, real PDF forward selection, included-file inverse selection and stale-request cancellation")
    }
}
