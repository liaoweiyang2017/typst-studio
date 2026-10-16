import AppKit
import PDFKit
import CoreImage
@main struct PDFColorTests {
    @MainActor static func main() throws {
        _ = NSApplication.shared
        precondition(PDFRenderingStyle.resolve("Automatic", darkAlternative: "Night", isDark: false) == .original)
        precondition(PDFRenderingStyle.resolve("Automatic", darkAlternative: "Night", isDark: true) == .night)
        precondition(PDFRenderingStyle.resolve("Automatic", darkAlternative: "Green", isDark: true) == .green)
        precondition(PDFRenderingStyle.resolve("Green", darkAlternative: "Night", isDark: false) == .green)
        let context = CIContext(options: [.useSoftwareRenderer: true])
        func pixel(_ style: PDFRenderingStyle, value: CGFloat) -> [UInt8] {
            let filter = style.makeFilter()!
            filter.setValue(CIImage(color: CIColor(red: value, green: value, blue: value)), forKey: kCIInputImageKey)
            var bytes = [UInt8](repeating: 0, count: 4)
            context.render(filter.outputImage!, toBitmap: &bytes, rowBytes: 4, bounds: CGRect(x: 0, y: 0, width: 1, height: 1), format: .RGBA8, colorSpace: CGColorSpaceCreateDeviceRGB())
            return bytes
        }
        let nightPaper = pixel(.night, value: 1), nightText = pixel(.night, value: 0), greenPaper = pixel(.green, value: 1)
        precondition(nightPaper[0] < 110 && nightText[0] > 180)
        precondition(greenPaper[1] > greenPaper[0] && greenPaper[1] > greenPaper[2])
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let compiled = Compiler.compile("= Original PDF\nColor settings do not change exported content.", directory: root, root: root)
        let doc = PDFDocument(data: compiled.pdf!)!, view = StudioPDFView(frame: NSRect(x: 0, y: 0, width: 600, height: 700))
        view.document = doc
        func renderExport(_ document: PDFDocument) -> Data {
            let exported = PDFDocument(data: document.dataRepresentation()!)!
            let bitmap = CGContext(data: nil, width: 300, height: 425, bitsPerComponent: 8, bytesPerRow: 1200, space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
            bitmap.setFillColor(NSColor.white.cgColor); bitmap.fill(CGRect(x: 0, y: 0, width: 300, height: 425))
            let page = exported.page(at: 0)!
            bitmap.scaleBy(x: 300 / page.bounds(for: .mediaBox).width, y: 425 / page.bounds(for: .mediaBox).height)
            page.draw(with: .mediaBox, to: bitmap)
            return Data(bytes: bitmap.data!, count: 1200 * 425)
        }
        let before = renderExport(doc)
        view.appearance = NSAppearance(named: .darkAqua)
        view.configureRendering(mode: "Automatic", darkAlternative: "Green")
        precondition(view.contentFilters.count == 1)
        view.appearance = NSAppearance(named: .aqua)
        precondition(view.contentFilters.isEmpty)
        view.configureRendering(mode: "Night", darkAlternative: "Green")
        precondition(view.contentFilters.count == 1)
        precondition(renderExport(doc) == before)
        print("PASS: automatic/light/dark color policy, night/green pixels, view filters and unchanged exported PDF rendering")
    }
}
