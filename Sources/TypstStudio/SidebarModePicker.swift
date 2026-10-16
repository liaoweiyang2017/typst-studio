import SwiftUI
import AppKit

final class SidebarModeButton: NSButton {
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
    override var alignmentRectInsets: NSEdgeInsets { NSEdgeInsetsZero }
    override func hitTest(_ point: NSPoint) -> NSView? {
        guard !isHidden, alphaValue > 0 else { return nil }
        return bounds.contains(convert(point, from: superview)) ? self : nil
    }
    override func mouseDown(with event: NSEvent) {
        if isEnabled { sendAction(action, to: target) }
    }
    override func mouseUp(with event: NSEvent) {}

}
/// Native controls give each symbol a complete, non-overlapping hit rectangle,
/// including the first click into an inactive window.
struct SidebarModePicker: NSViewRepresentable {
    @Binding var selection: Int
    static let modes = [("folder", "Files"), ("magnifyingglass", "Search"), ("list.bullet.indent", "Outline"), ("x.squareroot", "Symbols"), ("exclamationmark.triangle", "Issues")]
    func makeCoordinator() -> Coordinator { Coordinator($selection) }
    func makeNSView(context: Context) -> NSStackView {
        let stack = NSStackView(); stack.orientation = .horizontal; stack.spacing = 0
        stack.edgeInsets = NSEdgeInsets(top: 3, left: 3, bottom: 3, right: 3)
        stack.wantsLayer = true; stack.layer?.cornerRadius = 19
        for (index, mode) in Self.modes.enumerated() {
            let button = SidebarModeButton()
            button.tag = index; button.image = NSImage(systemSymbolName: mode.0, accessibilityDescription: mode.1)
            button.imagePosition = .imageOnly; button.isBordered = false; button.setButtonType(.momentaryChange)
            button.target = context.coordinator; button.action = #selector(Coordinator.select(_:))
            button.focusRingType = .none
            button.toolTip = mode.1; button.setAccessibilityLabel(mode.1)
            button.wantsLayer = true; button.layer?.cornerRadius = 16
            stack.addArrangedSubview(button)
            button.widthAnchor.constraint(equalToConstant: 32).isActive = true
            button.heightAnchor.constraint(equalToConstant: 32).isActive = true
        }
        updateNSView(stack, context: context)
        return stack
    }
    func updateNSView(_ stack: NSStackView, context: Context) {
        context.coordinator.selection = $selection
        stack.layer?.backgroundColor = NSColor.labelColor.withAlphaComponent(0.055).cgColor
        for case let button as SidebarModeButton in stack.arrangedSubviews {
            let active = button.tag == selection
            button.contentTintColor = active ? .white : .labelColor
            button.layer?.backgroundColor = active ? NSColor.systemBlue.cgColor : NSColor.clear.cgColor
            button.setAccessibilityValue(active ? "Selected" : "Not selected")
        }
    }
    final class Coordinator: NSObject {
        var selection: Binding<Int>
        init(_ selection: Binding<Int>) { self.selection = selection }
        @objc func select(_ sender: NSButton) { selection.wrappedValue = sender.tag }
    }
}
