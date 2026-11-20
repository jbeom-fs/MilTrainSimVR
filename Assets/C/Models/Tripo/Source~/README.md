# Tripo 모델 줄이기 스크립트 (Unity 가 읽지 않는 폴더)

Tripo 모델은 UV 가 아주 잘게 쪼개져 있어 원본 UV 를 지키며 줄이면 꼭짓점이 거의 안 준다. 그래서 **접어 줄이고 → UV 를 새로 펴고 → 원본 텍스처 색을 굽는다.** Blender 5.2 를 화면 없이 돌린다.

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python <스크립트> -- <spec.json>
```

## 가구 5종 (11/06) — 루트 폴더

- `make_lowpoly.py` — 원본 FBX 의 메시 배열만 바꿔 써서 노드 · 재질 이름 · GUID 를 지킨다(씬에 이미 놓인 모델용). spec 예: `target_tris` 1000~1400 · `tex_res` 4096 · `unfold` · `hide_remove` · `cage` 0.012 · `ray` 0.03
- `uvtools.py`(UV 펴기 · 뒤집힌 면 · 안 보이는 면 · 빗나간 굽기 다시 칠하기) · `fbxio.py`(FBX 읽기/쓰기) · `holecheck.py` · `fill_gaps.py` · `repair_texture.py` · `render_compare.py` · `render_ingame.py` · `summ.py` · `validate_fbx.py` · `inspect_fbx.py`
- `run_batch.sh`(spec 목록 일괄) · `replace.sh`(승인한 후보로 원본 덮어쓰기). 두 스크립트 안의 경로는 11/06 작업 폴더 기준이라 쓰기 전에 고친다
- Unity 쪽: 줄인 모델은 `.fbx.meta` 에서 자동 Mesh LOD 를 끈다(`generateMeshLods: 0`)

## 가챠 기계 (11/09) — `gacha/`

- `make_cards.py` — 카드 그림(1024 정사각)을 창 비율(약 2.1 : 1)로 맞춘다. 원본 그림은 `gacha/cards_src/`
- `make_gacha_variants.py` · `make_gacha_cards.py` — 비교용(창 안쪽 + 유리 / 앞줄 캡슐 / 카드판)
- `make_gacha_lowpoly.py` — 최종: 접어 줄이기(목표 1500 삼각형) → 창(카드 자리 · 유리, 카드는 유리 1 cm 뒤) → 몸통만 UV 펴고 굽기(2048) → 옆면 회색 반원판은 굽기 전에 원본 텍스처에서 옆면 흰색으로 덮음 → Weighted Normal. 새 FBX 로 내보낸다(GUID 를 지킬 인스턴스가 없어서)
- `spec_lowpoly.json` — 위 스크립트의 설정. `out_dir` 은 아무 임시 폴더
- 결과물: `Assets/C/Models/Tripo/CapsuleMachine/` (재질 칸: 몸통 · 유리 · 위 카드 · 아래 카드)

## 가게 기계 · 당구 (11/09) — `make_prop_lowpoly.py` + `props/`

- `make_prop_lowpoly.py` — 일반용: 접어 줄이기 → UV 새로 펴기 → 원본 색 · 법선 · 금속/거칠기 굽기 → 유리 자동 찾기 · 어두운 면 다시 칠하기 · 글자 판 굽기(decals). 새 FBX 로 내보낸다
- `props/spec_claw.json` 인형뽑기(4,506 삼각형, 유리 네 면) · `spec_changer.json` 지폐 교환기(깨진 한글을 맑은 고딕 글자 판으로 덮어 굽기) · `spec_table.json` 당구대(1,451) · `spec_cue.json` 큐 장(1,965) · `spec_bbq_table.json` 드럼통 불판 탁자(9,608 → 2,374 — Tripo 이름 floor_drain, 색 지도만 와서 금속 · 매끈함 지도는 Unity 에서 밝기로 만듦) · `spec_plastic_chair.json` 플라스틱 의자(2,599). 원본은 `Models/Tripo/Bridge/`, `out_dir` 은 아무 임시 폴더
- 결과물: `Assets/C/Models/Tripo/ClawMachine` · `CoinChanger` · `BilliardTable` · `CueRack` · `BbqTable` · `PlasticChair`(재질 흰 · 초록 · 짙은 파랑 — 텍스처 하나에 색만 곱함) — 재질은 Tripo 재질을 복사해 `_BaseMap` 만 바꾸고, `.fbx.meta` 자동 Mesh LOD 끔
