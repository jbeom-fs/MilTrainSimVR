# A에게 — NavMesh 파일 고침 (회신)

작성: C · 2026-10-28
관련: [A 요청 — NavMesh 파일 확인 부탁](../A/1027_C요청_NavMesh파일_확인부탁.md)

알려 주셔서 감사합니다. 추정하신 원인이 맞았습니다. 이 문서와 같은 커밋으로 고쳐 올렸습니다.

## 1. 확인한 것

- 저장소에 올라간 `Assets/C/Scenes/C_UrbanBlock/NavMesh_C_UrbanBlock.asset` 이 C PC 의 원본보다 **5바이트 짧았습니다** (원본 683,572 → 저장소 683,567).
- `.gitattributes` 의 `*.asset unity-yaml` 규칙(`eol=lf`)이 바이너리 파일에도 걸려서, 커밋할 때 파일 안의 `\r\n` 5곳이 `\n` 으로 바뀌었습니다.
- C PC 의 원본은 멀쩡합니다 (에디터에서 열림). 그래서 C 쪽에서는 드러나지 않았습니다.
- 저장소의 다른 `.asset` 파일 중에 같은 경우(바이너리인데 줄바꿈 변환이 걸린 것)는 없었습니다.

## 2. 고친 것

- `.gitattributes` 맨 끝에 한 줄을 넣었습니다: `Assets/**/NavMesh*.asset binary` (`LightingData.asset binary` 와 같은 방식).
  - 제안하신 `NavMesh*.asset` 은 `ProjectSettings/NavMeshAreas.asset`(텍스트 YAML 설정 파일)에도 걸려서 `Assets/` 안으로만 좁혔습니다. 지금 이 규칙에 걸리는 파일은 이 NavMesh 하나뿐입니다.
  - 나중에 A · B 가 자기 씬에서 NavMesh 를 구워도 (`NavMesh.asset`, `NavMesh-<이름>.asset`) 같은 규칙이 적용됩니다.
- NavMesh 는 다시 굽지 않고 C PC 의 원본을 그대로 다시 올렸습니다. 저장소에서 받는 파일이 원본과 바이트까지 같은 것을 확인했습니다.

## 3. A · B 가 할 일

- **pull 만 하면 됩니다.** 파일이 고친 것으로 바뀌고, 에디터가 열려 있으면 Unity 가 다시 가져옵니다.
- 콘솔에 같은 오류가 남아 있으면 그 파일을 Reimport 해 주세요.
