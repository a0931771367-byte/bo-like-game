#!/usr/bin/env python3
"""Checks that every Unity animation frame faces right (the game mirrors the whole sprite for left).

Each frame's facing is reviewed by eye once and recorded with its hash in tools/sprite_facing.json.
The check fails when a frame is missing from that list, has changed since it was reviewed, or was
reviewed as not facing right. A head-orientation heuristic also flags frames that look turned
around compared with the class's idle_0, to point the reviewer at the likely culprits.

  python3 tools/check_sprite_facing.py                  # check (CI)
  python3 tools/check_sprite_facing.py --sheet out.png  # contact sheet of every class for review
  python3 tools/check_sprite_facing.py --approve mage_attack_1 ...   # record frames reviewed as facing right
  python3 tools/check_sprite_facing.py --approve-all    # same, for every frame (only after looking at the sheet)
"""
import argparse
import hashlib
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ANIM_DIR = os.path.join(ROOT, 'unity', 'Assets', 'Resources', 'Sprites', 'anim')
ANIMS_JSON = os.path.join(ROOT, 'unity', 'Assets', 'Resources', 'Data', 'anims.json')
MANIFEST = os.path.join(ROOT, 'tools', 'sprite_facing.json')
LABELS = ('idle', 'walk', 'attack')
# Below this the frame's head matches the mirrored idle_0 clearly better than idle_0 itself.
SUSPECT_SCORE = -0.2


def frames():
    """(name, path) for every frame anims.json declares, in class / label / index order."""
    with open(ANIMS_JSON, encoding='utf-8') as f:
        anims = json.load(f)
    for a in anims:
        for label in LABELS:
            for i in range(a[label]):
                name = f"{a['classId']}_{label}_{i}"
                yield name, os.path.join(ANIM_DIR, name + '.png')


def sha256(path):
    with open(path, 'rb') as f:
        return hashlib.sha256(f.read()).hexdigest()


def head(path):
    """Greyscale crop around the head (top 40% of the figure), 48x48."""
    im = Image.open(path).convert('RGBA')
    alpha = np.array(im)[:, :, 3] > 40
    ys = np.where(alpha.any(axis=1))[0]
    y0, h = ys[0], ys[-1] - ys[0]
    top = alpha[y0:y0 + max(1, int(h * 0.25))]
    cx = int(np.median(np.where(top)[1]))
    w = int(h * 0.32)
    v = np.array(im.crop((cx - w, y0, cx + w, y0 + int(h * 0.4))).resize((48, 48))).astype(float)
    return v[:, :, :3].mean(axis=2) * (v[:, :, 3] / 255)


def corr(a, b):
    a, b = a - a.mean(), b - b.mean()
    d = np.sqrt((a * a).sum() * (b * b).sum())
    return float((a * b).sum() / d) if d else 0.0


def facing_score(path, ref):
    """> 0: turned the same way as ref; < 0: turned the other way."""
    h = head(path)
    return corr(h, ref) - corr(h, ref[:, ::-1])


def load_manifest():
    if not os.path.exists(MANIFEST):
        return {}
    with open(MANIFEST, encoding='utf-8') as f:
        return json.load(f)


def save_manifest(m):
    with open(MANIFEST, 'w', encoding='utf-8') as f:
        json.dump(dict(sorted(m.items())), f, ensure_ascii=False, indent=1)
        f.write('\n')


def check():
    manifest = load_manifest()
    errors, warnings = [], []
    refs = {}
    for name, path in frames():
        if not os.path.exists(path):
            errors.append(f'{name}: 檔案不存在')
            continue
        cls = name.split('_')[0]
        if cls not in refs:
            refs[cls] = head(os.path.join(ANIM_DIR, f'{cls}_idle_0.png'))
        score = facing_score(path, refs[cls])
        entry = manifest.get(name)
        if entry is None:
            errors.append(f'{name}: 新的格，還沒人工確認朝向（--sheet 產生對照表，確認朝右後 --approve {name}）')
        elif entry['sha256'] != sha256(path):
            errors.append(f'{name}: 圖檔在確認朝向後被改過，請重新確認（--approve {name}）')
        elif entry['facing'] != 'right':
            errors.append(f'{name}: 朝向為 {entry["facing"]}，所有角色格都必須朝右')
        if score < SUSPECT_SCORE:
            warnings.append(f'{name}: 頭部方向看起來和 {cls}_idle_0 相反（分數 {score:+.2f}）')
    for w in warnings:
        print('警告', w)
    for e in errors:
        print('錯誤', e)
    if errors:
        print(f'\n朝向檢查失敗：{len(errors)} 格')
        return 1
    print(f'朝向檢查通過：{len(manifest)} 格皆已確認朝右')
    return 0


def sheet(out):
    """One row per class: every frame, labelled, with its heuristic score."""
    rows, refs = {}, {}
    for name, path in frames():
        if not os.path.exists(path):
            continue
        cls = name.split('_')[0]
        refs.setdefault(cls, head(os.path.join(ANIM_DIR, f'{cls}_idle_0.png')))
        im = Image.open(path).convert('RGBA')
        im = im.crop(im.getbbox())
        im.thumbnail((150, 150))
        rows.setdefault(cls, []).append((name[len(cls) + 1:], facing_score(path, refs[cls]), im))
    cell = 155
    width = max(len(r) for r in rows.values()) * cell
    img = Image.new('RGBA', (width, len(rows) * (cell + 16)), (210, 210, 210, 255))
    d = ImageDraw.Draw(img)
    for y, (cls, row) in enumerate(rows.items()):
        y0 = y * (cell + 16)
        for x, (label, score, im) in enumerate(row):
            img.alpha_composite(im, (x * cell, y0 + 16))
            color = (200, 0, 0, 255) if score < SUSPECT_SCORE else (0, 0, 0, 255)
            d.text((x * cell + 2, y0 + 2), f'{cls} {label} {score:+.2f}', fill=color)
    img.convert('RGB').save(out)
    print(f'對照表：{out}')


def approve(names):
    manifest = load_manifest()
    paths = dict(frames())
    for n in names:
        if n not in paths or not os.path.exists(paths[n]):
            sys.exit(f'找不到 {n}')
        manifest[n] = {'facing': 'right', 'sha256': sha256(paths[n])}
        print(f'已記錄 {n} 朝右')
    save_manifest(manifest)


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--sheet')
    ap.add_argument('--approve', nargs='+')
    ap.add_argument('--approve-all', action='store_true')
    args = ap.parse_args()
    if args.sheet:
        sheet(args.sheet)
    elif args.approve:
        approve(args.approve)
    elif args.approve_all:
        approve([n for n, _ in frames()])
    else:
        sys.exit(check())
