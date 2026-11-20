"""Add unambiguous captions to Blender renders and assemble a review sheet."""
from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
SRC=Path(__file__).resolve().parent
PRE=SRC/'Previews'
FONT='C:/Windows/Fonts/malgun.ttf'
def font(size):return ImageFont.truetype(FONT,size)
names={'Front':'정면 · 앞쪽 −Z','Side':'측면 · +X에서 관찰','Rear':'후면 · 뒤쪽 +Z','Oblique':'사선 · 앞쪽에서 관찰'}
for model in ['K1','PC']:
    for view,label in names.items():
        path=PRE/f'{model}_{view}.png'
        raw=Image.open(path).convert('RGB')
        if raw.size!=(850,1050):raise RuntimeError('Rerender before reapplying labels')
        canvas=Image.new('RGB',(850,1170),'#f0f2f3');canvas.paste(raw,(0,52));d=ImageDraw.Draw(canvas)
        d.text((24,10),f'{model}  |  {label}',fill='#253139',font=font(24))
        d.text((24,1110),'청회색 중앙 면: 기존 Surface 위치의 임시 가이드',fill='#34444e',font=font(20))
        d.text((24,1138),'가이드·바닥·조명·카메라는 FBX에 포함되지 않음',fill='#34444e',font=font(20))
        canvas.save(path)
    sheet=Image.new('RGB',(1200,1760),'#f0f2f3');d=ImageDraw.Draw(sheet)
    d.text((28,16),f'{model} Mirror Frame  |  Blender 제작 미리보기',fill='#253139',font=font(28))
    for i,view in enumerate(names):
        tile=Image.open(PRE/f'{model}_{view}.png').resize((580,798),Image.Resampling.LANCZOS)
        sheet.paste(tile,(10+(i%2)*600,66+(i//2)*816))
    d.text((28,1708),'무광 금속 1종 · 프레임 20mm · 전방 10mm / 후방 150mm',fill='#253139',font=font(22))
    sheet.save(PRE/f'{model}_Overview.png')
print('Labeled 8 views and saved 2 overview sheets.')
