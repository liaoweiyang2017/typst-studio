import Foundation

/// Pinned Tinymist preview columns count Unicode scalars; AppKit uses UTF-16 offsets.
enum SourceCoordinates {
    static func position(in text: String, utf16Offset: Int) -> (line: Int, column: Int) {
        let ns = text as NSString
        let prefix = ns.substring(to: min(max(0, utf16Offset), ns.length))
        let lines = prefix.components(separatedBy: "\n")
        return (lines.count - 1, lines.last!.unicodeScalars.count)
    }
    static func range(in text: String, start: [Int], end: [Int]) -> NSRange? {
        func offset(_ position: [Int]) -> Int? {
            guard position.count == 2, position[0] >= 0, position[1] >= 0 else { return nil }
            let lines = text.components(separatedBy: "\n")
            guard position[0] < lines.count, position[1] <= lines[position[0]].unicodeScalars.count else { return nil }
            let prefix = String(String.UnicodeScalarView(lines[position[0]].unicodeScalars.prefix(position[1])))
            return lines.prefix(position[0]).reduce(0) { $0 + $1.utf16.count + 1 } + prefix.utf16.count
        }
        guard let a = offset(start), let b = offset(end), b >= a else { return nil }
        return NSRange(location: a, length: b - a)
    }
}
