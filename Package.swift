// swift-tools-version: 5.9
import PackageDescription
let package = Package(name: "TypstStudio", platforms: [.macOS(.v14)], products: [.executable(name: "TypstStudio", targets: ["TypstStudio"])], targets: [.executableTarget(name: "TypstStudio")])
