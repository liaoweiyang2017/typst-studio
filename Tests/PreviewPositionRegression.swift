import AppKit
import SwiftUI
import PDFKit
@main struct PreviewPositionRegression {
    @MainActor static func wait(_ condition: () -> Bool) async throws {
        for _ in 0..<200 { if condition() { return }; try await Task.sleep(nanoseconds: 40_000_000) }
        fatalError("Preview did not become ready")
    }
    @MainActor static func main() throws {
        _ = NSApplication.shared
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        let main = root.appendingPathComponent("main.typ"), preface = root.appendingPathComponent("preface.typ")
        try (1...6).map { "= Page \($0)\nText" }.joined(separator: "\n#pagebreak()\n").write(to: main, atomically: true, encoding: .utf8)
        try "= Preface".write(to: preface, atomically: true, encoding: .utf8)
        let model = StudioModel(recoveryLocation: root.appendingPathComponent("recovery.json"))
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 600, height: 700), styleMask: [.titled, .resizable], backing: .buffered, defer: false)
        window.contentView = NSHostingView(rootView: PreviewPane(model: model)); window.orderFront(nil)
        Task { @MainActor in
            do {
            model.openProject(root)
            try await wait { model.preview?.document?.pageCount == 6 && !model.compiling }
            let view = model.preview as! StudioPDFView
            view.fitsWidth = false; view.scaleFactor = 1.1
            view.go(to: PDFDestination(page: view.document!.page(at: 3)!, at: CGPoint(x: 0, y: 600)))
            try await Task.sleep(nanoseconds: 150_000_000)
            let before = view.currentDestination!, page = view.document!.index(for: before.page!)
            model.openFile(preface)
            try await wait { model.preview?.document?.pageCount == 1 && !model.compiling }
            model.openFile(main)
            try await wait { model.preview?.document?.pageCount == 6 && !model.compiling }
            try await Task.sleep(nanoseconds: 150_000_000)
            let restored = model.preview as! StudioPDFView
            let after = restored.currentDestination!, actual = restored.document!.index(for: after.page!)
            let good = actual == page && abs(after.point.y-before.point.y)<6 && abs(restored.scaleFactor-1.1)<0.02 && !restored.fitsWidth
            print(good ? "PASS: page, viewport offset and manual zoom restored across file switches" : "FAIL: saved page \(page), restored \(actual), y delta \(after.point.y-before.point.y), zoom \(restored.scaleFactor)")
            model.shutdownCompiler(); try? FileManager.default.removeItem(at: root)
            exit(good ? 0 : 1)
            } catch { model.shutdownCompiler(); print("FAIL: \(error)"); exit(1) }
        }
        NSApp.run()
    }
}
