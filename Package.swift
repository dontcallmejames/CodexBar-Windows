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
            path: "Sources/CodexBar",
            swiftSettings: [
                // Opt into Swift 6 strict concurrency (approachable migration path).
                .enableUpcomingFeature("StrictConcurrency"),
            ]),
        .testTarget(
            name: "CodexBarTests",
            dependencies: ["CodexBar"],
            path: "Tests",
            swiftSettings: [
                .enableUpcomingFeature("StrictConcurrency"),
            ]),
    ])
