"""第 3 章敵人：從切好的分格產生 <id>_side.png 與各姿勢圖。畫布高 320，腳底在第 318 列、水平置中；
同一張圖稿的格子用同一比例（以站姿高 314 推算），保留畫稿原本的大小關係。
用法: desertposes.py parts資料夾 輸出資料夾 id 名稱=索引,... [--flip] [--scale 比例] [--ref 站姿高]"""
import sys, os, argparse
from PIL import Image
ap = argparse.ArgumentParser(); ap.add_argument('src'); ap.add_argument('out'); ap.add_argument('cid'); ap.add_argument('map')
ap.add_argument('--flip', action='store_true'); ap.add_argument('--scale', type=float); ap.add_argument('--fitname', default='side')
ap.add_argument('--fith', type=float, default=314)
a = ap.parse_args(); H, BASE = 320, 318
items = [kv.split('=') for kv in a.map.split(',')]
ims = {}
for n, i in items:
    p = Image.open(f'{a.src}/part_{int(i):02d}.png').convert('RGBA'); p = p.crop(p.getbbox())
    if a.flip: p = p.transpose(Image.FLIP_LEFT_RIGHT)
    ims[n] = p
s = a.scale or a.fith / ims[a.fitname].height
os.makedirs(a.out, exist_ok=True); res = []
for n, p in ims.items():
    p = p.resize((max(1, round(p.width * s)), max(1, round(p.height * s))), Image.LANCZOS)
    if p.height > BASE: p = p.resize((round(p.width * BASE / p.height), BASE), Image.LANCZOS)
    w = p.width + 8
    c = Image.new('RGBA', (w, H), (0, 0, 0, 0)); c.alpha_composite(p, (4, BASE - p.height))
    fn = f'{a.cid}_{n}.png'; c.save(f'{a.out}/{fn}'); res.append((n, c.size))
print(a.cid, 'scale', round(s, 3), res)
