import AppKit

enum IconRenderer {
    private static let creditsCap: Double = 1000

    static func makeIcon(primaryRemaining: Double?, weeklyRemaining: Double?, creditsRemaining: Double?, stale: Bool, style: IconStyle) -> NSImage {
        let size = NSSize(width: 20, height: 18)
        let image = NSImage(size: size)
        image.lockFocus()

        // Keep monochrome template icons; Claude uses subtle shape cues only.
        let baseFill = NSColor.labelColor
        let trackColor = NSColor.labelColor.withAlphaComponent(stale ? 0.28 : 0.5)
        let fillColor = baseFill.withAlphaComponent(stale ? 0.55 : 1.0)

        func drawBar(y: CGFloat, remaining: Double?, height: CGFloat, alpha: CGFloat = 1.0, addNotches: Bool = false) {
            let width: CGFloat = 14
            let x: CGFloat = (size.width - width) / 2
            let radius = height / 2
            let trackRect = CGRect(x: x, y: y, width: width, height: height)
            let trackPath = NSBezierPath(roundedRect: trackRect, xRadius: radius, yRadius: radius)
            trackColor.setStroke()
            trackPath.lineWidth = 1
            trackPath.stroke()

            guard let rawRemaining = remaining ?? (addNotches ? 100 : nil) else { return }
            // Clamp fill because backend might occasionally send >100 or <0.
            let clamped = max(0, min(rawRemaining / 100, 1))
            let fillRect = CGRect(x: x, y: y, width: width * clamped, height: height)
            let fillPath = NSBezierPath(roundedRect: fillRect, xRadius: radius, yRadius: radius)
            fillColor.withAlphaComponent(alpha).setFill()
            fillPath.fill()

            // Claude twist: tiny eye cutouts + side “ears” and small legs to feel more characterful.
            if addNotches {
                let ctx = NSGraphicsContext.current?.cgContext
                ctx?.saveGState()
                ctx?.setBlendMode(.clear)
                let eyeSize: CGFloat = 1.5
                let eyeY = y + height * 0.50
                let eyeOffset: CGFloat = 3.2
                let center = x + width / 2
                ctx?.addEllipse(in: CGRect(x: center - eyeOffset - eyeSize / 2, y: eyeY - eyeSize / 2, width: eyeSize, height: eyeSize))
                ctx?.addEllipse(in: CGRect(x: center + eyeOffset - eyeSize / 2, y: eyeY - eyeSize / 2, width: eyeSize, height: eyeSize))
                ctx?.fillPath()

                // Ears: outward bumps on both ends (clear to carve) then refill to accent edges.
                let earWidth: CGFloat = 2.6
                let earHeight: CGFloat = height * 0.9
                ctx?.addRect(CGRect(x: x - 0.6, y: y + (height - earHeight) / 2, width: earWidth, height: earHeight))
                ctx?.addRect(CGRect(x: x + width - earWidth + 0.6, y: y + (height - earHeight) / 2, width: earWidth, height: earHeight))
                ctx?.fillPath()
                ctx?.restoreGState()

                // Refill outward “ears” so they protrude slightly beyond the bar using the fill color.
                fillColor.withAlphaComponent(alpha).setFill()
                NSBezierPath(roundedRect: CGRect(x: x - 0.8, y: y + (height - earHeight) / 2, width: earWidth * 0.8, height: earHeight), xRadius: 0.9, yRadius: 0.9).fill()
                NSBezierPath(roundedRect: CGRect(x: x + width - earWidth * 0.8 + 0.8, y: y + (height - earHeight) / 2, width: earWidth * 0.8, height: earHeight), xRadius: 0.9, yRadius: 0.9).fill()

                // Tiny legs under the bar.
                let legWidth: CGFloat = 1.4
                let legHeight: CGFloat = 2.1
                let legY = y - 1.4
                let legOffsets: [CGFloat] = [-4.2, -1.4, 1.4, 4.2]
                for offset in legOffsets {
                    let lx = center + offset - legWidth / 2
                    NSBezierPath(rect: CGRect(x: lx, y: legY, width: legWidth, height: legHeight)).fill()
                }
            }
        }

        let topValue = primaryRemaining
        let bottomValue = weeklyRemaining
        let creditsRatio = creditsRemaining.map { min($0 / Self.creditsCap * 100, 100) }

        let weeklyAvailable = (weeklyRemaining ?? 0) > 0
        let claudeExtraHeight: CGFloat = style == .claude ? 0.6 : 0
        let creditsHeight: CGFloat = 6.5 + claudeExtraHeight
        let topHeight: CGFloat = 3.2 + claudeExtraHeight
        let bottomHeight: CGFloat = 2.0
        let creditsAlpha: CGFloat = 1.0

        if weeklyAvailable {
            // Normal: top=5h, bottom=weekly, no credits.
            drawBar(y: 9.5, remaining: topValue, height: topHeight, addNotches: style == .claude)
            drawBar(y: 4.0, remaining: bottomValue, height: bottomHeight)
        } else {
            // Weekly exhausted/missing: show credits on top (thicker), weekly (likely 0) on bottom.
            if let ratio = creditsRatio {
                drawBar(y: 9.0, remaining: ratio, height: creditsHeight, alpha: creditsAlpha, addNotches: style == .claude)
            } else {
                // No credits available; fall back to 5h if present.
                drawBar(y: 9.5, remaining: topValue, height: topHeight, addNotches: style == .claude)
            }
            drawBar(y: 2.5, remaining: bottomValue, height: bottomHeight)
        }

        image.unlockFocus()
        image.isTemplate = true
        return image
    }
}
