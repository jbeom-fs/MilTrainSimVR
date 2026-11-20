using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 한 사람(플레이어·적)의 몸 상태 — 하드코어 피격 규칙 (C 범위 문서 4번, 사용자 10/26):
    //   · 머리·심장 → 전사
    //   · 가슴·배 → 중상: 이동 속도 70% 감소, 30초 뒤 전사
    //   · 팔·다리 → 부상: 이동 속도 30% 감소
    //   · 부상·중상인데 한 번 더 총상을 입으면 → 전사
    // 플레이어와 적이 같은 규칙을 쓴다. 이 컴포넌트는 상태만 정하고, 이동 속도·화면·쓰러지는 모습은 상태를 듣는 쪽이 맡는다
    // (플레이어 = PlayerWoundEffects, 적 = EnemySoldier · EnemyMover). 맞은 기록은 결과 화면(평가)에서 읽는다
    public class CombatantHealth : MonoBehaviour
    {
        public enum State { Healthy, Wounded, Severe, Dead }   // 정상 · 부상 · 중상 · 전사

        // 맞은 한 번의 기록 — 어느 부위에 맞아 어떤 상태가 됐는지
        public struct HitRecord
        {
            public HitZone.Part part;
            public State result;
            public float time;
            public Vector3 point;
        }

        [Tooltip("기록·결과 화면에 쓸 이름 (비우면 오브젝트 이름)")]
        [SerializeField] string m_DisplayName;

        [Tooltip("중상에서 전사까지(초)")]
        [SerializeField] float m_BleedOutSeconds = 30f;

        [Tooltip("부상일 때 이동 속도 배율")]
        [SerializeField] float m_WoundedSpeed = 0.7f;

        [Tooltip("중상일 때 이동 속도 배율")]
        [SerializeField] float m_SevereSpeed = 0.3f;

        State m_State = State.Healthy;
        float m_DeathTime;
        readonly List<HitRecord> m_Hits = new List<HitRecord>();

        // 상태가 바뀔 때마다 (누가, 새 상태, 맞은 부위 — 중상 시간이 다 돼 전사하면 부위 없음)
        public event System.Action<CombatantHealth, State, HitZone.Part?> StateChanged;

        // 살아 있는 몸이 총에 맞을 때마다 (상태가 바뀌기 전에) — 화면 번쩍·진동처럼 '맞은 순간'에 쓴다
        public event System.Action<CombatantHealth, HitZone.Part, Vector3> Hit;

        public State Current => m_State;
        public bool IsAlive => m_State != State.Dead;
        public string DisplayName => string.IsNullOrEmpty(m_DisplayName) ? name : m_DisplayName;
        public IReadOnlyList<HitRecord> Hits => m_Hits;
        public float BleedOutSeconds => m_BleedOutSeconds;

        // 중상일 때 전사까지 남은 시간(초) — 그 밖엔 0
        public float BleedOutRemaining => m_State == State.Severe ? Mathf.Max(0f, m_DeathTime - Time.time) : 0f;

        public float SpeedMultiplier => m_State switch
        {
            State.Wounded => m_WoundedSpeed,
            State.Severe => m_SevereSpeed,
            State.Dead => 0f,
            _ => 1f,
        };

        // 총에 맞음 — 부위와 지금 상태로 다음 상태를 정한다. 이미 전사한 몸은 그대로
        public void TakeHit(HitZone.Part part, Vector3 point)
        {
            if (m_State == State.Dead)
                return;
            State next;
            if (m_State != State.Healthy)
                next = State.Dead;   // 두 번째 총상
            else
                next = part switch
                {
                    HitZone.Part.Head or HitZone.Part.Heart => State.Dead,
                    HitZone.Part.Chest or HitZone.Part.Abdomen => State.Severe,
                    _ => State.Wounded,
                };
            m_Hits.Add(new HitRecord { part = part, result = next, time = Time.time, point = point });
            Hit?.Invoke(this, part, point);
            SetState(next, part);
        }

        // 다시하기 — 처음 상태로
        public void ResetState()
        {
            m_Hits.Clear();
            SetState(State.Healthy, null);
        }

        void Update()
        {
            // 중상: 정해진 시간이 지나면 전사 (출혈)
            if (m_State == State.Severe && Time.time >= m_DeathTime)
                SetState(State.Dead, null);
        }

        void SetState(State next, HitZone.Part? part)
        {
            if (next == m_State)
                return;
            m_State = next;
            if (next == State.Severe)
                m_DeathTime = Time.time + m_BleedOutSeconds;
            Debug.Log($"[C] {DisplayName}: {StateName(next)}" + (part.HasValue ? $" ({PartName(part.Value)})" : next == State.Dead ? " (출혈)" : ""));
            StateChanged?.Invoke(this, next, part);
        }

        public static string StateName(State state) => state switch
        {
            State.Healthy => "정상",
            State.Wounded => "부상",
            State.Severe => "중상",
            _ => "전사",
        };

        public static string PartName(HitZone.Part part) => part switch
        {
            HitZone.Part.Head => "머리",
            HitZone.Part.Heart => "심장",
            HitZone.Part.Chest => "가슴",
            HitZone.Part.Abdomen => "배",
            HitZone.Part.Arm => "팔",
            _ => "다리",
        };
    }
}
