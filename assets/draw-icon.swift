import AppKit
import CoreGraphics

func rounded(_ r: CGRect, _ radius: CGFloat) -> CGPath {
    CGPath(roundedRect: r, cornerWidth: radius, cornerHeight: radius, transform: nil)
}

func drawIcon(size s: CGFloat) -> CGImage {
    let cs = CGColorSpaceCreateDeviceRGB()
    let ctx = CGContext(data: nil, width: Int(s), height: Int(s), bitsPerComponent: 8,
                        bytesPerRow: 0, space: cs,
                        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.interpolationQuality = .high
    ctx.setAllowsAntialiasing(true)

    let tile = CGRect(x: s*0.05, y: s*0.05, width: s*0.90, height: s*0.90)
    ctx.saveGState()
    ctx.addPath(rounded(tile, s*0.225))
    ctx.clip()
    let grad = CGGradient(colorsSpace: cs, colors: [
        CGColor(red: 0.106, green: 0.510, blue: 0.470, alpha: 1),
        CGColor(red: 0.035, green: 0.267, blue: 0.251, alpha: 1),
    ] as CFArray, locations: [0, 1])!
    ctx.drawLinearGradient(grad, start: CGPoint(x: tile.minX, y: tile.maxY),
                           end: CGPoint(x: tile.maxX, y: tile.minY), options: [])
    ctx.restoreGState()

    // A white envelope, seen face on. Body first.
    let w = s*0.52, h = w*0.70
    let env = CGRect(x: (s-w)/2, y: (s-h)/2 - s*0.01, width: w, height: h)
    ctx.setFillColor(CGColor(red: 1, green: 1, blue: 1, alpha: 1))
    ctx.addPath(rounded(env, s*0.04))
    ctx.fillPath()

    // The flap: a teal V sitting inside the body, meeting a little above centre. Drawn
    // in the tile's own colour so it reads as a fold rather than a drawn line.
    let flapColour = CGColor(red: 0.055, green: 0.322, blue: 0.302, alpha: 1)
    let inset = s*0.045
    ctx.saveGState()
    ctx.addPath(rounded(env.insetBy(dx: s*0.012, dy: s*0.012), s*0.03))
    ctx.clip()
    ctx.setFillColor(flapColour)
    ctx.move(to: CGPoint(x: env.minX - inset, y: env.maxY + inset))
    ctx.addLine(to: CGPoint(x: env.midX, y: env.midY + h*0.03))
    ctx.addLine(to: CGPoint(x: env.maxX + inset, y: env.maxY + inset))
    ctx.closePath()
    ctx.fillPath()
    ctx.restoreGState()

    // A thin teal keyline so the white envelope holds its shape on a light background.
    ctx.setStrokeColor(flapColour)
    ctx.setLineWidth(s*0.012)
    ctx.addPath(rounded(env, s*0.04))
    ctx.strokePath()

    return ctx.makeImage()!
}

func write(_ img: CGImage, _ path: String) {
    try! NSBitmapImageRep(cgImage: img).representation(using: .png, properties: [:])!
        .write(to: URL(fileURLWithPath: path))
}

let out = CommandLine.arguments[1]
for size in [16, 32, 48, 64, 128, 256, 512, 1024] {
    write(drawIcon(size: CGFloat(size)), "\(out)/icon-\(size).png")
}
print("done")
