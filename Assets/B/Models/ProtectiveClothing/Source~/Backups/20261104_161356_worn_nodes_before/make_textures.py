from PIL import Image, ImageDraw, ImageFont
import numpy as np
from pathlib import Path
P=Path(__file__).resolve().parent.parent/'Textures'; P.mkdir(exist_ok=True)
N=2048; rng=np.random.default_rng(1204)
y,x=np.mgrid[:N,:N]; noise=rng.normal(0,1.25,(N,N))
weave=((x%4==0)*1.8+(y%4==0)*1.8-1.2)+noise
arr=np.zeros((N,N,3),dtype=np.uint8)
# Blender UV bottom-left: fabric below v=.74; upper strip holds swatches/labels.
arr[:]=np.clip(np.array([100,112,77])+weave[:,:,None],0,255)
arr[:512,:512]=np.clip(np.array([39,45,40])+weave[:512,:512,None]*.6,0,255)
arr[:512,512:1024]=np.clip(np.array([68,73,55])+weave[:512,512:1024,None],0,255)
arr[:512,1024:1536]=[155,157,133]
arr[:512,1536:]=[173,151,91]
im=Image.fromarray(arr); draw=ImageDraw.Draw(im)
font_path='C:/Windows/Fonts/arialbd.ttf'
for label,box in [('L',(32,32,224,224)),('R',(280,32,472,224))]:
    draw.rounded_rectangle(box,radius=18,fill=(182,163,105),outline=(215,204,160),width=5)
    font=ImageFont.truetype(font_path,145); draw.text(((box[0]+box[2])/2,(box[1]+box[3])/2),label,font=font,fill=(30,40,32),anchor='mm')
# seam strips, which can be assigned to broad panel UVs.
for xx in [44,1998]:
    draw.line((xx,560,xx,2010),fill=(66,79,51),width=6)
    for yy in range(570,2000,15):draw.line((xx+7,yy,xx+7,yy+7),fill=(142,147,107),width=2)
im.save(P/'PC_BaseColor_2K.png')
rough=np.full((1024,1024),206,dtype=np.uint8); rough[:256,:256]=173; rough[:256,512:768]=151
Image.fromarray(rough).save(P/'PC_Roughness_1K.png')
normal=np.zeros((1024,1024,3),dtype=np.uint8); normal[:]=[128,128,255]
normal[:,:,0]=128+((np.indices((1024,1024))[1]%4==0)*5).astype(np.uint8)
normal[:,:,1]=128+((np.indices((1024,1024))[0]%4==0)*5).astype(np.uint8)
Image.fromarray(normal).save(P/'PC_Normal_1K.png')
print('Saved shared PBR textures')
