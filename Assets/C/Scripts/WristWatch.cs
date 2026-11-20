using TMPro;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 왼 손목 안쪽(손바닥 쪽)에 찬 시계 (11/18, 사용자 지시). 둥근 화면:
    //     [적 아이콘] 남은 적 / 전체      (예: 20/20)
    //     ─────────────
    //     [타이머 아이콘] 분:초          시작 전 · 자유 전투 (시작 전엔 자유 전투 길이를 흐리게)
    //     [깃발 아이콘] 나 12초 / 적 0초  오버타임 — 점령 시간. 먼저 30초 채우는 쪽이 이긴다. 플레이어와 적이 같이 점령지에 있으면 '교착'
    //   알림 (11/18): 오버타임이 열리면 몇 초 동안 화면 전체에 '오버타임' + 점령지 이름, 삐비비빅 × 4 (앞판 알림 대신)
    //   시작 (11/19): 총을 집어 자유 전투가 시작되면 삐비비빅 한 번만 — 화면은 그대로 (사용자: "자유전투 시작 UI 도 그냥 없애고,
    //   총을 집으면 시계에서 삐비비빅 소리 한번 나게 해서 시작했다고만 알려"). 눈앞에 뜨던 '자유 전투 시작' 판을 대신한다 (BeepOnce)
    //   모델은 Tripo 러기드 스마트워치 (Models/Tripo/Watch, 원본 · 참고 그림 · 프롬프트는 Source~/watch). 프리팹 · 아이콘 · 소리는 WristWatchBuilder 가 만든다.
    //   글자는 화면 위에 TMP 로 띄운다 — 생성 텍스처의 글자는 깨지고, 값이 계속 바뀐다. 화면은 검은 판으로 덮어 켜진 화면처럼 보이게 한다.
    //   손 모델의 손목 뼈(L_Wrist)에 찬다 — 총을 쥐면 손 모델이 총으로 옮겨 붙으니(RifleHands) 컨트롤러가 아니라 보이는 손을 따라간다.
    //   화면 위쪽(12시)은 엄지 쪽, 3시(버튼 2개)는 손가락 쪽 — 시계를 안쪽으로 찬 사람이 손바닥을 위로 돌려 볼 때 똑바로 선다.
    //   값은 MatchDisplay 가 넣고, 보이는 값이 바뀔 때만 글자를 새로 만들고 줄을 가운데로 다시 맞춘다
    //   (매 프레임 문자열을 만들면 GC 로 VR 이 멈칫한다)
    public class WristWatch : MonoBehaviour
    {
        [Header("화면 — 두 줄")]
        [SerializeField] GameObject m_Rows;
        [SerializeField] TMP_Text m_EnemyText;
        [SerializeField] SpriteRenderer m_EnemyIcon;
        [SerializeField] TMP_Text m_BottomText;
        [SerializeField] SpriteRenderer m_BottomIcon;
        [SerializeField] Sprite m_TimerSprite;
        [SerializeField] Sprite m_FlagSprite;
        [Tooltip("아이콘 크기 · 아이콘과 글자 사이(m)")]
        [SerializeField] float m_IconSize = 0.005f;
        [SerializeField] float m_IconGap = 0.0008f;
        [Tooltip("아래 줄 글자 크기 — 분:초 / 점령 시간 (빌더가 화면 원 안에 들어가게 정한다)")]
        [SerializeField] float m_TimeFontSize = 0.5f;
        [SerializeField] float m_CaptureFontSize = 0.4f;

        [Header("화면 — 알림")]
        [SerializeField] GameObject m_Notice;
        [SerializeField] TMP_Text m_NoticeTitle;
        [SerializeField] TMP_Text m_NoticeBody;
        [SerializeField] AudioSource m_Beep;
        [Tooltip("삐비비빅 한 묶음만 낼 때 소리를 끊는 때(초) — 알람 소리는 네 묶음(묶음 0.34초, 0.9초마다)이라 첫 묶음과 둘째 묶음 사이에서 끊는다")]
        [SerializeField] float m_BeepOnceSeconds = 0.6f;

        [Header("손목 뼈 기준 자리 (헤드셋에서 보고 맞춘다)")]
        [Tooltip("손목 뼈(L_Wrist) 기준 시계 자리(m) — 케이스 뒷면 가운데. 뼈 축: +Z 손가락 쪽, -Y 손바닥 쪽, +X 엄지 쪽")]
        [SerializeField] Vector3 m_WristPosition = new Vector3(-0.001f, -0.0200f, -0.011f);
        [Tooltip("손목 뼈 기준 시계 회전(도) — 빌더가 모델 방향에서 계산해 넣는다")]
        [SerializeField] Vector3 m_WristEuler;

        [Header("색")]
        [SerializeField] Color m_Normal = new Color(0.90f, 0.96f, 0.90f);
        [SerializeField] Color m_Overtime = new Color(1f, 0.70f, 0.28f);
        [SerializeField] Color m_Contested = new Color(1f, 0.42f, 0.32f);
        [SerializeField] Color m_Idle = new Color(0.52f, 0.58f, 0.52f);

        public enum State { Idle, Running, Overtime, Ended }

        int m_Alive = int.MinValue, m_Total = int.MinValue;
        int m_BottomSignature = int.MinValue;
        float m_NoticeUntil = -1f;
        float m_BeepStopAt = -1f;   // 한 묶음만 낼 때 소리를 끊을 시각

        // 손목 뼈에 찬다 (자리 · 회전은 위 값)
        public void Wear(Transform wrist)
        {
            transform.SetParent(wrist, false);
            transform.SetLocalPositionAndRotation(m_WristPosition, Quaternion.Euler(m_WristEuler));
        }

        // 시작 전 · 자유 전투 · 끝: 아래 줄 = 타이머 + 분:초 (seconds < 0 이면 "--:--")
        public void ShowTime(State state, int seconds, int alive, int total)
        {
            ShowEnemies(state, alive, total);
            int signature = ((int)state + 1) * 100000 + (seconds < 0 ? 99999 : Mathf.Min(seconds, 99998));
            if (signature == m_BottomSignature)
                return;
            m_BottomSignature = signature;
            Color c = state == State.Overtime ? m_Overtime : state == State.Running ? m_Normal : m_Idle;
            m_BottomIcon.sprite = m_TimerSprite;
            m_BottomIcon.color = c;
            m_BottomText.color = c;
            m_BottomText.fontSize = m_TimeFontSize;
            if (seconds < 0)
                m_BottomText.SetText("--:--");
            else
                m_BottomText.SetText("{0}:{1:00}", seconds / 60, seconds % 60);
            Layout(m_BottomIcon, m_BottomText);
        }

        // 오버타임: 아래 줄 = 깃발 + 점령 시간(초). 둘 다 점령지에 있으면 교착
        public void ShowCapture(int alive, int total, int mine, int theirs, bool contested)
        {
            ShowEnemies(State.Overtime, alive, total);
            int signature = -(contested ? 1 : 2) * 1000000 - Mathf.Clamp(mine, 0, 999) * 1000 - Mathf.Clamp(theirs, 0, 999);
            if (signature == m_BottomSignature)
                return;
            m_BottomSignature = signature;
            Color c = contested ? m_Contested : m_Overtime;
            m_BottomIcon.sprite = m_FlagSprite;
            m_BottomIcon.color = c;
            m_BottomText.color = c;
            m_BottomText.fontSize = m_CaptureFontSize;
            if (contested)
                m_BottomText.SetText("교착");
            else
                m_BottomText.SetText("나 {0}초 / 적 {1}초", mine, theirs);   // '초'를 붙인다 — 다른 칸은 분:초라 안 헷갈려도 여긴 숫자만이면 헷갈린다 (사용자 11/18)
            Layout(m_BottomIcon, m_BottomText);
        }

        // 화면 전체 알림 몇 초 + 삐비비빅 (소리가 있으면)
        public void Notify(string title, string body, float seconds)
        {
            m_NoticeTitle.SetText(title);
            m_NoticeBody.SetText(body);
            m_NoticeTitle.color = m_Overtime;
            m_NoticeBody.color = m_Normal;
            m_Rows.SetActive(false);
            m_Notice.SetActive(true);
            m_NoticeUntil = Time.time + seconds;
            m_BeepStopAt = -1f;   // 네 묶음 다 낸다
            if (m_Beep != null && m_Beep.clip != null)
                m_Beep.Play();
        }

        // 삐비비빅 한 번만 — 화면은 바꾸지 않는다 (자유 전투 시작). 같은 알람 소리의 첫 묶음만 내고 끊는다
        public void BeepOnce()
        {
            if (m_Beep == null || m_Beep.clip == null)
                return;
            m_Beep.Stop();
            m_Beep.Play();
            m_BeepStopAt = Time.time + m_BeepOnceSeconds;
        }

        void Update()
        {
            if (m_NoticeUntil > 0f && Time.time >= m_NoticeUntil)
            {
                m_NoticeUntil = -1f;
                m_Notice.SetActive(false);
                m_Rows.SetActive(true);
            }
            if (m_BeepStopAt > 0f && Time.time >= m_BeepStopAt)
            {
                m_BeepStopAt = -1f;
                m_Beep.Stop();
            }
        }

        void ShowEnemies(State state, int alive, int total)
        {
            Color c = state == State.Idle || state == State.Ended ? m_Idle : m_Normal;
            if (m_EnemyText.color != c)
            {
                m_EnemyText.color = c;
                m_EnemyIcon.color = c;
            }
            if (alive == m_Alive && total == m_Total)
                return;
            m_Alive = alive;
            m_Total = total;
            m_EnemyText.SetText("{0}/{1}", alive, total);
            Layout(m_EnemyIcon, m_EnemyText);
        }

        // [아이콘][틈][글자] 를 가운데로 (글자는 왼쪽 맞춤, 기준점이 왼쪽 가운데)
        void Layout(SpriteRenderer icon, TMP_Text text)
        {
            text.ForceMeshUpdate();
            float width = text.textBounds.size.x;
            float left = -(m_IconSize + m_IconGap + width) * 0.5f;
            Vector3 p = icon.transform.localPosition;
            icon.transform.localPosition = new Vector3(left + m_IconSize * 0.5f, p.y, p.z);
            p = text.transform.localPosition;
            text.transform.localPosition = new Vector3(left + m_IconSize + m_IconGap, p.y, p.z);
        }
    }
}
