# 지혈대 교육 6단계 진행·TrainingRunner 연결 구현 기록 — 2026-11-02

상태: **코드·구성/검사 메뉴 작성, 정적 검토 완료. 컴파일·메뉴 1/2·Play·Simulator·실기 모두 미확인. 미커밋.**

> 후속 기록(2026-11-02): 이 변경은 커밋 `4135f17`에 포함됐다(위 "미커밋"은 작성 당시). 이후 [최종 확인·완료 화면](1102_지혈대_최종확인_완료화면_구현기록.md)이 마감 뒤 `tq-final-check` 단계를 추가해, 마감 직후 Completed와 "6단계 가상 조작 종료" 표시는 더 이상 쓰지 않는다.

기준: 작업 시작 HEAD `c11f8a2`(교육 시작화면, 사용자 검증·커밋 완료), 작업 트리 깨끗. [시작화면 기록](1102_지혈대_교육시작화면_구현기록.md), [시작화면 설계](1102_지혈대_교육시작화면_설계.md), [출혈처치·과정통합 설계](1029_출혈처치_교육과_과정통합_설계.md) §4·§6, [작업 규약](AGENTS.md). 시작화면 검증 완료 보고를 이번 변경의 검증 근거로 쓰지 않는다.

## 1. 범위

교육 시작 → 공통 `TrainingRunner` 시작 → 실제 장비 결과로 6단계 진행 → 마지막 단계 후 최소 종료 표시.

| # | 단계 ID | 이름 | 이 단계가 추가하는 장비 현재 상태(신호) | 장비 판정 출처 |
|---|---|---|---|---|
| 1 | `tq-place` | 지혈대 배치 | `attached` | `device.IsAttached && AttachedTarget == Target` |
| 2 | `tq-band` | 밴드 준비 | `band-prepared` | `operation.IsBandPrepared` (95% 이상에서 정상 놓기) |
| 3 | `tq-turn` | 막대 회전 | `turn-reached` | `WindlassTurnDegrees >= RequiredTurnDegrees` |
| 4 | `tq-secure` | 막대 고정 | `windlass-secured` | `operation.IsWindlassSecured` (하강·진입 완료) |
| 5 | `tq-record` | 시간 기록 | `time-recorded` | `operation.IsTimeRecorded` |
| 6 | `tq-fasten` | 마감 | `strap-fastened` | `operation.IsStrapFastened` (덮임 상태에서 정상 놓기) |

- 대상 ID `tq-device`, 과정 ID `tq-tourniquet-virtual-handling`. 모든 신호는 `operation.IsReady`일 때만 참.
- **조건:** 단계 i는 신호 1..i 모두 `State`(현재 값, true). 6단계는 여섯 신호가 동시에 현재 유효해야 끝난다. 과거 완료 플래그만 모아 완료하지 않는다.
- 교육 연결부는 당김 거리·회전량·고정 판정을 다시 계산하지 않는다. 장비 상태를 강제로 바꾸지 않는다.
- 제외: 대상 확인·도구 선택, 최종 재확인 단계·버튼, 완료 패널, 다시하기·중간 재시작, 과정 선택·복귀, 음성·점수·평가·저장, 교육 시작 기준 시계. 시간 기록은 **부착 시점 기준** 유지(`ElapsedSecondsSource` 미연결).
- 공통 `TrainingDefinition`·`TrainingRunner`·`TrainingContext`·`TrainingTarget`·`SceneBindings`는 변경하지 않았다.

## 2. 변경 파일

| 파일 | 역할 |
|---|---|
| `Tutorial/TourniquetTrainingIds.cs` (신규) | 과정·대상·신호·단계 ID와 순서 표. 구성 메뉴·연결부·표시부의 단일 출처 |
| `Tutorial/TourniquetTrainingLink.cs` (신규) | 장비 현재 상태 → `TrainingTarget` 신호, 회차 이력 별도 보관, Runner 시작·실제 실행 확인 |
| `Tutorial/TourniquetTrainingGuide.cs` (신규) | 조작 패널 제목(단계 줄)·지시(현재 행동) 표시 |
| `Tutorial/TourniquetTrainingStart.cs` | 시작 수락에 Runner 시작 확인 추가, 입력 허용을 실행 상태에 연결 |
| `Tutorial/TourniquetStartScreen.cs` | 시작 전 단계 표시부 실행 확인 추가, 주석 |
| `Tutorial/...Tutorial.asmdef` | `MilTrainSimVR.B.Training` 참조 추가(단방향. 장비 어셈블리는 Tutorial을 참조하지 않음) |
| `Tourniquet/TourniquetStatusBoard.cs` | 작은 연동 지점 `writesGuidance`: 끄면 제목·지시를 쓰지 않고 그 참조는 비어 있어야 함 |
| `Tourniquet/TrainingSubtitles.cs` | 단계 이름 6, 단계 줄 형식, 회전 복구 안내, 종료·중단 문구 |
| `Editor/BleedingControl/TourniquetTrainingSetup.cs` | 메뉴 1: 데이터·Runner·바인딩·대상·연결부·표시부 구성. 메뉴 2: 6단계 검사 구간 |
| 메뉴 1 실행 시 생성 | `Assets/B/Data/TQ_TourniquetTraining.asset`, 씬 `TQ_Training/TQ_TrainingFlow`(+`Target_Tourniquet`) |

`TourniquetDevice`·`TourniquetOperation`의 조작 계산, Prefab, 모델 원본, AED·K1, 공통 Training, A·C·공용 XR·Packages·ProjectSettings는 변경하지 않았다.

## 3. 시작 버튼 → 잠금 해제 처리 순서

1. `TourniquetStartScreen`: 대기 상태·버튼 활성 확인 → 버튼 즉시 비활성 → `TourniquetStatusBoard`·`TourniquetTrainingGuide` 실행·참조 확인.
2. `TourniquetTrainingStart.TryStart`(장비 잠금 상태 유지 중):
   1. 게이트 참조(장비·Operation·연결부·잠금 목록)
   2. 장비 준비(부품 조작 준비, 미부착·미잡힘, Manager, 선택 없음)
   3. `TourniquetTrainingLink.TryBeginRun`: Runner가 이미 Running이면 거부 → 신호·이력 초기화 → `runner.StartTraining()` → **`Status == Running` 이고 현재 단계가 `tq-place`인지 확인**. 아니면 (Running이면 `StopTraining`) 신호 초기화 후 실패.
3. 모두 성공한 뒤에만 `Started`, 눌린 잡기 입력 해제 대기 등록, `StartAccepted`, 시작 패널 → 조작 패널 전환.
4. 어느 단계든 실패: 게이트 `Faulted`, Runner 미실행, 장비 잠금 유지, 시작 패널에 `StartUnavailable`, 원인은 Console 오류.

- `StartTraining()` 호출 자체를 성공으로 보지 않는다. 장비만 풀리거나 Runner만 도는 상태가 생기지 않는다.
- 입력 허용 = 게이트 `Started` **그리고** 연결부가 시작한 회차가 `Running` 또는 `Completed`. `Completed`에서도 허용해 마지막 조작·정상 해제가 끊기지 않는다(Done 이후 부품 조작점은 장비가 이미 닫음). `Stopped`(중단)이면 새 선택 다시 잠금. 잡고 있던 선택은 유지.
- Runner `startOnStart = false`. 완료 후 자동 초기화·시작화면 복귀 없음.

## 4. 현재 상태와 회차 기록

- **현재 상태(Runner 판정용):** 연결부가 매 프레임(`LateUpdate`, 실행 순서 106 > `TourniquetOperation` 105)과 장비 이벤트(`StateChanged`, `Attached`, `Changed`, `BandPrepared`, `WindlassSecured`, `TimeRecorded`, `StrapFastened`)에서 장비 값을 읽어 `TrainingTarget` 신호를 참/거짓으로 덮어쓴다. 래치하지 않는다.
- **회차 이력(표시용):** `WasReached(i)` = 이번 회차에 한 번이라도 유효했음. 단계 완료에는 쓰지 않고 복구 안내 판단에만 쓴다.
- **회차 경계:** Runner `RunId`가 바뀌면 현재 값·이력·대상 신호를 모두 거짓으로 초기화. 시작 전 장비는 잠겨 있고 게이트가 초기 상태를 확인하므로 시작 전 입력이 성공으로 넘어오지 않는다. Play 재실행은 씬 로드로 전부 초기 상태.
- Action 조건·지연 보고를 쓰지 않아 토큰 보관이 필요한 생산자는 없다.
- 이벤트 구독은 `OnEnable`/`OnDisable` 짝, 게이트 필터도 기존대로 짝 등록.

## 5. 빠른 조작·상태 복구

- Runner는 한 프레임에 한 단계. 조건이 모두 현재 상태라 사용자가 앞서가도 이미 유효한 단계는 다음 프레임마다 하나씩 따라잡는다. 이벤트를 다음 단계 Action으로 보내지 않으므로 누락이 없다.
- 프레임 순서: XRI 선택 처리(Update) → Runner 판정(LateUpdate, 순서 0, 직전 프레임 신호) → Operation 입력·해제 판정(105) → 연결부 신호 갱신(106) → 표시부(110). 장비 계산 순서는 바꾸지 않았다.
- **회전량 감소:** 장비 코드상 회전량은 `Turning` 단계에서만 변하고 하강·진입 중에는 유지된다(`UpdateWindlass`). 하강 전 반대로 돌리면 `turn-reached`가 거짓이 되고, 누적 조건 때문에 4~6단계가 막힌다. 표시부는 "막대 회전량이 다시 부족합니다…" 복구 안내를 보이고, 다시 돌려 충족하면 진행된다. Runner를 되감지 않는다.
- 다른 상태(부착·밴드·고정·기록·마감)는 장비에서 한 번 성립하면 초기화 외에는 되돌아가지 않는다.

## 6. 안내판 텍스트 소유권

| 텍스트 | 작성자 | 내용 |
|---|---|---|
| `Notice` | `TourniquetStatusBoard` | 시제품 안내(두 화면 공용) |
| `OperationPanel/Title` | `TourniquetTrainingGuide` | `3 / 6 · 막대 회전`, 종료 시 `6단계 가상 조작 종료`, 중단 시 `교육 진행 중단` |
| `OperationPanel/Instruction` | `TourniquetTrainingGuide` | 현재 단계 행동(기존 조작 안내 상수 재사용) 또는 회전 복구 안내, 종료 문구 |
| `OperationPanel/Status` | `TourniquetStatusBoard` | 기존 장비 진행 수치(예: `막대 회전 180° / 360°`) |
| `OperationPanel/Detail` | `TourniquetStatusBoard` | 기존 실패·복구 이유, 시간 기록 |
| `StartPanel/*` | `TourniquetStartScreen` | 시작화면 |

- 메뉴 1이 StatusBoard의 `writesGuidance=false`, `titleText`·`instructionText` 참조를 비움 → 구조적으로 한 텍스트에 한 작성자.
- 표시부 규칙: Runner 단계 k의 행동을 보이되, 이번 회차에 도달했던 앞 단계 상태가 지금 무효이면 그 복구 행동을 보인다. 장비보다 몇 프레임 늦은 경우에만 직전 단계 행동이 잠깐 남을 수 있다.
- 종료 표시는 제목·한 줄 문구뿐. 완료 패널·버튼·재시작 없음. 장비 상태와 시간 기록은 그대로. "의료 판정 아님" 명시, 합격·지혈 성공 표현 없음.
- 안내판 월드 Transform·영역·글자 크기는 변경하지 않았다. 새 텍스트 영역 없음(기존 Title·Instruction 재사용). 레이아웃 revision 2 유지.

## 7. 구성·검사 메뉴

**메뉴 1** — 로그 `--- Training start screen and six steps ---`:
- 데이터 자산: 없으면 생성, 매 실행 `TourniquetTrainingIds`로 단계·조건 재기록(ID만, 문구 없음), 이 자산만 저장.
- `TQ_TrainingFlow`(Runner·SceneBindings·연결부) + `Target_Tourniquet`(대상 ID·신호 6개, 초기값 거짓): 이름으로 찾고 없을 때만 생성, 참조·`startOnStart=false` 매 실행 재지정. Runner UnityEvent 영구 리스너 추가 없음.
- 게이트 `link`, 안내판 `TourniquetTrainingGuide`, 시작화면 `trainingGuide` 연결. StatusBoard 연동 지점 설정.
- 기존 XR UI·EventSystem·시작 패널·Simulator 저장 비활성 기준·안내판 조정 보존 규칙 그대로.

**메뉴 2** — 새 구간 `== Six training steps ... ==`:
- 데이터 ID, 6단계 순서, 단계별 누적 State 조건 일치
- Runner·SceneBindings·연결부·표시부 각 1개, Runner 데이터·바인딩 참조, `startOnStart` 꺼짐, Runner 영구 리스너 0
- 바인딩 = 연결부 대상 1개, 대상 ID·신호 6개 순서·초기 거짓
- 연결부 ↔ 씬 장비·Operation·Runner, 게이트 ↔ 연결부, 표시부 ↔ Runner·연결부, 시작화면 ↔ 표시부
- 표시부가 `OperationPanel/Title`·`Instruction`을 쓰고 StatusBoard는 그 둘을 쓰지 않음
- 모든 단계의 이름·행동 문구 존재
- 레이아웃: Title·Instruction 후보를 단계 줄·행동·복구·종료·중단 문구로 교체해 높이·겹침 검사(안내판 좌표). 기존 시작화면·XR UI 검사 유지
- 정적 검사이며 단계 진행 검증이 아님을 보고에 명시

## 8. 정적 검토

- 공통 코드 확인: `StartTraining()` 반환값 없음, 실패 시 `Status` 미변경(Idle/Stopped) → 상태로 판정. `TrainingContext.Tick`은 진입 프레임 다음부터, 한 번에 한 단계. `TrainingTarget.SetState`는 활성일 때만.
- `StartScreen`의 `error` 변수는 단락 평가에서도 할당되도록 사전 선언.
- 남은 제약: Runner가 중단(`Stopped`)되는 경로는 Runner/바인딩 비활성화 같은 개발자 조작 외에는 없다. 중단 시 장비 새 선택 잠금·중단 문구만 있고 복구 UI는 없다.
- 조작 패널이 숨겨진 시작 전에는 표시부가 아무것도 쓰지 않는다.
- IDE 진단·컴파일은 실행하지 않았다.

## 9. 사용자 확인 절차 — 모두 미확인

| # | 절차 | 기대 결과 | 실패 기준 |
|---|---|---|---|
| ① | 컴파일 → 메뉴 1 → 메뉴 2 (Simulator 비활성 저장 상태) | `error CS` 없음, 메뉴 1 로그에 데이터·Flow·표시부, 메뉴 2 `no FAIL items` | 컴파일 오류, 참조 누락, 중복, FAIL |
| ② | Simulator 활성(저장 안 함) → Play, 버튼 누르지 않음 | 시작화면 유지, Console `[Training] Step:` 없음, 본체·부품 잡기 불가 | 자동 진행 로그, 장비 잡힘 |
| ③ | 레이로 교육 시작 | `Training started (run 1)` 1회, `[Training] Step: tq-place` 1회, 안내판 `1 / 6 · 지혈대 배치`, 입력 놓은 뒤 본체 정상 잡기 | 시작·Runner 로그 중복, 표시 없음 |
| ④ | 정상 속도로 6단계 | 각 성공 조건 뒤 2→6 순서 표시. 밴드 95% 미만·덜 덮기·고정 중간 놓기는 이유만 표시되고 단계 유지 | 미달 통과, 순서 건너뜀 |
| ⑤ | 연속 조작을 빠르게(예: 밴드 직후 바로 회전) | 표시가 늦어도 Console 단계 로그가 순서대로 따라잡음, 누락 없음 | 단계 정지, 건너뜀 |
| ⑥ | 360° 충족 → 3단계 넘어간 뒤 하강 전 반대로 회전 | 지시가 회전 복구 안내로 바뀌고 Status에 회전량 표시, 4단계 이후로 진행 안 됨. 다시 돌려 충족하면 고정 진행 | 무효 상태에서 진행, 안내 모순 |
| ⑦ | 마감까지 | `[Training] Completed` 1회, 제목 `6단계 가상 조작 종료`, 장비 자세·시간 기록 유지, 자동 초기화 없음 | 조기 완료, 상태 초기화 |
| ⑧ | Play 종료 → 재실행 | 시작 대기·잠금, 이전 단계·기록 없음 | 이전 상태 잔존 |
| ⑨ | 시작화면 회귀(연속 클릭·양손·그립 유지 시작), 메뉴 1 재실행 → 메뉴 2 | 기존 동작 유지, 데이터·Flow·대상·표시부·EventSystem 중복 없음, 안내판 위치·조정 유지, 겹침 FAIL 없음 | 중복, 위치 초기화 |

- 새 문구 표시로 동적 TMP 폰트 자산이 git 변경으로 보일 수 있다(코드가 폰트를 수정하지 않음).
- 헤드셋 실기는 기존 방침대로 추후.

## 10. 후속 범위

최종 재확인·완료 화면(수행 요약, `지혈대 가상 조작 체험 완료`) → 다시하기(Runner 정지 → XRI 선택 종료 → `ResetToInitial()` → 회차 기록 초기화 → 게이트 재잠금 → 시작화면) → 교육 시계 연결 여부 결정 → 직접 압박·압박붕대 → 과정 선택·복귀.
