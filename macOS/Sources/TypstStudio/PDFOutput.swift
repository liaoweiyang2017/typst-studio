import Foundation

/// One pending snapshot per entry; IO never runs on the UI thread.
final class PDFOutput: @unchecked Sendable {
    typealias Completion = @Sendable (Result<URL, Error>) -> Void
    private let queue = DispatchQueue(label: "studio.pdf-output", qos: .utility)
    private let lock = NSLock()
    private var pending: [URL: (Data, Completion)] = [:]
    private var running = false
    func write(_ data: Data, beside entry: URL, completion: @escaping Completion) {
        let target = entry.deletingPathExtension().appendingPathExtension("pdf")
        lock.lock()
        let replaced = pending.updateValue((data, completion), forKey: target)
        let start = !running; running = true
        lock.unlock()
        replaced?.1(.failure(CancellationError()))
        if start { queue.async { self.drain() } }
    }
    func flush(_ completion: @escaping @Sendable () -> Void) { queue.async(execute: completion) }
    private func drain() {
        while true {
            lock.lock()
            guard let (target, job) = pending.first else { running = false; lock.unlock(); return }
            pending.removeValue(forKey: target); lock.unlock()
            job.1(Result {
                let fm = FileManager.default
                if let attributes = try? fm.attributesOfItem(atPath: target.path) {
                    guard attributes[.type] as? FileAttributeType == .typeRegular,
                          ((attributes[.posixPermissions] as? NSNumber)?.intValue ?? 0) & 0o222 != 0 else {
                        throw NSError(domain: "PDFOutput", code: 1, userInfo: [NSLocalizedDescriptionKey: "The output PDF is not a writable regular file."])
                    }
                    if try Data(contentsOf: target) == job.0 { return target }
                }
                try job.0.write(to: target, options: .atomic)
                return target
            })
        }
    }
}
