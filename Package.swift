// swift-tools-version: 5.9
import PackageDescription
let package = Package(
    name: "TypstStudio",
    platforms: [.macOS(.v14)],
    products: [.executable(name: "TypstStudio", targets: ["TypstStudio"])],
    dependencies: [.package(url: "https://github.com/sparkle-project/Sparkle", exact: "2.10.0")],
    targets: [.executableTarget(name: "TypstStudio", dependencies: [.product(name: "Sparkle", package: "Sparkle")])]
)
