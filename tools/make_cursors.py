# python make_cursors.py <output folder>
import math
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

OUT = 256
SS = 4
C = OUT * SS


def px(points):
    return [(x * C, y * C) for x, y in points]


def mask_of(draw_fn):
    m = Image.new("L", (C, C), 0)
    draw_fn(ImageDraw.Draw(m))
    return m


def grow(mask, radius):
    if radius <= 0:
        return mask
    size = int(radius * 2) | 1
    return mask.filter(ImageFilter.MaxFilter(size))


def shrink(mask, radius):
    size = int(radius * 2) | 1
    return mask.filter(ImageFilter.MinFilter(size))


def gradient(top, bottom, angle=90):
    g = Image.new("RGBA", (C, C))
    a = math.radians(angle)
    dx, dy = math.cos(a), math.sin(a)
    row = Image.new("RGBA", (256, 1))
    for i in range(256):
        t = i / 255
        row.putpixel((i, 0), tuple(int(top[k] + (bottom[k] - top[k]) * t) for k in range(4)))
    strip = row.resize((C * 2, C * 2), Image.BILINEAR).rotate(-math.degrees(math.atan2(dy, dx)), Image.BILINEAR)
    off = C // 2
    return strip.crop((off, off, off + C, off + C))


def solid(colour):
    return Image.new("RGBA", (C, C), colour)


def paint(canvas, fill, mask):
    canvas.alpha_composite(Image.composite(fill, Image.new("RGBA", (C, C), (0, 0, 0, 0)), mask))


def finish(canvas):
    return canvas.resize((OUT, OUT), Image.LANCZOS)



ARROW = [(0.06, 0.05), (0.62, 0.36), (0.36, 0.40), (0.24, 0.64)]
ARROW_INNER = [(0.11, 0.12), (0.47, 0.34), (0.31, 0.36), (0.235, 0.52)]


UNIT = 0.62 / 19
STEMMED = [(0.06 + x * UNIT, 0.05 + y * UNIT) for x, y in [(0, 0), (0, 16), (4, 12), (7, 19), (9, 18), (6, 11), (11, 11)]]


def arrow_mask(scale=1.0, ox=0.0, oy=0.0, shape=ARROW):
    pts = [(ox + x * scale, oy + y * scale) for x, y in shape]
    return mask_of(lambda d: d.polygon(px(pts), fill=255))


def double_arrow(length=0.70, head=0.20, shaft=0.07):
    half = length / 2
    h = head
    w = shaft / 2
    return [
        (0.5 - half, 0.5),
        (0.5 - half + h, 0.5 - h * 0.9),
        (0.5 - half + h, 0.5 - w),
        (0.5 + half - h, 0.5 - w),
        (0.5 + half - h, 0.5 - h * 0.9),
        (0.5 + half, 0.5),
        (0.5 + half - h, 0.5 + h * 0.9),
        (0.5 + half - h, 0.5 + w),
        (0.5 - half + h, 0.5 + w),
        (0.5 - half + h, 0.5 + h * 0.9),
    ]


def rotate(points, degrees):
    a = math.radians(degrees)
    c, s = math.cos(a), math.sin(a)
    return [(0.5 + (x - 0.5) * c - (y - 0.5) * s, 0.5 + (x - 0.5) * s + (y - 0.5) * c) for x, y in points]


def stretch_mask(degrees):
    return mask_of(lambda d: d.polygon(px(rotate(double_arrow(), degrees)), fill=255))


def hand_mask():
    def draw(d):
        r = 0.045 * C
        d.rounded_rectangle(px([(0.36, 0.06), (0.47, 0.50)]), radius=r, fill=255)
        d.rounded_rectangle(px([(0.47, 0.32), (0.58, 0.56)]), radius=r, fill=255)
        d.rounded_rectangle(px([(0.58, 0.35), (0.69, 0.58)]), radius=r, fill=255)
        d.rounded_rectangle(px([(0.69, 0.39), (0.79, 0.60)]), radius=r, fill=255)
        d.rounded_rectangle(px([(0.36, 0.44), (0.79, 0.80)]), radius=r * 2.2, fill=255)
        d.polygon(px([(0.16, 0.50), (0.23, 0.45), (0.40, 0.60), (0.40, 0.76), (0.30, 0.74)]), fill=255)
        d.ellipse(px([(0.13, 0.44), (0.25, 0.56)]), fill=255)
    return mask_of(draw)


def hand_lines():
    def draw(d):
        w = int(0.012 * C)
        d.line(px([(0.47, 0.44), (0.47, 0.56)]), fill=255, width=w)
        d.line(px([(0.58, 0.46), (0.58, 0.58)]), fill=255, width=w)
        d.line(px([(0.69, 0.48), (0.69, 0.60)]), fill=255, width=w)
    return mask_of(draw)


def lock_mask():
    def draw(d):
        d.rounded_rectangle(px([(0.56, 0.62), (0.86, 0.88)]), radius=0.03 * C, fill=255)
        d.arc(px([(0.61, 0.46), (0.81, 0.72)]), 180, 360, fill=255, width=int(0.045 * C))
        d.line(px([(0.632, 0.59), (0.632, 0.64)]), fill=255, width=int(0.045 * C))
        d.line(px([(0.788, 0.59), (0.788, 0.64)]), fill=255, width=int(0.045 * C))
    return mask_of(draw)


def lock_hole():
    def draw(d):
        d.ellipse(px([(0.69, 0.70), (0.73, 0.74)]), fill=255)
        d.rectangle(px([(0.70, 0.72), (0.72, 0.80)]), fill=255)
    return mask_of(draw)


def pin_mask():
    def draw(d):
        d.ellipse(px([(0.62, 0.48), (0.86, 0.72)]), fill=255)
        d.polygon(px([(0.71, 0.68), (0.77, 0.68), (0.66, 0.94), (0.64, 0.93)]), fill=255)
    return mask_of(draw)



def tactical(canvas, mask, accent=None, outline=0.012, shadow=True):
    o = outline * C
    if shadow:
        sh = grow(mask, o).filter(ImageFilter.GaussianBlur(0.025 * C))
        sh = ImageChops.offset(sh, int(0.012 * C), int(0.018 * C))
        paint(canvas, solid((0, 0, 0, 170)), sh)
    paint(canvas, solid((22, 24, 26, 255)), grow(mask, o))
    paint(canvas, gradient((255, 255, 255, 255), (150, 156, 162, 255), 60), mask)
    inner = shrink(mask, 0.016 * C)
    paint(canvas, gradient((232, 236, 240, 255), (128, 134, 140, 255), 60), inner)
    if accent:
        rim = ImageChops.subtract(inner, shrink(inner, 0.026 * C))
        paint(canvas, solid(accent), rim)


def flat(fill, edge, outline=0.022, shadow=True):
    def style(canvas, mask, accent=None):
        o = outline * C
        if shadow:
            sh = grow(mask, o).filter(ImageFilter.GaussianBlur(0.02 * C))
            sh = ImageChops.offset(sh, int(0.01 * C), int(0.016 * C))
            paint(canvas, solid((0, 0, 0, 120)), sh)
        paint(canvas, solid(edge), grow(mask, o))
        paint(canvas, solid(accent or fill), mask)
    return style


def tactical_style(canvas, mask, accent=None):
    tactical(canvas, mask, accent)


def build_set(style, detail, shape):
    cursors = {}

    def layer(*masks, accent=None):
        c = Image.new("RGBA", (C, C), (0, 0, 0, 0))
        for m in masks:
            style(c, m, accent)
        return c

    arrow = arrow_mask(shape=shape)
    cursors["idle"] = layer(arrow)

    badge_arrow = arrow_mask(0.92, shape=shape)
    lock = layer(badge_arrow, lock_mask())
    paint(lock, solid(detail), lock_hole())
    cursors["set_lock"] = lock
    cursors["set_pin"] = layer(badge_arrow, pin_mask())

    hand = layer(hand_mask())
    paint(hand, solid(detail), hand_lines())
    cursors["hand"] = hand

    cursors["stretch_horizontal"] = layer(stretch_mask(0))
    cursors["stretch_vertical"] = layer(stretch_mask(90))
    cursors["stretch_corner"] = layer(stretch_mask(45))
    cursors["stretch_corner_2"] = layer(stretch_mask(-45))
    return {k: finish(v) for k, v in cursors.items()}


SETS = {
    "Tactical HD": (tactical_style, (40, 44, 48, 255), ARROW),
    "Classic White": (flat((250, 250, 250, 255), (10, 10, 10, 255)), (10, 10, 10, 255), ARROW),
    "Classic Black": (flat((12, 12, 12, 255), (245, 245, 245, 255)), (245, 245, 245, 255), ARROW),
    "Windows Classic": (flat((255, 255, 255, 255), (0, 0, 0, 255), outline=0.016), (0, 0, 0, 255), STEMMED),
}


def hotspots(shape):
    tip = shape[0]
    return {
        "idle": tip,
        "set_lock": (tip[0] * 0.92, tip[1] * 0.92),
        "set_pin": (tip[0] * 0.92, tip[1] * 0.92),
        "hand": (0.415, 0.06),
        "stretch_horizontal": (0.5, 0.5),
        "stretch_vertical": (0.5, 0.5),
        "stretch_corner": (0.5, 0.5),
        "stretch_corner_2": (0.5, 0.5),
    }


def main():
    out = sys.argv[1]
    for name, (style, detail, shape) in SETS.items():
        folder = os.path.join(out, name)
        os.makedirs(folder, exist_ok=True)
        for key, img in build_set(style, detail, shape).items():
            img.save(os.path.join(folder, key + ".png"), optimize=True)
        with open(os.path.join(folder, "hotspots.txt"), "w", newline="\n") as f:
            f.write("# Where each cursor clicks, as a fraction of the image width and height from the top left.\n")
            for key, (x, y) in hotspots(shape).items():
                f.write(f"{key} = {x:.4f}, {y:.4f}\n")
        print("drew", name)


if __name__ == "__main__":
    main()
