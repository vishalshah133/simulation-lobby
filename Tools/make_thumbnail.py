"""Turn a gameplay screenshot into a YouTube thumbnail (1280x720).

Steps: crop to 16:9 around a focus point, punch saturation/contrast, then overlay a big outlined
headline, an optional giant number/word, the four team colour dots, and an optional arrow pointing
at the action.

Example:
    python Tools/make_thumbnail.py shot.png --out thumb.png --headline "WHO WINS?" \
        --number "BOMB!" --arrow 0.62,0.55 --focus 0.5,0.5

Coordinates (--focus, --arrow) are fractions of the SOURCE image: 0,0 = top-left, 1,1 = bottom-right.
Lives outside Assets/ on purpose: Unity never imports it.
"""
import argparse
import math
import os

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

W, H = 1280, 720
FONTS = "C:/Windows/Fonts"
TEAM_COLOURS = [(61, 220, 74), (255, 210, 58), (46, 107, 255), (255, 59, 59)]  # green, yellow, blue, red


def font(name, size):
    for candidate in (name, "ariblk.ttf", "arialbd.ttf"):
        path = os.path.join(FONTS, candidate)
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def crop_16x9(img, fx, fy, zoom):
    """Largest 16:9 window (shrunk by zoom) centred as close to the focus point as the image allows."""
    sw, sh = img.size
    cw = min(sw, sh * 16 / 9) / zoom
    ch = cw * 9 / 16
    cx = min(max(fx * sw, cw / 2), sw - cw / 2)
    cy = min(max(fy * sh, ch / 2), sh - ch / 2)
    box = (cx - cw / 2, cy - ch / 2, cx + cw / 2, cy + ch / 2)
    return img.crop(tuple(int(round(v)) for v in box)).resize((W, H), Image.LANCZOS), box


def fit_text(draw, text, font_name, max_w, max_size):
    size = max_size
    while size > 24:
        f = font(font_name, size)
        if draw.textlength(text, font=f) <= max_w:
            return f
        size -= 4
    return font(font_name, size)


def outlined(draw, xy, text, f, fill, stroke, anchor="mm"):
    sw = max(4, f.size // 9)
    draw.text(xy, text, font=f, fill=fill, stroke_width=sw, stroke_fill=stroke, anchor=anchor)


def shadow_layer(base, draw_fn, blur=10, offset=(6, 8), alpha=170):
    """Soft drop shadow behind whatever draw_fn draws (keeps text readable on busy frames)."""
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(layer), (0, 0, 0, alpha))
    layer = layer.filter(ImageFilter.GaussianBlur(blur))
    shifted = Image.new("RGBA", base.size, (0, 0, 0, 0))
    shifted.paste(layer, offset, layer)
    return Image.alpha_composite(base, shifted)


def arrow(draw, tip, tail, colour, width):
    draw.line([tail, tip], fill=(0, 0, 0), width=width + 12)
    draw.line([tail, tip], fill=colour, width=width)
    ang = math.atan2(tip[1] - tail[1], tip[0] - tail[0])
    head = width * 2.6
    pts = [tip,
           (tip[0] - head * math.cos(ang - 0.5), tip[1] - head * math.sin(ang - 0.5)),
           (tip[0] - head * math.cos(ang + 0.5), tip[1] - head * math.sin(ang + 0.5))]
    draw.polygon(pts, fill=colour, outline=(0, 0, 0), width=6)


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("source")
    p.add_argument("--out", default=None, help="output path (default: <source>_thumb.png)")
    p.add_argument("--headline", default="WHO WINS?")
    p.add_argument("--headline-pos", choices=["top", "bottom"], default="top")
    p.add_argument("--number", default=None, help='giant accent word/number, e.g. "×5!" or "BOMB!"')
    p.add_argument("--number-color", default="#FF4FA0")
    p.add_argument("--number-pos", choices=["left", "right"], default="right")
    p.add_argument("--arrow", default=None, help="tip as fx,fy (fractions of the source image)")
    p.add_argument("--focus", default="0.5,0.5", help="crop centre as fx,fy")
    p.add_argument("--zoom", type=float, default=1.0, help=">1 crops tighter around --focus")
    p.add_argument("--saturation", type=float, default=1.25)
    p.add_argument("--contrast", type=float, default=1.15)
    p.add_argument("--no-dots", action="store_true")
    a = p.parse_args()

    src = Image.open(a.source).convert("RGB")
    fx, fy = (float(v) for v in a.focus.split(","))
    img, box = crop_16x9(src, fx, fy, max(1.0, a.zoom))
    img = ImageEnhance.Color(img).enhance(a.saturation)
    img = ImageEnhance.Contrast(img).enhance(a.contrast)
    img = ImageEnhance.Sharpness(img).enhance(1.3)
    canvas = img.convert("RGBA")

    # Darken the headline band slightly so white text pops on any frame.
    band = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    bd = ImageDraw.Draw(band)
    y0, y1 = (0, 230) if a.headline_pos == "top" else (H - 230, H)
    for i in range(y1 - y0):
        t = i / (y1 - y0)
        alpha = int(150 * ((1 - t) if a.headline_pos == "top" else t))
        bd.line([(0, y0 + i), (W, y0 + i)], fill=(0, 0, 0, alpha))
    canvas = Image.alpha_composite(canvas, band)

    probe = ImageDraw.Draw(canvas)
    hf = fit_text(probe, a.headline, "impact.ttf", W - 120, 170)
    hy = 110 if a.headline_pos == "top" else H - 110
    canvas = shadow_layer(canvas, lambda d, c: d.text((W / 2, hy), a.headline, font=hf, fill=c, anchor="mm"))
    outlined(ImageDraw.Draw(canvas), (W / 2, hy), a.headline, hf, (255, 255, 255), (0, 0, 0))

    if a.number:
        nf = fit_text(probe, a.number, "ariblk.ttf", 520, 190)
        nx = W - 300 if a.number_pos == "right" else 300
        ny = H - 150 if a.headline_pos == "top" else 150
        canvas = shadow_layer(canvas, lambda d, c: d.text((nx, ny), a.number, font=nf, fill=c, anchor="mm"))
        outlined(ImageDraw.Draw(canvas), (nx, ny), a.number, nf, a.number_color, (0, 0, 0))

    draw = ImageDraw.Draw(canvas)
    if a.arrow:
        tx, ty = (float(v) for v in a.arrow.split(","))
        # Map the source-fraction tip into the cropped/resized frame.
        tip = ((tx * src.width - box[0]) / (box[2] - box[0]) * W, (ty * src.height - box[1]) / (box[3] - box[1]) * H)
        tail = (tip[0] + (-220 if tip[0] > W / 2 else 220), tip[1] + (-170 if tip[1] > H / 2 else 170))
        arrow(draw, tip, tail, (255, 210, 58), 26)

    if not a.no_dots:
        r, gap = 26, 18
        x = 40 + r
        y = H - 40 - r if a.headline_pos == "top" else 40 + r
        for c in TEAM_COLOURS:
            draw.ellipse([x - r, y - r, x + r, y + r], fill=c, outline=(0, 0, 0), width=6)
            x += 2 * r + gap

    out = a.out or os.path.splitext(a.source)[0] + "_thumb.png"
    canvas.convert("RGB").save(out, quality=95)
    small = canvas.convert("RGB").resize((320, 180), Image.LANCZOS)
    small.save(os.path.splitext(out)[0] + "_preview320.png")
    print(f"saved {out} (1280x720) and a 320px phone-size preview next to it")


if __name__ == "__main__":
    main()
