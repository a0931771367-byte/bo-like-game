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
    'enemies/bee.png': ('bee', 1),
    'enemies/flower.png': ('flower', 1),
    'enemies/mushroom.png': ('mushroom', 1),
    'enemies/treant.png': ('treant', 1),
}
# icon sheets -> grid of skill icons, row-major; names are <class>_<index>
ICON_SHEETS = {
    'ui/skills.png': [f'swordsman_{i}' for i in range(4)] + [f'mage_{i}' for i in range(4)],
    'ui/skills2.png': [f'{c}_{i}' for c in ('merchant', 'acolyte', 'thief', 'archer') for i in range(4)],
}
# animation sheets -> rows of frames (row label, expected frame count); frames face right
ANIM_SHEETS = {
    f'anim/{c}-anim.png': (c, [('idle', 4), ('walk', 6), ('attack', 4)])
    for c in ('swordsman', 'mage', 'merchant', 'acolyte', 'thief', 'archer')
}
# background paintings -> (output, where the floor strip starts in the 720px-high export)
BACKGROUNDS = {
    'backgrounds/bg-chapter1.png': 'chapter1',
    'backgrounds/bg-chapter2.png': 'chapter2',
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


def export_icons(rel, names):
    img = Image.open(A(rel)).convert('RGB')
    a = np.array(img).astype(int)
    fg = a.max(axis=2) > 70
    cols = clusters(fg.any(axis=0), 6, 100)
    rows = clusters(fg.any(axis=1), 6, 100)
    k = 0
    for y0, y1 in rows:
        for x0, x1 in cols:
            tile = img.crop((x0 + 2, y0 + 2, x1 - 2, y1 - 2)).resize((128, 128), Image.LANCZOS)
            tile.save(O('icons', f'skill_{names[k]}.png'))
            k += 1
    print(f'{k} skill icons ({len(cols)} cols x {len(rows)} rows)')


def export_anim(rel, name, rows_spec):
    """Cut an animation sheet into fixed-size frames: feet on the bottom edge, feet-centre at the middle."""
    img = Image.open(A(rel)).convert('RGBA')
    a = np.array(img.convert('RGB')).astype(int)
    fg = a.min(axis=2) < 235
    rows = clusters(fg.any(axis=1), min_gap=2, min_size=80)
    if len(rows) != len(rows_spec):
        print(f'!! {rel}: found {len(rows)} rows, expected {len(rows_spec)}')
    frames = []  # (label, index, RGBA crop, feet_cx)
    for (label, n), (y0, y1) in zip(rows_spec, rows):
        band = fg[y0:y1]
        cols = clusters(band.any(axis=0), min_gap=14, min_size=40)
        # drop stray bits drawn beside a frame (a loose arrow, a spark): they are far shorter than the character
        cols = [c for c in cols if np.ptp(np.where(band[:, c[0]:c[1]].any(axis=1))[0]) > (y1 - y0) * 0.5]
        if len(cols) != n:
            print(f'!! {rel} row {label}: found {len(cols)} frames, expected {n}')
        for i, (x0, x1) in enumerate(cols):
            ys = np.where(band[:, x0:x1].any(axis=1))[0]
            crop = img.crop((x0 - 3, y0 + ys[0] - 3, x1 + 3, y0 + ys[-1] + 4))
            crop = knockout_white(crop)
            al = np.array(crop)[:, :, 3] > 0
            feet = al[int(al.shape[0] * 0.88):]
            xs = np.where(feet.any(axis=0))[0]
            cx = (xs[0] + xs[-1]) / 2 if len(xs) else crop.width / 2
            frames.append((label, i, crop, cx))
    # one canvas size for every frame so the sprite never jitters when frames swap
    fh = max(f[2].height for f in frames)
    half = max(max(f[3], f[2].width - f[3]) for f in frames)
    fw = int(half * 2 + 4)
    os.makedirs(O('sprites', 'anim'), exist_ok=True)
    for label, i, crop, cx in frames:
        canvas = Image.new('RGBA', (fw, fh), (0, 0, 0, 0))
        canvas.paste(crop, (int(fw / 2 - cx), fh - crop.height), crop)
        if fh > MAX_H:
            canvas = canvas.resize((round(fw * MAX_H / fh), MAX_H), Image.LANCZOS)
        canvas.save(O('sprites', 'anim', f'{name}_{label}_{i}.png'))
    print(f'{name}: {len(frames)} frames, canvas {fw}x{fh}')


def export_backgrounds():
    for rel, out in BACKGROUNDS.items():
        bg = Image.open(A(rel)).convert('RGB')
        bg = bg.resize((round(bg.width * 720 / bg.height), 720), Image.LANCZOS)
        # painting + its mirror image, so the tile repeats without a visible seam
        pair = Image.new('RGB', (bg.width * 2, bg.height))
        pair.paste(bg, (0, 0))
        pair.paste(bg.transpose(Image.FLIP_LEFT_RIGHT), (bg.width, 0))
        pair.save(O('bg', f'{out}.jpg'), quality=86)
        print('background', out, bg.size)
    title = Image.open(A('backgrounds', 'title-keyart.png')).convert('RGB')
    title = title.resize((1280, round(title.height * 1280 / title.width)), Image.LANCZOS)
    title.save(O('bg', 'title.jpg'), quality=88)
    print('title', title.size)


if __name__ == '__main__':
    for rel, (name, d) in SHEETS.items():
        export_sheet(rel, name, d)
    for rel, names in ICON_SHEETS.items():
        export_icons(rel, names)
    for rel, (name, spec) in ANIM_SHEETS.items():
        export_anim(rel, name, spec)
    export_backgrounds()
