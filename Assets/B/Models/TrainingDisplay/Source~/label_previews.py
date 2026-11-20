from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json
SRC=Path(__file__).resolve().parent;PRE=SRC/'Previews'
report=json.loads((SRC/'validation.json').read_text(encoding='utf-8'))
def font(n):return ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',n)
views={'Front':'정면 / 앞 -Z','Side':'측면 / +X에서 관찰','Rear':'후면 / 뒤 +Z','Oblique':'사선 / 앞에서 관찰','RearOblique':'사선 / 뒤에서 관찰'}
for key,item in report['models'].items():
    for view,title in views.items():
        p=PRE/f'{key}_{view}.png'
        if not p.exists():continue
        raw=Image.open(p).convert('RGB');assert raw.size==(1000,850),'Rerender before labeling'
        canvas=Image.new('RGB',(1000,978),'#eef1f2');canvas.paste(raw,(0,56));d=ImageDraw.Draw(canvas)
        w,h=item['screen_m'];d.text((24,12),f'{key}  |  {title}  |  Canvas {w:.3f} x {h:.3f} m',font=font(25),fill='#26313a')
        d.text((24,916),'청회색 면 = 기존 Canvas 위치의 임시 가이드 · 실제 UI 적용 화면 아님',font=font(21),fill='#34434b')
        d.text((24,946),'가이드·카메라·조명은 FBX와 Blender 원본에 포함되지 않음',font=font(21),fill='#34434b');canvas.save(p)
sheet=Image.new('RGB',(1600,1690),'#eef1f2');d=ImageDraw.Draw(sheet)
d.text((24,16),'TrainingDisplay / 대표 AED 외형 4방향',font=font(32),fill='#26313a')
for i,view in enumerate(['Front','Side','Rear','Oblique']):
    tile=Image.open(PRE/f'AED_{view}.png').resize((780,763),Image.Resampling.LANCZOS);sheet.paste(tile,(10+(i%2)*800,74+(i//2)*780))
d.text((24,1650),'베젤 14mm · 앞 돌출 0.5mm · 벽 접촉 Z=+70mm · 규격별 독립 제작',font=font(25),fill='#26313a')
sheet.save(PRE/'Representative_4Views.png')
# Identical ortho camera, resolution and tile scaling preserve real size differences.
sheet=Image.new('RGB',(2200,1310),'#eef1f2');d=ImageDraw.Draw(sheet)
d.text((24,15),'TrainingDisplay / 7개 규격 정면 비교 / 동일 축척',font=font(34),fill='#26313a')
for i,(key,item) in enumerate(report['models'].items()):
    tile=Image.open(PRE/f'{key}_Front.png').resize((540,528),Image.Resampling.LANCZOS);sheet.paste(tile,(5+(i%4)*550,76+(i//4)*558))
d.text((1660,725),'7개 규격 공통',font=font(29),fill='#26313a')
for y,s in [(780,'화면 비율 그대로'),(825,'각 변 개구 여유 2mm'),(870,'916삼각형 / 4부품'),(915,'공유 재질 2개'),(960,'Canvas는 임시 가이드')]:d.text((1660,y),s,font=font(25),fill='#34434b')
d.text((24,1258),'Blender 형상 미리보기. Unity 임포트·UI 가독성·입력·벽 배치는 검증하지 않음.',font=font(28),fill='#26313a')
sheet.save(PRE/'All_7_Front_Comparison.png')
print('Labeled previews, representative sheet, and seven-size comparison saved.')
