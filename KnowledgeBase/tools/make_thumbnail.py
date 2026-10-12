"""Turn a Unity screenshot into a 1920x1080 YouTube thumbnail.

    pip install pillow
    python KnowledgeBase/tools/make_thumbnail.py frame.png out.jpg "WHO WINS?" [--sub "12 MARBLES. 1 WINNER."]

Crops to 16:9, boosts saturation/contrast, adds a vignette, then draws outlined
text in the upper-left (bottom-right is left clear for YouTube's duration badge).
"""
import argparse
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

W, H = 1920, 1080
FONTS = ["C:/Windows/Fonts/impact.ttf", "C:/Windows/Fonts/arialbd.ttf"]


def font(size):
    for f in FONTS:
        try:
            return ImageFont.truetype(f, size)
        except OSError:
            pass
    return ImageFont.load_default()


def cover(img):
    s = max(W / img.width, H / img.height)
    img = img.resize((round(img.width * s), round(img.height * s)), Image.LANCZOS)
    l, t = (img.width - W) // 2, (img.height - H) // 2
    return img.crop((l, t, l + W, t + H))


def vignette(img, strength=0.55):
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).ellipse((-W * 0.25, -H * 0.25, W * 1.25, H * 1.25), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(220))
    dark = Image.new("RGB", (W, H), (0, 0, 0))
    return Image.composite(img, Image.blend(img, dark, strength), mask)


def outlined(draw, xy, text, fnt, fill, outline=(0, 0, 0), width=10):
    draw.text(xy, text, font=fnt, fill=fill, stroke_width=width, stroke_fill=outline)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src")
    ap.add_argument("dst")
    ap.add_argument("text")
    ap.add_argument("--sub", default="")
    ap.add_argument("--size", type=int, default=230)
    ap.add_argument("--right", action="store_true", help="put text on the right half, centred vertically")
    ap.add_argument("--crop", default="", help="left,top,right,bottom in source pixels, applied before the 16:9 fit")
    a = ap.parse_args()

    img = Image.open(a.src).convert("RGB")
    if a.crop:
        img = img.crop(tuple(int(v) for v in a.crop.split(",")))
    img = cover(img)
    img = ImageEnhance.Color(img).enhance(1.25)
    img = ImageEnhance.Contrast(img).enhance(1.12)
    img = ImageEnhance.Brightness(img).enhance(1.08)
    img = vignette(img)

    d = ImageDraw.Draw(img)
    big, small = font(a.size), font(int(a.size * 0.42))
    bw = d.textlength(a.text, font=big)
    sw = d.textlength(a.sub, font=small) if a.sub else 0
    block_h = a.size + (int(a.size * 0.42) + 40 if a.sub else 0)
    if a.right:
        cx, y = W - 70 - max(bw, sw) / 2 - 20, (H - block_h) // 2 - 60
        bx, sx = cx - bw / 2, cx - sw / 2
    else:
        bx, sx, y = 70, 80, 50
    outlined(d, (bx, y), a.text, big, (255, 255, 255))
    if a.sub:
        outlined(d, (sx, y + a.size + 20), a.sub, small, (255, 215, 120), width=7)

    img.save(a.dst, quality=92)
    img.resize((200, 112)).save(a.dst.rsplit(".", 1)[0] + "_preview200.png")
    print("wrote", a.dst, "+ 200px preview (check it still reads)")


main()
