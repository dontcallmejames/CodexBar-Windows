import AppKit

enum IconRenderer {
    static func makeIcon(primaryRemaining: Double?, weeklyRemaining: Double?, stale: Bool) -> NSImage {
        let size = NSSize(width: 20, height: 18)
        let image = NSImage(size: size)
        image.lockFocus()

        let trackColor = NSColor.labelColor.withAlphaComponent(stale ? 0.35 : 0.6)
        let fillColor = NSColor.labelColor.withAlphaComponent(stale ? 0.55 : 1.0)

        func drawBar(y: CGFloat, remaining: Double?, height: CGFloat) {
            let width: CGFloat = 14
            let x: CGFloat = (size.width - width) / 2
            let radius = height / 2
            let trackRect = CGRect(x: x, y: y, width: width, height: height)
            let trackPath = NSBezierPath(roundedRect: trackRect, xRadius: radius, yRadius: radius)
            trackColor.setStroke()
            trackPath.lineWidth = 1
            trackPath.stroke()

            guard let remaining else { return }
            let clamped = max(0, min(remaining / 100, 1))
            let fillRect = CGRect(x: x, y: y, width: width * clamped, height: height)
            let fillPath = NSBezierPath(roundedRect: fillRect, xRadius: radius, yRadius: radius)
            fillColor.setFill()
            fillPath.fill()
        }

        drawBar(y: 9.5, remaining: primaryRemaining, height: 3.2)
        drawBar(y: 4.0, remaining: weeklyRemaining, height: 2.0)

        image.unlockFocus()
        image.isTemplate = true
        return image
    }
}
