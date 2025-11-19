import AppKit

enum IconRenderer {
    private static let creditsCap: Double = 1000

    static func makeIcon(primaryRemaining: Double?, weeklyRemaining: Double?, creditsRemaining: Double?, stale: Bool, style: IconStyle) -> NSImage {
        let size = NSSize(width: 20, height: 18)
        let image = NSImage(size: size)
        image.lockFocus()

        let trackColor = NSColor.labelColor.withAlphaComponent(stale ? 0.35 : 0.6)
        let fillColor = NSColor.labelColor.withAlphaComponent(stale ? 0.55 : 1.0)

        func drawBar(y: CGFloat, remaining: Double?, height: CGFloat, alpha: CGFloat = 1.0, addNotches: Bool = false) {
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

            // Claude twist: tiny eye cutouts + claw bumps on the top bar.
            if addNotches {
                let ctx = NSGraphicsContext.current?.cgContext
                ctx?.saveGState()
                ctx?.setBlendMode(.clear)
                let eyeSize: CGFloat = 1.6
                let eyeY = y + height * 0.50
                let eyeOffset: CGFloat = 3.4
                let center = x + width / 2
                ctx?.addEllipse(in: CGRect(x: center - eyeOffset - eyeSize / 2, y: eyeY - eyeSize / 2, width: eyeSize, height: eyeSize))
                ctx?.addEllipse(in: CGRect(x: center + eyeOffset - eyeSize / 2, y: eyeY - eyeSize / 2, width: eyeSize, height: eyeSize))
                ctx?.fillPath()

                // Claws: small inward cuts near ends.
                let clawWidth: CGFloat = 1.6
                let clawHeight: CGFloat = height * 0.82
                ctx?.addRect(CGRect(x: x + 0.2, y: y + (height - clawHeight) / 2, width: clawWidth, height: clawHeight))
                ctx?.addRect(CGRect(x: x + width - clawWidth - 0.2, y: y + (height - clawHeight) / 2, width: clawWidth, height: clawHeight))
                ctx?.fillPath()
                ctx?.restoreGState()

                // Legs: three tiny downward bumps under the top bar to hint the crab.
                let legWidth: CGFloat = 1.3
                let legHeight: CGFloat = 1.6
                let legY = y - 1.0
                let legOffsets: [CGFloat] = [-4.0, 0.0, 4.0]
                fillColor.withAlphaComponent(alpha).setFill()
                for offset in legOffsets {
                    let lx = center + offset - legWidth / 2
                    NSBezierPath(roundedRect: CGRect(x: lx, y: legY, width: legWidth, height: legHeight), xRadius: 0.4, yRadius: 0.4).fill()
                }
            }
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
