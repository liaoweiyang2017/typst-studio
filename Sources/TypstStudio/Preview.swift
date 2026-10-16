import SwiftUI
import PDFKit
import CoreImage
enum PDFRenderingStyle: String {
    case original = "Original", night = "Night", green = "Green"
    static func resolve(_ mode: String, darkAlternative: String, isDark: Bool, lightAlternative: String = "Original") -> Self {
        if mode == "Automatic" { return isDark ? (Self(rawValue: darkAlternative) ?? .night) : (Self(rawValue: lightAlternative) ?? .original) }
        return Self(rawValue: mode) ?? .original
    }
    func makeFilter() -> CIFilter? {
        guard self != .original, let filter = CIFilter(name: "CIColorMatrix") else { return nil }
        // Core Image works in linear light; these map white to charcoal or soft green.
        let diagonal: [CGFloat] = self == .night ? [-0.708, -0.743, -0.775] : [0.556, 0.693, 0.567]
        let bias: [CGFloat] = self == .night ? [0.72, 0.76, 0.80] : [0.014, 0.045, 0.024]
        filter.setValue(CIVector(x: diagonal[0], y: 0, z: 0, w: 0), forKey: "inputRVector")
        filter.setValue(CIVector(x: 0, y: diagonal[1], z: 0, w: 0), forKey: "inputGVector")
        filter.setValue(CIVector(x: 0, y: 0, z: diagonal[2], w: 0), forKey: "inputBVector")
        filter.setValue(CIVector(x: 0, y: 0, z: 0, w: 1), forKey: "inputAVector")
        filter.setValue(CIVector(x: bias[0], y: bias[1], z: bias[2], w: 0), forKey: "inputBiasVector")
        return filter
    }
}
struct PreviewPosition {
    let page: Int
    let point: CGPoint
    let scale: CGFloat
    let fitsWidth: Bool
}
final class StudioPDFView: PDFView {
    private var renderingPreference = "Automatic"
    private var darkRenderingPreference = "Night"
    private var lightRenderingPreference = "Original"
    private var appliedRenderingStyle: PDFRenderingStyle?
    func configureRendering(mode: String, darkAlternative: String, lightAlternative: String = "Original") {
        renderingPreference = mode; darkRenderingPreference = darkAlternative; lightRenderingPreference = lightAlternative; refreshPDFColors()
    }
    override func viewDidChangeEffectiveAppearance() {
        super.viewDidChangeEffectiveAppearance(); refreshPDFColors()
    }
    private func refreshPDFColors() {
        let isDark = effectiveAppearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua
        let style = PDFRenderingStyle.resolve(renderingPreference, darkAlternative: darkRenderingPreference, isDark: isDark, lightAlternative: lightRenderingPreference)
        guard style != appliedRenderingStyle else { return }
        appliedRenderingStyle = style; wantsLayer = true; layer?.masksToBounds = true
        // Filter only this viewport; retain PDFKit text selection, links and original PDF bytes.
        contentFilters = style.makeFilter().map { [$0] } ?? []
        backgroundColor = NSColor(calibratedWhite: style == .night ? 0.99 : 0.95, alpha: 1)
    }

    var documentKey: String?
    var activated: (() -> Void)?
    func readingPosition() -> PreviewPosition? {
        guard let document, let destination = currentDestination, let page = destination.page else { return nil }
        return PreviewPosition(page: document.index(for: page), point: destination.point, scale: scaleFactor, fitsWidth: fitsWidth)
    }
    func restore(_ position: PreviewPosition?) {
        guard let document, document.pageCount > 0 else { return }
        fitsWidth = position?.fitsWidth ?? true
        if fitsWidth { fitWidth() } else if let position { scaleFactor = position.scale }
        let index = min(max(0, position?.page ?? 0), document.pageCount - 1)
        if let page = document.page(at: index) {
            let point = position?.point ?? CGPoint(x: page.bounds(for: displayBox).minX, y: page.bounds(for: displayBox).maxY)
            go(to: PDFDestination(page: page, at: point))
        }
    }
    override func scrollWheel(with event: NSEvent) { activated?(); super.scrollWheel(with: event) }

    var fitsWidth = true
    private var fitting = false
    private var lastWidth: CGFloat = 0
    override func layout() {
        super.layout()
        if fitsWidth && abs(bounds.width - lastWidth) > 0.5 { fitWidth() }
    }
    func fitWidth() {
        guard !fitting, bounds.width > 20, let page = currentPage ?? document?.page(at: 0) else { return }
        fitting = true; defer { fitting = false }
        lastWidth = bounds.width; autoScales = false
        let pageWidth = page.bounds(for: displayBox).width
        if pageWidth > 0 { scaleFactor = max(0.1, (bounds.width - 24) / pageWidth) }
    }
    var clickedText: ((String) -> Void)?
    override func mouseDown(with event: NSEvent) {
        activated?(); super.mouseDown(with: event)
        guard event.modifierFlags.contains(.command) else { return }
        let point = convert(event.locationInWindow, from: nil)
        if let page = page(for: point, nearest: true), let line = page.selectionForLine(at: convert(point, to: page))?.string { clickedText?(line) }
    }
}
@MainActor final class PreviewControls: ObservableObject {
    weak var view: StudioPDFView?
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
    var primary = true
    func makeCoordinator() -> Coordinator { Coordinator(controls, model: model) }
    func makeNSView(context: Context) -> StudioPDFView {
        let view = StudioPDFView(); view.autoScales = false; view.displayMode = .singlePageContinuous; view.displayDirection = .vertical
        view.backgroundColor = NSColor(calibratedWhite: 0.95, alpha: 1); view.pageShadowsEnabled = false
        view.clickedText = { model.jumpFromPDF($0) }
        view.activated = { [weak view] in model.preview = view }
        if primary || model.preview == nil { model.preview = view }; controls.view = view
        view.configureRendering(mode: model.pdfAppearance, darkAlternative: model.darkPDFAppearance, lightAlternative: model.lightPDFAppearance)
        context.coordinator.observe(view)
        return view
    }
    func updateNSView(_ view: StudioPDFView, context: Context) {
        view.configureRendering(mode: model.pdfAppearance, darkAlternative: model.darkPDFAppearance, lightAlternative: model.lightPDFAppearance)
        let key = model.previewDocumentKey
        guard view.document !== model.pdf || view.documentKey != key else { return }
        if let previousKey = view.documentKey, let position = view.readingPosition() {
            model.rememberPreview(position, for: previousKey)
        }
        let saved = model.previewPosition(for: key)
        view.documentKey = key; view.document = model.pdf
        view.restore(saved)
        let document = model.pdf
        DispatchQueue.main.async {
            guard view.documentKey == key, view.document === document else { return }
            view.layoutSubtreeIfNeeded(); view.restore(saved); controls.update()
        }
    }
    static func dismantleNSView(_ view: StudioPDFView, coordinator: Coordinator) {
        if let key = view.documentKey, let position = view.readingPosition() { coordinator.model.rememberPreview(position, for: key) }
    }
    @MainActor final class Coordinator {
        let controls: PreviewControls
        let model: StudioModel
        var tokens: [NSObjectProtocol] = []
        init(_ controls: PreviewControls, model: StudioModel) { self.controls = controls; self.model = model }
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
    var primary = true
    @StateObject private var controls = PreviewControls()
    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 13) {
                Button { controls.view?.fitsWidth = false; controls.view?.zoomOut(nil) } label: { Image(systemName: "minus.magnifyingglass") }.help("Zoom out")
                Text("\(controls.percent)%").monospacedDigit().frame(minWidth: 35)
                Button { controls.view?.fitsWidth = false; controls.view?.zoomIn(nil) } label: { Image(systemName: "plus.magnifyingglass") }.help("Zoom in")
                Button { controls.view?.fitsWidth = true; controls.view?.fitWidth() } label: { Image(systemName: "arrow.up.left.and.arrow.down.right").foregroundStyle(.blue) }.help("Fit width")
                Spacer(minLength: 4)
                Text("Page \(controls.page) of \(model.pdf?.pageCount ?? 0)").foregroundStyle(.secondary).monospacedDigit()
                Spacer(minLength: 4)
                Button { model.popOut() } label: { Image(systemName: "macwindow") }.help("Open preview window")
                Button { model.followCursor.toggle() } label: { Image(systemName: "scope").foregroundStyle(model.followCursor ? .blue : .secondary) }.help("Follow source text")
                Divider().frame(height: 13)
                Button { model.exportPDF() } label: { Image(systemName: "arrow.down") }.help("Export PDF").disabled(model.successfulRevision != model.revision)
            }.font(.system(size: 11)).buttonStyle(.borderless).padding(.horizontal, 10).frame(height: 29).background(Color(nsColor: .textBackgroundColor))
            Divider()
            if model.pdf != nil { PDFCanvas(model: model, controls: controls, primary: primary) }
            else { ContentUnavailableView(model.compiling ? "Typesetting…" : "No preview yet", systemImage: "doc.richtext", description: Text("Your document appears here after a successful compile.")).frame(maxWidth: .infinity, maxHeight: .infinity) }
        }.preferredColorScheme(model.theme == "Dark" ? .dark : model.theme == "Light" ? .light : nil)
    }
}
