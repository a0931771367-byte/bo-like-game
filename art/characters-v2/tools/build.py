import sys,json,os
from PIL import Image
from psd_tools import PSDImage
from psd_tools.api.layers import PixelLayer
cfg=json.load(open(sys.argv[1])); c=cfg['name']; src=cfg['src']; R=f"/home/user/bo-like-game/art/characters-v2/{cfg.get('out',c)}"
P={n:Image.open(f"{src}/part_{i:02d}.png") for n,i in cfg['map'].items()}
for n,s in cfg.get('scale',{}).items(): P[n]=P[n].resize((int(P[n].width*s),int(P[n].height*s)),Image.LANCZOS)
W,H=cfg.get('canvas',[1400,1800]); C=Image.new('RGBA',(W,H),(0,0,0,0)); pos={}
for n in cfg['order']:
  cx,ty=cfg['layout'][n]; x=int(cx-P[n].width/2); pos[n]=(x,ty)
  if n not in cfg.get('hidden',[]): C.alpha_composite(P[n],(x,ty)); pos[n]=(x,ty)
bb=C.getbbox(); pad=20; x0,y0=bb[0]-pad,bb[1]-pad; w=bb[2]-bb[0]+2*pad; h=bb[3]-bb[1]+2*pad
os.makedirs(f"{R}/parts",exist_ok=True)
bg=Image.new('RGBA',(w,h),(220,235,250,255)); bg.alpha_composite(C.crop((x0,y0,x0+w,y0+h)))
bg.convert('RGB').resize((w//2,h//2)).save(f"{src}/preview.jpg",quality=85)
if len(sys.argv)>2 and sys.argv[2]=='preview': sys.exit()
C.crop((x0,y0,x0+w,y0+h)).save(f"{R}/{c}-assembled.png")
layers=[]
for n in cfg['order']:
  P[n].save(f"{R}/parts/{n}.png"); layers.append({'name':n,'left':pos[n][0]-x0,'top':pos[n][1]-y0,'w':P[n].width,'h':P[n].height})
json.dump({'canvas':[w,h],'pivot':'bottom-center','layers':layers},open(f"{R}/layout.json",'w'),indent=1)
for tag,s in (('master',1.0),('game',cfg.get('game_h',430)/h)):
  psd=PSDImage.new('RGBA',(round(w*s),round(h*s))); psd._record.header.version=2
  for l in layers:
    im=P[l['name']]
    if s!=1: im=im.resize((max(1,round(im.width*s)),max(1,round(im.height*s))),Image.LANCZOS)
    L=PixelLayer.frompil(im,psd,l['name'],top=round(l['top']*s),left=round(l['left']*s))
    if l['name'] in cfg.get('hidden',[]): L.visible=False
    psd.append(L)
  psd.save(f"{R}/{c}_{tag}.psb"); print(tag,PSDImage.open(f"{R}/{c}_{tag}.psb").size)
