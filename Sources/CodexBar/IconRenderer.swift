import AppKit

enum IconRenderer {
    private static let creditsCap: Double = 1000

    static func makeIcon(primaryRemaining: Double?, weeklyRemaining: Double?, creditsRemaining: Double?, stale: Bool) -> NSImage {
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
            // Clamp fill because backend might occasionally send >100 or <0.
            let clamped = max(0, min(remaining / 100, 1))
            let fillRect = CGRect(x: x, y: y, width: width * clamped, height: height)
            let fillPath = NSBezierPath(roundedRect: fillRect, xRadius: radius, yRadius: radius)
            fillColor.setFill()
            fillPath.fill()
        }

        func drawHatchedBar(y: CGFloat, fillRatio: Double, height: CGFloat) {
            let width: CGFloat = 14
            let x: CGFloat = (size.width - width) / 2
            let radius = height / 2
            let trackRect = CGRect(x: x, y: y, width: width, height: height)
            let trackPath = NSBezierPath(roundedRect: trackRect, xRadius: radius, yRadius: radius)
            trackColor.setStroke()
            trackPath.lineWidth = 1
            trackPath.stroke()

            let clamped = max(0, min(fillRatio, 1))
            let fillRect = CGRect(x: x, y: y, width: width * clamped, height: height)
            let fillPath = NSBezierPath(roundedRect: fillRect, xRadius: radius, yRadius: radius)
            fillColor.withAlphaComponent(0.7).setFill()
            fillPath.fill()

            // Hatch overlay
            NSGraphicsContext.current?.saveGraphicsState()
            fillPath.addClip()
            let hatchPath = NSBezierPath()
            let spacing: CGFloat = 2.0
            let thickness: CGFloat = 0.8
            let maxX = fillRect.maxX
            let maxY = fillRect.maxY
            var startX: CGFloat = fillRect.minX - maxY
            while startX < maxX {
                hatchPath.move(to: CGPoint(x: startX, y: fillRect.minY))
                hatchPath.line(to: CGPoint(x: startX + maxY, y: maxY))
                startX += spacing
            }
            NSColor.labelColor.withAlphaComponent(0.6).setStroke()
            hatchPath.lineWidth = thickness
            hatchPath.stroke()
            NSGraphicsContext.current?.restoreGraphicsState()
        }

        var topValue = primaryRemaining
        var bottomValue = weeklyRemaining
        let creditsRatio = creditsRemaining.map { min($0 / Self.creditsCap * 100, 100) }

        if let primary = primaryRemaining, primary <= 0, creditsRemaining != nil {
            topValue = nil
        } else if primaryRemaining == nil, creditsRemaining != nil {
            topValue = nil
        }

        if let weekly = weeklyRemaining, weekly <= 0, bottomValue != nil, creditsRemaining != nil {
            bottomValue = nil
        } else if weeklyRemaining == nil, creditsRemaining != nil {
            bottomValue = nil
        }

        // If both top and bottom are cleared, show credits only.
        if (topValue == nil && bottomValue == nil), let ratio = creditsRatio {
            drawHatchedBar(y: (size.height - 3.2) / 2, fillRatio: ratio / 100, height: 3.2)
        } else {
            if let topValue {
                drawBar(y: 9.5, remaining: topValue, height: 3.2)
            } else if let ratio = creditsRatio {
                drawHatchedBar(y: 9.5, fillRatio: ratio / 100, height: 3.2)
            }

            if let bottomValue {
                drawBar(y: 4.0, remaining: bottomValue, height: 2.0)
            } else if let ratio = creditsRatio {
                drawHatchedBar(y: 4.0, fillRatio: ratio / 100, height: 2.0)
            }
        }

        image.unlockFocus()
        image.isTemplate = true
        return image
    }
}
