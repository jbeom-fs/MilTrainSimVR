K2 최종 UV / 텍스처 작업용 패키지

K2_Assembly_UVReady.blend : 8개 부품과 조립 애니메이션을 포함한 Blender 파일.
01_Upper_UVReady.fbx : 상부 본체
02_Lower_UVReady.fbx : 하부 본체 (노리쇠멈치 포함)
03_PushRod_UVReady.fbx : 밀대뭉치
04_GasRod_UVReady.fbx : 가스활대
05_BoltAssembly_UVReady.fbx : 노리쇠뭉치 (링·앞쪽 톱니·삽입 부품 포함)
06_GasRegulator_UVReady.fbx : 가스조절기
07_Selector_UVReady.fbx : 조정간
08_ChargingHandle_UVReady.fbx : 장전손잡이

각 FBX는 메시 1개 / UV_Game 1개(UV0) / 재질 1개입니다.
8개 부품마다 독립된 0–1 UV와 텍스처 세트를 사용합니다.
같은 부품 안에서는 좌우 UV를 겹치거나 대칭 복제하지 않았습니다.
FBX는 조립 완료 프레임 867의 배치·크기를 유지한 정적 메시입니다.
같은 원점에 가져오면 서로 맞춰집니다. 애니메이션은 Blender 파일에 있습니다.
FBX 축은 -Z Forward / Y Up이며 단위 변환 정보가 포함되어 있습니다.
FBX 삼각분할을 고정했습니다. 텍스처 작업 이후에는 UV·토폴로지를 유지해 주세요.
피벗과 Empty 계층을 포함한 조립 애니메이션을 Blender 파일에 보존했습니다.

애니메이션: 30fps, 프레임 1–867. 가스조절기부터 조립하며 완전 조립 후 1초 유지.
탄창은 포함하지 않았습니다. 이전 작업용 고해상도 메시와 다른 장면도 제외했습니다.
원본 작업용 Blender 파일과 백업은 별도로 남아 있습니다.

UV 배치: UV_Guides/(부품별 UV 배치도 PNG·SVG)는 텍스처 완료 후 10/26 삭제했습니다 (git 기록에 없음).
  UV 여백은 2048 기준 섬 사이 약 8px, 외곽 최소 16px입니다.
  긴 원통은 비율을 유지해 펼쳤기 때문에 일부 UV 타일에는 여유 공간이 있습니다.
  곡면·베벨·톱니·구멍 내부는 필요에 따라 별도 조각으로 분리했습니다.

최종 텍스처(10/26): ../Textures/{번호_부품}/ 에 부품별 3장 (1024×1024, URP Lit용).
  {번호_부품}_BaseColor.png          = sRGB
  {번호_부품}_MetallicSmoothness.png = Linear, R=Metallic, A=Smoothness
  {번호_부품}_Normal.png             = Normal map, OpenGL(+Y)
  재질은 ../Materials/{번호_부품}.mat (URP Lit), 각 FBX의 재질 Remap으로 연결했습니다.
  이전 참고용 맵(Reference_Textures/, ORM 방식)은 삭제했습니다. Blender 파일에는 참고 맵이 패킹되어 있습니다.

수정 기록(10/26): GAME_Lower 앞쪽 끝(탄창 삽입구 앞, x > -0.05) 꼭짓점 64개를 +X로 7mm 옮겨
  상부 본체와의 틈(안쪽이 보이던 곳)을 메웠습니다. 02_Lower_UVReady.fbx만 다시 내보냈습니다.
  UV·면 구성·삼각형 나눔은 그대로입니다 (Verification/ 기록은 수정 전 기준).

FBX 다시 내보내기 설정 (원래 FBX와 같게 나오는 것을 확인):
  선택한 메시만, Forward -Z / Up Y, Apply Unit 켬, Apply Scalings = FBX Units Scale,
  Smoothing = Face, Triangulate 켬, Leaf Bones 끔, Bake Animation 끔, Path Mode = Copy + Embed.
  오브젝트 이름은 FBX 파일 번호 이름(예: 02_Lower)으로 바꿔서 내보냅니다. 이름이 바뀌면 Unity 프리팹 참조가 끊깁니다.
  꼭짓점을 옮겼다면 원래 위치에서 삼각형으로 나눈 뒤 옮겨야 삼각형 나눔이 유지됩니다.

Verification/ : 8개 FBX 재가져오기 결과, 전체 UV 검사, 형상·노멀·867프레임 비교 기록.
  원래 형상·노멀·조립 움직임을 유지했고 기존 1–5번 UV도 그대로 보존했습니다.
  부품별 상세 삼각형 수와 원본 오브젝트 이름은 Parts_Manifest.json에서 확인하세요.
