# Project Working Guidelines

Working instructions for user B's work on the Quest 2 reserve training simulation Unity project. This file is intentionally stored under Docs/B; do not move or copy it to the project root.
Project root: `D:\TeamProject\MilTrainSimVR`.
Unity Editor version: `6000.3.13f1` (ProjectSettings/ProjectVersion.txt).
Store B's project documents under `D:\TeamProject\MilTrainSimVR\Docs\B`. This supersedes the earlier `D:\Docs` and project-level `Docs` document locations.

## Resume pointer — protective clothing worn on the user's own body of 2026-11-03

- 착용 대상을 전시 마네킹 → HMD·양손을 따르는 사용자 몸(`PC_UserBody`, `ProtectiveClothingUserBody`; 몸통·다리·발은 추정)으로 바꿨다. [기록](1103_보호장비_사용자몸착용_전환_구현기록.md)을 먼저 읽는다. 아래 기본 착용 포인터의 전시 배치·단일 `wearRadius`는 대체됐다.
- Blender: 후드·어깨 덮개·목 연결·접힌 두건 삭제, 위장 칼라 마감, Worn 6종을 `PC_Armature`(마네킹 골격 복사본)에 스키닝, FBX 재내보내기·재수입 대조 완료(`Source~/userwear_model.py`, `UserWearReport.json`, 백업 `Source~/Backups/20261103_userwear_before/`). 상의 자세별 관통·늘어남은 남은 제한.
- 착용: 상의 가슴 앞·하의 허리 앞(r 0.15), 장갑은 대상 손(r 0.15, 반대 손으로만), 덮개는 허벅지 앞 보조 영역(r 0.12). 잡은 직후 착용 방지 arming(반경+0.05 m 밖 관측 후). 거울 `PC_Mirror`. 메뉴 1이 생성된 전시물만 삭제.
- 컴파일·메뉴 1/2·Play·Simulator·헤드셋 모두 미확인, 미커밋. 확인 ①~⑨ 미확인. 다음 챕터: 정식 순서·Runner·시작/완료·제독·K1.

## Resume pointer — protective clothing basic wearing of 2026-11-03

- 보호장비 기본 착용 조작(6종 독립 착용·초기화, 교육 순서 없음). 런타임 `Assets/B/Scripts/ProtectiveClothing/`(상태 소유자 `ProtectiveClothingEquipment`), 메뉴 `B/Protective Clothing/1. Build Equipment Scene` → `2. Validate ...`, 씬 `B_ProtectiveClothing_Equipment.unity`(메뉴 1 생성). [기록](1103_보호장비_기본착용조작_구현기록.md).
- 후속 수정(기록 §10): 설정 검증 실패 시 grab 비활성으로 실제 잡기 차단·필터 Awake 등록, 메뉴 1은 Play·컴파일·미저장 씬이면 어떤 변경 전에 중단(저장 대화상자 없음), 전시 배치를 루트째 62° 눕힌 마네킹(목표 0.80~1.42 m)으로 변경(이전 기본값과 정확히 같을 때만 갱신).
- 후속 수정 2(기록 §11): 안내판 이전 기본값 판정을 Canvas 축척 0.001 기준으로 수정(이전 기본만 1회 이동, 현재 기본·사용자 조정 유지), 장비 비활성 시 "장비 사용이 일시 중지되었습니다." 안내·표식 숨김·초기화 버튼 꺼짐.
- 정렬: 마네킹·Worn 인스턴스를 `PC_MannequinRoot` 원점에 두고 마네킹 본을 bindpose로 설정(인스턴스 오버라이드, Animator 끔). 메뉴 2가 ReferenceInspection/AnchorManifest와 대조. 컴파일·메뉴 1/2·Play 미확인, 미커밋. 확인 ①~⑧ 미확인. 다음 챕터: 정식 순서·Runner·시작/완료·제독·K1.

## Resume pointer — dressing condition in steps 3-6 of 2026-11-03

- 마감 §2.3 해결: ③ 직접 압박~⑥ 끝·테이프 고정 단계 조건에 Dressing(`pb-equipment`/`dressing-placed`, State/true) 추가. `PressureBandageTrainingIds.Steps`와 데이터 자산 YAML 동시 반영(GUID·순서·기존 조건 보존), Setup 변경 없음(메뉴 1 생성·메뉴 2 비교와 일치). [기록](1103_압박붕대_3-6단계_드레싱조건_보완.md).
- 컴파일·메뉴 1/2·Play 미확인, 미커밋. 확인 ①~⑤ 미확인.

## Resume pointer — seven-step course without final check of 2026-11-03

- 최종 확인 단계·화면·버튼 제거. 교육 7단계(⑦ 처치 후 확인이 모든 요구를 State로 검사) → Runner Completed → 요약 → 완료 화면. `PressureBandageFinalCheck.cs` 삭제, 데이터 자산 YAML 7단계로 갱신. 다시하기는 기존 `TrainingReset`·`ResetEquipment` 그대로. [기록](1103_압박붕대_최종확인제거_7단계_자동완료.md).
- 메뉴 1(레이아웃 개정 7)이 Final 패널·Box_8·missing script를 1회 제거해야 시작 가능. 컴파일·메뉴 1/2·Play 미확인, 미커밋. 확인 ①~⑩ 미확인.

## Resume pointer — bandage end distance attach of 2026-11-03

- 붕대 끝은 잡은 채 `Anchor_EndFix`가 EndMark 0.08 m(`endSnapRadius`) 안에 오면 자동 부착(선택 취소 → SeatEnd → 잠금 → Taping 1회). 각도 조건·놓기 요구 제거, 테이프 판정 불변. Prefab 0.03/40/50 → `endSnapRadius: 0.08`. 메뉴 1이 `Guide_Arrow`를 1회 삭제. [기록](1103_압박붕대_붕대끝_거리SNAP.md).
- 컴파일·메뉴 1/2·Play 미확인, 미커밋. 확인 ①~⑨ 미확인.

## Resume pointer — hold-to-wrap bandage of 2026-11-03

- 고리 통과식 감기를 영역 유지식 자동 감김으로 대체했다. 롤 중심이 `PB_WrapGuide`의 감기 영역(`zoneCentre`·`zoneSize`) 안에서 추적된 손에 선택돼 있으면 Application이 `wrapSeconds`(5 s) 동안 진행을 누적하고, 감긴 띠·진행률이 같은 값을 쓴다. 감기~테이프는 드레싱 GRAB 불필요(배치만 유효). [기록](1103_압박붕대_감기_영역유지식_전환.md).
- 메뉴 1이 `Guide_Path`→`Guide_WrapZone`, `Practice/ContactProgress`→`Practice/StepProgress` 이름을 한 번 바꾼다. 컴파일·메뉴 1/2·Play 미확인, 미커밋. 확인 ①~⑩ 미확인.

## Resume pointer — direct pressure zone and progress line of 2026-11-03

- 직접 압박 영역 반경 0.12 m·높이 0.22 m(아래 0.03 m·5 s 유지). C# 기본값 + Prefab 저장값 갱신, 씬 override 없음. 진행률 줄 `압박 유지 N%`(Guide 단독, `ContactProgress` 읽기만). 안내판 레이아웃 개정 6(진행률 줄 추가, 확인 카드만 아래로). [기록](1103_압박붕대_직접압박_판정완화_진행률.md).
- 컴파일·메뉴 1/2·Play 미확인, 미커밋. 확인 ①~⑧ 미확인.

## Resume pointer — pre-wrap check input timing fix of 2026-11-03

- 마감 §2.2 해결: 확인 입력은 입력 순간 현재 데이터로 1차 판정 후 같은 프레임 Link LateUpdate(104, 장비 갱신 뒤)에서 재판정해 기록, 아니면 폐기. Dressing에 `IsHoldValidNow`·`IsInputFocusedNow`(Refresh와 같은 판정식, 상태 변경 없음). [기록](1103_압박붕대_감기전확인_입력판정_수정.md).
- 컴파일·Play 미확인, 미커밋. 확인 ①~⑨ 미확인. 마감 §2.3(③~⑦ Dressing 조건)은 미착수.

## Resume pointer — pressure bandage course UI simplification of 2026-11-03

- 사용자 요청으로 교육 화면을 단계 박스 8개(현재 하나만 강조) + 단계 줄 + 행동 한 문장 + 확인 카드 한 항목으로 바꿨다. [교육 UI 간소화 기록](1103_압박붕대_교육UI_간소화_구현기록.md)을 먼저 읽는다.
- 안내판 레이아웃 개정 4(640×560). 메뉴 1이 한 번 적용하고 개정 3 전용 텍스트를 제거한다. 상태판 상세 줄은 숨김(`writesDeviceLines` 끔). Link·Runner·완료 조건 변경 없음.
- 컴파일·메뉴 1/2·Play·Simulator·헤드셋 모두 미확인, 미커밋. 확인 순서는 기록 §9. 아래 감김 경로 수정의 사용자 확인도 별도로 미보고.
- 후속(정적 검토 3건): 시작·확인 버튼 영역 겹침 → 레이아웃 개정 5(개정 4 안내판은 시작 설명·준비·시작·다시하기만 1회 재배치), 장비 표식은 현재 행동 기준(`Guidance.equipmentMarks`), 끝·테이프는 `EndCheck`·`TapeCheck` 기반 교정·놓기 안내. 기록 §11, 확인 §11.5 ①~⑧ 미확인.
- 후속 2: 끝·테이프를 잡은 손의 추적 소실 시 추적 복구 안내 우선, 장비·잡기 표식 숨김(Application 읽기 전용 `EndHandTracking`·`TapeHandTracking` 추가). 놓기 확정 조건 불변. 기록 §12, 확인 ①~⑧ 미확인.

## Resume pointer — wrap path cache fix of 2026-11-03

- 사용자 "감김경로부터 수정" 요청 범위에서 `PressureBandageWrapPath.cs` 직접 수정. [감김 경로 캐시 수정 기록](1103_압박붕대_감김경로_캐시수정.md)을 먼저 읽는다. 일반 코드 전달 정책 전체를 변경한 것은 아니다.
- 기존 `Editor-prev.log`에서 경로 검사 PASS → 스크립트 재컴파일/재로드 → FAIL 3건 흐름 확인. readonly 프로파일 배열은 초기화되지만 private `prepared`는 복원 가능한 캐시 불일치 경로를 수정했다: `prepared`·`widened`에 NonSerialized, OnValidate에서 캐시 무효화. 경로 수학식·검사 기준·씬·모델 변경 없음.
- 사용자 실행 확인 전·미커밋. 다음은 재컴파일 → 메뉴 2 → 재컴파일/재로드 후 메뉴 2 → 메뉴 1/2 → Simulator. 실행은 계속 사용자 담당. 감기 전 확인·중간 단계 조건 문제는 아직 수정하지 않음.

## Resume pointer — close of 2026-11-02

- 후속 기록: 오늘 개발일지 `D:\TeamProject\DevLog\2026-11-02_B_지혈대_압박붕대_VR튜토리얼_개발일지.md` 작성 완료. 작성 시 Git에서 3챕터 커밋 `0a000b9`와 이후 병합 HEAD `d60be6a`를 확인했다. 아래 최초 마감의 HEAD·미커밋 표현은 이력이며, 수정 3건의 반영·실행 통과를 뜻하지 않는다.

- 사용자 지시로 오늘 작업 종료. 먼저 [1102_작업마감_진행상황.md](1102_작업마감_진행상황.md)를 읽는다. HEAD `7a182d7`, 3챕터는 종료 당시 미커밋이다. 이후 커밋·사용자 변경을 재확인한다.
- 다음 수정 필요: 기존 Editor.log의 감김 경로 검사 FAIL 3건(피부 여유 -42.3 mm, 팔 축 거리 0.000 m, 안쪽 롤 출구 거부 실패), 감기 전 확인이 이전 LateUpdate의 유지 상태로 수락될 수 있는 경로, 중간 단계의 Dressing 조건 누락과 누적 조건 설명 불일치. 코드 수정은 하지 않았으며 세부 근거·재개 순서는 마감 문서 §2·§4.
- 사용자 추가 검사 보고서는 `<message truncated>`로 끝나 FAIL 줄·최종 결과가 없다. FAIL 근거는 별도 기존 로그다. 최신 코드 컴파일·교육 완주·반복 회차 통과는 미확인. 실행 확인은 계속 사용자 담당이다.

## Historical resume pointer — pressure bandage chapter 3 of 2026-11-02

- 2챕터 커밋 `7a182d7` 뒤 압박붕대 3챕터(교육 과정 전체 연결: 시작 잠금 → 상황·도구 확인 → 드레싱 배치 → 직접 압박 연습 → 감기 전 확인 → 감기 → 끝·테이프 고정 → 처치 후 확인 → 최종 확인 → 완료 → 완료 후 다시하기)를 구현했다. [1102_압박붕대_3챕터_교육과정_연결_구현기록.md](1102_압박붕대_3챕터_교육과정_연결_구현기록.md)를 읽는다.
- 컴파일·메뉴 1/2·Play·Simulator·헤드셋 모두 미확인, 미커밋. 재개는 구현 기록 §14 ①~⑩. 커밋된 2챕터도 사용자 실행 확인 결과는 별도 미보고.
- 교육 연결은 `Assets/B/Scripts/BleedingControl/PressureBandage/Tutorial/`(별도 어셈블리, 장비는 이를 참조하지 않음). 공통 Training은 확장하지 않았다. 조건은 모두 State.
- `B_PressureBandage_Tutorial`은 이제 교육 씬이다(안내판 `writesGuidance` 끔). 장비 조작 확인은 교육 시작 뒤 해당 단계에서 한다.

## Historical resume pointer — pressure bandage chapter 2 of 2026-11-02

- 1챕터 사용자 검증 완료 보고(커밋 `5e59ab9`) 후 압박붕대 2챕터(롤 잡기·순차 감기·풀린/감긴 띠 표현·붕대 끝 배치·테이프 고정·장비 초기화 진입점)를 구현했다. [1102_압박붕대_2챕터_붕대감기_고정_구현기록.md](1102_압박붕대_2챕터_붕대감기_고정_구현기록.md)를 읽는다.
- 컴파일·메뉴 `B/Pressure Bandage/1`, `2`·Play·Simulator·헤드셋 모두 미확인, 미커밋. 재개는 구현 기록 §17 ①~⑩.
- 붕대 적용 상태 소유자는 `PressureBandageApplication` 하나(감기·끝·테이프·취소·초기화 조정). 표시는 `PressureBandageRibbons`·`PressureBandageGuide`·안내판이 읽기만 한다. 연속 접촉 시간은 계속 `PressureDressing`만 센다.
- 3챕터 연결점: `PressureBandageApplication.ResetEquipment()`(교육생 버튼 없음). 시작화면·입력 잠금·Runner·감기 전후 확인·최종 확인·완료·다시하기 UI는 미구현.
- FBX importer(Read/Write 포함) 미변경. 띠는 런타임 작업 메시를 생성한다.
- 후속 보완(정적 검토 지적): `ResetEquipment` 미할당 변수(CS0165) 수정, 롤 크기·실제 출구·연결 띠 판정(`LooseValid`)과 롤 여유를 반영한 고리 위치, 감기 중 드레싱 이동 시 외곽 갱신·밀림/회전 한계 초과 시 적용 무효화, 테이프 선택 정책 서술 정정. 구현 기록 §18, 사용자 확인 §18.8 ①~⑨ 미확인. 2차 보완(롤 이동·회전 구간 검사 `RollSweepClear`, 테이프를 든 손의 안내)은 §19, 확인 §19.6 ①~⑥ 미확인.

## Historical resume pointer — pressure bandage chapter 1 of 2026-11-02

- 사용자가 모델(Blender 제작 완료, Unity 미확인)을 전달한 뒤 압박붕대 1챕터(장비 구성·잡기·드레싱 배치·직접 압박 동작 연습)를 구현했다. [1102_압박붕대_1챕터_드레싱_직접압박_구현기록.md](1102_압박붕대_1챕터_드레싱_직접압박_구현기록.md)를 읽는다. 아래 설계 포인터의 "코드 작성 없음"은 이 챕터에 한해 대체된다.
- 장비 동작 확인 씬이다. 시작화면·Runner·감기·고정·최종 확인·완료·다시하기는 미구현. 컴파일·메뉴 `B/Pressure Bandage/1`, `2`·Play·Simulator·헤드셋 모두 미확인, 미커밋. 재개는 구현 기록 §14 ①~⑧.
- 연속 접촉 시간 소유자는 `PressureDressing` 하나. 한 손 유지(`IsHoldValid`)와 두 손 접촉(`IsTwoHandContactValid`)·연습 이력(`HasCompletedContactPractice`)은 분리돼 있다.
- 보고만 한 경계 사항: 압박붕대 `.blend`·검사 JSON·Previews가 임포트 폴더에 있음(Source~ 아님). 이동하지 않았다.
- 후속 보완(정적 검토 지적): 완료 문구는 이력이 아닌 현재 연속 접촉 비율로 표시, `PB_WoundMarker`는 `PB_WoundTarget` 자식(상대 오프셋), 피부 측정 예외 시 메뉴 중단으로 기록 정정. 구현 기록 §17, 사용자 확인 §17.5 ①~⑥ 미확인.

## Historical resume pointer — pressure bandage design of 2026-11-02

- 지혈대 다시하기·표식 가시성 수정은 사용자 확인 완료 보고, 커밋 `6b81667`을 확인했다. 사용자는 지혈대 추가 작업이 불필요하다고 했으며 다음 범위는 압박붕대 설계다. 아래 미확인·미커밋 표현은 당시 이력이다.
- [1102_압박붕대_교육과정_설계.md](1102_압박붕대_교육과정_설계.md): 사용자 선택은 제품 미정, 드레싱·롤붕대 기본형. 교육 전체 흐름·조작·복구·완료 후 다시하기를 설계했다. 교육 중 다시하기를 넣지 않는 것은 이번 초안의 선택이며 압박붕대에 대한 사용자 확정 지시로 확대하지 않는다.
- **사용자 지시: 모델링은 추후 Blender에서 진행하므로 지금 모델링하지 않는다.** 이번 작업은 B 문서만. 코드·모델·씬·Prefab 생성·수정, Unity 실행·검증, 스테이징·커밋 없음. 설계를 구현 프롬프트 요청이나 구현 승인으로 해석하지 않는다.

## Historical resume pointer — tourniquet retry after completion and step markers of 2026-11-02

- 최종 확인·완료는 커밋 `08dcd67`, 사용자 검증 완료 보고. 이후 완료 후 다시하기와 단계별 조작 표식을 구현했다. [1102_지혈대_완료후다시하기_단계표식_구현기록.md](1102_지혈대_완료후다시하기_단계표식_구현기록.md)를 읽는다. 남은 범위는 [1102_남은작업_전체목록.md](1102_남은작업_전체목록.md).
- 컴파일·메뉴 1/2·Play·Simulator 모두 미확인, 미커밋. 재개는 구현 기록 §8 ①~⑨.
- 교육 중(실습·최종 확인) 다시하기·확인창은 사용자 지시로 범위 제외. 이전 문서의 중간 다시하기 계획은 이력이다.
- 다시하기 조정 주체 `TourniquetTrainingReset`(시작 게이트 객체). 표식은 Guide가 텍스트와 같은 안내 대상으로 켠다. 패널 전환은 `TourniquetStartScreen`만.
- 후속(사용자가 다시하기·표식 검증 완료 보고): 표식 C 모양 가시성 수정 — 전용 셰이더 `Assets/B/Shaders/TQ_GuideMarker.shader`(양면, 깊이 카메라 쪽 0.05 m 당김). 구현 기록 §10, 컴파일·헤드셋 미확인.

## Historical resume pointer — tourniquet final check and completion screen of 2026-11-02

- 6단계·Runner 연결은 커밋 `4135f17`(아래 6단계 포인터의 "미커밋"은 당시 기록, 6단계 사용자 실행 결과는 미보고). 이후 최종 확인·완료 화면을 구현했다. [1102_지혈대_최종확인_완료화면_구현기록.md](1102_지혈대_최종확인_완료화면_구현기록.md)를 읽는다.
- 컴파일·메뉴 1/2·Play·Simulator 모두 미확인, 미커밋. 재개는 구현 기록 §10 ①~⑩.
- 최종 확인 단계 `tq-final-check` = 장비 6상태 + 교육생 대상 `tq-trainee`의 `final-confirmed`(State, Action 아님). 입력 순간 재검사·토큰 귀속·철회는 `TourniquetTrainingLink`. 패널 전환은 `TourniquetStartScreen`만.
- 다시하기·과정 선택·결과 저장은 미구현. 시간 기록은 부착 기준 유지.

## Historical resume pointer — tourniquet six steps and TrainingRunner of 2026-11-02

- 시작화면은 사용자 검증·커밋 `c11f8a2` 완료(아래 시작화면 포인터의 "미커밋"은 당시 기록). 이후 6단계 진행·Runner 연결을 구현했다. [1102_지혈대_교육6단계_Runner연결_구현기록.md](1102_지혈대_교육6단계_Runner연결_구현기록.md)를 읽는다.
- 컴파일·메뉴 1/2·Play·Simulator 모두 미확인, 미커밋. 재개는 구현 기록 §9 ①~⑨. 시작화면 검증 완료를 이번 변경 통과로 확대하지 않는다.
- 단계는 장비 현재 상태 누적 조건(단계 i = 신호 1..i). 텍스트 소유: Guide = 조작 패널 Title·Instruction, StatusBoard = Notice·Status·Detail(`writesGuidance=false`).
- 최종 재확인·완료 화면·다시하기·과정 선택은 미구현. 시간 기록은 부착 기준 유지.

## Historical resume pointer — tourniquet start screen of 2026-11-02

- 사용자 요청으로 지혈대 교육 시작화면만 구현했다. [1102_지혈대_교육시작화면_구현기록.md](1102_지혈대_교육시작화면_구현기록.md)를 읽는다. 새 어셈블리 `Tourniquet/Tutorial`(시작 게이트·시작화면), 문구 상수, 구성/검사 메뉴 갱신.
- 보고서·실제 코드 정적 대조 후 사용자가 교육 시작화면 검증 완료를 보고했다(2026-11-02). 미커밋. 개별 §8 ①~⑧·손 조합·장치·실기 결과는 별도 미보고이며 에이전트 실행 검증은 하지 않았다. 다음 착수는 단계별 교육 진행·Runner 연결이다.
- Runner·완료·다시하기·과정 선택은 미구현. 시간 기록은 부착 기준 유지.

## Resume pointer — close of 2026-10-29

- 먼저 [1029_작업마감_진행상황.md](1029_작업마감_진행상황.md)를 읽는다. 아래 구현 당시의 미구현·미생성·미확인 서술은 이력이며, 현재 상태는 마감 문서와 최신 사용자 보고를 우선한다.
- 1챕터는 사용자 기능 확인 완료·커밋 `41d0de5`. 마감 문서 작업 시작 HEAD는 `665961f`다. 기본 도형 Prefab·씬은 이미 존재한다. 모델 교체·2챕터 코드와 모델 자산은 작업 트리에 있으며 미커밋이다. 다음 세션에 git status와 HEAD를 다시 확인한다.
- 최신 4건(CS0136, 기록→마감 순서, 부품 해제 프레임 입력, 안내판 revision 2)은 실제 코드와 대조해 반영을 확인했다. 추가로 확정한 수정 필수 오류는 없었지만, 최신 수정본의 컴파일·구성/검사 메뉴·Simulator는 모두 미확인이다. 이전 1챕터 사용자 확인을 최신 모델·2챕터 통과로 확대하지 않는다.
- 재개 순서: 사용자 임포트·컴파일 → 구성/검사 메뉴와 재실행 → [모델 교체·2챕터 기록 §9.6](1029_지혈대_모델교체_2챕터_구현기록.md#96-사용자-직접-확인모두-미확인) ①~⑫. A1~A8·B1~B9도 미확인으로 유지하며, 옛 B8은 §9.6의 새 순서로 대체한다. 사용자가 결과를 보고한 항목만 상태를 올린다.
- 현재 가상 조작 순서: 부착 → 밴드 준비 → 막대 회전·고정 → 시간 기록 → 마감 → Done. 의료 절차 확정이 아니다. TIME 반전 가능성·마감 후 패치 가림·변형 관통 미확인을 유지한다. NearFar 이외 입력의 완료 제한과 OnBeforeRender 미반영도 기록돼 있다.
- 3챕터 Runner·교육 UI·재시작 UI 연결은 후속 작업이다. `ResetToInitial()` 경로는 있으나 사용자 동작 확인·교육 UI 연결은 아직 없다. 직접 압박·압박붕대와 과정 허브는 설계 상태다. 다음 기능·수정 프롬프트를 자동 작성하거나 구현하지 않는다.
- 실기는 추후 진행한다는 사용자 지시를 유지한다. K1 Worn 끈 풀림 보류, 기존 AED·K1 및 음성 보류도 유지한다. 에이전트는 실행 검증·코드 변경·스테이징·커밋을 하지 않는다.
- 오늘 개발일지: `D:\TeamProject\DevLog\2026-10-29_B_지혈대_VR튜토리얼_개발일지.md`. 오늘 마감 작업은 B 문서와 개발일지만 갱신했으며 기존 코드·씬·폰트·모델 변경을 보존했다.

## Historical resume pointer — tourniquet model replacement and chapter 2 of 2026-10-29

- 사용자 요청으로 완성 모델 `Tourniquet_Model.fbx`(v1.1)를 기존 Prefab 래퍼에 연결(A)하고 2챕터 밴드 당김·막대 회전·고정·마감·시간 기록(B)을 Assets/B 안에 구현했다. [1029_지혈대_모델교체_2챕터_구현기록.md](1029_지혈대_모델교체_2챕터_구현기록.md)를 읽는다.
- 사용자 보고: 1챕터 기능 확인 완료(개별 체크리스트·실기 미보고). 1챕터 메뉴는 실행되어 자산이 존재한다. 아래 1챕터 포인터의 "메뉴 실행 전 자산 미생성"은 과거 기록이다.
- 사용자 첫 임포트에서 `TourniquetDevice.cs` CS0136 1건이 보고되어 2차 수정(구현기록 §9)을 했다: 변수명, 순서 밴드→막대 회전·고정→시간 기록→마감→Done(가상 흐름), 부품 해제 프레임 attachUpdated 표본 반영, 안내판 레이아웃 리비전 2. 2차 수정본의 컴파일·메뉴 재실행·Simulator·실기 모두 사용자 미확인.
- 사용자 메뉴 실행으로 씬·폰트 자산에 변경이 있다. 보존한다.
- 정적 지적(미해결): TIME 인쇄 거울 글씨 가능성, 덮은 마감부에서 패치 글자가 가려짐(기록은 막대 고정 후·덮기 전), 변형 근사의 관통 미검증. Runner·교육 단계·다시하기 UI는 3챕터.

## Historical resume pointer — tourniquet chapter 1 of 2026-10-29

- 사용자 요청으로 지혈대 1챕터(모델 구성·잡기·배치)를 Assets/B 안에 구현했다. [1029_지혈대_1챕터_잡기배치_구현기록.md](1029_지혈대_1챕터_잡기배치_구현기록.md)를 읽는다. 아래 설계 포인터의 "신규 장비 아직 구현하지 않음"은 이 챕터에 한해 대체된다.
- 컴파일·구성 메뉴(`B/Tourniquet/1`, `2`)·Simulator·실기 모두 사용자 미확인. 메뉴를 실행하기 전에는 Prefab·씬·재질·메시가 생성되지 않은 상태다.
- 제품 미선정 기술 시제품, 허용오차·영역은 개발용 VR 설정이다. 밴드·막대·고정·시간 기록·교육 연결·다시하기는 미구현.

## Historical resume pointer — design of 2026-10-29

- 최신 작업 선택: 사용자는 제독을 제외하고 **지혈대 → 직접 압박·압박붕대 → 교육 선택·복귀 통합** 순서의 설계에 동의했다. [1029_출혈처치_교육과_과정통합_설계.md](1029_출혈처치_교육과_과정통합_설계.md)를 읽는다.
- 이는 설계 승인이다. 신규 장비·교육·허브는 아직 구현하지 않았으며, 코드 직접 작성 또는 구현 프롬프트 제공 요청으로 확대하지 않는다. 기존 코드 전달 정책을 유지한다.
- 사용자 보고: 기존 기능은 시뮬레이션으로 확인했으며 실기 확인은 추후 진행한다. 실기를 오늘의 우선 작업으로 다시 배정하지 않는다. 개별 체크·장치별 결과를 추정하지 않는다.
- 다음 착수 단위는 지혈대 장비 모델·조작 규격 확정이다. 실제 제품·세대·사용설명서·적용 교안과 모델 부품·피벗·조작점·배치 앵커를 맞춘다. 제독·음성·평가·CPR·보호의는 이번 설계에 추가하지 않았다.
- 기존 AED·K1 구현 상태와 보류 사항은 아래 10/28 마감과 각 마감 문서를 따른다. 옛 문서의 미구현·미확인 표현을 현재 상태로 되돌리지 않는다.
- 설계 시작 시 HEAD는 `bae7fd1`. 폰트 `Assets/B/Fonts/NotoSansKR-Regular SDF.asset`와 K1 씬 `Assets/B/Scene/B_K1_Tutorial.unity`에 기존 수정이 있었으며 보존했다. 이번 작업은 B 문서만 변경하고 실행 검증·스테이징·커밋하지 않는다.

## Resume pointer — close of 2026-10-28

- Start with [1028_작업마감_진행상황.md](1028_작업마감_진행상황.md). It supersedes the historical 10/27 pointer and chapter-time unimplemented/unverified/uncommitted statements below. Preserve those as history, not current tasks.
- Closeout HEAD: `692f916`. Today's B commits: `cbfb198` simplified wear, `db38038` five-step training/UI and official-source completion guidance, `3c7837e` avatar/mirror/rear view, `692f916` retry/equipment reset. Working tree was clean before today's closeout document edits; recheck next session.
- User confirmed Worn and the equipment functions, the training/completion UI, mirror operation after rerunning the setup, and finally retry verification complete. Do not keep assigning the old no-strap-pull or unimplemented retry task. Do not infer per-checklist or HMD success from these general reports.
- Final wear: fully extract -> approach face -> snap and prepare folded straps automatically -> let go of body -> move either strap point with either hand roughly behind the head -> Worn. Manual unfolding is optional; no crown/ordered-zone requirement. T/Activate while holding the body explicitly releases it; it is not unfolding or retry.
- Five training steps: closure release, lid opening, extraction, face placement, straps. Worn immediately shows completion plus source-based tightening/check guidance. No actual tightening/seal test or acknowledgement requirement. Preserve the user's SeatStrapsInstruction.
- Retry is implemented: completion Retry -> stop Runner -> end K1 XRI selections -> restore wear/extraction/carrier -> clear five facts -> start screen. A new course starts only from Start. Avatar/mirror/rear view and their adjustments persist. Scene reload is not the reset mechanism.
- **User explicitly deferred Worn strap loosening on hand release. Do not investigate/fix it as the next task unless requested.** Retry resets only on request; it does not fix normal Worn behaviour.
- Avatar uses the original Generic mannequin rig; HMD/hands tracked, remaining body pose estimated. Mirror is a monocular centre-eye texture, not per-eye stereo. Quest 2 + Link, stereo/depth/clipping/performance remain separately unverified.
- Static review found a conditional guide issue: runtime WorldBounds button-overlap check can falsely block a rolled board while menu 6 uses board-space rectangles. User's verification report is not proof this condition was fixed. Keep as a static, not runtime-reproduced, issue; do not change code proactively.
- Current scene/prefabs stay `Assets/B/Scene/B_K1_Tutorial.unity`, `Assets/B/Prefabs/K1_Carrier_Training.prefab`, `K1_GasMask_Training.prefab`. Menus 3/4 equipment, 5/6 guide/retry, 7/8 avatar/views. Existing scene already contains the guide/retry and views; do not assume menus must be run again without evidence of stale configuration.
- Next work is user-selected. HMD validation and conditional guide geometry review are remaining candidates, not authorization. Preserve bounds ownership, source assets, common Runner and AED.
- Verification remains user-run only; no Unity/Simulator/batchmode/tests/builds or verification clones. Code changes remain natural-language prompts only on explicit request. Today's document edit does not authorize code changes.
- Development diary: `D:\TeamProject\DevLog\2026-10-28_B_K1_방독면_VR튜토리얼_개발일지.md`. Closeout edits documents only; no staging/commit.

## Historical resume pointer — close of 2026-10-27 (superseded)

- Start with [1027_작업마감_진행상황.md](1027_작업마감_진행상황.md). Current K1 status supersedes chapter-time "not yet implemented / no user run / uncommitted" statements. Preserve those reports as history.
- Closeout HEAD: `03237f5` (main merge). B feature commits: `20bbb61` carrier/storage, `9d49ee9` closure/lid, `0843b31` extraction/free grab, `a97cb2e` face snap/one-hand wear. Working tree was clean before documentation edits. Recheck status next session; do not assume these hashes remain HEAD.
- Current scene: `Assets/B/Scene/B_K1_Tutorial.unity`. Prefabs: `K1_Carrier_Training.prefab`, `K1_GasMask_Training.prefab` under `Assets/B/Prefabs`.
- User confirmed initial storage generally, lid opening, then extraction implementation. Final one-hand wear is NOT confirmed: user reported "줄이 당겨지지 않는다"; no later successful unfolding-to-Worn report. Committing the feature is not verification. First resume task is to distinguish body/strap selection, clearance blocking and hand path; do not start another feature automatically.
- Final input: one hand holds the body, the other grips ONE strap point and pulls to unfold. Both hands initially grip different targets; they do not each grip a strap. After face snap, the body hand may let go; one strap hand takes the harness over the crown to the back. T/Activate is explicit face release only, never unfolding. Use [wear record §11.3](1027_K1_착용_얼굴스냅_머리끈_구현기록.md#113-한-손-끈-조작-정정-확인-2026-10-27), not obsolete two-strap/automatic unfolding instructions.
- Logs show final L/R route 101 + unfolding 21 points, bounds support 322 baked poses, component data storage and static menu passes. Latest final-version interaction evidence reaches Extracted only. Earlier Unfolding/Ready/FaceHeld logs belong to the prior input version. Do not promote them to final-version success or claim final-source compilation correspondence without evidence.
- Bounds are serialized on K1MaskHarnessPose and applied to Renderer.localBounds at OnEnable. Editor-only localBounds assignment plus Prefab saving is not the persistence mechanism. Keep head/body ownership, snap rearm hysteresis and folded storage/extraction pose.
- Static extraction baseline caveat remains in closeout §6: downward hand movement while holding at storage with the lid closed may retain the old baseline, causing dead travel after reopening. It is not runtime-reproduced or confirmed fixed. Keep separate from the fixed closed-lid pull-through issue.
- Verification belongs to the user. Do not run Unity/Simulator, batchmode, tests or builds, or create/reuse verification clones. Static reading is allowed. Simulator OFF is the editor validator's saved-scene condition; PC Simulator interaction and HMD validation are separate. OpenXR form-factor errors were logged; Quest 2 + Link success is unreported and hardware verification stays deferred until full functionality.
- K1 tightening, Runner/UI/evaluation/retry/audio and actual seal/protection validation are outside today's completed scope. Source FBX/JSON and other members' assets remain protected. Future implementation requires an explicitly requested Korean natural-language prompt; no direct code writing.
- AED remains at the [1023 closeout](1023_작업마감_진행상황.md): fixed shock-advised cycle through explanation/retry, final state ShockDeliveredAwaitingNext=13, no CPR/no-shock/repeated-care implementation. Preserve patient model, wrapper/material/pad-anchor decisions. Voice remains deferred; the old retry warning is historical, not a new reproduced defect.
- Development diaries: `D:\TeamProject\DevLog`; today's diary: `2026-10-27_B_K1_방독면_VR튜토리얼_개발일지.md`. Closeout changes documents only, with no staging/commit. Preserve any newly observed user/team changes.

## Document ownership and access

- The user is team member B.
- Documents under `Docs/B` may be read, created, and edited within the user's requested task scope, including this AGENTS.md.
- Documents under other members' directories within `Docs`, including `Docs/A` and `Docs/C` if added, are read-only. Apply the same rule regardless of directory name casing.
- Do not create, edit, delete, rename, move, or overwrite other members' documents. Do not move them into B's directory to bypass this restriction.
- Do not create shared or project-root instruction files to override this ownership policy.
- This is an agent working policy, not an operating-system ACL change. Do not change filesystem permissions unless explicitly requested.
- This document ownership policy does not grant additional write access to code, scenes, assets, or project settings outside Docs/B. Follow the user's separately authorized implementation scope for those files.

## Code delivery policy — user instruction, 2026-10-21

- Do not directly write, create, edit, or apply project code. This includes implementation scripts, test code, generated code, and code changes made through tools or other agents.
- Provide coding instructions only as a natural-language implementation prompt, and only when the user explicitly requests that prompt. Do not proactively provide code or implementation prompts.
- Requests such as "explain the implementation", "next step", or "proceed" do not authorize direct code changes. Explain the functions, workflow, setup, and verification criteria within the requested scope.
- Reading and analyzing existing code is allowed. B-owned planning and instruction documents may still be updated within the user's requested task scope.
- A requested implementation prompt should describe the objective, allowed paths, required behavior, constraints, and acceptance criteria; do not replace it with a direct source-code solution.
- Preserve code already written before this rule. Do not delete or revert it unless the user explicitly requests that change.
- This delivery policy takes precedence over the writable asset boundary below and over earlier implementation guidance. `Assets/B/**` being writable does not authorize direct code changes.

## Verification policy — user instruction, 2026-10-23

- 앞으로 실행 검증은 사용자가 직접 수행한다. 에이전트는 원본·복제본 모두에서 Unity Play, Simulator 조작, batchmode, 자동 테스트, 빌드 등의 실행 검증을 대신 수행하지 않는다.
- 검증용 복제 프로젝트·임시 프로젝트를 새로 만들지 않는다. 기존 복제 프로젝트도 검증에 재사용하지 않는다.
- 에이전트는 기존 코드·씬·Prefab·설정·로그·테스트 결과 파일의 읽기 전용 정적 검토를 수행할 수 있다. 파일 검토를 실행 검증으로 표현하지 않는다.
- 구현 프롬프트와 작업 계획에는 사용자가 직접 확인할 순서·입력·기대 결과·실패 기준을 제공한다. 구현 담당 에이전트나 도구에 검증 실행을 위임하지 않는다.
- 결과 보고는 정적 검토 결과, 사용자 실행 결과, 미검증 항목을 구분한다. 사용자 확인 전에는 실행 검증 완료·통과로 기록하지 않는다.
- 과거 복제 프로젝트의 검증 결과는 당시 이력으로 보존한다. 새 변경의 검증 완료 근거로 확대하지 않는다. 기존 복제본·테스트 자료의 삭제는 이 지시로 수행하지 않는다.
- 이 정책은 기존 문서·일반 작업 규칙·구현 프롬프트의 에이전트 실행 검증 및 복제 프로젝트 검증 안내보다 우선한다. 코드 직접 작성 금지와 자산 소유권 규칙은 그대로 유지한다.

## Review scope — user instruction, 2026-10-23

- 기능 검토와 수정 우선순위는 빌드된 VR 프로그램을 사용하는 교육생의 조작·실제 운영 흐름을 기준으로 판단한다. 사용자에게 발생 가능한 경로와 영향을 구체적으로 설명한다.
- Inspector에서 오브젝트·컴포넌트를 강제로 끄기, 런타임 참조 수동 삭제 등 개발자 전용 조작을 일반 VR 사용자 결함이나 필수 수정으로 분류하지 않는다. 개발 도구 대응은 사용자가 요청하거나 실제 운영 기능과 연결되는 근거가 있을 때 별도로 다룬다.
- 컨트롤러 추적 소실·실제 앱 수명 주기 등 배포 환경에서 발생할 수 있는 상황은 검토 대상이다. 개발자 강제 조작만으로 그 문제가 실제 사용 중에도 발생한다고 추정하지 않는다.
- 정적 검토에서 가능한 경로, 사용자 실행으로 재현한 문제, 알려진 근사 판정의 제약을 구분한다. 근거가 부족한 방어 기능·예외 처리를 늘리지 않는다.

## Modeling tool — user instruction, 2026-11-02

- 앞으로 원본 3D 모델의 제작·수정 등 모델링 작업은 Blender를 사용한다. 모델링 구현 프롬프트와 전달 기준에도 이를 명시한다.
- Unity 임포트·재질 연결·상호작용 및 게임 실행 중 띠 변형은 기능 구현으로 구분한다. 이 규칙을 이유로 기존 런타임 메시 생성·변형 기능을 임의로 교체하지 않는다.
- 기존 B 자산 경계·원본 보존·코드 전달·사용자 실행 검증 정책은 유지한다.

## Asset ownership and access — user instruction, 2026-10-21

- Reading is allowed throughout `Assets`. Only paths inside `Assets/B/**` may be created, written, edited, deleted, renamed, or moved within the user's requested task scope. This boundary is not an instruction to begin implementation when the task is planning only.
- All other paths under `Assets` are read-only, including other members' folders, shared folders, root-level files, and their `.meta` files. Apply the rule regardless of directory name casing.
- `Assets/B.meta` is outside `Assets/B/**` and is therefore read-only; `.meta` files inside `Assets/B/**` are within B's writable scope.
- Before any necessary change outside `Assets/B/**`, explain the exact target, why the change is needed, and how it will be changed; ask the user and wait for explicit approval. Approval covers only the described change.
- Do not bypass this boundary through tools, Unity import/serialization, automatic fixes, asset moves, or generated `.meta` changes. Reading or referencing shared assets is allowed; a needed working copy may be created inside `Assets/B/**` without changing the source.
- This policy grants no additional write access to `Packages`, `ProjectSettings`, or other paths outside `Assets`. Existing document ownership and task authorization still apply.
- Preserve existing user/team changes. Do not revert, stage, or include them as part of B's work without authorization.

Subtitle requirement: keep subtitle strings as constants in a dedicated static class (`TrainingSubtitles`). Do not duplicate subtitle literals in training scripts or Inspector assets. Data-driven steps refer to stable subtitle IDs resolved to that class; presentation controls display and audio playback.

## Communication: caveman

- Use the `caveman` skill for project communication; default intensity: `full`.
- Skill location: `C:\Users\MBC\.agents\skills\caveman\SKILL.md`.
- Keep responses concise in Korean. Preserve technical accuracy, assumptions, tradeoffs, and verification results.
- Write all implementation/fix prompts and task explanations in Korean, per the user's 2026-10-27 instruction. Preserve API names, file paths and necessary original identifiers; do not switch prose to Japanese.
- Use full explanations when compression would create ambiguity. Write code, commits, and PRs normally.
- Continue until the user requests `stop caveman` or `normal mode`.

## Commit message preference — user instruction, 2026-10-21

- From the next commit message onward, describe the actual changes in sufficient detail, using a subject and descriptive body when appropriate.
- Omit verification details, test results/counts, and validation status from commit messages unless the user explicitly requests them.
- This preference applies to commit messages, not ordinary implementation or review reports. Do not rewrite an existing commit or run git commit merely to apply this preference.

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:

- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

Project-specific user requirement: prioritize reusable, extensible code. This takes precedence over the single-use abstraction restriction below when a shared interaction or training contract is needed. Separate training progression, interaction reporting, and presentation. Keep extension points small and grounded in the agreed content; do not add speculative frameworks or features.

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:

- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:

- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:

- "Add validation" → "Statically review invalid-input handling; provide user execution checks"
- "Fix the bug" → "Identify the cause; provide reproduction and acceptance steps for the user"
- "Refactor X" → "Statically review the change; have the user verify behavior before and after"

Execution of the checks above belongs to the user under the 2026-10-23 verification policy. The agent does not run tests or create verification projects.

For multi-step tasks, state a brief plan:

```less
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.
