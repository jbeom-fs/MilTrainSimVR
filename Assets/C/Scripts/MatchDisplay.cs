using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace MilTrainSimVR.UrbanCombat
{
    // 경기 진행을 플레이어에게 보여 준다 (10/27):
    //   · (10/27 ~ 11/18) 손목 표시 — 왼손 위에 떠 있던 작은 글자판. 11/18 사용자: 손목시계가 잘 나오니 지운다 (아래 11/18)
    //   · 점령지 이름은 CaptureZone.DisplayName — 11/16 부터 "백화점 3층" (사용자: 건물 번호(N21) 말고 누구나 아는 이름으로)
    //   · 앞판 — 시작 전엔 탁자 위 안내, 단계가 바뀌면 눈앞에 몇 초 알림, 끝나면 결과(평가)와 다시하기 버튼.
    //     11/12: 실패(전사 · 적 점령 · 무승부)로 끝나면 '연병장으로' 버튼도 같이 보인다 — 허브로 돌아간다 (MatchExit).
    //     성공하면 그 버튼 대신 근처와 출발 자리에 포탈이 열리고, 결과판에 그 안내가 한 줄 붙는다
    //   · 단계가 바뀌면 두 손 진동
    // 11/16 (사용자: "게임오버시 UI 가 건물 외벽이나 오브젝트에 막혀서 안 보이고 상호작용이 안 된다"):
    //   · 눈앞에 띄운 앞판(알림 · 결과)은 벽 · 차 · 기둥을 무시하고 맨 위에 그린다 — 깊이 검사 Always + 투명 물체 중 맨 나중.
    //     벽이 0.6m 보다 가까우면 판이 벽 속에 들어가 안 보였다. 탁자 위 시작 안내는 그대로(벽 너머로 비치지 않게)
    //   · 결과판이 뜨면 두 손 광선이 세상 물체를 지나 판까지 간다 — 벽에 먼저 맞아 광선 끝이 벽에 멈췄다.
    //     총을 쥔 손도 판을 누를 수 있게 한다 (XRI 기본값은 뭔가 쥔 손의 UI 광선을 끈다 — 총을 쥔 채 전사하면 못 눌렀다).
    //     다시하기 · 연병장으로 모두 씬을 새로 불러오니 되돌릴 필요가 없다
    // 11/17: 왼손 메뉴 버튼으로 다시하기 하던 것을 뺐다 — 사용자가 시킨 적 없고, 그 버튼은 B 의 일시정지 메뉴가 쓴다.
    //     결과판은 광선으로 버튼을 눌러서만 고른다
    // 11/18: 왼 손목 안쪽 손목시계(WristWatch) — 시작할 때 손 모델의 손목 뼈에 채운다.
    //     위 = 적 아이콘 + 남은/전체. 아래 = 타이머 + 분:초 (시작 전 · 자유 전투), 오버타임엔 깃발 + 점령 시간 '나 12초 / 적 0초' (같이 있으면 '교착').
    //     떠 있던 손목 표시는 지웠다 (사용자 11/18 — 예비 탄창 수 · 점령지까지 거리 · 오버타임 남은 시간도 같이 빠졌다).
    //     오버타임 알림은 앞판 대신 시계 화면에 몇 초 + 삐비비빅 × 4 (시계가 없으면 예전처럼 앞판). 자유 전투 시작 알림은 앞판 그대로
    // 11/19: 앞판은 이제 결과 때만 뜬다 (시계가 있을 때) — 시작 전 안내는 집결지의 임무 안내판이, 자유 전투 시작은 시계의 삐비비빅 한 번이 대신한다
    //     (사용자: "자유전투 시작 UI 도 그냥 없애고, 총을 집으면 시계에서 삐비비빅 소리 한번 나게 해서 시작했다고만 알려"). 위 줄들의
    //     '시작 전엔 탁자 위 안내' · '자유 전투 시작 알림은 앞판 그대로'는 그 전 이야기다
    // 11/19 (사용자: 결과 판을 B 의 UI 기준에 맞춰 달라 — Docs/B/1117_B_UI_색상팔레트_스타일가이드.md):
    //   · 색은 B 의 역할 색 — 배경 · 글자 · 보조 안내(노랑) · 버튼(파랑). 그 전의 순백 글자 · 초록 다시하기 · 반투명 배경 ·
    //     포탈 안내 하늘색은 B 팔레트에 없어 버렸다. 제목의 승리 초록 / 패배 빨강도 처음엔 뺐다가 같은 날 사용자 말로 되살렸다
    //     ("색 되살려주고") — B 교육엔 실패가 없어 그 문서에 승패 색 기준이 없다. 예전 값 그대로 (k_WinColor · k_LoseColor)
    //   · 제목 줄(승리 · 패배 + 이유)은 위에 따로, 가운데, 보통 굵기 — m_BoardTitle. 그 전엔 본문 첫 줄 왼쪽에 굵게
    //   · 본문은 세로 스크롤 — m_BoardScroll. 맞은 곳이 많으면 마지막 줄이 다시하기 버튼 위에 겹쳤다. 버튼은 스크롤 밖 아래 줄
    //   판 크기 · 글자 크기는 B 문서가 C 화면에 강제하지 않는다 (눈앞에 띄우는 판 — 글자 4 cm 그대로)
    // 글자는 보이는 값이 바뀔 때만 새로 만든다 — 매 프레임 문자열을 만들면 쓰레기(GC)가 쌓여 VR 에서 멈칫한다 (TargetRange 와 같은 까닭)
    public class MatchDisplay : MonoBehaviour
    {
        [SerializeField] MatchFlow m_Flow;
        [SerializeField] MatchRecord m_Record;
        [SerializeField] CombatantHealth m_Player;

        [Header("손목시계")]
        [Tooltip("왼 손목 안쪽에 채울 시계 (WristWatchBuilder 가 만든 C_WristWatch) — 비우면 시계 없이")]
        [SerializeField] WristWatch m_WatchPrefab;

        [Header("앞판 (안내 · 알림 · 결과)")]
        [SerializeField] Transform m_Board;
        [Tooltip("제목 줄 (위 · 가운데) — 승리 · 패배와 이유. 비어 있으면 본문 첫 줄에 쓴다")]
        [SerializeField] TMP_Text m_BoardTitle;
        [SerializeField] TMP_Text m_BoardText;
        [Tooltip("본문 세로 스크롤 — 판이 뜰 때 맨 위로 돌린다. 비어 있으면 스크롤 없이")]
        [SerializeField] ScrollRect m_BoardScroll;
        [SerializeField] GameObject m_RestartButton;
        [Tooltip("눈앞 거리(m) — 벽이 더 가까우면 벽 바로 앞에")]
        [SerializeField] float m_BoardDistance = 1.5f;
        [Tooltip("단계가 바뀔 때 알림을 보여 주는 시간(초)")]
        [SerializeField] float m_NoticeSeconds = 4f;

        Transform m_Head;
        WristWatch m_Watch;
        MatchExit m_Exit;
        GameObject m_HubButton;   // '연병장으로' — 다시하기 버튼을 실행 중에 복제해 만든다 (씬은 그대로)
        HapticImpulsePlayer[] m_Haptics;
        float m_NoticeUntil = -1f;
        Material m_OnTopLine;
        readonly HashSet<Material> m_OnTopMaterials = new HashSet<Material>();   // 맨 위로 바꾼 재질 (값은 셰이더에 선언이 없어 GetFloat 로 못 읽는다)
        readonly List<Material> m_MadeMaterials = new List<Material>();           // 여기서 만든 사본 — 끝날 때 지운다
        readonly StringBuilder m_Text = new StringBuilder(512);

        // B 의 UI 기준 색 (Docs/B/1117_B_UI_색상팔레트_스타일가이드.md §2 — B 의 TrainingBoardStyle 과 같은 값). 생성기(MatchSetupBuilder)가 판에 칠한다
        public static readonly Color BoardBackground = new Color(0.07f, 0.09f, 0.11f, 1f);
        public static readonly Color BoardTextColor = new Color(0.94f, 0.95f, 0.96f, 1f);
        public static readonly Color BoardButton = new Color(0.16f, 0.42f, 0.70f, 1f);   // 다시하기 · 연병장으로 · 스크롤 손잡이
        public static readonly Color BoardTrack = new Color(0.22f, 0.25f, 0.28f, 1f);    // 스크롤바 바탕
        const string k_NoticeColor = "#FFC759";                                             // 보조 안내 (1.00, 0.78, 0.35)
        const string k_WinColor = "#7CFC9A", k_LoseColor = "#FF6B6B";                       // 제목의 '승리' · '패배' (C 가 정한 색 — 무승부는 기본 글자색)

        // uGUI · TMP 기본 셰이더는 깊이 검사를 이 값으로 정한다 (ZTest [unity_GUIZTestMode]) — 판의 재질 사본에만 Always 를 넣는다
        static readonly int k_GUIZTestMode = Shader.PropertyToID("unity_GUIZTestMode");
        // 같은 투명 물체 사이에선 정렬 순서가 거리보다 먼저다 — 유리 · 비네트보다 나중에 그린다
        const int k_OnTopSortingOrder = 30000;

        void Awake()
        {
            XROrigin origin = m_Player.GetComponent<XROrigin>();
            if (origin != null && origin.Camera != null)
                m_Head = origin.Camera.transform;
            m_Haptics = m_Player.GetComponentsInChildren<HapticImpulsePlayer>(true);
            m_Exit = m_Player.GetComponent<MatchExit>();
            m_Flow.PhaseChanged += OnPhaseChanged;
        }

        void Start()
        {
            // 시작 전 안내는 집결지에 세운 임무 안내판이 한다 (11/19 사용자: "UI 로 뜨던 목표도 … 큰 안내판으로 바꿔서 안내판 옆에" —
            // StartBackdropBuilder.BriefBoard. 그 전엔 이 앞판이 탁자 위에 떠서 같은 내용을 보여 줬다). 앞판은 알림 · 결과 때만 눈앞에 뜬다
            m_RestartButton.SetActive(false);
            m_Board.gameObject.SetActive(false);
            WearWatch();
        }

        // 손목시계를 왼손 모델의 손목 뼈(L_Wrist)에 채운다 — 손 모델은 ControllerHandPose 가 들고 있다
        void WearWatch()
        {
            if (m_WatchPrefab == null)
                return;
            foreach (ControllerHandPose pose in m_Player.GetComponentsInChildren<ControllerHandPose>(true))
            {
                if (pose.HandModel == null)
                    continue;
                foreach (Transform bone in pose.HandModel.GetComponentsInChildren<Transform>(true))
                {
                    if (bone.name != "L_Wrist")
                        continue;
                    m_Watch = Instantiate(m_WatchPrefab);
                    m_Watch.Wear(bone);
                    UpdateWatch();
                    return;
                }
            }
            Debug.LogWarning("[C] 손목시계 — 왼손 모델의 손목 뼈(L_Wrist)를 못 찾아 시계를 채우지 않았다");
        }

        void UpdateWatch()
        {
            if (m_Watch == null)
                return;
            int alive = m_Flow.EnemiesAlive, total = m_Flow.EnemyCount;
            switch (m_Flow.CurrentPhase)
            {
                case MatchFlow.Phase.Waiting:
                    m_Watch.ShowTime(WristWatch.State.Idle, Mathf.RoundToInt(m_Flow.FreeSeconds), alive, total);
                    break;
                case MatchFlow.Phase.Free:
                    m_Watch.ShowTime(WristWatch.State.Running, Mathf.CeilToInt(m_Flow.PhaseRemaining), alive, total);
                    break;
                case MatchFlow.Phase.Overtime:
                    m_Watch.ShowCapture(alive, total, Mathf.FloorToInt(m_Flow.PlayerCapture), Mathf.FloorToInt(m_Flow.EnemyCapture),
                        m_Flow.PlayerInZone && m_Flow.EnemiesInZone);
                    break;
                default:
                    m_Watch.ShowTime(WristWatch.State.Ended, -1, alive, total);
                    break;
            }
        }

        void OnDestroy()
        {
            if (m_Flow != null)
                m_Flow.PhaseChanged -= OnPhaseChanged;
            foreach (Material material in m_MadeMaterials)
                if (material != null)
                    Destroy(material);
        }

        void OnPhaseChanged(MatchFlow.Phase phase)
        {
            foreach (HapticImpulsePlayer haptics in m_Haptics)
                haptics.SendHapticImpulse(0.5f, 0.2f);
            switch (phase)
            {
                case MatchFlow.Phase.Free:
                    // 11/19 사용자: "자유전투 시작 UI 도 그냥 없애고, 총을 집으면 시계에서 삐비비빅 소리 한번 나게 해서 시작했다고만 알려" —
                    // 눈앞에 뜨던 '자유 전투 시작 · N분 뒤 오버타임' 판은 없다. 시계가 삐비비빅 한 번 (위의 손 진동은 그대로)
                    if (m_Watch != null)
                        m_Watch.BeepOnce();
                    break;
                case MatchFlow.Phase.Overtime:
                    if (m_Watch != null)
                    {
                        // 11/18 사용자: 오버타임 알림은 시계에서 — 화면 전체 알림 + 삐비비빅
                        m_Watch.Notify("오버타임", m_Flow.Zone.DisplayName, m_NoticeSeconds);
                        break;
                    }
                    m_Text.Clear();
                    SetTitle("오버타임", null);
                    m_Text.Append("점령지 ").Append(m_Flow.Zone.DisplayName)
                        .Append("이 열렸다\n적이 모두 그리로 간다 — 먼저 점령하거나 섬멸하라\n적의 점령은 ")
                        .Append(Mathf.RoundToInt(m_Flow.EnemyCaptureDelay)).Append("초 뒤부터");
                    ShowNotice();
                    break;
                case MatchFlow.Phase.Ended:
                    ShowResult();
                    break;
            }
        }

        // m_Text 에 써 둔 알림을 눈앞에 잠깐
        void ShowNotice()
        {
            m_BoardText.SetText(m_Text);
            m_RestartButton.SetActive(false);
            if (m_HubButton != null)
                m_HubButton.SetActive(false);
            PlaceBoardInFront();
            ScrollToTop();
            m_NoticeUntil = Time.time + m_NoticeSeconds;
        }

        // 제목 줄 — 제목(판의 글자 크기 52)과 그 아래 한 줄(본문 크기). 제목 글이 없는 옛 판이면 본문(m_Text) 첫머리에 쓴다.
        // color = 제목 낱말만 칠할 색 (#RRGGBB) — 이유 줄은 기본 글자색
        void SetTitle(string title, string sub, string color = null)
        {
            if (color != null)
                title = "<color=" + color + ">" + title + "</color>";
            if (m_BoardTitle == null)
            {
                m_Text.Append("<size=130%>").Append(title).Append("</size>");
                if (!string.IsNullOrEmpty(sub))
                    m_Text.Append("  ").Append(sub);
                m_Text.Append("\n\n");
                return;
            }
            m_BoardTitle.SetText(string.IsNullOrEmpty(sub) ? title : title + "\n<size=77%>" + sub + "</size>");
        }

        void ScrollToTop()
        {
            if (m_BoardScroll == null)
                return;
            m_BoardScroll.StopMovement();
            m_BoardScroll.content.anchoredPosition = Vector2.zero;
        }

        void Update()
        {
            if (m_NoticeUntil > 0f && Time.time >= m_NoticeUntil)
            {
                m_NoticeUntil = -1f;
                if (m_Flow.CurrentPhase != MatchFlow.Phase.Ended)
                    m_Board.gameObject.SetActive(false);
            }
            UpdateWatch();
        }

        // ---------------------------------------------------------------- 결과판

        void ShowResult()
        {
            m_Text.Clear();
            switch (m_Flow.Result)
            {
                case MatchFlow.Outcome.Annihilation: SetTitle("승리", "적을 모두 섬멸했다", k_WinColor); break;
                case MatchFlow.Outcome.PlayerCapture: SetTitle("승리", "점령지를 점령했다", k_WinColor); break;
                case MatchFlow.Outcome.PlayerKilled: SetTitle("패배", "전사했다", k_LoseColor); break;
                case MatchFlow.Outcome.EnemyCapture: SetTitle("패배", "적이 점령지를 점령했다", k_LoseColor); break;
                default: SetTitle("무승부", "오버타임이 끝났다"); break;
            }

            // 나가는 길 — 성공이면 포탈(MatchExit 가 세운다), 아니면 아래 '연병장으로' 버튼. 안내는 본문 맨 위에 둔다
            // (맨 아래에 두면 맞은 곳이 많아 줄이 늘 때 스크롤해야 보인다). 보조 안내 색(노랑)
            bool success = MatchExit.IsSuccess(m_Flow.Result);
            int portals = success && m_Exit != null ? m_Exit.OpenExits() : 0;
            if (portals > 0)
                m_Text.Append("<size=85%><color=").Append(k_NoticeColor).Append(">연병장으로 돌아가는 포탈이 열렸다 — ").Append(portals > 1 ? "가까이에 하나, 출발 자리에 하나" : "가까이에 하나").Append("</color></size>\n");

            m_Text.Append("걸린 시간  ");
            AppendTime(m_Text, Mathf.RoundToInt(m_Flow.MatchSeconds));

            int shots = m_Record.Shots, hits = m_Record.Hits;
            m_Text.Append("\n명중률  ").Append(hits).Append(" / ").Append(shots).Append("발");
            if (shots > 0)
                m_Text.Append("  (").Append(Mathf.RoundToInt(100f * hits / shots)).Append("%)");

            // 부위별 처치 — 내가 쓰러뜨린 적만 부위로 세고, 적끼리 맞아 쓰러진 건 따로
            var byPart = new int[System.Enum.GetValues(typeof(HitZone.Part)).Length];
            int bleed = 0, mine = 0, friendly = 0;
            foreach (MatchRecord.Kill kill in m_Record.Kills)
            {
                if (!kill.byPlayer)
                {
                    friendly++;
                    continue;
                }
                mine++;
                if (kill.part.HasValue)
                    byPart[(int)kill.part.Value]++;
                else
                    bleed++;
            }
            m_Text.Append("\n처치  ").Append(mine).Append(" / ").Append(m_Flow.EnemyCount).Append("명");
            bool first = true;
            for (int i = 0; i < byPart.Length; i++)
            {
                if (byPart[i] == 0)
                    continue;
                m_Text.Append(first ? "  — " : " · ").Append(CombatantHealth.PartName((HitZone.Part)i)).Append(' ').Append(byPart[i]);
                first = false;
            }
            if (bleed > 0)
                m_Text.Append(first ? "  — " : " · ").Append("출혈 ").Append(bleed);
            if (friendly > 0)
                m_Text.Append("  (적끼리 ").Append(friendly).Append(')');

            // 내가 맞은 곳 — 부위 → 결과 (경기 시작부터 몇 분 몇 초에)
            m_Text.Append("\n내가 맞은 곳  ");
            if (m_Player.Hits.Count == 0)
                m_Text.Append("없음");
            for (int i = 0; i < m_Player.Hits.Count; i++)
            {
                CombatantHealth.HitRecord hit = m_Player.Hits[i];
                if (i > 0)
                    m_Text.Append(" · ");
                m_Text.Append(CombatantHealth.PartName(hit.part)).Append(' ').Append(CombatantHealth.StateName(hit.result)).Append(" (");
                AppendTime(m_Text, Mathf.RoundToInt(hit.time - m_Record.StartTime));
                m_Text.Append(')');
            }
            if (!m_Player.IsAlive && (m_Player.Hits.Count == 0 || m_Player.Hits[m_Player.Hits.Count - 1].result != CombatantHealth.State.Dead))
                m_Text.Append(" · 출혈로 전사");

            m_BoardText.SetText(m_Text);
            m_RestartButton.SetActive(true);
            ShowHubButton(!success && m_Exit != null);
            m_NoticeUntil = -1f;
            PlaceBoardInFront();
            ScrollToTop();
            LetHandsReachBoard();
            // 전사했어도 결과판이 보이게 — 시야를 다 덮던 검붉은 비네트를 가장자리만 남긴다
            if (m_Player.TryGetComponent(out PlayerWoundEffects wounds))
                wounds.ResultsShown = true;
        }

        // '연병장으로' 버튼 — 보일 때는 다시하기와 나란히(왼쪽 다시하기 · 오른쪽 연병장으로), 안 보일 때는 다시하기를 가운데로.
        // 다시하기 버튼을 복제해 글자 · 누를 때 하는 일만 바꾼다 (색은 다시하기와 같은 B 기준 파랑 — 11/19 전엔 따로 칠했다)
        void ShowHubButton(bool show)
        {
            var restart = (RectTransform)m_RestartButton.transform;
            if (show && m_HubButton == null)
            {
                m_HubButton = Instantiate(m_RestartButton, restart.parent);
                m_HubButton.name = "연병장으로";
                var button = m_HubButton.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent();   // 복제해 온 '다시하기' 연결을 버린다
                button.onClick.AddListener(m_Exit.GoToHub);
                TMP_Text label = m_HubButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.SetText(m_Exit.HubReady ? "연병장으로" : "연병장 (준비 중)");
            }
            float half = show ? restart.sizeDelta.x * 0.5f + 20f : 0f;
            restart.anchoredPosition = new Vector2(-half, restart.anchoredPosition.y);
            if (m_HubButton == null)
                return;
            ((RectTransform)m_HubButton.transform).anchoredPosition = new Vector2(half, restart.anchoredPosition.y);
            m_HubButton.SetActive(show);
        }

        // 눈앞(수평으로) m_BoardDistance 에, 눈 쪽을 보게 — 앞이 벽이면 벽 바로 앞
        void PlaceBoardInFront()
        {
            if (m_Head == null)
                return;
            Vector3 forward = Vector3.ProjectOnPlane(m_Head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(m_Head.up, Vector3.up);
            forward.Normalize();
            float distance = m_BoardDistance;
            if (Physics.Raycast(m_Head.position, forward, out RaycastHit hit, m_BoardDistance + 0.1f, Gunfire.ShotMask, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(0.6f, hit.distance - 0.1f);
            Vector3 p = m_Head.position + forward * distance - Vector3.up * 0.1f;
            m_Board.SetPositionAndRotation(p, Quaternion.LookRotation(forward, Vector3.up));
            DrawBoardOnTop();
            m_Board.gameObject.SetActive(true);
        }

        // 판의 모든 글 · 그림을 벽 위에 그린다. 재질은 사본을 만들어 바꾼다 — 공용 재질(Default UI Material, 글꼴 재질)은 그대로.
        // 결과 때 복제되는 '연병장으로' 버튼은 다시하기의 재질 사본을 그대로 물려받는다
        void DrawBoardOnTop()
        {
            foreach (Canvas canvas in m_Board.GetComponentsInChildren<Canvas>(true))
                canvas.sortingOrder = k_OnTopSortingOrder;
            foreach (Graphic graphic in m_Board.GetComponentsInChildren<Graphic>(true))
            {
                // TMP 는 fontMaterial 을 처음 읽을 때 이 글만의 사본을 만든다 (예비 글꼴 아틀라스의 재질도 이 사본을 복사해 만든다)
                Material material = graphic is TMP_Text text ? text.fontMaterial : graphic.material;
                if (material == null || m_OnTopMaterials.Contains(material))
                    continue;
                if (!(graphic is TMP_Text))
                {
                    material = new Material(material) { name = material.name + " (맨 위)" };
                    m_MadeMaterials.Add(material);
                    graphic.material = material;
                }
                material.SetFloat(k_GUIZTestMode, (float)CompareFunction.Always);
                m_OnTopMaterials.Add(material);
            }
        }

        // 결과판이 뜬 뒤 — 두 손 광선이 벽 · 차를 지나 판까지 가고, 무언가 쥔 손으로도 판을 누른다.
        // 광선이 UI 층만 보게 하고 판을 UI 층으로 옮긴다 (그래픽 광선 검사도 광선의 층 마스크로 거른다)
        void LetHandsReachBoard()
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer < 0)
                return;
            foreach (Transform t in m_Board.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = uiLayer;
            foreach (NearFarInteractor hand in m_Player.GetComponentsInChildren<NearFarInteractor>(true))
            {
                hand.blockUIOnInteractableSelection = false;
                if (hand.farInteractionCaster is CurveInteractionCaster ray)
                    ray.raycastMask = 1 << uiLayer;
                // 광선 줄도 판 위에 — 판 밑에 깔리면 어느 버튼을 겨누는지 안 보인다 (판 배경이 거의 불투명).
                // 줄의 기본 재질(Legacy 입자 셰이더)은 깊이 검사를 못 바꿔 uGUI 기본 재질 사본으로 바꾼다
                foreach (LineRenderer line in hand.GetComponentsInChildren<LineRenderer>(true))
                {
                    if (m_OnTopLine == null)
                    {
                        m_OnTopLine = new Material(Graphic.defaultGraphicMaterial) { name = "광선 (맨 위)" };
                        m_MadeMaterials.Add(m_OnTopLine);
                        if (line.sharedMaterial != null)
                            m_OnTopLine.mainTexture = line.sharedMaterial.mainTexture;
                        m_OnTopLine.SetFloat(k_GUIZTestMode, (float)CompareFunction.Always);
                    }
                    line.sharedMaterial = m_OnTopLine;
                    line.sortingOrder = k_OnTopSortingOrder + 1;
                }
            }
        }

        static void AppendTime(StringBuilder sb, int seconds)
        {
            seconds = Mathf.Max(0, seconds);
            sb.Append(seconds / 60).Append(':');
            if (seconds % 60 < 10)
                sb.Append('0');
            sb.Append(seconds % 60);
        }
    }
}
