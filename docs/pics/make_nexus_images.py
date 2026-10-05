"""Builds the 1.1 Nexus images in docs/pics/nexus from the raw screenshots in docs/pics (shot list:
nexus-pages/shots/DisplayUiTweaks/SHOTLIST.md). The 1.0 images that are still correct (hud_size, ultrawide_menu,
ultrawide_stats, header) come from make_nexus_images.ps1.

Needs Pillow and numpy (pip install pillow numpy). Usage: python make_nexus_images.py [--thumbs]
Raw screenshots (git-ignored) were taken on a 2878x2559 (9:8) monitor: full-monitor captures, or snips of the game band.
House rules (nexus-pages/house-rules.md): captions and a rounded box are the only highlighting; the main image is 16:9
with the mod name as the biggest text; no black bars.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "nexus")
FONT = "C:/Windows/Fonts/segoeuib.ttf"
GOLD = (216, 201, 163)
W, H = 1920, 1080

# The game frame of each raw shot (left, top, right, bottom). The monitor's FPS overlay sits in the bottom 50 px of
# full-monitor captures; the 9:8 frames stop above it.
SHOTS = {
    "uw_vanilla": ("Screenshot 2026-10-05 172946.png", (0, 98, 2878, 1058)),     # 3:1, game's default UI
    "uw_mod": ("Screenshot 2026-10-05 172929.png", (0, 98, 2878, 1058)),         # 3:1, UI Area 1.78
    "sq_vanilla": ("Screenshot 2026-10-05 173345.png", (0, 0, 2878, 2505)),      # 9:8 monitor, the game's 16:9 band
    "sq_mod": ("Screenshot 2026-10-05 173419.png", (0, 0, 2878, 2505)),          # 9:8, UI Area 1.00
    "editor_panel": ("Screenshot 2026-10-05 173512.png", (0, 470, 2878, 2090)),  # 16:9, instructions readable
    "editor": ("Screenshot 2026-10-05 173534.png", (0, 470, 2878, 2090)),        # 16:9, mid-drag on the chat bracket
    "layout_play": ("mpv-shot0001.jpg", (0, 470, 2880, 2090)),                   # 16:9 video frame, moved HUD in a fight
    "preview_area": ("Screenshot 2026-10-05 174630.png", (0, 470, 2878, 2090)),  # UI Area row, outline at 1.50
    "preview_hud": ("Screenshot 2026-10-05 174655.png", (0, 470, 2878, 2090)),   # HUD size row, HUD copy at 50%
    "settings": ("Screenshot 2026-10-05 175027.png", (0, 169, 2878, 1789)),      # 16:9, with the heading row
}

_cache = {}


def shot(key):
    if key not in _cache:
        name, crop = SHOTS[key]
        _cache[key] = Image.open(os.path.join(HERE, name)).convert("RGB").crop(crop)
    return _cache[key]


def font(size):
    return ImageFont.truetype(FONT, size)


def box(img, b, k=1.0):
    """The highlight: a rounded gold outline, 5 px wide, radius 14 at a 1080 px tall image (k = image height / 1080)."""
    w = max(3, round(5 * k))
    ImageDraw.Draw(img).rounded_rectangle(b, round(14 * k), outline=GOLD, width=w)


def label(img, text, xy, size, anchor="la"):
    """White text on a dark pill."""
    d = ImageDraw.Draw(img, "RGBA")
    f = font(size)
    l, t, r, b = d.textbbox(xy, text, font=f, anchor=anchor)
    p = size // 4
    d.rounded_rectangle((l - p, t - p, r + p, b + p), p, fill=(0, 0, 0, 190))
    d.text(xy, text, font=f, fill="white", anchor=anchor)


def outlined(img, text, xy, size, anchor="mm"):
    """The title: white with a dark outline and a soft shadow, straight on the scene."""
    f = font(size)
    sh = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).text((xy[0] + 6, xy[1] + 8), text, font=f, fill=(0, 0, 0, 200), anchor=anchor)
    img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(10)))
    ImageDraw.Draw(img).text(xy, text, font=f, fill="white", anchor=anchor, stroke_width=4, stroke_fill=(20, 20, 20))


def shadowed_paste(dst, src, xy):
    x, y = xy
    sh = Image.new("RGBA", dst.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).rectangle((x + 6, y + 10, x + src.width + 6, y + src.height + 10), fill=(0, 0, 0, 210))
    dst.alpha_composite(sh.filter(ImageFilter.GaussianBlur(14)))
    dst.paste(src, (x, y))


def backdrop(key, dim=0.75, blur=6):
    """A blurred, dimmed copy of a shot filling the 16:9 frame (cover crop), so composites have no black bars."""
    im = shot(key)
    s = max(W / im.width, H / im.height)
    im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
    x, y = (im.width - W) // 2, (im.height - H) // 2
    im = im.crop((x, y, x + W, y + H)).filter(ImageFilter.GaussianBlur(blur))
    return ImageEnhance.Brightness(im).enhance(dim).convert("RGBA")


def fit(key, w, h):
    im = shot(key)
    s = min(w / im.width, h / im.height)
    return im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.convert("RGB").save(os.path.join(OUT, name), quality=92)
    print(f"{name:24} {img.width}x{img.height} {os.path.getsize(os.path.join(OUT, name)) // 1024:5} KB")


def pair_side_by_side(keys, labels, top, bottom, gap=40, size=64, bg="sq_mod", title=None):
    img = backdrop(bg)
    if title:
        outlined(img, title, (W // 2, 112), 150)
    ph = bottom - top
    a, b = (fit(k, (W - 2 * 40 - gap) // 2, ph) for k in keys)
    x = (W - a.width - b.width - gap) // 2
    for im, lbl in ((a, labels[0]), (b, labels[1])):
        shadowed_paste(img, im.convert("RGBA"), (x, top))
        label(img, lbl, (x + 20, top + 20), size)
        x += im.width + gap
    return img


def main_image():
    """Main image = listing tile: the 9:8 monitor, the game's 16:9 band vs the mod filling the screen, the name on top."""
    img = pair_side_by_side(("sq_vanilla", "sq_mod"), ("Vanilla", "With mod"), 216, H - 36, title="Display & UI Tweaks")
    save(img, "1_main.jpg")
    return img


def ultrawide():
    """3:1, top-bottom: the game's default UI across the whole width vs UI Area 1.78 (a 16:9 UI in the middle)."""
    img = backdrop("uw_mod")
    m, gap = 36, 24
    ph = (H - 2 * m - gap) // 2
    pw = ph * 3
    x = (W - pw) // 2
    for i, (k, lbl) in enumerate((("uw_vanilla", "Game default"), ("uw_mod", "UI Area 1.78"))):
        y = m + i * (ph + gap)
        shadowed_paste(img, shot(k).resize((pw, ph), Image.LANCZOS).convert("RGBA"), (x, y))
        label(img, lbl, (x + pw - 20, y + ph - 20), 56, anchor="rd")
    save(img, "2_ultrawide.jpg")


def full_frame(key, out, caption, boxes=(), cap_xy=(40, H - 40), anchor="ld"):
    """A 16:9 frame at 1920x1080 with a caption and boxes (in raw frame pixels)."""
    im = shot(key)
    k = W / im.width
    img = im.resize((W, H), Image.LANCZOS).convert("RGBA")
    for b in boxes:
        box(img, tuple(round(v * k) for v in b))
    label(img, caption, cap_xy, 64, anchor=anchor)
    save(img, out)


def chat_resize():
    """A 1280x720 window of the editor shot around the chat window being resized (raw frame pixels), shown 1:1."""
    l, t = 1598, 900
    img = shot("editor").crop((l, t, l + 1280, t + 720)).convert("RGBA")
    box(img, (2245 - l, 1000 - t, 2868 - l, 1445 - t), 720 / 1080)
    label(img, "Chat: drag the corner to resize", (30, 30), 48)
    save(img, "4_chat_resize.jpg")


def settings():
    """The rows with the heading and the description panel, cropped (raw frame pixels)."""
    im = shot("settings").crop((60, 200, 2560, 1580))
    img = im.convert("RGBA")
    label(img, "Options > Display", (img.width - 40, img.height - 40), 72, anchor="rd")
    save(img, "8_settings.jpg")


def thumbs(img):
    """The listing-tile test (style guide section 7): 300 and 170 px renders next to the outputs."""
    for w, h in ((300, 169), (170, 96)):
        img.convert("RGB").resize((w, h), Image.LANCZOS).save(os.path.join(OUT, f"_tile_{w}.png"))
    g = np.asarray(img.convert("L"), dtype=np.float32) / 255
    print(f"tile brightness {g.mean():.3f} (reference tiles 0.24), contrast {g.std():.3f}")


if __name__ == "__main__":
    tile = main_image()
    ultrawide()
    full_frame("editor_panel", "3_editor.jpg", "Edit HUD Layout", cap_xy=(W - 40, 560), anchor="rm")
    chat_resize()
    # Health moved to the bottom centre, equipment to the bottom right.
    full_frame("layout_play", "5_layout_play.jpg", "Custom HUD layout", cap_xy=(40, 40), anchor="la",
               boxes=[(1070, 1345, 1715, 1600), (2200, 1295, 2862, 1585)])
    full_frame("preview_area", "6_preview_area.jpg", "Live preview: UI Area 1.50", cap_xy=(W - 40, H - 40), anchor="rd")
    full_frame("preview_hud", "7_preview_hud.jpg", "Live preview: HUD size 50%", cap_xy=(W - 40, H - 40), anchor="rd")
    settings()
    if "--thumbs" in sys.argv:
        thumbs(tile)
