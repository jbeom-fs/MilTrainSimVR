using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.UrbanCombat
{
    // 탄착 효과 — 총알이 맞은 자리에 불꽃을 튀기고 탄흔을 남긴다.
    // 모든 총(플레이어·적)의 탄착을 Gunfire.SurfaceHit 로 듣기만 한다 → 총 코드는 효과를 몰라도 된다 (10/26 전엔 소총 하나만 들었다).
    // 탄흔은 개수 상한을 두고 가장 오래된 것부터 다시 쓴다 — 계속 새로 만들면 쌓여서 느려진다.
    public class ImpactEffects : MonoBehaviour
    {
        [Tooltip("불꽃 파티클 (월드 공간, 자동 방출 끔)")]
        [SerializeField] ParticleSystem m_Sparks;

        [SerializeField] int m_SparkCount = 8;
        [SerializeField] float m_SparkSpeed = 2.5f;

        [Tooltip("탄흔 재질 — 사각형(Quad)에 입힌다")]
        [SerializeField] Material m_HoleMaterial;

        [Tooltip("탄흔 지름(m)")]
        [SerializeField] float m_HoleSize = 0.03f;

        [SerializeField] int m_MaxHoles = 60;

        readonly Queue<Transform> m_Holes = new Queue<Transform>();
        Mesh m_QuadMesh;

        public int HoleCount => m_Holes.Count;

        void OnEnable()
        {
            Gunfire.SurfaceHit += Spawn;
        }

        void OnDisable()
        {
            Gunfire.SurfaceHit -= Spawn;
        }

        public void Spawn(RaycastHit hit)
        {
            EmitSparks(hit.point, hit.normal);
            PlaceHole(hit);
        }

        void EmitSparks(Vector3 point, Vector3 normal)
        {
            if (m_Sparks == null)
                return;
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < m_SparkCount; i++)
            {
                // 맞은 면에서 튀어나오는 쪽으로 — 면의 바깥 방향 둘레로 흩뿌린다
                Vector3 dir = (normal + Random.insideUnitSphere * 0.8f).normalized;
                emit.position = point + normal * 0.01f;
                emit.velocity = dir * (m_SparkSpeed * Random.Range(0.5f, 1f));
                m_Sparks.Emit(emit, 1);
            }
        }

        void PlaceHole(RaycastHit hit)
        {
            if (m_HoleMaterial == null)
                return;

            Transform hole = m_Holes.Count >= m_MaxHoles ? m_Holes.Dequeue() : CreateHole();

            // 움직이는 물체(쓰러지는 표적·적의 몸)에 맞으면 그 물체에 붙여 같이 움직이고, 아니면 이 오브젝트 아래에 둔다
            bool moves = hit.rigidbody != null || hit.collider.GetComponentInParent<CombatantHealth>() != null;
            Transform parent = moves ? hit.collider.transform : transform;
            // 탄흔의 위쪽을 붙을 물체의 위쪽에 맞춘다 — 비틀어 붙이면 늘어난 물체(얇은 표적판)에서 찌그러져 보인다
            Vector3 up = Vector3.ProjectOnPlane(parent.up, hit.normal);
            if (up.sqrMagnitude < 0.001f)
                up = Vector3.ProjectOnPlane(parent.forward, hit.normal);

            hole.SetParent(null, false);
            hole.localScale = Vector3.one * m_HoleSize;
            // 면에서 2mm 띄운다 — 딱 붙이면 면과 겹쳐 깜빡인다. 사각형은 -Z 쪽이 앞면이라 면 바깥을 보게 돌린다
            hole.SetPositionAndRotation(hit.point + hit.normal * 0.002f, Quaternion.LookRotation(-hit.normal, up));
            hole.SetParent(parent, true);
            hole.gameObject.SetActive(true);
            m_Holes.Enqueue(hole);
        }

        Transform CreateHole()
        {
            // 콜라이더 없는 사각형으로 만든다 — CreatePrimitive 는 콜라이더가 붙어 나와서
            // 쓰러지는 표적(Rigidbody)에 붙이는 순간 물리 오류가 나고, 다음 총알도 막는다
            if (m_QuadMesh == null)
                m_QuadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var go = new GameObject("BulletHole", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = m_QuadMesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = m_HoleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        // 표적을 다시 세울 때 탄흔도 지운다
        public void ClearHoles()
        {
            foreach (Transform hole in m_Holes)
            {
                if (hole != null)
                    Destroy(hole.gameObject);
            }
            m_Holes.Clear();
        }
    }
}
