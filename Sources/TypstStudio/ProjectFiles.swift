import Foundation

/// File discovery is separate from the lazy UI tree. No generated build tree is indexed.
enum ProjectFiles {
    static let ignored = Set(["build", "node_modules", "target", "dist", "__pycache__"])
    static func index(_ root: URL) -> [URL] {
        let enumerator = FileManager.default.enumerator(at: root, includingPropertiesForKeys: [.isDirectoryKey, .isSymbolicLinkKey], options: [.skipsHiddenFiles, .skipsPackageDescendants])
        var files: [URL] = []
        while let url = enumerator?.nextObject() as? URL {
            let values = try? url.resourceValues(forKeys: [.isDirectoryKey, .isSymbolicLinkKey])
            if values?.isSymbolicLink == true { enumerator?.skipDescendants(); continue }
            if values?.isDirectory == true {
                if ignored.contains(url.lastPathComponent) { enumerator?.skipDescendants() }
            } else { files.append(url.standardizedFileURL.resolvingSymlinksInPath()) }
        }
        return files.sorted { $0.path.localizedStandardCompare($1.path) == .orderedAscending }
    }
    static func mainFile(in files: [URL], root: URL) -> URL? {
        let sources = files.filter { $0.pathExtension.lowercased() == "typ" }
        // Prefer explicit main.typ, shallowest first, then index.typ; otherwise first source.
        return sources.sorted { a, b in
            func rank(_ url: URL) -> Int { url.lastPathComponent.lowercased() == "main.typ" ? 0 : url.lastPathComponent.lowercased() == "index.typ" ? 1 : 2 }
            if rank(a) != rank(b) { return rank(a) < rank(b) }
            if a.pathComponents.count != b.pathComponents.count { return a.pathComponents.count < b.pathComponents.count }
            return a.path < b.path
        }.first
    }
    static func children(_ directory: URL) -> [URL] {
        let items = (try? FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: [.isDirectoryKey], options: [.skipsHiddenFiles])) ?? []
        return items.map { $0.standardizedFileURL.resolvingSymlinksInPath() }.sorted {
            let a = isDirectory($0), b = isDirectory($1)
            return a == b ? $0.lastPathComponent.localizedStandardCompare($1.lastPathComponent) == .orderedAscending : a
        }
    }
    static func isDirectory(_ url: URL) -> Bool { (try? url.resourceValues(forKeys: [.isDirectoryKey, .isSymbolicLinkKey])).map { $0.isDirectory == true && $0.isSymbolicLink != true } ?? false }
}
