"""魔物受擊／空中／倒地／壓扁 4 姿勢：對齊到與 <id>_side.png 同高（320）的畫布，腳底同基準線、水平置中。
比例以「受擊格高度 ≈ 站姿高度 × 0.95」推算，4 格共用同一比例。姿勢比畫布寬時左右對稱加寬畫布。
用法: enemyposes.py 切好的資料夾 side.png 輸出資料夾 id hurt,knock,down,flat(索引)"""
import sys, os
import numpy as np
from PIL import Image
src, side_p, out, cid = sys.argv[1:5]; idx = [int(x) for x in sys.argv[5].split(',')]
side = Image.open(side_p); W, H = side.size; sb = side.getbbox(); base = sb[3]; cx = (sb[0] + sb[2]) / 2
poses = [Image.open(f'{src}/part_{i:02d}.png').convert('RGBA') for i in idx]
poses = [p.crop(p.getbbox()) for p in poses]
s = (sb[3] - sb[1]) * 0.95 / poses[0].height
os.makedirs(out, exist_ok=True); names = ['hurt', 'knock', 'down', 'flat']; sizes = []
hw = poses[0].width * s
for n, p in zip(names, poses):
    k = s
    if n != 'hurt' and p.width * s < hw * 0.9: k = hw * 0.9 / p.width  # 圖稿裡倒地格畫太小時補回來
    p = p.resize((max(1, round(p.width * k)), max(1, round(p.height * k))), Image.LANCZOS)
    w = W; x = round(cx - p.width / 2); pad = 0
    if x < 0 or x + p.width > W:  # 太寬：對稱加寬
        pad = max(-x, x + p.width - W); w = W + 2 * pad; x += pad
    c = Image.new('RGBA', (w, H), (0, 0, 0, 0)); c.alpha_composite(p, (x, min(base - p.height, H - p.height)))
    c.save(f'{out}/{cid}_{n}.png'); sizes.append((n, w, H))
print(cid, sizes)
