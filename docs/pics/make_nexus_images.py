"""Builds every Nexus / README image in docs/pics/nexus from the raw screenshots in docs/pics (shot list:
nexus-pages/shots/DisplayUiTweaks/SHOTLIST.md). File names are numbered in gallery order. header.jpg (README / Nexus page header, 1300x372)
is the 1.0 one, built by make_nexus_images.ps1 (removed; in git history at 69fbe0a).

Needs Pillow, numpy and OpenCV (pip install pillow numpy opencv-python-headless). Usage: python make_nexus_images.py
[--thumbs]. Raw screenshots (git-ignored) were taken on a 2878x2559 (9:8) monitor: full-monitor captures, or snips of
the game band. hide_hud.gif (made by hand) is copied in as 03_hide_hud.gif (git-ignored, 8 MB).
House rules (nexus-pages/house-rules.md): captions and a rounded box are the only highlighting, full frames may dim the
rest to 60%; the main image is 16:9 with the mod name as the biggest text; no black bars.
"""
import os
import shutil
import sys

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "nexus")
FONT = "C:/Windows/Fonts/segoeuib.ttf"
FONT_LIGHT = "C:/Windows/Fonts/segoeui.ttf"
GOLD = (216, 201, 163)
W, H = 1920, 1080

# The game frame of each raw shot (left, top, right, bottom). The monitor's FPS overlay sits in the bottom 50 px of
# full-monitor captures; the 9:8 frames stop above it.
SHOTS = {
    "showcase": ("Screenshot 2026-10-05 222141.png", (0, 451, 2878, 1411)),     # 3:1, UI Area 1.78, showcase mode (F9)
    "uw_vanilla": ("Screenshot 2026-10-05 172946.png", (0, 98, 2878, 1058)),     # 3:1, game's default UI
    "uw_mod": ("Screenshot 2026-10-05 172929.png", (0, 98, 2878, 1058)),         # 3:1, UI Area 1.78
    "sq_vanilla": ("Screenshot 2026-10-05 173345.png", (0, 0, 2878, 2505)),      # 9:8 monitor, the game's 16:9 band
    "sq_mod": ("Screenshot 2026-10-05 173419.png", (0, 0, 2878, 2505)),          # 9:8, HUD in the screen corners (UI Area at or above 1.125)
    "editor_panel": ("Screenshot 2026-10-05 173512.png", (0, 470, 2878, 2090)),  # 16:9, instructions readable
    "editor": ("Screenshot 2026-10-05 173534.png", (0, 470, 2878, 2090)),        # 16:9, mid-drag on the chat bracket
    "layout_play": ("mpv-shot0001.jpg", (0, 470, 2880, 2090)),                   # 16:9 video frame, moved HUD in a fight
    "preview_area": ("Screenshot 2026-10-05 174630.png", (0, 470, 2878, 2090)),  # UI Area row, outline at 1.50
    "preview_hud": ("Screenshot 2026-10-05 174655.png", (0, 470, 2878, 2090)),   # HUD size row, HUD copy at 50%
    "settings": ("Screenshot 2026-10-05 175027.png", (0, 169, 2878, 1789)),      # 16:9, with the heading row
    # 1.0 shots (2026-09-27), still correct
    "hud_75": ("ui_75_pct.png", (0, 71, 2878, 1691)),
    "hud_100": ("ui_100_pct.png", (0, 101, 2878, 1721)),
    "hud_125": ("ui_125_pct.png", (0, 88, 2878, 1708)),
    "uw_menu": ("ultrawide_with_mod_menu.png", (0, 89, 2878, 1049)),             # 3:1, vendor, UI Aspect 16:9
    "uw_stats": ("ultrawide_with_mod_stats.png", (0, 108, 2878, 1068)),          # 3:1, stats
    "uw_header": ("3_by_1_with_mod.png", (0, 107, 2878, 1067)),
}

# Raw-pixel boxes to paint out before cropping (the mouse cursor in the video frame).
RETOUCH = {"layout_play": [(855, 1620, 900, 1678)]}

_cache = {}


def retouch(im, boxes):
    """Inpaint the bright cursor pixels inside each box from their surroundings."""
    a = np.asarray(im).copy()
    mask = np.zeros(a.shape[:2], np.uint8)
    for l, t, r, b in boxes:
        g = a[t:b, l:r].astype(float).mean(axis=2)
        m = (g > 110) | (g < 12)  # the white arrow and its black outline
        mask[t:b, l:r] = m.astype(np.uint8) * 255
    mask = cv2.dilate(mask, np.ones((5, 5), np.uint8))
    out = cv2.inpaint(a[:, :, ::-1], mask, 7, cv2.INPAINT_TELEA)[:, :, ::-1]
    return Image.fromarray(out)


def shot(key):
    if key not in _cache:
        name, crop = SHOTS[key]
        im = Image.open(os.path.join(HERE, name)).convert("RGB")
        if key in RETOUCH:
            im = retouch(im, RETOUCH[key])
        _cache[key] = im.crop(crop)
    return _cache[key]


def font(size, light=False):
    return ImageFont.truetype(FONT_LIGHT if light else FONT, size)


def box(img, b, k=1.0, dashed=False):
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


def sized(key, w, h, bright=1.0):
    im = shot(key).resize((w, h), Image.LANCZOS)
    return ImageEnhance.Brightness(im).enhance(bright) if bright != 1.0 else im


def fit(key, w, h):
    im = shot(key)
    s = min(w / im.width, h / im.height)
    return sized(key, round(im.width * s), round(im.height * s))


def ui_area(img, rect, k=1.0, dim=0.6, text="UI area"):
    """The UI area of a panel: everything outside it dimmed, a gold outline, a small label at its top edge."""
    rect = tuple(round(v) for v in rect)
    dark = ImageEnhance.Brightness(img).enhance(dim)
    mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(mask).rectangle(rect, fill=255)
    out = Image.composite(img, dark, mask).convert("RGBA")
    box(out, rect, k)
    if text:
        f = font(max(18, round(30 * k)))
        d = ImageDraw.Draw(out, "RGBA")
        xy = ((rect[0] + rect[2]) // 2, rect[3] - round(8 * k))
        l, t, r, b = d.textbbox(xy, text, font=f, anchor="md")
        p = round(8 * k)
        d.rounded_rectangle((l - p, t - p, r + p, b + p), p, fill=(0, 0, 0, 170))
        d.text(xy, text, font=f, fill=GOLD, anchor="md")
    return out


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.convert("RGB").save(os.path.join(OUT, name), quality=92)
    print(f"{name:26} {img.width}x{img.height} {os.path.getsize(os.path.join(OUT, name)) // 1024:5} KB")


# ---- 01 main image / tile ------------------------------------------------------------------------------------------

# Main image: what each numbered caption points at, in raw frame pixels of the showcase shot.
UI_AREA = (585, 4, 2293, 956)  # UI Area 1.78 on 3:1
FEATURES = [
    ("HUD where you look", None),                     # 1 = the UI area outline itself
    ("Pickups & bounties come along", [(640, 425, 1015, 615), (2060, 270, 2285, 318)]),
    ("Bigger chat", [(1812, 548, 2296, 950)]),
    ("Move HUD elements", [(1110, 806, 1665, 940)]),
]


def badge(img, n, xy, r=30):
    """A numbered gold disc."""
    d = ImageDraw.Draw(img)
    x, y = xy
    d.ellipse((x - r, y - r, x + r, y + r), fill=GOLD, outline=(20, 20, 20), width=3)
    d.text((x, y + 1), str(n), font=font(round(r * 1.25)), fill=(15, 15, 15), anchor="mm")


def main_image():
    """Main image = listing tile: one ultrawide scene with the mod on (showcase mode: chat lines, pickups and the
    challenge held on screen), the UI area outlined, four numbered features boxed and captioned below. Title on top."""
    img = backdrop("showcase", dim=0.65, blur=10)
    outlined(img, "Display & UI Tweaks", (W // 2, 92), 132)
    pw = W - 2 * 40
    k = pw / 2878
    ph = round(960 * k)
    x0, y0 = 40, 178
    panel = ImageEnhance.Brightness(shot("showcase").resize((pw, ph), Image.LANCZOS)).enhance(1.22)
    l, t, r, b = (round(v * k) for v in UI_AREA)
    panel = ui_area(panel, (l, t, r, b), ph / 540, dim=0.6, text=None)
    for i, (_, boxes) in enumerate(FEATURES, 1):
        for bx in boxes or []:
            bb = tuple(round(v * k) for v in bx)
            box(panel, bb)
            badge(panel, i, (bb[0] - 30, (bb[1] + bb[3]) // 2))  # on the box's left edge, clear of the text
    badge(panel, 1, ((l + r) // 2, t + 34))
    shadowed_paste(img, panel, (x0, y0))
    cy = y0 + ph + 70
    for i, (text, _) in enumerate(FEATURES, 1):
        cx = 120 + ((i - 1) % 2) * 900
        y = cy + ((i - 1) // 2) * 100
        badge(img, i, (cx, y), 34)
        label(img, text, (cx + 56, y), 52, anchor="lm")
    save(img, "01_main.jpg")
    return img


# ---- gallery -------------------------------------------------------------------------------------------------------

def square():
    """The 9:8 monitor, side by side: the game's 16:9 band vs the mod filling the screen."""
    img = backdrop("sq_mod")
    top, bottom, gap = 130, H - 36, 40
    a, b = fit("sq_vanilla", (W - 80 - gap) // 2, bottom - top), fit("sq_mod", (W - 80 - gap) // 2, bottom - top)
    x = (W - a.width - b.width - gap) // 2
    for im, lbl in ((a, "Vanilla"), (b, "With mod")):
        shadowed_paste(img, im.convert("RGBA"), (x, top))
        label(img, lbl, (x + 20, top + 20), 56)
        x += im.width + gap
    label(img, "Square and tall monitors", (W // 2, 64), 64, anchor="mm")
    save(img, "02_square.jpg")


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
    save(img, "05_chat_resize.jpg")


def three_to_one(pairs, out, title=None, bright=1.0):
    """3:1 panels stacked on a blurred backdrop; pairs = [(shot, label, ui-area width in parts of the panel or None)]."""
    img = backdrop(pairs[-1][0])
    m, gap = 36, 24
    top = 120 if title else m
    ph = (H - top - m - gap * (len(pairs) - 1)) // len(pairs)
    pw = ph * 3
    x = (W - pw) // 2
    for i, (k, lbl, area) in enumerate(pairs):
        y = top + i * (ph + gap)
        p = sized(k, pw, ph, bright)
        if area:
            aw = pw * area
            p = ui_area(p, ((pw - aw) / 2, 4, (pw + aw) / 2 - 1, ph - 5), ph / 540)
        shadowed_paste(img, p.convert("RGBA"), (x, y))
        label(img, lbl, (x + 20, y + 20), 48)
    if title:
        label(img, title, (W // 2, 62), 60, anchor="mm")
    save(img, out)


def hud_size():
    """HUD 75 / 100 / 125%: the left third of each 16:9 frame (the health and equipment corner), side by side."""
    img = Image.new("RGBA", (W, H))
    cw = W // 3
    for i, (k, lbl) in enumerate((("hud_75", "HUD 75%"), ("hud_100", "HUD 100%"), ("hud_125", "HUD 125%"))):
        im = shot(k)
        crop = im.crop((0, 0, round(im.height * cw / H), im.height)).resize((cw, H), Image.LANCZOS)
        img.paste(ImageEnhance.Brightness(crop).enhance(1.25), (i * cw, 0))
        label(img, lbl, (i * cw + cw // 2, H // 2), 64, anchor="mm")
    d = ImageDraw.Draw(img)
    for i in (1, 2):
        d.line((i * cw, 0, i * cw, H), fill=(0, 0, 0), width=4)
    save(img, "08_hud_size.jpg")


def settings():
    """The rows with the heading and the description panel, cropped (raw frame pixels)."""
    im = shot("settings").crop((60, 200, 2560, 1580))
    img = im.convert("RGBA")
    label(img, "Options > Display", (img.width - 40, img.height - 40), 72, anchor="rd")
    save(img, "12_settings.jpg")


def thumbs(img):
    """The listing-tile test (style guide section 7): 300 and 170 px renders next to the outputs."""
    for w, h in ((300, 169), (170, 96)):
        img.convert("RGB").resize((w, h), Image.LANCZOS).save(os.path.join(OUT, f"_tile_{w}.png"))
    g = np.asarray(img.convert("L"), dtype=np.float32) / 255
    print(f"tile brightness {g.mean():.3f} (reference tiles 0.24), contrast {g.std():.3f}")


if __name__ == "__main__":
    tile = main_image()
    square()
    if os.path.exists(os.path.join(HERE, "hide_hud.gif")):
        shutil.copyfile(os.path.join(HERE, "hide_hud.gif"), os.path.join(OUT, "03_hide_hud.gif"))
    full_frame("editor_panel", "04_editor.jpg", "Edit HUD Layout", cap_xy=(W - 40, 560), anchor="rm")
    chat_resize()
    # Health moved to the bottom centre, equipment to the bottom right.
    full_frame("layout_play", "06_move_hud.jpg", "Move HUD elements", cap_xy=(40, 40), anchor="la",
               boxes=[(1070, 1345, 1715, 1600), (2200, 1295, 2862, 1585)])
    three_to_one([("uw_vanilla", "Game default", 1.0), ("uw_mod", "With mod: UI Area 1.78", 16 / 9 / 3)],
                 "07_ultrawide.jpg")
    hud_size()
    three_to_one([("uw_menu", "Vendor", 16 / 9 / 3), ("uw_stats", "Stats", 16 / 9 / 3)], "09_menus.jpg",
                 title="Menus stay in the UI area", bright=1.5)
    full_frame("preview_area", "10_ui_area.jpg", "Change UI Area", cap_xy=(W - 40, H - 40), anchor="rd")
    full_frame("preview_hud", "11_hud_size_setting.jpg", "Change HUD size", cap_xy=(W - 40, H - 40), anchor="rd")
    settings()
    if "--thumbs" in sys.argv:
        thumbs(tile)
