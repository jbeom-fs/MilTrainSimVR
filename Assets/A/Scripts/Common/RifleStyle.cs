using UnityEngine;

namespace MilTrainSimVR.A.Common
{
    // 허브에서 고르는 조준 방식 (11/17). 앱을 꺼도 남는다 (PlayerPrefs). 처음 기본값 C — 가늠자 조준 (11/18 사용자, A 부재 중 C 가 바꿈. 그 전엔 A).
    //   · A — 확대 조준: 총을 든 채 눈을 가늠자 뒤로 가져가면 4배 창 (A SightAim)
    //   · C — 가늠자 조준: 확대 없이 C 가늠자 그대로
    // 11/17 오후 A · C 결정 (사용자 동의): 총은 C 총 하나 — 리그 · 총 · 조작은 어느 쪽이든 C 것이고 조준만 바뀐다.
    //   C 총 프리팹의 C RifleSightZoom 이 씬 시작 때 이 값을 읽어 확대 조준을 켜고 끈다 (사격장 · 시가지 모두).
    //   그 전엔 A = A 리그 + A 총이었다 (Docs/A/1117_C총으로_통일.md). 이름(RifleStyle · A/C)은 C 가 읽고 있어 그대로 둔다.
    public static class RifleStyle
    {
        public enum Style { A, C }

        const string k_Key = "MilTrainSimVR.RifleStyle";

        static bool s_Loaded;
        static Style s_Current;

        public static event System.Action<Style> Changed;

        public static Style Current
        {
            get
            {
                if (!s_Loaded)
                {
                    s_Current = PlayerPrefs.GetInt(k_Key, (int)Style.C) == (int)Style.A ? Style.A : Style.C;
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
                Debug.Log($"[Course] 사격 방식: {value}");
                Changed?.Invoke(value);
            }
        }
    }
}
