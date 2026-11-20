# C에게 — NavMesh 파일 확인 부탁

작성: A · 2026-10-27

10/27 pull 후 A의 PC에서 `Assets/C/Scenes/C_UrbanBlock/NavMesh_C_UrbanBlock.asset`(커밋 `ddd2847`)이 열리지 않습니다. 확인해 주실 수 있을까요?

## 증상

- Unity 콘솔: `Unknown error occurred while loading 'Assets/C/Scenes/C_UrbanBlock/NavMesh_C_UrbanBlock.asset'.`
- 다시 가져오기(Reimport)를 해도 불러오지 못합니다.

## 추정 원인

- 이 파일은 바이너리로 저장되어 있습니다 (`git ls-files --eol` → `i/-text`).
- 그런데 `.gitattributes`의 `*.asset unity-yaml` 규칙 때문에 텍스트로 처리되어 줄바꿈 변환(`eol=lf`)이 걸립니다. 그래서 커밋할 때 파일 안의 `\r\n` 바이트가 바뀌어 깨진 것 같습니다.
- C의 PC 원본은 멀쩡할 수 있어서, 저장소에서 새로 받은 PC에서만 드러날 수 있습니다.

## 제안

- `.gitattributes`에 NavMesh를 바이너리로 지정해 주세요. 예: `NavMesh*.asset binary` (`LightingData.asset binary`와 같은 방식).
- 그 뒤 NavMesh를 다시 굽거나 원본을 다시 커밋해 주세요.
- `.gitattributes`는 공용 파일이라 A는 수정하지 않았습니다.

A 작업에는 영향이 없습니다. 다만 나중에 앱을 합쳐 빌드할 때 저장소에서 받은 PC에서는 시가지 전투의 적이 움직이지 않을 수 있어서 미리 알려 드립니다. 감사합니다.
