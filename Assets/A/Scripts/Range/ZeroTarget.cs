using System.Collections.Generic;
using MilTrainSimVR.A.Rifle;
using UnityEngine;

namespace MilTrainSimVR.A.Range
{
    // 25 m 영점 표적지 — 총의 광선(히트스캔)이 맞은 자리를 조준점 기준 2D 좌표(cm)로 조마다 기록한다 (기획 4절).
    // 좌표: 사수가 표적을 볼 때 오른쪽 +x, 위 +y. 표적지 바깥(표적판·기둥)에 맞으면 기록하지 않는다 → 그 조의 빗나간 탄.
    // 쏜 발 수는 총이 센다 — 흐름(RangeTrainingFlow)이 조를 시작·마감할 때 넘겨준다.
    public class ZeroTarget : MonoBehaviour, IHittable
    {
        [Tooltip("조준점 — 표적지 가운데. 이 오브젝트의 오른쪽(+X)·위(+Y)가 좌표축이다. 비우면 이 오브젝트")]
        [SerializeField] Transform m_AimPoint;

        readonly List<ShotGroup> m_Groups = new List<ShotGroup>();
        ShotGroup m_Current;

        public IReadOnlyList<ShotGroup> Groups => m_Groups;
        public ShotGroup Current => m_Current;

        // 맞을 때마다 — 조준점 기준 cm
        public event System.Action<Vector2> Hit;

        Transform Aim => m_AimPoint != null ? m_AimPoint : transform;

        // 기록을 모두 지운다 (영점사격을 처음 시작할 때)
        public void Clear()
        {
            m_Groups.Clear();
            m_Current = null;
        }

        // 새 조를 연다. 열린 조가 없으면 맞아도 기록하지 않는다
        public void BeginGroup()
        {
            m_Current = new ShotGroup();
            m_Groups.Add(m_Current);
        }

        // 지금 조를 닫는다 — 그 조에서 쏜 발 수를 적고 돌려준다
        public ShotGroup EndGroup(int shots)
        {
            ShotGroup group = m_Current;
            if (group != null)
                group.Shots = shots;
            m_Current = null;
            return group;
        }

        public Vector2 ToCm(Vector3 worldPoint)
        {
            Vector3 d = worldPoint - Aim.position;
            return new Vector2(Vector3.Dot(d, Aim.right), Vector3.Dot(d, Aim.up)) * 100f;
        }

        public void OnHit(RaycastHit hit, Vector3 direction)
        {
            Vector2 cm = ToCm(hit.point);
            Debug.Log($"[Range] 탄착 ({cm.x:+0.0;-0.0} cm, {cm.y:+0.0;-0.0} cm)");
            m_Current?.AddHit(cm);
            Hit?.Invoke(cm);
        }
    }
}
