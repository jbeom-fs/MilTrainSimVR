from pathlib import Path
import hashlib,json,shutil
from PIL import Image
import numpy as np
S=Path(__file__).resolve().parent;P=S.parent;PROJECT=P.parents[3];B=Path((S/'UpgradeBackupPath.txt').read_text())
matdir=PROJECT/'Assets'/'B'/'Materials'/'ProtectiveClothing'
target=matdir/'PC_MetallicSmoothness_1K.png'
assert target.exists(),target
snapshot=B/'ExternalDependenciesBefore.json'
if not snapshot.exists():
    files=[matdir/'PC_Shared.mat',matdir/'PC_Shared.mat.meta',target,target.with_suffix('.png.meta'),P.parent.with_name('ProtectiveClothing.meta')]
    for path in [PROJECT/'Assets'/'B'/'Scripts'/'ProtectiveClothing',PROJECT/'Assets'/'B'/'Editor'/'ProtectiveClothing']:
        files.extend(path.rglob('*'))
    files.extend([PROJECT/'Assets'/'B'/'Scene'/'B_ProtectiveClothing_Equipment.unity',PROJECT/'Assets'/'B'/'Scene'/'B_ProtectiveClothing_Equipment.unity.meta'])
    data={}
    for f in files:
        if not f.is_file():continue
        rel=f.relative_to(PROJECT);dest=B/'ExternalDependencies'/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,dest)
        data[str(f)]={'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'intentional_image_update':f==target}
    snapshot.write_text(json.dumps(data,indent=2))
rough=np.array(Image.open(P/'Textures'/'PC_Roughness_1K.png').convert('L'))
rgba=np.zeros((*rough.shape,4),dtype='uint8');rgba[:,:,3]=255-rough
Image.fromarray(rgba).save(target)
print('Updated existing derived PNG only; RGB=0, A=255-Roughness:',target)
