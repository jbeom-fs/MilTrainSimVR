트럭 텍스처 / 4096 x 4096

T_Canvas_Truck_BaseColor.png: 기본색, sRGB 켜기.
T_Canvas_Truck_Metallic.png: 금속성, sRGB 끄기.
T_Canvas_Truck_Roughness.png: 거칠기, sRGB 끄기.
T_Canvas_Truck_Unity_MetallicSmoothness.png: Unity용 R=Metallic, A=Smoothness(1-Roughness), sRGB 끄기. 투명도용 맵이 아닙니다.

Unity Standard/URP Lit 사용 시:
BaseColor -> Albedo/Base Map(흰색 Tint), Unity_MetallicSmoothness -> Metallic Map.
Smoothness Source는 Metallic Alpha, Smoothness 배율은 1. Surface는 Opaque.
별도 Metallic/Roughness는 다른 PBR 도구용입니다. Unity에 네 맵을 모두 연결할 필요 없습니다.
FBX의 Truck_Static 메시에 공유 재질을 사용합니다. Blender 재질과 엔진 셰이더는 다르므로 엔진에서 재질 연결 확인이 필요합니다.

원래 단색 재질의 색/금속성/거칠기를 조명 없이 베이크했습니다.
현재 형상의 디테일은 메시 자체에 있으므로 빈 Normal/AO 맵은 만들지 않았습니다.
Blender 파일에는 세 맵의 노드 연결과 원본 재질 데이터가 보존되어 있습니다.
빛·환경 반사가 달라지면 보이는 느낌도 달라집니다.
