import Foundation

final class FeedProtocol: URLProtocol, @unchecked Sendable {
    static let lock = NSLock()
    static var data = Data()
    static var code = 200
    static var requests = 0
    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    override func startLoading() {
        Self.lock.lock(); let data = Self.data, code = Self.code; Self.requests += 1; Self.lock.unlock()
        let response = HTTPURLResponse(url: request.url!, statusCode: code, httpVersion: "HTTP/1.1", headerFields: ["Content-Type": "application/json"])!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data); client?.urlProtocolDidFinishLoading(self)
    }
    override func stopLoading() {}
}
@main struct UpdateCheckerTests {
    @MainActor static func main() async throws {
        precondition(AppVersion("0.1.10")! > AppVersion("0.1.9")!)
        precondition(AppVersion("1.0.0")! > AppVersion("0.99.99")!)
        precondition(AppVersion("0.1.5-beta") == nil)
        var manifest: [String: String] = [
            "version": "0.1.6", "minimum_macos": "14.0", "architecture": "arm64",
            "sha256": String(repeating: "a", count: 64),
            "download_url": "https://github.com/liaoweiyang2017/typst-studio-releases/releases/download/v0.1.6/Typst-Studio-0.1.6-macOS-arm64.dmg",
            "release_url": "https://github.com/liaoweiyang2017/typst-studio-releases/releases/tag/v0.1.6"
        ]
        let valid = try JSONSerialization.data(withJSONObject: manifest)
        let decoded = try JSONDecoder().decode(UpdateManifest.self, from: valid); try decoded.validate()
        manifest["download_url"] = "https://example.com/fake.dmg"
        let invalid = try JSONDecoder().decode(UpdateManifest.self, from: JSONSerialization.data(withJSONObject: manifest))
        do { try invalid.validate(); fatalError("Untrusted download URL accepted") } catch {}
        let config = URLSessionConfiguration.ephemeral; config.protocolClasses = [FeedProtocol.self]
        let session = URLSession(configuration: config)
        FeedProtocol.data = valid
        let checker = UpdateChecker(currentVersion: "0.1.5", session: session)
        await checker.check(manual: false)
        precondition(checker.available?.version == "0.1.6" && checker.showResult && !checker.isChecking)
        let future = UpdateChecker(currentVersion: "0.2.0", session: session)
        await future.check(manual: false)
        precondition(future.available == nil && !future.showResult)
        let startup = UpdateChecker(currentVersion: "0.1.5", session: session)
        startup.checkOnStartup = true
        let count = FeedProtocol.requests
        await startup.checkAtLaunch(); await startup.checkAtLaunch()
        precondition(FeedProtocol.requests == count + 1)
        FeedProtocol.code = 503
        await checker.check(manual: false)
        precondition(checker.available == nil && !checker.showResult && !checker.isChecking)
        FeedProtocol.code = 200; FeedProtocol.data = Data("not json".utf8)
        await checker.check(manual: true)
        precondition(checker.available == nil && checker.showResult)
        print("PASS: semantic versions, allowlisted URLs, startup once, newer/no downgrade, HTTP and malformed-feed recovery")
        if ProcessInfo.processInfo.environment["STUDIO_LIVE_UPDATE_TEST"] == "1" {
            let live = UpdateChecker(currentVersion: "0.1.7")
            await live.check(manual: false)
            precondition(live.available?.version == "0.1.8", live.status)
            print("PASS: anonymous public update feed reports 0.1.8 to 0.1.7")
        }
    }
}
