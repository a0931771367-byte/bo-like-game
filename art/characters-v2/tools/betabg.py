"""封測背景重繪：把 AI 稿對齊成 Unity 用格式。高 720，bgGroundY=420（上 420 天空／遠景、下 300 地面走道），
左右鏡像拼成可無縫平鋪。用法: betabg.py 原圖 輸出.png"""
import sys
import numpy as np
from PIL import Image
src, out = sys.argv[1], sys.argv[2]
GY, H = 420, 720
im = Image.open(src).convert('RGB'); a = np.asarray(im).astype(int); h, w = a.shape[:2]
dark = (a.max(2) < 90).sum(1)
lo, hi = int(h * 0.55), int(h * 0.85)
horizon = lo + int(np.argmax(dark[lo:hi])) + 3          # 遠景與走道之間的深色分界線
sky = im.crop((0, 0, w, horizon)); ground = im.crop((0, horizon, w, h))
s = GY / horizon
sky = sky.resize((round(w * s), GY), Image.LANCZOS)
gw = sky.width; gh = round(ground.height * s)
ground = ground.resize((gw, gh), Image.LANCZOS)
if gh < H - GY:  # 走道不夠高：用上下鏡像接續往下補，不拉伸，地面記號大小才一致
    g2 = Image.new('RGB', (gw, H - GY)); y = 0; flip = False
    while y < H - GY:
        band = ground.transpose(Image.FLIP_TOP_BOTTOM) if flip else ground
        g2.paste(band, (0, y)); y += gh; flip = not flip
    ground = g2
else:
    ground = ground.crop((0, 0, gw, H - GY))
col = Image.new('RGB', (gw, H)); col.paste(sky, (0, 0)); col.paste(ground, (0, GY))
col = col.crop((6, 0, gw - 6, H))                         # 去掉邊緣雜色
arr = np.asarray(col); full = Image.fromarray(np.concatenate([arr, arr[:, ::-1]], 1))
full.save(out); print(out, full.size, 'horizon', horizon, 'bgGroundY', GY)
