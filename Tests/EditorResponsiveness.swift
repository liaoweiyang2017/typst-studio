import AppKit
import SwiftUI

@main struct EditorResponsiveness {
    @MainActor static func main() throws {
        _ = NSApplication.shared
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        let source = root.appendingPathComponent("main.typ")
        try "= Test\n\nA document that must not freeze.".write(to: source, atomically: true, encoding: .utf8)
        let model = StudioModel(recoveryLocation: root.appendingPathComponent("recovery.json"))
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1100, height: 650), styleMask: [.titled], backing: .buffered, defer: false)
        window.contentView = NSHostingView(rootView: HSplitView { Text("Files").frame(width: 230); EditorPreviewSplit { SourceEditor(model: model) } preview: { PreviewPane(model: model) } })
        window.orderFront(nil)
        model.openProject(root)
        DispatchQueue.main.asyncAfter(deadline: .now() + 3) {
            guard model.editor?.string.contains("must not freeze") == true, model.jumpLine == nil else { print("FAIL: source did not load"); exit(1) }
            func findSplit(_ view: NSView) -> EqualSplitView? {
                if let split = view as? EqualSplitView { return split }
                return view.subviews.compactMap(findSplit).first
            }
            guard let split = findSplit(window.contentView!), split.subviews.count == 2,
                  abs(split.subviews[0].frame.width - split.subviews[1].frame.width) < 2 else { print("FAIL: initial panels are not equal width"); exit(1) }
            print("PASS: initial editor/PDF widths match")
            print("PASS: real hosted editor loads project and main run loop stays responsive")
            model.shutdownCompiler()
            try? FileManager.default.removeItem(at: root)
            exit(0)
        }
        NSApp.run()
    }
}
