// 원본: Assets/C/Scripts/MuzzleFlash.cs (커밋 a1a51ac, C 이주석). A 가 복사해 namespace 만 바꿨다 — Docs/A/1027_C사격코드_재사용검토.md
using UnityEngine;

namespace MilTrainSimVR.A.Rifle
{
    // 총구 화염 — 쏠 때마다 파티클 몇 개를 뿜고, 아주 잠깐 불빛을 켠다.
    // 파티클은 계속 재생 중(자동 방출은 0)으로 두고 Emit 으로만 뿜는다 — 멈춘 파티클은 Emit 해도 움직이지 않는다.
    public class MuzzleFlash : MonoBehaviour
    {
        [Tooltip("화염 파티클 (자동 방출 끔)")]
        [SerializeField] ParticleSystem m_Particles;

        [Tooltip("한 번에 뿜는 파티클 수")]
        [SerializeField] int m_Count = 6;

        [Tooltip("총구 불빛 (평소엔 꺼 둔다)")]
        [SerializeField] Light m_Light;

        [Tooltip("불빛이 켜져 있는 시간(초)")]
        [SerializeField] float m_LightSeconds = 0.04f;

        float m_LightOffTime;

        void Awake()
        {
            if (m_Light != null)
                m_Light.enabled = false;
        }

        public void Play()
        {
            if (m_Particles != null)
                m_Particles.Emit(m_Count);
            if (m_Light != null)
            {
                m_Light.enabled = true;
                m_LightOffTime = Time.time + m_LightSeconds;
            }
        }

        void Update()
        {
            if (m_Light != null && m_Light.enabled && Time.time >= m_LightOffTime)
                m_Light.enabled = false;
        }
    }
}
