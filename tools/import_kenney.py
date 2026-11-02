# python import_kenney.py <kenney_cursor-pack folder> <output folder>
import io
import os
import shutil
import sys

import resvg_py
from PIL import Image

OUT = 256
SS = 4
C = OUT * SS
STYLE = "Outline"

ARROW_ORIGIN = 0.05
ARROW_FILL = 0.60
CENTRED_FILL = 0.70

POINTERS = {
    "Kenney Arrow": "pointer_a",
    "Kenney Sharp": "pointer_c",
    "Kenney Shaded": "pointer_c_shaded",
    "Kenney Bold": "pointer_h",
    "Kenney Toon": "pointer_toon_a",
    "Kenney Sci-Fi": "pointer_scifi_b",
}

SHARED = {
    "hand": "hand_point",
    "stretch_horizontal": "resize_a_horizontal",
    "stretch_vertical": "resize_a_vertical",
    "stretch_corner": "resize_a_diagonal_mirror",
    "stretch_corner_2": "resize_a_diagonal",
}


def render(pack, name):
    png = resvg_py.svg_to_bytes(svg_path=os.path.join(pack, "Vector", STYLE, name + ".svg"), width=C, height=C)
    im = Image.open(io.BytesIO(bytes(png))).convert("RGBA")
    return im.crop(im.getchannel("A").getbbox())


def place(shape, fill, origin=None):
    scale = fill * C / max(shape.size)
    shape = shape.resize((round(shape.width * scale), round(shape.height * scale)), Image.LANCZOS)
    canvas = Image.new("RGBA", (C, C), (0, 0, 0, 0))
    if origin is None:
        pos = ((C - shape.width) // 2, (C - shape.height) // 2)
    else:
        pos = (round(origin * C), round(origin * C))
    canvas.alpha_composite(shape, pos)
    return canvas


def tip(im):
    alpha = im.getchannel("A").load()
    best = None
    for s in range(0, 2 * C, 2):
        for x in range(max(0, s - C + 1), min(s, C - 1) + 1):
            if alpha[x, s - x] > 128:
                best = (x, s - x)
                break
        if best:
            break
    return best[0] / C, best[1] / C


def finish(im):
    return im.resize((OUT, OUT), Image.LANCZOS)


def main():
    pack, out = sys.argv[1], sys.argv[2]
    shared = {key: place(render(pack, name), CENTRED_FILL) for key, name in SHARED.items()}
    hand_spot = tip(shared["hand"])
    lock = render(pack, "lock")

    for set_name, pointer in POINTERS.items():
        folder = os.path.join(out, set_name)
        os.makedirs(folder, exist_ok=True)

        idle = place(render(pack, pointer), ARROW_FILL, ARROW_ORIGIN)
        with_lock = idle.copy()
        badge = lock.resize((round(lock.width * 0.34 * C / max(lock.size)), round(lock.height * 0.34 * C / max(lock.size))), Image.LANCZOS)
        with_lock.alpha_composite(badge, (round(0.60 * C), round(0.58 * C)))

        cursors = dict(shared, idle=idle, set_lock=with_lock)
        for key, im in cursors.items():
            finish(im).save(os.path.join(folder, key + ".png"), optimize=True)

        spot = tip(idle)
        spots = {"idle": spot, "set_lock": spot, "set_pin": spot, "hand": hand_spot}
        for key in ("stretch_horizontal", "stretch_vertical", "stretch_corner", "stretch_corner_2"):
            spots[key] = (0.5, 0.5)
        with open(os.path.join(folder, "hotspots.txt"), "w", newline="\n") as f:
            f.write("# Where each cursor clicks, as a fraction of the image width and height from the top left.\n")
            for key, (x, y) in spots.items():
                f.write(f"{key} = {x:.4f}, {y:.4f}\n")

        shutil.copy(os.path.join(pack, "License.txt"), os.path.join(folder, "License.txt"))
        print("built", set_name)


if __name__ == "__main__":
    main()
