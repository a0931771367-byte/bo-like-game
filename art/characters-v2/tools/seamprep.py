"""把背景原圖排成可平鋪的待補畫布：縮放使地平線在 GY，左右對調（右半在左、左半在右），
中間留空帶、底部留空給 AI 補畫。畫布左右邊緣是原圖同一位置，補完即可無縫平鋪（不用鏡像）。
用法: seamprep.py 原圖 地平線y 輸出畫布.png 輸出遮罩.png"""
import sys
from PIL import Image
src, horizon, out, maskout = sys.argv[1], int(sys.argv[2]), sys.argv[3], sys.argv[4]
GY, H, W = 420, 720, 1680
im = Image.open(src).convert('RGB')
s = GY / horizon
im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
im = im.crop((6, 0, im.width - 6, min(im.height, H)))   # 去邊緣雜色
w = im.width; half = w // 2
L = im.crop((0, 0, half, im.height)); R = im.crop((half, 0, w, im.height))
c = Image.new('RGB', (W, H), 'white'); m = Image.new('L', (W, H), 0)
c.paste(R, (0, 0)); m.paste(255, (0, 0, R.width, im.height))
c.paste(L, (W - L.width, 0)); m.paste(255, (W - L.width, 0, W, im.height))
c.save(out); m.save(maskout)
print(out, 'gap', R.width, W - L.width, 'bottom', im.height)
