using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace MilTrainSimVR.A.Common
{
    // 허브에서 고르는 화면 회전 방식 (11/17). 오른 스틱으로 도는 방식 — 앱을 꺼도 남는다 (PlayerPrefs).
    //   · Smooth — 부드럽게 돌기 (A 리그 방식, 초당 60°)
    //   · Snap   — 끊어 돌기 (C 리그 · XRI 기본, 45°씩 · 스틱을 뒤로 당기면 뒤돌기)
    //   · None   — (11/17 엔 "아직 안 고름 = 씬마다 원래 설정"이었다. 11/18 사용자: 기본 끊어 돌기 — 안 고르면 Snap, None 은 이제 안 나온다. A 부재 중 C 가 바꿈)
    // 고르면(안 골라도 기본 Snap) 모든 씬에 저절로 적용된다: 씬이 열릴 때마다 켜진 리그의 ControllerInputActionManager.smoothTurnEnabled 를 바꾼다.
    // B · C 파일은 고치지 않는다 — 실행 중 값만 바꾼다. 각도 · 속도는 각 리그 값 그대로 (지금 모두 45° · 60°/초).
    public static class TurnStyle
    {
        public enum Style { None, Smooth, Snap }

        const string k_Key = "MilTrainSimVR.TurnStyle";

        static bool s_Loaded;
        static Style s_Current;

        public static event System.Action<Style> Changed;

        public static Style Current
        {
            get
            {
                if (!s_Loaded)
                {
                    int saved = PlayerPrefs.GetInt(k_Key, (int)Style.Snap);
                    s_Current = saved == (int)Style.Smooth ? Style.Smooth : Style.Snap;
                    s_Loaded = true;
                }
                return s_Current;
            }
            set
            {
                if (s_Loaded && s_Current == value)
                    return;
                s_Current = value;
                s_Loaded = true;
                PlayerPrefs.SetInt(k_Key, (int)value);
                PlayerPrefs.Save();
                Debug.Log($"[Course] 회전 방식: {value}");
                ApplyToScene();
                Changed?.Invoke(value);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyToScene();   // 첫 씬은 이 함수보다 먼저 열린다
        }

        // 씬의 Awake · OnEnable 뒤, Start 전 — 사격장 리그 바꾸기(RifleStyleSwitch, Awake)가 끝난 뒤라 켜진 리그에만 적용된다
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToScene();

        // 켜진 리그의 두 손 모두. 꺼진 리그는 건드리지 않는다 (입력 동작은 리그끼리 같은 것을 쓴다).
        // 손(컨트롤러)은 추적이 없으면 꺼져 있다(XRInputModalityManager — 헤드셋 없는 에디터, 손 추적 중) → 꺼진 손은 값만 바꿔 둔다.
        // setter 는 입력 동작을 켜고 끄므로 꺼진 손엔 쓰지 않는다 — 다시 켜질 때(OnEnable) 이 값으로 동작을 켠다
        public static void ApplyToScene()
        {
            Style style = Current;
            if (style == Style.None)
                return;
            bool smooth = style == Style.Smooth;
            int count = 0;
            foreach (XROrigin origin in Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None))
            {
                foreach (ControllerInputActionManager hand in origin.GetComponentsInChildren<ControllerInputActionManager>(true))
                {
                    if (hand.isActiveAndEnabled)
                        hand.smoothTurnEnabled = smooth;
                    else
                        s_SmoothTurnField?.SetValue(hand, smooth);
                    count++;
                }
            }
            if (count > 0)
                Debug.Log($"[Course] 회전 방식 {style} 적용 — 손 {count}개");
        }

        // XRI Starter Assets 3.4.1 의 직렬화 칸 (프로젝트에 복사된 샘플 코드라 바뀌지 않는다)
        static readonly System.Reflection.FieldInfo s_SmoothTurnField = typeof(ControllerInputActionManager)
            .GetField("m_SmoothTurnEnabled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    }
}
