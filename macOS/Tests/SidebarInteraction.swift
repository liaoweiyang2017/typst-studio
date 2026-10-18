import AppKit
import SwiftUI
@main struct SidebarInteraction {
    @MainActor static func main() {
        _ = NSApplication.shared
        var selected = 0, actions = 0
        let binding = Binding<Int>(get: { selected }, set: { selected = $0; actions += 1 })
        let host = NSHostingView(rootView: SidebarModePicker(selection: binding).frame(width: 166, height: 38))
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 166, height: 38), styleMask: [.titled], backing: .buffered, defer: false)
        window.contentView = host; window.orderFront(nil)
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) {
            func buttons(_ view: NSView) -> [SidebarModeButton] {
                (view as? SidebarModeButton).map { [$0] } ?? view.subviews.flatMap(buttons)
            }
            let controls = buttons(host)
            precondition(controls.count == 5)
            for button in controls {
                precondition(button.acceptsFirstMouse(for: nil))
                for local in [NSPoint(x: 1,y: 1), NSPoint(x: 30,y: 1), NSPoint(x: 1,y: 30), NSPoint(x: 30,y: 30)] {
                    let point = button.convert(local, to: host)
                    var hit = host.hitTest(point)
                    while hit != nil && hit !== button { hit = hit?.superview }
                    precondition(hit === button, "Button padding must be hittable")
                }
                let previous = actions
                button.performClick(nil)
                precondition(selected == button.tag && actions == previous + 1)
            }
            print("PASS: all five buttons have full hit rectangles, accept first click, and dispatch one action")
            exit(0)
        }
        NSApp.run()
    }
}
