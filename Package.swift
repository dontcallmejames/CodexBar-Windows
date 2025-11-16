// swift-tools-version: 6.2
import PackageDescription

let package = Package(
    name: "CodexBar",
    platforms: [
        .macOS(.v15),
    ],
    targets: [
        .executableTarget(
            name: "CodexBar",
            path: "Sources/CodexBar"),
        .testTarget(
            name: "CodexBarTests",
            dependencies: ["CodexBar"],
            path: "Tests"),
    ])
