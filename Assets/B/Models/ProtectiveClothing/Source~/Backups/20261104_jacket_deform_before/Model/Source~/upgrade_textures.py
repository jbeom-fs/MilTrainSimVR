"""Prepare the shared, unlit production atlas from the ImageGen albedo master.
UV packing, palette quantization, periodic mirror sampling, and analytic normal/
roughness synthesis are asset processing, not photographic lighting transfer.
"""
from pathlib import Path
from PIL import Image
import numpy as np
S=Path(__file__).resolve().parent;P=S.parent;T=P/'Textures'
N=2048;C=1536
master=np.array(Image.open(S/'ReferenceImages'/'Woodland_Generated_Master.png').convert('RGB').resize((512,512),Image.Resampling.LANCZOS)).astype(float)
# Quantize away the generated image's low-frequency shading and grain.
palette=np.array([[59,72,31],[80,57,34],[25,42,25],[10,16,11]],dtype=float)
centres=np.array([[76,81,36],[105,71,43],[30,48,26],[18,26,18]],dtype=float)
indices=np.argmin(((master[:,:,None,:]-centres[None,None,:,:])**2).sum(3),axis=2)
tile=palette[indices]
atlas=np.zeros((N,N,3),dtype=float)
y,x=np.mgrid[:N,:N];rng=np.random.default_rng(719)
grain=rng.normal(0,.62,(N,N));weave=.45*np.sin(2*np.pi*x/4)*np.sin(2*np.pi*y/4)
# Fabric square occupies Blender UV [0,.75]²; mirror sampling gives continuous borders.
for j in range(3):
    for i in range(3):
        block=tile[::(-1 if j%2 else 1),::(-1 if i%2 else 1)]
        atlas[N-C+j*512:N-C+(j+1)*512,i*512:(i+1)*512]=block
rubber=(.9*np.sin(x*.013)*np.cos(y*.009)+.4*grain)
atlas[:]=np.where((x<C)[:,:,None]& (y>=N-C)[:,:,None],atlas,np.array([15,18,17])+rubber[:,:,None])
# Swatches to the right: olive hood/cape, seam thread, deep rubber. No visible L/R patches.
olive=(x>=1570)&(y>=1024)&(y<1516)
atlas[olive]=np.array([57,67,33])+grain[olive,None]
thread=(x>=1570)&(y>=1550)
atlas[thread]=np.array([57,63,34])+grain[thread,None]*.3
# Microtexture is albedo grain only; no cast shadows or directional baked lighting.
cloth=(x<C)&(y>=N-C)
atlas[cloth]+=grain[cloth,None]+weave[cloth,None]
Image.fromarray(np.clip(atlas,0,255).astype('uint8')).save(T/'PC_BaseColor_2K.png')
R=1024;yy,xx=np.mgrid[:R,:R];u=xx/R;v=1-yy/R
rough=.46+.045*np.sin(xx*.035)*np.cos(yy*.031)+rng.normal(0,.008,(R,R))
fabric=(u<.75)&(v<.75);olive=(u>.76)&(v>.25)&(v<.5);thread=(u>.76)&(v<.25)
rough[fabric]=.86+.025*np.sin(xx[fabric]*.8)*np.cos(yy[fabric]*.7)
rough[olive]=.84;rough[thread]=.91
Image.fromarray((np.clip(rough,0,1)*255).astype('uint8')).save(T/'PC_Roughness_1K.png')
height=.0008*np.sin(xx*.057)*np.cos(yy*.036)
clothheight=.007*np.sin(xx*np.pi/2)*np.cos(yy*np.pi/2)+.003*np.sin(xx*.12+yy*.041)
height[fabric|olive]=clothheight[fabric|olive]
height[thread]=.012*np.sin(yy[thread]*1.8)
dy,dx=np.gradient(height);normal=np.stack([-dx*5,dy*5,np.ones_like(dx)],axis=2);normal/=np.linalg.norm(normal,axis=2)[:,:,None]
Image.fromarray(np.clip((normal*.5+.5)*255,0,255).astype('uint8')).save(T/'PC_Normal_1K.png')
print('Updated existing BaseColor 2K, Normal 1K, Roughness 1K; material atlas fabric=.75², olive=right-middle, rubber=right-upper.')
