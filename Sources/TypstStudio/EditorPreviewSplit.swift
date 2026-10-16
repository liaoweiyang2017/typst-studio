import SwiftUI
import AppKit

/// Native divider with an explicit initial 50/50 allocation. User drags are retained.
final class EqualSplitView: NSSplitView {
    private var fraction: CGFloat = 0.5
    private var dragging = false
    private var applying = false
    override func resizeSubviews(withOldSize oldSize: NSSize) {
        super.resizeSubviews(withOldSize: oldSize)
        applyFraction()
    }
    override func layout() { super.layout(); applyFraction() }
    override func mouseDown(with event: NSEvent) {
        dragging = true
        super.mouseDown(with: event)
        if subviews.count == 2 { fraction = subviews[0].frame.width / max(1, bounds.width - dividerThickness) }
        dragging = false
    }
    func applyFraction() {
        guard !dragging, !applying, subviews.count == 2, bounds.width > 10 else { return }
        let target = (bounds.width - dividerThickness) * min(0.85, max(0.15, fraction))
        guard abs(subviews[0].frame.width - target) > 0.5 else { return }
        applying = true; defer { applying = false }
        setPosition(target, ofDividerAt: 0)
    }

}
struct EditorPreviewSplit<Editor: View, Preview: View>: NSViewRepresentable {
    let editor: Editor
    let preview: Preview
    init(@ViewBuilder editor: () -> Editor, @ViewBuilder preview: () -> Preview) { self.editor = editor(); self.preview = preview() }
    func makeCoordinator() -> Coordinator { Coordinator() }
    func makeNSView(context: Context) -> EqualSplitView {
        let split = EqualSplitView(); split.isVertical = true; split.dividerStyle = .thin
        let left = NSHostingView(rootView: editor), right = NSHostingView(rootView: preview)
        left.sizingOptions = []; right.sizingOptions = []
        split.addArrangedSubview(left); split.addArrangedSubview(right)
        context.coordinator.left = left; context.coordinator.right = right
        return split
    }
    func updateNSView(_ split: EqualSplitView, context: Context) {
        context.coordinator.left?.rootView = editor; context.coordinator.right?.rootView = preview
        split.applyFraction()
    }
    final class Coordinator {
        var left: NSHostingView<Editor>?
        var right: NSHostingView<Preview>?
    }
}
