"""從 AI 跑步分格自動挑 8 格並對齊成遊戲格（舊版職業用）。
挑法：前 10 格（排除第三排攻擊格），兩排各自縮放到同一身高；依腳部張開寬度分成大步／收腿兩組，
排成 大步→收腿→收腿→大步(騰空) 兩輪。畫布尺寸、腳底線、身高對齊 Unity 現有 {cls}_walk_0.png（各職業畫布不同）。
用法: runauto.py 切好的資料夾 現有walk_0.png 輸出資料夾 職業id [手動順序]"""
import sys, os, glob
import numpy as np
from PIL import Image
src, ref, out, cid = sys.argv[1:5]
LIFT = 7
r = Image.open(ref); W, H = r.size; rb = r.getbbox(); ref_h = rb[3] - rb[1]; cx = (rb[0] + rb[2]) / 2; BASE = rb[3]  # 畫布與腳底線沿用舊格
items = []
for f in sorted(glob.glob(f'{src}/part_*.png'))[:10]:
    im = Image.open(f).convert('RGBA'); im = im.crop(im.getbbox())
    a = np.asarray(im)[..., 3] > 128; h = a.shape[0]
    feet = a[int(h * 0.8):]; xs = np.where(feet.any(0))[0]
    items.append({'im': im, 'h': h, 'spread': xs[-1] - xs[0]})
# 兩排身高不同時各自歸一：以高度分群
hs = np.array([i['h'] for i in items]); hi = hs > np.median(hs) + 4
for grp in (hi, ~hi):
    if grp.any():
        m = np.median(hs[grp])
        for i, it in enumerate(items):
            if grp[i]: it['norm'] = it['h'] / m
target = ref_h * 0.97
for it in items:
    s = target / (it['h'] / it['norm'])
    it['im'] = it['im'].resize((round(it['im'].width * s), round(it['im'].height * s)), Image.LANCZOS)
by = sorted(range(len(items)), key=lambda i: items[i]['spread'])
narrow, wide = by[:4], by[-4:][::-1]
seq = [(wide[0], 0), (narrow[0], 0), (narrow[1], 0), (wide[1], 1), (wide[2], 0), (narrow[2], 0), (narrow[3], 0), (wide[3], 1)]
if len(sys.argv) > 5:  # 手動指定順序，例如 0,2,4a,5,7,9a（a＝騰空格）
    seq = [(int(t.rstrip('a')), t.endswith('a')) for t in sys.argv[5].split(',')]
os.makedirs(out, exist_ok=True); frames = []
for k, (i, air) in enumerate(seq):
    im = items[i]['im']; a = np.asarray(im)[..., 3] > 128
    hx = np.where(a[:int(a.shape[0] * 0.35)].any(0))[0]; hc = (hx[0] + hx[-1]) / 2
    c = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    c.alpha_composite(im, (round(cx + 6 - hc), BASE - im.height - LIFT * air))
    c.save(f'{out}/{cid}_walk_{k}.png'); frames.append(c)
bg = [Image.alpha_composite(Image.new('RGBA', (W, H), (120, 190, 235, 255)), f).convert('P', palette=Image.ADAPTIVE) for f in frames]
bg[0].save(f'{out}/{cid}_run_preview.gif', save_all=True, append_images=bg[1:], duration=80, loop=0, disposal=2)
print(cid, 'seq', [s[0] for s in seq], 'ref_h', ref_h)
