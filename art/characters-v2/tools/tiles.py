"""草原地形磚與前景道具：從切好的分件組出可無縫延伸的 9-slice 地面、平台、泥土填充。"""
import sys, json, shutil
import numpy as np
from PIL import Image
props_src, tiles_src, out = sys.argv[1], sys.argv[2], sys.argv[3]
S = 2  # 原圖放大倍率

def load(d, i):
    im = Image.open(f'{d}/part_{i:02d}.png')
    return im.resize((im.width * S, im.height * S), Image.LANCZOS)

def trim(im):
    return im.crop(im.getbbox())

def slice9(im, cw):
    """[左半 P | 中帶鏡像 | 中帶 | P 鏡像]：中帶取自原圖正中央，重複時無接縫，左右端蓋跟中帶接得上。"""
    a = np.asarray(trim(im)); c = a.shape[1] // 2
    P, B = a[:, :c], a[:, c - cw:c]
    row = np.concatenate([P, B[:, ::-1], B, P[:, ::-1]], 1)
    return Image.fromarray(row), {'left': c, 'right': c}

def seamless(im):
    """平塗泥土：裁掉外框後與自身偏移半格的版本以羽化遮罩混合，四邊可無縫鋪排。"""
    a = np.asarray(trim(im).convert('RGB')).astype(float)
    m = int(min(a.shape[:2]) * 0.08); a = a[m:-m, m:-m]
    h, w = a.shape[:2]; r = np.roll(np.roll(a, h // 2, 0), w // 2, 1)
    yy, xx = np.mgrid[0:h, 0:w]
    d = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy)) / (min(h, w) / 2)
    k = np.clip(d * 2.5, 0, 1)[..., None]
    return Image.fromarray((a * k + r * (1 - k)).astype(np.uint8)).resize((256, 256), Image.LANCZOS)

meta = {}
props = {0: 'jelly_big', 1: 'jelly_small', 2: 'jelly_mid', 3: 'flower_peach', 4: 'bush_big', 5: 'flower_red', 6: 'flower_blue',
         7: 'bush_small', 8: 'mushroom_green', 9: 'mushroom_lavender', 10: 'mushroom_peach', 11: 'rock', 12: 'grass_tuft'}
for i, n in props.items():
    trim(load(props_src, i)).save(f'{out}/props/{n}.png')
for i, n, b in ((0, 'ground_grass', 60), (7, 'platform_grass', 60), (9, 'platform_jelly', 60)):
    im, bd = slice9(load(tiles_src, i), b)
    im.save(f'{out}/tiles/{n}.png'); meta[n] = {'size': im.size, 'border_px': bd}
for i, n in ((1, 'soil_fill'), (3, 'soil_pebble')):
    seamless(load(tiles_src, i)).save(f'{out}/tiles/{n}.png')
json.dump(meta, open(f'{out}/tiles/slice.json', 'w'), indent=1)
print(meta)
