import sys,os
from PIL import Image
import numpy as np
from scipy import ndimage
src,out=sys.argv[1],sys.argv[2]; os.makedirs(out,exist_ok=True)
a=np.asarray(Image.open(src).convert('RGB')).astype(int)
white=(a.min(axis=2)>235)
lab,n=ndimage.label(white)
border=set(lab[0,:])|set(lab[-1,:])|set(lab[:,0])|set(lab[:,-1]); border.discard(0)
sizes=ndimage.sum(white,lab,range(n+1))
big=[i for i in range(1,n+1) if sizes[i]>4000]
bg=np.isin(lab,list(border|set(big))); fg=~bg
plab,pn=ndimage.label(ndimage.binary_closing(fg,iterations=2))
res=[]
for i,sl in enumerate(ndimage.find_objects(plab)):
  area=(plab[sl]==i+1).sum()
  if area<3000: continue
  res.append((sl[1].start,sl[0].start,sl[1].stop,sl[0].stop,area,i+1))
res.sort(key=lambda r:(r[1]//300,r[0]))
for k,(x0,y0,x1,y1,area,idx) in enumerate(res):
  m=ndimage.binary_dilation(plab[y0:y1,x0:x1]==idx,iterations=2)&fg[y0:y1,x0:x1]
  piece=np.dstack([a[y0:y1,x0:x1].astype(np.uint8),(m*255).astype(np.uint8)])
  Image.fromarray(piece,'RGBA').save(f'{out}/part_{k:02d}.png'); print(k,x0,y0,x1,y1,area)
