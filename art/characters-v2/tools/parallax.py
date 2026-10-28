"""草原視差層：洋紅去背、遠景城堡拆出單獨一張、各層左右鏡像拼成可無限橫向重複的寬圖。用法: parallax.py raw資料夾 輸出資料夾 [後綴]"""
import sys, os
import numpy as np
from PIL import Image
from scipy import ndimage as nd
raw, out = sys.argv[1], sys.argv[2]; sfx = sys.argv[3] if len(sys.argv) > 3 else ''
os.makedirs(out, exist_ok=True)
H = 1080  # 遊戲內高度（px）

def key(im, despill_red=False):
    a = np.asarray(im.convert('RGB')).astype(float); r, g, b = a[..., 0], a[..., 1], a[..., 2]
    d = np.sqrt((255 - r) ** 2 + g ** 2 + (255 - b) ** 2)   # 與純洋紅的距離
    alpha = np.clip((d - 80) / 90, 0, 1)
    solid = alpha > 0.98
    # 半透明邊緣改用最近的實色像素顏色，去掉洋紅溢色
    idx = nd.distance_transform_edt(~solid, return_distances=False, return_indices=True)
    rgb = a[idx[0], idx[1]]
    if despill_red:  # 遠景只有藍綠，紅色不該高過綠色，壓掉洋紅溢色
        rgb[..., 0] = np.minimum(rgb[..., 0], rgb[..., 1])
    return np.dstack([rgb, alpha * 255]).astype(np.uint8)

def mirror_pair(a):
    a = a[:, 6:-6]  # 放大後的最邊緣欄位有雜色，裁掉才不會在接縫出現細線
    return np.concatenate([a, a[:, ::-1]], 1)

def fit(im):
    return im.resize((round(im.width * H / im.height), H), Image.LANCZOS)

sky = np.asarray(Image.open(f'{raw}/sky{sfx}.png').convert('RGBA'))
fit(Image.fromarray(mirror_pair(sky))).save(f'{out}/bg_sky.png')

for n in ('far', 'mid'):
    k = key(Image.open(f'{raw}/{n}{sfx}.png'), despill_red=(n == 'far'))
    if n == 'mid':  # 只取中間不含邊緣蘑菇的一段，避免鏡像拼接時同一朵蘑菇左右成雙
        w = k.shape[1]; k = k[:, int(w * 1800 / 5090):int(w * 3600 / 5090)]  # 兩端都切在山谷底，鏡像後是兩座山相交的 V 形，跟原畫的疊山一致
    if n == 'far':
        # 城堡：與底部山丘帶不相連的最大一塊
        m = k[..., 3] > 128; lab, cnt = nd.label(m)
        h = m.shape[0]; bottom = set(np.unique(lab[-3:])) - {0}
        sizes = nd.sum(m, lab, range(cnt + 1))
        castle = max((i for i in range(1, cnt + 1) if i not in bottom), key=lambda i: sizes[i])
        cm = nd.binary_dilation(lab == castle, iterations=3)
        c = k.copy(); c[..., 3] = np.where(cm, c[..., 3], 0)
        ys, xs = np.where(c[..., 3] > 0); ci = Image.fromarray(c[ys.min():ys.max() + 1, xs.min():xs.max() + 1])
        ci.resize((round(ci.width * H / h), round(ci.height * H / h)), Image.LANCZOS).save(f'{out}/bg_far_castle.png')
        k[..., 3] = np.where(cm, 0, k[..., 3])
    fit(Image.fromarray(mirror_pair(k))).save(f'{out}/bg_{n}_hills.png')
for f in sorted(os.listdir(out)):
    print(f, Image.open(f'{out}/{f}').size)
