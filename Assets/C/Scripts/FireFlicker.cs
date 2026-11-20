using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 불붙은 차 · 잔해의 일렁이는 불빛 (사용자 11/17 — 서쪽 지하 입구에 처박혀 불타는 차).
    // 점 조명 세기를 두 겹 펄린 잡음으로 흔든다 — 느린 출렁임 + 빠른 깜빡임. 그림자는 끈 조명에 붙인다 (VR 비용)
    // 같은 오브젝트에 불 소리(AudioSource, 반복)가 있으면 시작 자리를 무작위로 하고 크기도 불빛과 같이 조금 출렁이게 한다
    [RequireComponent(typeof(Light))]
    public class FireFlicker : MonoBehaviour
    {
        [Tooltip("평균 세기")]
        [SerializeField] float m_Intensity = 4f;

        [Tooltip("세기가 흔들리는 비율 (0.35 = ±35 %)")]
        [SerializeField] float m_Flicker = 0.35f;

        [Tooltip("흔들림 빠르기")]
        [SerializeField] float m_Speed = 7f;

        [Tooltip("불 소리 크기가 불빛과 같이 흔들리는 비율 (0.15 = ±15 %)")]
        [SerializeField] float m_SoundFlicker = 0.15f;

        Light m_Light;
        AudioSource m_Sound;
        float m_Volume;
        float m_Seed;

        void Awake()
        {
            m_Light = GetComponent<Light>();
            m_Sound = GetComponent<AudioSource>();
            m_Seed = Random.value * 100f;
            if (m_Sound != null)
            {
                m_Volume = m_Sound.volume;
                // 불이 여러 곳이어도 같은 자리부터 나오지 않게
                if (m_Sound.clip != null)
                    m_Sound.time = Random.value * m_Sound.clip.length;
                if (!m_Sound.isPlaying && m_Sound.playOnAwake)
                    m_Sound.Play();
            }
        }

        void Update()
        {
            float t = Time.time * m_Speed;
            float n = Mathf.PerlinNoise(m_Seed, t) * 0.7f + Mathf.PerlinNoise(m_Seed + 13f, t * 2.3f) * 0.3f;
            m_Light.intensity = m_Intensity * (1f + (n - 0.5f) * 2f * m_Flicker);
            if (m_Sound != null)
                m_Sound.volume = m_Volume * (1f + (n - 0.5f) * 2f * m_SoundFlicker);
        }
    }
}
