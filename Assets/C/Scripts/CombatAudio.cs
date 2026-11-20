using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 전투 소리 (11/16) — 총소리를 '어디서 쐈는지' 들리게 낸다. 플레이어 리그(C_XR Origin — 소리를 듣는 머리가 있는 곳)에 붙는다.
    // 모든 총(플레이어 K2C1 · 적)의 사격을 Gunfire.ShotFired 로 듣기만 한다 → 총 코드는 사격음을 몰라도 된다 (탄착 효과 ImpactEffects 와 같은 방식).
    // 한 발마다 쏜 자리에 3D 소리를 하나 놓고:
    //   · 거리 — 멀수록 작게(로그 감쇠) + 고음을 깎아 둔하게 (멀리서 나는 총소리는 "탕"보다 "쿵")
    //   · 소리 속도 — 343 m/s 만큼 늦게 (60 m 면 0.17초 — 총구 불빛이 먼저 보이고 소리가 뒤에 온다)
    //   · 가림 — 쏜 자리와 내 머리 사이를 벽이 막았으면 더 작고 더 둔하게 (벽 하나 / 둘 이상)
    //   · 쏜 자리가 실내면(위로 지붕 · 윗층 바닥이 있으면) 그 소리에만 돌방 울림을 얹는다. 실외는 클립에 든 꼬리 그대로.
    //     Unity 의 리버브 존은 '듣는 사람' 자리를 보므로, 쏜 자리마다 다른 울림은 소리 하나하나에 붙인 필터로 한다
    // 플레이어 자기 총은 거리 · 속도 · 가림 없이 총구에서 나고 실내 울림만 같은 규칙을 쓴다. 매 발 탄피가 발밑에 떨어지는 소리도 낸다.
    // 방향은 Unity 기본 3D(좌우 음량 차이)라 앞뒤 · 위아래 구분은 약하다 — 그건 HRTF 플러그인(Steam Audio 등)이 있어야 한다 (11/16 결정: 1단계는 플러그인 없이).
    // 클립은 임시다 (11/16 사용자가 가져온 녹음을 잘라 Assets/C/Audio 에 둠) — 나중에 만든 것 · 산 것을 같은 파일 이름으로 덮어쓰면 그대로 바뀐다
    public class CombatAudio : MonoBehaviour
    {
        public enum Kind { Step, Casing, Impact, Shot }   // 목소리가 모자라면 앞쪽(덜 중요한 것)부터 끊는다

        [Header("사격")]
        [Tooltip("사격음 — 여러 개면 무작위. 플레이어 · 적이 같이 쓴다 (둘 다 K2C1)")]
        [SerializeField] AudioClip[] m_ShotClips;
        [SerializeField] float m_ShotVolume = 1f;
        [Tooltip("적 총소리가 이 거리(m) 안에서는 줄지 않는다 — 그 밖은 거리에 반비례 (로그 감쇠)")]
        [SerializeField] float m_ShotMinDistance = 6f;
        [SerializeField] float m_ShotMaxDistance = 300f;
        [Tooltip("한 발마다 음높이를 이만큼(±) 흔든다 — 연발이 같은 소리의 반복으로 들리지 않게")]
        [SerializeField] float m_PitchJitter = 0.03f;

        [Header("거리 · 가림")]
        [SerializeField] float m_SpeedOfSound = 343f;
        [Tooltip("이 거리(m)까지는 고음을 그대로 둔다")]
        [SerializeField] float m_ClearDistance = 10f;
        [Tooltip("이 거리(m)에서 고음을 아래 주파수까지 깎는다 (사이는 로그로 이어짐)")]
        [SerializeField] float m_FarDistance = 150f;
        [SerializeField] float m_FarCutoff = 2500f;
        [Tooltip("벽 하나에 막혔을 때 — 소리 크기 배율, 고음 상한(Hz)")]
        [SerializeField] float m_WallVolume = 0.45f;
        [SerializeField] float m_WallCutoff = 1200f;
        [Tooltip("벽 둘 이상에 막혔을 때")]
        [SerializeField] float m_ThickWallVolume = 0.25f;
        [SerializeField] float m_ThickWallCutoff = 600f;

        [Header("실내 울림 (쏜 자리 기준)")]
        [SerializeField] AudioReverbPreset m_IndoorReverb = AudioReverbPreset.Stoneroom;
        [Tooltip("실외 — Off 면 클립에 든 꼬리만")]
        [SerializeField] AudioReverbPreset m_OutdoorReverb = AudioReverbPreset.Off;
        [Tooltip("쏜 자리 위로 이 높이(m) 안에 지붕 · 윗층 바닥이 있으면 실내로 본다")]
        [SerializeField] float m_RoofCheckHeight = 25f;

        [Header("탄피 · 탄창")]
        [SerializeField] AudioClip[] m_CasingClips;
        [SerializeField] float m_CasingVolume = 0.35f;
        [Tooltip("탄피 소리를 한꺼번에 몇 개까지 — 연발(초당 12.5발)에 목소리를 다 쓰지 않게")]
        [SerializeField] int m_MaxCasings = 4;
        [Tooltip("탄피가 튀어 나가는 거리(m, 총 오른쪽) — 그 아래 바닥에 떨어진다")]
        [SerializeField] float m_CasingThrow = 0.6f;
        [SerializeField] AudioClip[] m_MagazineDropClips;
        [SerializeField] float m_MagazineDropVolume = 0.8f;

        [Header("목소리")]
        [Tooltip("동시에 날 수 있는 소리 수")]
        [SerializeField] int m_Voices = 24;

        class Voice
        {
            public AudioSource Source;
            public AudioLowPassFilter LowPass;
            public AudioReverbFilter Reverb;
            public float BusyUntil;
            public float StartedAt;
            public Kind Kind;
        }

        // 한 소리를 낼 때 정하는 것들 — 기본값은 '그냥 그 자리에서 3D 로'
        public struct Sound
        {
            public AudioClip Clip;
            public Vector3 Position;
            public float Volume;
            public float Pitch;
            public float MinDistance;
            public float MaxDistance;
            public float SpatialBlend;
            public float Delay;
            public float Cutoff;
            public AudioReverbPreset Reverb;
            public Kind Kind;
        }

        static CombatAudio s_Instance;
        public static CombatAudio Instance => s_Instance;

        static readonly RaycastHit[] s_Hits = new RaycastHit[16];

        readonly List<Voice> m_Pool = new List<Voice>();
        Transform m_VoiceRoot;
        Transform m_Rig;
        Transform m_Head;
        int m_LastShot = -1;

        // 테스트(Unity CLI)가 읽는다 — 마지막으로 낸 사격음이 어떻게 정해졌는지
        public Sound LastShot { get; private set; }
        public int LastShotWalls { get; private set; }
        public bool LastShotIndoors { get; private set; }

        void Awake()
        {
            XROrigin rig = GetComponentInParent<XROrigin>();
            m_Rig = rig != null ? rig.transform : transform;
            m_Head = rig != null && rig.Camera != null ? rig.Camera.transform : null;
            m_VoiceRoot = new GameObject("C_CombatAudioVoices").transform;
            for (int i = 0; i < Mathf.Max(4, m_Voices); i++)
            {
                var go = new GameObject($"Voice {i}");
                go.transform.SetParent(m_VoiceRoot, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.dopplerLevel = 0f;
                var lowPass = go.AddComponent<AudioLowPassFilter>();
                lowPass.enabled = false;
                var reverb = go.AddComponent<AudioReverbFilter>();
                reverb.enabled = false;
                m_Pool.Add(new Voice { Source = source, LowPass = lowPass, Reverb = reverb });
            }
        }

        void OnDestroy()
        {
            if (m_VoiceRoot != null)
                Destroy(m_VoiceRoot.gameObject);
        }

        void OnEnable()
        {
            s_Instance = this;
            Gunfire.ShotFired += OnShotFired;
        }

        void OnDisable()
        {
            Gunfire.ShotFired -= OnShotFired;
            if (s_Instance == this)
                s_Instance = null;
        }

        // ---------------------------------------------------------------- 사격

        void OnShotFired(Vector3 origin, Component shooter)
        {
            AudioClip clip = Pick(m_ShotClips, ref m_LastShot);
            if (clip == null)
                return;
            // 플레이어 총(TempRifle)은 손에서, 적(EnemyShooter)은 멀리서 — 적만 거리 · 소리 속도 · 가림을 계산한다
            bool own = shooter is TempRifle;
            Transform ignore = shooter != null ? shooter.transform : null;
            Sound s = Basic(clip, origin, m_ShotVolume, Kind.Shot);
            s.Pitch = 1f + Random.Range(-m_PitchJitter, m_PitchJitter);
            s.MinDistance = own ? 1f : m_ShotMinDistance;
            s.MaxDistance = m_ShotMaxDistance;
            // 실내인지는 총구가 아니라 몸으로 본다 — 창가에 선 적은 총구가 창밖으로 나가 있어 위가 하늘이었다 (11/16 Play 모드, N05 3층)
            Vector3 body = own ? (m_Head != null ? m_Head.position : origin) : ignore != null ? ignore.position + Vector3.up * 1.5f : origin;
            LastShotIndoors = IsIndoors(body, ignore);
            s.Reverb = LastShotIndoors ? m_IndoorReverb : m_OutdoorReverb;
            LastShotWalls = 0;
            if (!own)
                LastShotWalls = AddDistanceAndWalls(ref s, ignore, true);
            LastShot = s;
            Play(s);
            if (own)
                DropCasing(shooter.transform);
        }

        // 매 발 탄피 — 총 오른쪽(배출구 쪽)으로 튀어 그 아래 바닥에 떨어진다. 떨어지는 시간만큼 늦게
        void DropCasing(Transform rifle)
        {
            Vector3 from = rifle.position + Vector3.ProjectOnPlane(rifle.right, Vector3.up).normalized * m_CasingThrow;
            PlayCasing(from, 0f);
        }

        void PlayCasing(Vector3 from, float extraDelay)
        {
            AudioClip clip = Pick(m_CasingClips);
            if (clip == null || !GroundBelow(from, 3f, out Vector3 ground))
                return;
            float fall = Mathf.Sqrt(2f * Mathf.Max(0.05f, from.y - ground.y) / Mathf.Abs(Physics.gravity.y));
            Sound s = Basic(clip, ground, m_CasingVolume, Kind.Casing);
            s.Pitch = 1f + Random.Range(-0.08f, 0.08f);
            s.MinDistance = 0.5f;
            s.MaxDistance = 15f;
            s.Delay = fall + extraDelay;
            Play(s);
        }

        // 장전 손잡이를 당겨 약실에서 빠진 탄 (TempRifle)
        public static void PlayEjectedRound(Transform rifle)
        {
            if (s_Instance != null && rifle != null)
                s_Instance.PlayCasing(rifle.position + Vector3.ProjectOnPlane(rifle.right, Vector3.up).normalized * 0.3f, 0f);
        }

        // 떨어뜨린 탄창이 바닥 · 벽에 부딪힘 (RifleMagazine) — 세게 부딪힐수록 크게
        public static void PlayMagazineDrop(Vector3 at, float speed)
        {
            if (s_Instance == null)
                return;
            AudioClip clip = Pick(s_Instance.m_MagazineDropClips);
            if (clip == null)
                return;
            Sound s = s_Instance.Basic(clip, at, s_Instance.m_MagazineDropVolume * Mathf.InverseLerp(0.5f, 4f, speed), Kind.Impact);
            s.Pitch = 1f + Random.Range(-0.05f, 0.05f);
            s.MinDistance = 0.7f;
            s.MaxDistance = 20f;
            s_Instance.AddDistanceAndWalls(ref s, null, false);
            s_Instance.Play(s);
        }

        // ---------------------------------------------------------------- 공용

        public Sound Basic(AudioClip clip, Vector3 position, float volume, Kind kind)
        {
            return new Sound
            {
                Clip = clip, Position = position, Volume = volume, Pitch = 1f,
                MinDistance = 1f, MaxDistance = 30f, SpatialBlend = 1f, Delay = 0f,
                Cutoff = 22000f, Reverb = AudioReverbPreset.Off, Kind = kind,
            };
        }

        // 멀리서 난 소리 — 거리만큼 고음을 깎고, 벽에 막혔으면 더 작고 둔하게, (delay 면) 소리 속도만큼 늦게. 막은 벽 수를 돌려준다
        public int AddDistanceAndWalls(ref Sound s, Transform ignore, bool delay)
        {
            if (m_Head == null)
                return 0;
            Vector3 to = m_Head.position - s.Position;
            float distance = to.magnitude;
            if (delay && m_SpeedOfSound > 1f)
                s.Delay += distance / m_SpeedOfSound;
            if (distance > m_ClearDistance)
            {
                float t = Mathf.InverseLerp(m_ClearDistance, m_FarDistance, distance);
                s.Cutoff = Mathf.Min(s.Cutoff, Mathf.Exp(Mathf.Lerp(Mathf.Log(22000f), Mathf.Log(m_FarCutoff), t)));
            }
            int walls = CountWalls(s.Position, to, distance, ignore);
            if (walls == 1)
            {
                s.Volume *= m_WallVolume;
                s.Cutoff = Mathf.Min(s.Cutoff, m_WallCutoff);
            }
            else if (walls >= 2)
            {
                s.Volume *= m_ThickWallVolume;
                s.Cutoff = Mathf.Min(s.Cutoff, m_ThickWallCutoff);
            }
            return walls;
        }

        public void Play(Sound s)
        {
            if (s.Clip == null || s.Volume <= 0.001f)
                return;
            Voice v = Take(s.Kind);
            if (v == null)
                return;
            AudioSource source = v.Source;
            source.Stop();
            source.transform.position = s.Position;
            source.clip = s.Clip;
            source.volume = Mathf.Clamp01(s.Volume);
            source.pitch = s.Pitch;
            source.spatialBlend = s.SpatialBlend;
            source.minDistance = s.MinDistance;
            source.maxDistance = Mathf.Max(s.MinDistance + 0.1f, s.MaxDistance);
            v.LowPass.enabled = s.Cutoff < 21000f;
            v.LowPass.cutoffFrequency = Mathf.Clamp(s.Cutoff, 200f, 22000f);
            v.Reverb.enabled = s.Reverb != AudioReverbPreset.Off;
            if (v.Reverb.enabled)
                v.Reverb.reverbPreset = s.Reverb;
            if (s.Delay > 0.005f)
                source.PlayDelayed(s.Delay);
            else
                source.Play();
            v.StartedAt = Time.time;
            v.BusyUntil = Time.time + s.Delay + s.Clip.length / Mathf.Max(0.1f, s.Pitch) + 0.05f;
            v.Kind = s.Kind;
        }

        // 쉬는 목소리 — 없으면 덜 중요한 종류 중 가장 오래된 것을 끊는다. 탄피는 한꺼번에 m_MaxCasings 개까지만
        Voice Take(Kind kind)
        {
            float now = Time.time;
            if (kind == Kind.Casing)
            {
                int busy = 0;
                foreach (Voice v in m_Pool)
                {
                    if (v.Kind == Kind.Casing && v.BusyUntil > now)
                        busy++;
                }
                if (busy >= m_MaxCasings)
                    return null;
            }
            Voice pick = null;
            foreach (Voice v in m_Pool)
            {
                if (v.BusyUntil <= now)
                    return v;
                if (v.Kind > kind)
                    continue;
                if (pick == null || v.Kind < pick.Kind || (v.Kind == pick.Kind && v.StartedAt < pick.StartedAt))
                    pick = v;
            }
            return pick;
        }

        // 쏜 자리 위가 막혔으면 실내 (지붕 · 윗층 바닥 · 지하도 천장). 움직이는 물체 · 쏜 사람 · 내 몸은 보지 않는다
        public bool IsIndoors(Vector3 point, Transform ignore)
        {
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 0.05f, Vector3.up, s_Hits, m_RoofCheckHeight, Gunfire.ShotMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!Skip(s_Hits[i].collider, ignore))
                    return true;
            }
            return false;
        }

        int CountWalls(Vector3 from, Vector3 to, float distance, Transform ignore)
        {
            if (distance < 0.01f)
                return 0;
            int count = Physics.RaycastNonAlloc(from, to / distance, s_Hits, distance, Gunfire.ShotMask, QueryTriggerInteraction.Ignore);
            int walls = 0;
            for (int i = 0; i < count; i++)
            {
                if (!Skip(s_Hits[i].collider, ignore))
                    walls++;
            }
            return walls;
        }

        bool GroundBelow(Vector3 from, float depth, out Vector3 ground)
        {
            int count = Physics.RaycastNonAlloc(from, Vector3.down, s_Hits, depth, Gunfire.ShotMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            ground = from;
            for (int i = 0; i < count; i++)
            {
                if (Skip(s_Hits[i].collider, null) || s_Hits[i].distance >= best)
                    continue;
                best = s_Hits[i].distance;
                ground = s_Hits[i].point;
            }
            return best < float.MaxValue;
        }

        // 소리를 막는 벽 · 지붕이 아닌 것 — 움직이는 물체(총 · 탄창 · 쓰러지는 몸), 사람 몸 판정, 쏜 사람, 내 리그
        bool Skip(Collider c, Transform ignore)
        {
            if (c.attachedRigidbody != null || c.GetComponent<HitZone>() != null)
                return true;
            Transform t = c.transform;
            return (ignore != null && t.IsChildOf(ignore)) || (m_Rig != null && t.IsChildOf(m_Rig));
        }

        static AudioClip Pick(AudioClip[] clips)
        {
            int last = -1;
            return Pick(clips, ref last);
        }

        // 무작위로 하나 — 여러 개면 바로 앞과 다른 것
        static AudioClip Pick(AudioClip[] clips, ref int last)
        {
            if (clips == null || clips.Length == 0)
                return null;
            int i = Random.Range(0, clips.Length);
            if (clips.Length > 1 && i == last)
                i = (i + 1) % clips.Length;
            last = i;
            return clips[i];
        }
    }
}
