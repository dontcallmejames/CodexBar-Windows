import AppKit

enum IconRenderer {
    private static let creditsCap: Double = 1000

    static func makeIcon(primaryRemaining: Double?, weeklyRemaining: Double?, creditsRemaining: Double?, stale: Bool) -> NSImage {
        let size = NSSize(width: 20, height: 18)
        let image = NSImage(size: size)
        image.lockFocus()

        let trackColor = NSColor.labelColor.withAlphaComponent(stale ? 0.35 : 0.6)
        let fillColor = NSColor.labelColor.withAlphaComponent(stale ? 0.55 : 1.0)

        func drawBar(y: CGFloat, remaining: Double?, height: CGFloat, alpha: CGFloat = 1.0) {
            let width: CGFloat = 14
            let x: CGFloat = (size.width - width) / 2
            let radius = height / 2
            let trackRect = CGRect(x: x, y: y, width: width, height: height)
            let trackPath = NSBezierPath(roundedRect: trackRect, xRadius: radius, yRadius: radius)
            trackColor.setStroke()
            trackPath.lineWidth = 1
            trackPath.stroke()

            guard let remaining else { return }
            // Clamp fill because backend might occasionally send >100 or <0.
            let clamped = max(0, min(remaining / 100, 1))
            let fillRect = CGRect(x: x, y: y, width: width * clamped, height: height)
            let fillPath = NSBezierPath(roundedRect: fillRect, xRadius: radius, yRadius: radius)
            fillColor.withAlphaComponent(alpha).setFill()
            fillPath.fill()
        }

        let topValue = primaryRemaining
        let bottomValue = weeklyRemaining
        let creditsRatio = creditsRemaining.map { min($0 / Self.creditsCap * 100, 100) }

        let weeklyAvailable = (weeklyRemaining ?? 0) > 0
        let creditsHeight: CGFloat = 6.5
        let topHeight: CGFloat = 3.2
        let bottomHeight: CGFloat = 2.0
        let creditsAlpha: CGFloat = 1.0

        if weeklyAvailable {
            // Normal: top=5h, bottom=weekly, no credits.
            drawBar(y: 9.5, remaining: topValue, height: topHeight)
            drawBar(y: 4.0, remaining: bottomValue, height: bottomHeight)
        } else {
            // Weekly exhausted/missing: show credits on top (thicker), weekly (likely 0) on bottom.
            if let ratio = creditsRatio {
                drawBar(y: 9.0, remaining: ratio, height: creditsHeight, alpha: creditsAlpha)
            } else {
                // No credits available; fall back to 5h if present.
                drawBar(y: 9.5, remaining: topValue, height: topHeight)
            }
            drawBar(y: 2.5, remaining: bottomValue, height: bottomHeight)
        }

        image.unlockFocus()
        image.isTemplate = true
        return image
    }
}
