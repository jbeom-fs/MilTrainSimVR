using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;

namespace MilTrainSimVR.UrbanCombat
{
    // 손에 든 물건은 밟고 설 땅이 아니다 (11/12 사용자: "특정 상황에서 점프를 하고 내려올 때 착지하지 않고 공중부양을 하는 경우가 있다")
    // XRI 의 GravityProvider 는 머리에서 발끝까지 아래로 구(반지름 9cm)를 쏴서 무엇이든 맞으면 '서 있다'로 보고 중력을 끈다.
    // 조준(뺨에 개머리판) · 로우 레디 · 가슴 앞에 든 총은 머리 바로 아래라 그 구에 맞는다 → 점프 꼭대기에서 중력이 꺼져 그대로 떠 있었다.
    // Play 모드에서 재현 (11/12, 헤드셋 없이 K2C1 을 머리에 붙여서): 네 자세 모두 1.77 m 에 멈췄고, 착지 판정이 맞은 물체는 매번 그 총이었다.
    // 고친 방법 — XRI 코드는 그대로 두고, GravityProvider 가 땅을 보는 그 순간에만 손에 든 물건의 충돌체를 Ignore Raycast 레이어
    // (땅 판정 마스크 밖)로 옮겼다가 곧바로 되돌린다: 이 컴포넌트가 GravityProvider 바로 앞에서 옮기고, HeldItemGroundFilterRestore 가 바로 뒤에서 되돌린다.
    // 레이어를 든 동안 내내 바꿔 두지 않는 건 — 앞손이 든 총을 다시 잡는 근접 잡기(마스크 Default 만)와 총알 판정이 든 총을 그대로 봐야 해서다.
    // 몸통(CharacterController)도 손에 든 물건과 부딪히지 않게 한다 — 든 동안만 IgnoreCollision.
    [DefaultExecutionOrder(XRInteractionUpdateOrder.k_GravityProvider - 1)]
    public class HeldItemGroundFilter : MonoBehaviour
    {
        const int k_IgnoreRaycastLayer = 2;

        XRBaseInteractor[] m_Hands = new XRBaseInteractor[0];
        CharacterController m_Body;
        bool m_GroundCheckSkipsLayer;

        readonly List<Collider> m_Held = new List<Collider>();
        readonly HashSet<Collider> m_HeldSet = new HashSet<Collider>();
        readonly List<Collider> m_BodyIgnores = new List<Collider>();
        readonly List<Collider> m_Scratch = new List<Collider>();
        readonly List<GameObject> m_Moved = new List<GameObject>();
        readonly List<int> m_MovedLayers = new List<int>();

        void Awake()
        {
            XROrigin rig = GetComponentInParent<XROrigin>();
            if (rig == null)
            {
                enabled = false;
                return;
            }
            m_Hands = rig.GetComponentsInChildren<XRBaseInteractor>(true);
            // GravityProvider 와 같은 자리에서 몸통을 찾는다 — Origin 먼저, 없으면 XR Origin
            if (rig.Origin == null || !rig.Origin.TryGetComponent(out m_Body))
                rig.TryGetComponent(out m_Body);
            GravityProvider gravity = rig.GetComponentInChildren<GravityProvider>(true);
            m_GroundCheckSkipsLayer = gravity != null && (gravity.sphereCastLayerMask.value & (1 << k_IgnoreRaycastLayer)) == 0;
            if (gravity != null && !m_GroundCheckSkipsLayer)
                Debug.LogWarning("[C] GravityProvider 의 땅 판정 마스크에 Ignore Raycast 가 들어 있어, 손에 든 물건을 땅 판정에서 뺄 수 없다", this);
            if (!TryGetComponent(out HeldItemGroundFilterRestore restore))
                restore = gameObject.AddComponent<HeldItemGroundFilterRestore>();
            restore.Filter = this;
        }

        void OnDisable()
        {
            Restore();
            foreach (Collider c in m_BodyIgnores)
                SetBodyIgnore(c, false);
            m_BodyIgnores.Clear();
        }

        void Update()
        {
            Restore();
            CollectHeld();
            UpdateBodyIgnores();
            if (m_GroundCheckSkipsLayer)
                MoveOutOfGroundCheck();
        }

        // 이번 프레임에 이 리그의 손(인터랙터)이 쥐고 있는 물건들의 충돌체 — 총에 꽂힌 탄창처럼 그 아래 붙은 것까지
        void CollectHeld()
        {
            m_Held.Clear();
            m_HeldSet.Clear();
            foreach (XRBaseInteractor hand in m_Hands)
            {
                if (hand == null || !hand.isActiveAndEnabled || !hand.hasSelection)
                    continue;
                foreach (IXRSelectInteractable item in hand.interactablesSelected)
                {
                    if (!(item is Object alive) || alive == null)
                        continue;
                    item.transform.GetComponentsInChildren(m_Scratch);
                    foreach (Collider c in m_Scratch)
                    {
                        if (c.enabled && !c.isTrigger && m_HeldSet.Add(c))
                            m_Held.Add(c);
                    }
                }
            }
        }

        // 새로 든 것은 몸통과 안 부딪히게, 내려놓은 것은 다시 부딪히게
        void UpdateBodyIgnores()
        {
            if (m_Body == null)
                return;
            for (int i = m_BodyIgnores.Count - 1; i >= 0; i--)
            {
                Collider c = m_BodyIgnores[i];
                if (c != null && m_HeldSet.Contains(c))
                    continue;
                SetBodyIgnore(c, false);
                m_BodyIgnores.RemoveAt(i);
            }
            foreach (Collider c in m_Held)
            {
                if (m_BodyIgnores.Contains(c))
                    continue;
                SetBodyIgnore(c, true);
                m_BodyIgnores.Add(c);
            }
        }

        void SetBodyIgnore(Collider c, bool ignore)
        {
            if (c != null && m_Body != null)
                Physics.IgnoreCollision(m_Body, c, ignore);
        }

        void MoveOutOfGroundCheck()
        {
            foreach (Collider c in m_Held)
            {
                GameObject go = c.gameObject;
                if (go.layer == k_IgnoreRaycastLayer || m_Moved.Contains(go))
                    continue;
                m_Moved.Add(go);
                m_MovedLayers.Add(go.layer);
                go.layer = k_IgnoreRaycastLayer;
            }
        }

        // GravityProvider 가 땅을 본 직후(HeldItemGroundFilterRestore) — 옮겼던 레이어를 그대로 되돌린다
        public void Restore()
        {
            for (int i = 0; i < m_Moved.Count; i++)
            {
                if (m_Moved[i] != null)
                    m_Moved[i].layer = m_MovedLayers[i];
            }
            m_Moved.Clear();
            m_MovedLayers.Clear();
        }
    }
}
