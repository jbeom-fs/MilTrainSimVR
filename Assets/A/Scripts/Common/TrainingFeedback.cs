using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.A.Common
{
    // 교육 피드백 — 소리, 진동. 분해·결합과 사격이 같이 쓴다. 글자 표시는 패널이 맡는다.
    // 소리 클립이 비어 있으면 임시 신호음을 코드로 만들어 쓴다 (실제 소리 자산으로 교체 예정).
    public class TrainingFeedback : MonoBehaviour
    {
        [SerializeField] AudioSource m_Audio;

        [Header("소리 (비우면 임시 신호음)")]
        [SerializeField] AudioClip m_ErrorClip;
        [SerializeField] AudioClip m_BlockedClip;
        [SerializeField] AudioClip m_ClickClip;
        [SerializeField] AudioClip m_StepClip;

        void Awake()
        {
            if (m_ErrorClip == null) m_ErrorClip = MakeError();
            if (m_BlockedClip == null) m_BlockedClip = MakeBlocked();
            if (m_ClickClip == null) m_ClickClip = MakeClick();
            if (m_StepClip == null) m_StepClip = MakeStep();
        }

        public void PlayError() => Play(m_ErrorClip);
        public void PlayBlocked() => Play(m_BlockedClip);
        public void PlayClick() => Play(m_ClickClip);
        public void PlayStep() => Play(m_StepClip);

        public void Haptic(IXRSelectInteractor hand, float amplitude, float duration)
        {
            if (hand is XRBaseInputInteractor input)
                input.SendHapticImpulse(amplitude, duration);
        }

        void Play(AudioClip clip)
        {
            if (m_Audio != null && clip != null)
                m_Audio.PlayOneShot(clip);
        }

        // --- 임시 신호음 (TempSound) ---

        static AudioClip MakeError()
        {
            // 높은음 → 낮은음 두 번
            return TempSound.Build("Temp_Error", 0.28f, t =>
            {
                float f = t < 0.13f ? 520f : (t > 0.15f ? 380f : 0f);
                float s = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t));
                return f > 0f ? s * 0.25f : 0f;
            });
        }

        // 짧고 낮은 덜컥
        static AudioClip MakeBlocked() => TempSound.Clack("Temp_Blocked", 0.12f, 90f, 40f, 0.6f, 0.8f, 1);

        // 짧고 높은 철컥
        static AudioClip MakeClick() => TempSound.Clack("Temp_Click", 0.06f, 2200f, 120f, 0.5f, 0.8f, 2);

        static AudioClip MakeStep()
        {
            // 올라가는 두 음
            return TempSound.Build("Temp_Step", 0.22f, t =>
            {
                float f = t < 0.1f ? 660f : 880f;
                float env = 1f - Mathf.Repeat(t, 0.11f) / 0.11f;
                return Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.3f;
            });
        }
    }
}
