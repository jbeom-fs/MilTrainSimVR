using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 유리 파편 · 쨍그랑 (11/18) — 깨지는 유리(BreakableGlass)들이 같이 쓰는 하나. 처음 깨질 때 프리팹(C_GlassShards)에서 하나 만든다.
    //   · 파편: 월드 공간 파티클 하나에 몰아 넣는다 (메시 파편 · 중력 · 바닥에 튕김). 칸 하나에 파편 하나쯤
    //   · 소리: 깨질 때마다 4개 중 하나(C_GlassBreak_1~4 — 사용자가 준 소리 3개, 하나는 둘로 잘라 4개. 피치 · 음량 맞춤)를 그 자리에서,
    //     피치를 ±5% 흔들어 — 같은 소리가 연달아 나도 덜 똑같게
    public class GlassShards : MonoBehaviour
    {
        [SerializeField] ParticleSystem m_Particles;
        [SerializeField] AudioClip[] m_Clips = new AudioClip[0];
        [SerializeField] Vector2 m_Pitch = new Vector2(0.95f, 1.05f);
        [SerializeField] float m_Volume = 1f;
        [Tooltip("소리가 줄기 시작하는 거리 · 들리는 끝 (m)")]
        [SerializeField] float m_MinDistance = 2f;
        [SerializeField] float m_MaxDistance = 45f;

        int m_LastClip = -1;

        public void Shard(Vector3 position, Vector3 velocity, float size)
        {
            if (m_Particles == null)
                return;
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
                angularVelocity3D = Random.insideUnitSphere * 540f,
                startLifetime = Random.Range(3f, 5f),   // 바닥에 떨어진 파편이 잠깐 남아 보이게 (끝 15% 동안 작아지며 사라진다)
                applyShapeToPosition = false,
            };
            m_Particles.Emit(p, 1);
        }

        public void Sound(Vector3 position, float volume, float pitchScale)
        {
            if (m_Clips.Length == 0)
                return;
            int pick = Random.Range(0, m_Clips.Length);
            if (m_Clips.Length > 1 && pick == m_LastClip)
                pick = (pick + 1) % m_Clips.Length;   // 같은 소리가 바로 또 나지 않게
            m_LastClip = pick;
            AudioClip clip = m_Clips[pick];
            if (clip == null)
                return;
            var go = new GameObject("쨍그랑");
            go.transform.position = position;
            var audio = go.AddComponent<AudioSource>();
            audio.clip = clip;
            audio.volume = m_Volume * volume;
            audio.pitch = Random.Range(m_Pitch.x, m_Pitch.y) * pitchScale;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            audio.minDistance = m_MinDistance;
            audio.maxDistance = m_MaxDistance;
            audio.dopplerLevel = 0f;
            audio.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, audio.pitch) + 0.2f);
        }
    }
}
