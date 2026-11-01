"""把 AI 生成的跑步分格對齊成遊戲用格：同一畫布（485×244）、腳底貼底邊、頭部水平置中、騰空格抬高。
用法: runframes.py 切好的資料夾 輸出資料夾 職業id 順序(逗號) 騰空格(逗號) [縮放]"""
import sys, os
import numpy as np
from PIL import Image
src, out, cid = sys.argv[1], sys.argv[2], sys.argv[3]
order = [int(x) for x in sys.argv[4].split(',')]
air = {int(x) for x in sys.argv[5].split(',') if x}
scale = float(sys.argv[6]) if len(sys.argv) > 6 else 0.95
W, H, BASE, CX, LIFT = 485, 244, 241, 250, 7
os.makedirs(out, exist_ok=True)
frames = []
for k, idx in enumerate(order):
    im = Image.open(f'{src}/part_{idx:02d}.png').convert('RGBA')
    im = im.crop(im.getbbox())
    im = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
    a = np.asarray(im)[..., 3] > 128
    head = a[:int(a.shape[0] * 0.35)]
    hx = np.where(head.any(0))[0]; hc = (hx[0] + hx[-1]) / 2
    x = round(CX - hc); y = BASE - im.height - (LIFT if idx in air else 0)
    c = Image.new('RGBA', (W, H), (0, 0, 0, 0)); c.alpha_composite(im, (x, y))
    c.save(f'{out}/{cid}_walk_{k}.png'); frames.append(c)
bg = [Image.alpha_composite(Image.new('RGBA', (W, H), (120, 190, 235, 255)), f).convert('P', palette=Image.ADAPTIVE) for f in frames]
bg[0].save(f'{out}/{cid}_run_preview.gif', save_all=True, append_images=bg[1:], duration=80, loop=0, disposal=2)
print('ok', len(frames))
