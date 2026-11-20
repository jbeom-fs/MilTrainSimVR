using MilTrainSimVR.A.Rifle;
using MilTrainSimVR.UrbanCombat;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using CMagazine = MilTrainSimVR.UrbanCombat.RifleMagazine;
using CMagazineWell = MilTrainSimVR.UrbanCombat.MagazineWell;

namespace MilTrainSimVR.A.CBridge
{
    // C 의 총(C_K2C1 — TempRifle · MagazineWell · RifleChargingHandle)을 A 사로의 약속(RangeRifle)에 맞춘다 (허브 사격 방식 = 실전 사격).
    // C 파일은 고치지 않고 C 의 공개 값 · 이벤트만 쓴다. 이 폴더는 asmdef 가 없어(Assembly-CSharp) C 코드를 볼 수 있다.
    // 11/17 오후: C 가 TempRifle 에 사로용 이벤트 · 함수를 열어(Docs/C/1117_A통지_C총_숨참기.md) 그것만 따라간다 —
    //   전에는 손 트리거를 직접 읽고 ShotsFired 를 매 프레임 보고, 되돌리기를 리플렉션으로 했다.
    //   · 실탄 · 안전에서 당김 · 조정간 · 장전 손잡이: C 이벤트를 그대로 넘긴다
    //   · 공이치기: C 총엔 없어서 여기서만 흉내 낸다 — 장전 손잡이를 끝까지 당기거나 실탄이 나가면 젖혀지고,
    //     약실이 빈 채 당기면(C DryFired) 젖혀져 있었으면 빈 격발(DryFired) 하고 떨어지고, 떨어져 있었으면 HammerDown. 사로 공격발 판정이 이걸 본다.
    //   · 빈 격발은 소리 없이 손에 약한 진동만 (C 가 낸다) — 사용자 11/17: 빈 격발 소리는 A · C 모두 뺀다
    // 탄창 놓기 · 시험 끼우기는 A 총과 같은 C 탄창 다리(CMagazineFeed)의 것을 쓴다 (11/17 총기 통합 5단계).
    // C 가 이 이름들을 바꾸면 이 파일도 맞춰야 한다 (Docs/A/1117_허브_사격방식_선택.md 4절).
    [RequireComponent(typeof(TempRifle))]
    public class CRangeRifle : RangeRifle
    {
        [Tooltip("사로에 놓는 C 탄창 (Assets/C/Prefabs/C_Magazine.prefab)")]
        [SerializeField] CMagazine m_MagazinePrefab;

        [Tooltip("총구 — 비우면 Anchor_Muzzle 을 찾는다")]
        [SerializeField] Transform m_Muzzle;

        TempRifle m_Rifle;
        CMagazineWell m_Well;
        RifleChargingHandle m_Handle;
        XRGrabInteractable m_Grab;

        bool m_HammerCocked;

        public override event System.Action Fired;
        public override event System.Action DryFired;
        public override event System.Action TriggerBlocked;
        public override event System.Action HammerDown;
        public override event System.Action<FireMode> FireModeChanged;
        public override event System.Action ChargingHandlePulled;
        public override event System.Action ChargingHandleReturned;
        public override event System.Action MagazineInserted;
        public override event System.Action MagazineRemoved;

        public override FireMode Mode => (FireMode)(int)m_Rifle.Mode;
        public override bool HasMagazine => m_Well != null && m_Well.HasMagazine;
        public override int MagazineRounds => m_Well != null ? m_Well.Rounds : 0;
        public override bool Chambered => m_Rifle.Chambered;
        public override bool HammerCocked => m_HammerCocked;
        public override bool IsHeld => m_Grab != null && m_Grab.isSelected;
        public override Vector3 MuzzleForward => m_Muzzle != null ? m_Muzzle.forward : transform.forward;
        public override float ChargingOffset => m_Handle != null ? m_Handle.Offset : 0f;
        public override float ChargingStroke => m_Handle != null ? m_Handle.Stroke : 0f;

        void Awake()
        {
            m_Rifle = GetComponent<TempRifle>();
            m_Well = GetComponent<CMagazineWell>();
            m_Handle = GetComponent<RifleChargingHandle>();
            m_Grab = GetComponent<XRGrabInteractable>();
            if (m_Muzzle == null)
                m_Muzzle = transform.Find("Anchor_Muzzle");

            m_Rifle.Fired += OnFired;
            m_Rifle.DryFired += OnDryFired;
            m_Rifle.TriggerBlocked += OnTriggerBlocked;
            m_Rifle.FireModeChanged += OnFireModeChanged;
            m_Rifle.ChargingHandlePulled += OnHandlePulled;
            m_Rifle.ChargingHandleReturned += OnHandleReturned;
            if (m_Well != null)
            {
                m_Well.Inserted += OnInserted;
                m_Well.Removed += OnRemoved;
            }
        }

        void OnDestroy()
        {
            if (m_Rifle != null)
            {
                m_Rifle.Fired -= OnFired;
                m_Rifle.DryFired -= OnDryFired;
                m_Rifle.TriggerBlocked -= OnTriggerBlocked;
                m_Rifle.FireModeChanged -= OnFireModeChanged;
                m_Rifle.ChargingHandlePulled -= OnHandlePulled;
                m_Rifle.ChargingHandleReturned -= OnHandleReturned;
            }
            if (m_Well != null)
            {
                m_Well.Inserted -= OnInserted;
                m_Well.Removed -= OnRemoved;
            }
        }

        // 실탄 — 노리쇠가 저절로 뒤로 갔다 오며 공이치기를 다시 젖힌다. C 는 명중 처리가 끝난 뒤 알린다 (조 닫기 순서가 A 총과 같다)
        void OnFired()
        {
            m_HammerCocked = true;
            Fired?.Invoke();
        }

        // 약실이 빈 채 당김 — 공이치기가 젖혀져 있었으면 빈 격발하며 떨어지고, 이미 떨어져 있었으면 아무 일 없음
        void OnDryFired()
        {
            if (m_HammerCocked)
            {
                m_HammerCocked = false;
                DryFired?.Invoke();
            }
            else
            {
                HammerDown?.Invoke();
            }
        }

        void OnTriggerBlocked() => TriggerBlocked?.Invoke();
        void OnFireModeChanged(TempRifle.FireMode mode) => FireModeChanged?.Invoke((FireMode)(int)mode);
        void OnInserted() => MagazineInserted?.Invoke();
        void OnRemoved() => MagazineRemoved?.Invoke();

        void OnHandlePulled()
        {
            m_HammerCocked = true;
            ChargingHandlePulled?.Invoke();
        }

        void OnHandleReturned() => ChargingHandleReturned?.Invoke();

        public override void PullTrigger() => m_Rifle.PullTrigger();
        public override void ReleaseTrigger() => m_Rifle.ReleaseTrigger();
        public override void SetFireMode(FireMode mode) => m_Rifle.SetFireMode((TempRifle.FireMode)(int)mode);

        public override void ResetState()
        {
            m_Rifle.ResetState();
            m_HammerCocked = false;
        }

        public override bool ResetMagazines(Vector3 position, Quaternion rotation, int rounds)
        {
            CMagazineFeed.ResetCMagazines(m_MagazinePrefab, position, rotation, rounds);
            return true;   // A 총과 같은 C 탄창 — 놓지 못해도 A 쪽으로 넘기지 않는다
        }

        // 시험용 (손 없이) — 놓인 C 탄창 하나를 꽂는다 (CMagazineFeed 와 같은 길)
        public override void TestInsertMagazine() => CMagazineFeed.TestInsert(m_Well, OnInserted);

        public override void TestRemoveMagazine()
        {
            if (m_Well != null)
                m_Well.Drop();
        }

        public override void TestCycleChargingHandle() => m_Rifle.TestCycleChargingHandle();
    }
}
