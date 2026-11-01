"""把白底多角色圖依垂直空白欄切成單隻去背 PNG。用法: cutout.py 來源.png 輸出資料夾 名稱1 名稱2 ..."""
import sys, os
import numpy as np
from PIL import Image
from scipy import ndimage as nd

src, out, names = sys.argv[1], sys.argv[2], sys.argv[3:]
im = Image.open(src).convert('RGB'); a = np.asarray(im).astype(int)
white = a.min(2) > 232
lab, n = nd.label(white)
h, w = white.shape
border = set(np.unique(np.r_[lab[0], lab[-1], lab[:, 0], lab[:, -1]])) - {0}
sizes = nd.sum(white, lab, range(n + 1))
bg = np.isin(lab, [i for i in range(1, n + 1) if i in border or sizes[i] > 6000])
fg = nd.binary_opening(~bg, iterations=1)
fg = nd.binary_closing(fg, iterations=2)
lab2, n2 = nd.label(fg)
s2 = nd.sum(fg, lab2, range(n2 + 1))
fg = np.isin(lab2, [i for i in range(1, n2 + 1) if s2[i] > 150])
# 依欄位空白分群
cols = fg.sum(0) > 0
runs, x = [], 0
while x < w:
    if cols[x]:
        s = x
        while x < w and cols[x]: x += 1
        runs.append([s, x])
    x += 1
merged = []
for r in runs:
    if merged and r[0] - merged[-1][1] < 25: merged[-1][1] = r[1]
    else: merged.append(r)
merged = [r for r in merged if r[1] - r[0] > 80]
assert len(merged) == len(names), (merged, names)
alpha = nd.binary_erosion(fg, iterations=1)  # 吃掉白邊
rgba = np.dstack([a, (alpha * 255)]).astype(np.uint8)
os.makedirs(out, exist_ok=True)
for (x0, x1), name in zip(merged, names):
    m = alpha[:, x0:x1]; ys = np.where(m.any(1))[0]
    crop = rgba[ys[0]:ys[-1] + 1, x0:x1].copy()
    pad = 12
    canvas = np.zeros((crop.shape[0] + 2 * pad, crop.shape[1] + 2 * pad, 4), np.uint8)
    canvas[pad:-pad, pad:-pad] = crop
    Image.fromarray(canvas).save(f'{out}/{name}.png')
    print(name, canvas.shape[1], canvas.shape[0])
