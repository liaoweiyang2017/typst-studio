import SwiftUI
import PDFKit
final class StudioPDFView: PDFView {
    var clickedText: ((String) -> Void)?
    override func mouseDown(with event: NSEvent) {
        super.mouseDown(with: event)
        guard event.modifierFlags.contains(.command) else { return }
        let point = convert(event.locationInWindow, from: nil)
        if let page = page(for: point, nearest: true), let line = page.selectionForLine(at: convert(point, to: page))?.string { clickedText?(line) }
    }
}
@MainActor final class PreviewControls: ObservableObject {
    weak var view: PDFView?
    @Published var percent = 100
    @Published var page = 0
    func update() {
        guard let view else { return }
        percent = Int((view.scaleFactor * 100).rounded())
        page = view.currentPage.flatMap { view.document?.index(for: $0) }.map { $0 + 1 } ?? 0
    }
}
struct PDFCanvas: NSViewRepresentable {
    @ObservedObject var model: StudioModel
    @ObservedObject var controls: PreviewControls
    func makeCoordinator() -> Coordinator { Coordinator(controls) }
    func makeNSView(context: Context) -> StudioPDFView {
        let view = StudioPDFView(); view.autoScales = true; view.displayMode = .singlePageContinuous; view.displayDirection = .vertical
        view.backgroundColor = NSColor(calibratedWhite: 0.95, alpha: 1); view.pageShadowsEnabled = false
        view.clickedText = { model.jumpFromPDF($0) }; model.preview = view; controls.view = view
        context.coordinator.observe(view)
        return view
    }
    func updateNSView(_ view: StudioPDFView, context: Context) {
        guard view.document !== model.pdf else { return }
        let page = view.currentPage.flatMap { view.document?.index(for: $0) } ?? 0
        let scale = view.scaleFactor, auto = view.autoScales
        let destination = view.currentDestination
        view.document = model.pdf
        if let p = model.pdf?.page(at: min(page, max(0, (model.pdf?.pageCount ?? 1) - 1))) {
            if let destination { view.go(to: PDFDestination(page: p, at: destination.point)) } else { view.go(to: p) }
        }
        if !auto { view.scaleFactor = scale }
        DispatchQueue.main.async { controls.update() }
    }
    @MainActor final class Coordinator {
        let controls: PreviewControls
        var tokens: [NSObjectProtocol] = []
        init(_ controls: PreviewControls) { self.controls = controls }
        func observe(_ view: PDFView) {
            for name in [Notification.Name.PDFViewPageChanged, .PDFViewScaleChanged] {
                tokens.append(NotificationCenter.default.addObserver(forName: name, object: view, queue: .main) { [weak self] _ in DispatchQueue.main.async { self?.controls.update() } })
            }
        }
        deinit { tokens.forEach(NotificationCenter.default.removeObserver) }
    }
}
struct PreviewPane: View {
    @ObservedObject var model: StudioModel
    @StateObject private var controls = PreviewControls()
    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 13) {
                Button { controls.view?.zoomOut(nil) } label: { Image(systemName: "minus.magnifyingglass") }.help("Zoom out")
                Text("\(controls.percent)%").monospacedDigit().frame(minWidth: 35)
                Button { controls.view?.zoomIn(nil) } label: { Image(systemName: "plus.magnifyingglass") }.help("Zoom in")
                Button { controls.view?.autoScales = true } label: { Image(systemName: "arrow.up.left.and.arrow.down.right").foregroundStyle(.blue) }.help("Fit page")
                Spacer(minLength: 4)
                Text("Page \(controls.page) of \(model.pdf?.pageCount ?? 0)").foregroundStyle(.secondary).monospacedDigit()
                Spacer(minLength: 4)
                Button { model.popOut() } label: { Image(systemName: "macwindow") }.help("Open preview window")
                Button { model.followCursor.toggle() } label: { Image(systemName: "scope").foregroundStyle(model.followCursor ? .blue : .secondary) }.help("Follow source text")
                Divider().frame(height: 13)
                Button { model.exportPDF() } label: { Image(systemName: "arrow.down") }.help("Export PDF").disabled(model.successfulRevision != model.revision)
            }.font(.system(size: 11)).buttonStyle(.borderless).padding(.horizontal, 10).frame(height: 29).background(Color(nsColor: .textBackgroundColor))
            Divider()
            if model.pdf != nil { PDFCanvas(model: model, controls: controls) }
            else { ContentUnavailableView(model.compiling ? "Typesetting…" : "No preview yet", systemImage: "doc.richtext", description: Text("Your document appears here after a successful compile.")).frame(maxWidth: .infinity, maxHeight: .infinity) }
        }
    }
}
