import SwiftUI
import AppKit

final class CodeTextView: NSTextView {
    override func drawBackground(in rect: NSRect) {
        super.drawBackground(in: rect)
        guard let layoutManager, let textContainer, selectedRange().length == 0 else { return }
        let index = min(selectedRange().location, max(0, (string as NSString).length - 1))
        guard !string.isEmpty else { return }
        let glyph = layoutManager.glyphIndexForCharacter(at: index)
        var line = layoutManager.lineFragmentRect(forGlyphAt: glyph, effectiveRange: nil)
        line.origin.y += textContainerOrigin.y; line.origin.x = 0; line.size.width = bounds.width
        NSColor.selectedContentBackgroundColor.withAlphaComponent(0.07).setFill()
        NSBezierPath(roundedRect: line, xRadius: 3, yRadius: 3).fill()
        _ = textContainer
    }

    override func insertNewline(_ sender: Any?) {
        let prefix = (string as NSString).substring(to: selectedRange().location)
        let last = prefix.components(separatedBy: "\n").last ?? ""
        let indent = String(last.prefix { $0 == " " || $0 == "\t" })
        super.insertNewline(sender)
        if !indent.isEmpty { insertText(indent, replacementRange: selectedRange()) }
    }
}
final class LineRuler: NSRulerView {
    weak var textView: NSTextView?
    init(_ scroll: NSScrollView, text: NSTextView) {
        textView = text; super.init(scrollView: scroll, orientation: .verticalRuler); clientView = text; ruleThickness = 48
    }
    required init(coder: NSCoder) { fatalError() }
    override func drawHashMarksAndLabels(in rect: NSRect) {
        guard let text = textView, let layout = text.layoutManager, let container = text.textContainer else { return }
        NSColor.textBackgroundColor.setFill(); bounds.fill()
        let ns = text.string as NSString
        var offset = 0, number = 1
        let attrs: [NSAttributedString.Key: Any] = [.font: NSFont.monospacedDigitSystemFont(ofSize: 10, weight: .regular), .foregroundColor: NSColor.secondaryLabelColor]
        while offset < ns.length {
            let glyph = layout.glyphIndexForCharacter(at: offset)
            let fragment = layout.lineFragmentRect(forGlyphAt: glyph, effectiveRange: nil)
            let y = convert(NSPoint(x: 0, y: fragment.minY + text.textContainerOrigin.y), from: text).y
            if y > bounds.maxY + 30 { break }
            if y > -30 { let s = "\(number)" as NSString; s.draw(at: NSPoint(x: 35 - s.size(withAttributes: attrs).width, y: y + 2), withAttributes: attrs) }
            offset = NSMaxRange(ns.lineRange(for: NSRange(location: offset, length: 0))); number += 1
        }
        _ = container
    }
}
struct SourceEditor: NSViewRepresentable {
    @ObservedObject var model: StudioModel
    func makeCoordinator() -> Coordinator { Coordinator(model) }
    func makeNSView(context: Context) -> NSScrollView {
        let scroll = NSScrollView(); scroll.hasVerticalScroller = true; scroll.hasHorizontalScroller = false; scroll.autohidesScrollers = true
        let text = CodeTextView(frame: NSRect(x: 0, y: 0, width: 500, height: 600))
        text.isRichText = false; text.allowsUndo = true; text.isAutomaticQuoteSubstitutionEnabled = false
        text.isAutomaticDashSubstitutionEnabled = false; text.isAutomaticTextReplacementEnabled = false
        text.isAutomaticSpellingCorrectionEnabled = false; text.isContinuousSpellCheckingEnabled = model.spellCheck
        text.isVerticallyResizable = true; text.isHorizontallyResizable = false; text.autoresizingMask = [.width]
        text.textContainer?.widthTracksTextView = true; text.textContainer?.containerSize = NSSize(width: 500, height: CGFloat.greatestFiniteMagnitude)
        text.textContainerInset = NSSize(width: 6, height: 5); text.usesFindBar = true
        text.string = model.text; text.delegate = context.coordinator; text.font = .monospacedSystemFont(ofSize: model.fontSize, weight: .regular)
        scroll.documentView = text; scroll.verticalRulerView = LineRuler(scroll, text: text); scroll.hasVerticalRuler = true; scroll.rulersVisible = true
        model.editor = text; context.coordinator.highlight(text)
        return scroll
    }
    func updateNSView(_ scroll: NSScrollView, context: Context) {
        guard let text = scroll.documentView as? NSTextView else { return }
        var refresh = false
        if text.string != model.text {
            context.coordinator.updating = true; text.string = model.text; context.coordinator.updating = false; refresh = true
        }
        if text.font?.pointSize != CGFloat(model.fontSize) { text.font = .monospacedSystemFont(ofSize: model.fontSize, weight: .regular); refresh = true }
        if context.coordinator.lastDiagnostic != model.diagnostics { refresh = true; context.coordinator.lastDiagnostic = model.diagnostics }
        text.isContinuousSpellCheckingEnabled = model.spellCheck
        if refresh { context.coordinator.highlight(text) }
        if let target = model.jumpLine {
            let ns = text.string as NSString; var pos = 0
            for _ in 1..<max(1, target) { if pos >= ns.length { break }; pos = NSMaxRange(ns.lineRange(for: NSRange(location: pos, length: 0))) }
            let range = NSRange(location: min(pos, ns.length), length: 0)
            text.setSelectedRange(range); text.scrollRangeToVisible(range); text.window?.makeFirstResponder(text)
            DispatchQueue.main.async { model.jumpLine = nil }
        }
        scroll.verticalRulerView?.needsDisplay = true
    }
    @MainActor final class Coordinator: NSObject, NSTextViewDelegate {
        let model: StudioModel; var updating = false; var lastDiagnostic = ""
        init(_ model: StudioModel) { self.model = model }
        func textDidChange(_ notification: Notification) {
            guard !updating, let text = notification.object as? NSTextView else { return }
            model.changed(text.string); highlight(text); text.enclosingScrollView?.verticalRulerView?.needsDisplay = true
            if model.typingSound { NSSound(named: "Tink")?.play() }
        }
        func textViewDidChangeSelection(_ notification: Notification) {
            guard !updating, let text = notification.object as? NSTextView else { return }
            model.selectionChanged(text.selectedRange()); text.needsDisplay = true
        }
        func textView(_ textView: NSTextView, completions words: [String], forPartialWordRange range: NSRange, indexOfSelectedItem index: UnsafeMutablePointer<Int>?) -> [String] {
            let prefix = (textView.string as NSString).substring(with: range)
            let candidates = ["set", "show", "let", "import", "include", "text", "page", "heading", "figure", "table", "image", "align", "columns", "grid", "bibliography", "cite", "outline", "equation", "sqrt", "frac", "integral", "infinity", "alpha", "beta", "gamma", "sum", "product", "matrix", "cases", "context", "numbering"]
            return candidates.filter { $0.hasPrefix(prefix) }
        }
        func highlight(_ text: NSTextView) {
            guard let storage = text.textStorage, !text.hasMarkedText() else { return }
            let full = NSRange(location: 0, length: storage.length), selection = text.selectedRange()
            updating = true; storage.beginEditing()
            storage.setAttributes([.font: NSFont.monospacedSystemFont(ofSize: model.fontSize, weight: .regular), .foregroundColor: NSColor.labelColor], range: full)
            let rules: [(String, NSColor)] = [
                (#"(?m)^=+ .*$"#, .systemTeal),
                (#"#[a-zA-Z][\w.-]*"#, .systemBlue),
                (#"#(?:set|show|let|import|include)\b"#, .systemRed),
                (#"\b\d+(?:\.\d+)?(?:pt|mm|cm|em|%)?\b"#, .systemOrange),
                (#"\$[^$]*\$"#, .systemBlue),
                (#""(?:[^"\\]|\\.)*""#, .systemGreen),
                (#"(?m)//.*$"#, .secondaryLabelColor)
            ]
            for (pattern, color) in rules {
                if let regex = try? NSRegularExpression(pattern: pattern) {
                    for match in regex.matches(in: text.string, range: full) { storage.addAttribute(.foregroundColor, value: color, range: match.range) }
                }
            }
            if let errorLine = model.errorLine {
                let ns = text.string as NSString; var pos = 0
                for _ in 1..<max(1, errorLine) { if pos >= ns.length { break }; pos = NSMaxRange(ns.lineRange(for: NSRange(location: pos, length: 0))) }
                if pos < ns.length {
                    let r = ns.lineRange(for: NSRange(location: pos, length: 0))
                    storage.addAttributes([.underlineStyle: NSUnderlineStyle.single.rawValue, .underlineColor: NSColor.systemRed, .toolTip: model.diagnostics], range: r)
                }
            }
            storage.endEditing(); text.setSelectedRange(selection); updating = false
        }
    }
}
