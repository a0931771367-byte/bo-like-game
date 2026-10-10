"""Cut game sprites out of the Higgsfield reference sheets in art/.
Usage: python3 tools/extract_sprites.py
Writes transparent PNGs to public/sprites, public/icons, public/bg."""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
A = lambda *p: os.path.join(ROOT, 'art', *p)
O = lambda *p: os.path.join(ROOT, 'public', *p)

# sheet -> (output name, direction the SIDE view faces in the sheet: 1 right, -1 left)
SHEETS = {
    'characters/swordsman.png': ('swordsman', 1),
    'characters/mage.png': ('mage', -1),
    'characters/merchant.png': ('merchant', -1),
    'characters/acolyte.png': ('acolyte', -1),
    'characters/thief.png': ('thief', -1),
    'characters/archer.png': ('archer', -1),
    'enemies/enemy-jelly.png': ('jelly', -1),
    'enemies/enemy-bug.png': ('bug', -1),
    'enemies/enemy-wolf.png': ('wolf', -1),
    'enemies/boss-jelly-king.png': ('jellyKing', -1),
}
MAX_H = 320


def knockout_white(img: Image.Image) -> Image.Image:
    """Make the white background transparent via flood fill from the corners."""
    rgb = img.convert('RGB')
    w, h = rgb.size
    sentinel = (255, 0, 255)
    for seed in [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1), (w // 2, 0), (w // 2, h - 1)]:
        if rgb.getpixel(seed) != sentinel:
            ImageDraw.floodfill(rgb, seed, sentinel, thresh=48)
    a = np.array(rgb)
    bg = np.all(a == sentinel, axis=2)
    alpha = np.where(bg, 0, 255).astype(np.uint8)
    # soften the 2px rim: near-white pixels touching the background fade out
    rim = Image.fromarray(alpha).filter(ImageFilter.MinFilter(5))
    rim = np.array(rim) == 0
    src = np.array(img.convert('RGB')).astype(int)
    whiteness = src.min(axis=2)
    fade = np.clip((255 - whiteness) * 3, 0, 255).astype(np.uint8)
    alpha = np.where(rim & ~bg, np.minimum(alpha, fade), alpha)
    out = np.dstack([np.array(img.convert('RGB')), alpha])
    return Image.fromarray(out, 'RGBA')


def clusters(mask_1d, min_gap, min_size):
    """Split a 1-D boolean projection into [start, end) runs separated by gaps."""
    runs, start, gap = [], None, 0
    for i, v in enumerate(mask_1d):
        if v:
            if start is None:
                start = i
            gap = 0
        elif start is not None:
            gap += 1
            if gap >= min_gap:
                runs.append((start, i - gap + 1))
                start, gap = None, 0
    if start is not None:
        runs.append((start, len(mask_1d)))
    return [r for r in runs if r[1] - r[0] >= min_size]


def split_views(img: Image.Image):
    a = np.array(img.convert('RGB')).astype(int)
    fg = a.min(axis=2) < 235
    cols = clusters(fg.any(axis=0), min_gap=25, min_size=60)
    cols = sorted(sorted(cols, key=lambda r: r[1] - r[0], reverse=True)[:3])
    if len(cols) < 3:
        # views touch each other: cut at the sparsest columns near the thirds
        dens = fg.sum(axis=0)
        w = img.width
        xs = np.where(fg.any(axis=0))[0]
        left, right = xs[0], xs[-1] + 1
        cuts = []
        for frac in (1 / 3, 2 / 3):
            c = left + (right - left) * frac
            lo, hi = int(c - 140), int(c + 140)
            cuts.append(lo + int(np.argmin(dens[lo:hi])))
        cols = [(left, cuts[0]), (cuts[0], cuts[1]), (cuts[1], right)]
    views = []
    for x0, x1 in cols:
        rows = fg[:, x0:x1].any(axis=1)
        ys = np.where(rows)[0]
        views.append(img.crop((max(0, x0 - 4), max(0, ys[0] - 4), min(img.width, x1 + 4), min(img.height, ys[-1] + 4))))
    return views


def export_sheet(rel, name, side_dir):
    img = Image.open(A(rel)).convert('RGBA')
    views = split_views(img)
    if len(views) != 3:
        print(f'!! {rel}: found {len(views)} views, expected 3')
    labels = ['front', 'side', 'back'][:len(views)]
    for label, v in zip(labels, views):
        v = knockout_white(v)
        if label == 'side' and side_dir < 0:
            v = v.transpose(Image.FLIP_LEFT_RIGHT)
        if v.height > MAX_H:
            v = v.resize((round(v.width * MAX_H / v.height), MAX_H), Image.LANCZOS)
        v.save(O('sprites', f'{name}_{label}.png'))
        print(f'{name}_{label}.png {v.width}x{v.height}')


def export_icons():
    img = Image.open(A('ui', 'skills.png')).convert('RGB')
    a = np.array(img).astype(int)
    fg = a.max(axis=2) > 70
    cols = clusters(fg.any(axis=0), 20, 100)
    rows = clusters(fg.any(axis=1), 20, 100)
    names = [f'swordsman_{i}' for i in range(4)] + [f'mage_{i}' for i in range(4)]
    k = 0
    for y0, y1 in rows:
        for x0, x1 in cols:
            tile = img.crop((x0 + 2, y0 + 2, x1 - 2, y1 - 2)).resize((128, 128), Image.LANCZOS)
            tile.save(O('icons', f'skill_{names[k]}.png'))
            k += 1
    print(f'{k} skill icons ({len(cols)} cols x {len(rows)} rows)')


def export_backgrounds():
    bg = Image.open(A('backgrounds', 'bg-chapter1.png')).convert('RGB')
    bg = bg.resize((round(bg.width * 720 / bg.height), 720), Image.LANCZOS)
    bg.save(O('bg', 'chapter1.jpg'), quality=88)
    title = Image.open(A('backgrounds', 'title-keyart.png')).convert('RGB')
    title = title.resize((1280, round(title.height * 1280 / title.width)), Image.LANCZOS)
    title.save(O('bg', 'title.jpg'), quality=88)
    print('backgrounds', bg.size, title.size)


if __name__ == '__main__':
    for rel, (name, d) in SHEETS.items():
        export_sheet(rel, name, d)
    export_icons()
    export_backgrounds()
