// Kept only as a compatibility fixture for the public feed consumed by 0.1.5–0.1.6.
import Foundation
import Combine
import AppKit

struct AppVersion: Comparable, Equatable {
    let parts: [Int]
    init?(_ text: String) {
        guard text.range(of: #"^[0-9]{1,6}\.[0-9]{1,6}\.[0-9]{1,6}$"#, options: .regularExpression) != nil else { return nil }
        parts = text.split(separator: ".").compactMap { Int($0) }
    }
    static func < (lhs: Self, rhs: Self) -> Bool { lhs.parts.lexicographicallyPrecedes(rhs.parts) }
}
struct UpdateManifest: Decodable {
    let version: String
    let minimumMacOS: String
    let architecture: String
    let downloadURL: URL
    let releaseURL: URL
    let sha256: String
    enum CodingKeys: String, CodingKey {
        case version, architecture, sha256
        case minimumMacOS = "minimum_macos", downloadURL = "download_url", releaseURL = "release_url"
    }
    static let repository = "liaoweiyang2017/typst-studio-releases"
    func validate() throws {
        guard AppVersion(version) != nil, architecture == "arm64",
              minimumMacOS.range(of: #"^[0-9]{1,3}\.[0-9]{1,3}(\.[0-9]{1,3})?$"#, options: .regularExpression) != nil,
              sha256.range(of: #"^[a-f0-9]{64}$"#, options: .regularExpression) != nil,
              downloadURL.absoluteString == "https://github.com/\(Self.repository)/releases/download/v\(version)/Typst-Studio-\(version)-macOS-arm64.dmg",
              releaseURL.absoluteString == "https://github.com/\(Self.repository)/releases/tag/v\(version)" else {
            throw UpdateError.invalidManifest
        }
    }
    var isCompatible: Bool {
        let required = minimumMacOS.split(separator: ".").compactMap { Int($0) }
        let os = ProcessInfo.processInfo.operatingSystemVersion
        let current = [os.majorVersion, os.minorVersion, os.patchVersion]
        let padded = required + Array(repeating: 0, count: max(0, 3-required.count))
        return !current.lexicographicallyPrecedes(padded)
    }
}
enum UpdateError: LocalizedError {
    case invalidManifest, http(Int)
    var errorDescription: String? {
        switch self {
        case .invalidManifest: return "The update information could not be verified. Please try again later."
        case .http(let code): return code == 404 ? "Update information is not available yet. Please try again later." : "The update service returned HTTP \(code). Please try again later."
        }
    }
}
@MainActor final class UpdateChecker: ObservableObject {
    static let feedURL = URL(string: "https://github.com/\(UpdateManifest.repository)/releases/latest/download/update.json")!
    @Published var checkOnStartup = (UserDefaults.standard.object(forKey: "checkUpdatesOnStartup") as? Bool) ?? true {
        didSet { UserDefaults.standard.set(checkOnStartup, forKey: "checkUpdatesOnStartup") }
    }
    @Published private(set) var isChecking = false
    @Published private(set) var status = "Not checked yet."
    @Published private(set) var available: UpdateManifest?
    @Published private(set) var lastChecked: Date?
    @Published var showResult = false
    let currentVersion: String
    private var checkedOnLaunch = false
    private let session: URLSession
    private let feedURL: URL
    init(currentVersion: String = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0.1.6", session: URLSession? = nil, feedURL: URL? = nil) {
        self.currentVersion = currentVersion; self.feedURL = feedURL ?? Self.feedURL
        if let session { self.session = session }
        else {
            let config = URLSessionConfiguration.ephemeral
            config.timeoutIntervalForRequest = 8; config.timeoutIntervalForResource = 10
            config.httpCookieStorage = nil; config.urlCache = nil
            self.session = URLSession(configuration: config)
        }
    }
    func checkAtLaunch() async {
        guard !checkedOnLaunch else { return }
        checkedOnLaunch = true
        if checkOnStartup { await check(manual: false) }
    }
    func check(manual: Bool = true, presentResult: Bool = true) async {
        guard !isChecking else { return }
        isChecking = true; status = "Checking for updates…"
        defer { isChecking = false }
        do {
            var request = URLRequest(url: feedURL, cachePolicy: .reloadIgnoringLocalCacheData, timeoutInterval: 8)
            request.setValue("TypstStudio/\(currentVersion)", forHTTPHeaderField: "User-Agent")
            request.setValue("no-cache", forHTTPHeaderField: "Cache-Control")
            let (data, response) = try await session.data(for: request)
            guard let response = response as? HTTPURLResponse, response.statusCode == 200 else {
                throw UpdateError.http((response as? HTTPURLResponse)?.statusCode ?? 0)
            }
            guard data.count <= 32_768 else { throw UpdateError.invalidManifest }
            let manifest = try JSONDecoder().decode(UpdateManifest.self, from: data)
            try manifest.validate()
            guard let installed = AppVersion(currentVersion), let latest = AppVersion(manifest.version) else { throw UpdateError.invalidManifest }
            lastChecked = Date()
            if latest > installed {
                available = manifest
                status = manifest.isCompatible ? "Typst Studio \(manifest.version) is available. You have \(currentVersion)." : "Version \(manifest.version) requires macOS \(manifest.minimumMacOS) or later."
                showResult = presentResult && (manual || manifest.isCompatible)
            } else {
                available = nil; status = "You’re up to date (\(currentVersion))."; showResult = presentResult && manual
            }
        } catch {
            available = nil
            status = "Could not check for updates. " + error.localizedDescription
            showResult = presentResult && manual
        }
    }
    func displayStatus(language: String) -> String {
        guard StudioLanguage.resolved(language) == "zh-Hans" else { return status }
        if isChecking { return "正在检查更新…" }
        if let available {
            return available.isCompatible ? "Typst Studio \(available.version) 已发布，当前版本为 \(currentVersion)。" : "版本 \(available.version) 需要 macOS \(available.minimumMacOS) 或更高版本。"
        }
        if status.hasPrefix("Could not check") { return "无法检查更新，请检查网络后重试。\n" + status }
        if status.hasPrefix("You’re up to date") { return "已是最新版本（\(currentVersion)）。" }
        return StudioLanguage.text(status, language: language)
    }
    func openDownload() {
        guard let available, available.isCompatible else { return }
        NSWorkspace.shared.open(available.downloadURL)
    }
}
