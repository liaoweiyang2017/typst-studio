import Foundation
import PDFKit

/// Text matching, not a compiler source map. Preserve UTF-16 offsets for AppKit/PDFKit.
struct NavigationText {
    let normalized: String
    private let offsets: [Int]
    private let ends: [Int]
    init(_ text: String) {
        var output = "", offsets: [Int] = [], ends: [Int] = [], offset = 0
        for character in text {
            let raw = String(character), length = raw.utf16.count
            let folded = raw.folding(options: [.caseInsensitive, .diacriticInsensitive, .widthInsensitive], locale: Locale(identifier: "en_US_POSIX"))
            for scalar in folded.unicodeScalars where CharacterSet.alphanumerics.contains(scalar) {
                let value = String(scalar)
                output += value
                offsets.append(contentsOf: repeatElement(offset, count: value.utf16.count))
                ends.append(contentsOf: repeatElement(offset + length, count: value.utf16.count))
            }
            offset += length
        }
        normalized = output; self.offsets = offsets; self.ends = ends
    }
    func matches(_ query: String, limit: Int = 2) -> [NSRange] {
        let needle = NavigationText(query).normalized as NSString
        guard needle.length >= 3 else { return [] }
        let haystack = normalized as NSString
        var result: [NSRange] = [], start = 0
        while start < haystack.length && result.count < limit {
            let range = haystack.range(of: needle as String, range: NSRange(location: start, length: haystack.length - start))
            guard range.location != NSNotFound else { break }
            let end = NSMaxRange(range) - 1
            result.append(NSRange(location: offsets[range.location], length: ends[end] - offsets[range.location]))
            start = NSMaxRange(range)
        }
        return result
    }
}
struct PDFTextMatch { let page: Int; let range: NSRange }
struct SourceTextMatch { let url: URL?; let range: NSRange; let source: String }
enum TextNavigation {
    static func pdfMatches(_ data: Data, phrase: String, cancelled: () -> Bool = { false }) -> [PDFTextMatch] {
        PDFNavigationIndex().matches(data: data, generation: UUID(), phrase: phrase, cancelled: cancelled)
    }
    static func sourceMatches(phrase: String, current: String, currentURL: URL?, files: [URL], cancelled: () -> Bool = { false }) -> [SourceTextMatch] {
        var matches = NavigationText(current).matches(phrase).map { SourceTextMatch(url: currentURL, range: $0, source: current) }
        // Prefer the active source. For an included source, search project files off the UI thread.
        if !matches.isEmpty { return matches }
        for url in files where url.pathExtension.lowercased() == "typ" && url != currentURL {
            if cancelled() { return [] }
            guard let size = try? url.resourceValues(forKeys: [.fileSizeKey]).fileSize, size <= 4 * 1024 * 1024,
                  let source = try? String(contentsOf: url, encoding: .utf8) else { continue }
            matches += NavigationText(source).matches(phrase, limit: 2 - matches.count).map { SourceTextMatch(url: url, range: $0, source: source) }
            if matches.count > 1 { break }
        }
        return matches
    }
}

/// Confined to StudioModel's serial navigation queue. Cache only the current PDF generation.
final class PDFNavigationIndex: @unchecked Sendable {
    private var generation: UUID?
    private var document: PDFDocument?
    private var pages: [Int: NavigationText] = [:]
    func matches(data: Data, generation: UUID, phrase: String, cancelled: () -> Bool) -> [PDFTextMatch] {
        if cancelled() { return [] }
        if self.generation != generation {
            document = PDFDocument(data: data); pages.removeAll(); self.generation = generation
        }
        guard let document else { return [] }
        var matches: [PDFTextMatch] = []
        for index in 0..<document.pageCount {
            if cancelled() { return [] }
            if pages[index] == nil { pages[index] = NavigationText(document.page(at: index)?.string ?? "") }
            for range in pages[index]!.matches(phrase, limit: 2 - matches.count) { matches.append(.init(page: index, range: range)) }
            if matches.count > 1 { break }
        }
        return matches
    }
}
