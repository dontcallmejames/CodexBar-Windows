import XCTest
@testable import CodexBar

final class CodexBarTests: XCTestCase {
    func testIconRendererProducesTemplateImage() {
        let image = IconRenderer.makeIcon(primaryRemaining: 50, weeklyRemaining: 75, stale: false)
        XCTAssertTrue(image.isTemplate)
        XCTAssertGreaterThan(image.size.width, 0)
    }
}
