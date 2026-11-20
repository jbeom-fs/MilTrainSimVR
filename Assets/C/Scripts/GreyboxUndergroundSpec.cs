using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 회색 박스 맵 '배치'의 지하 한 칸 — 천장이 덮인 홀·통로, 하늘이 열린 바깥 입구(계단·선큰 광장).
    // 상자의 위치·가로·세로·회전이 바깥 테두리(벽 두께 0.2m 포함)다. 회전은 0·90·180·270 만.
    // 맞닿은 상자끼리는 맞닿은 면이 뚫려 이어진다 — 통로 끝을 홀·계단 상자 가장자리에 딱 맞춘다.
    // 지하 바닥 -4.5m, 천장 -1.0m. 입구는 파란 띠(로컬 -Z, 앞면)가 바깥 땅에서 내려가기 시작하는 쪽이다.
    public class GreyboxUndergroundSpec : MonoBehaviour
    {
        public enum Kind { Hall, Passage, Stair, TurnStair, SunkenPlaza }

        [Tooltip("홀(기둥·가판대) · 통로 · 직선 계단(앞면 = 꼭대기, 뒷면에 통로) · ㄱ자 계단(앞면 왼쪽으로 들어가 안쪽에서 오른쪽으로 꺾여 오른쪽 면에 통로) · 선큰 광장(앞면 넓은 계단 + 안쪽 마당, 뒷면에 통로)")]
        public Kind kind = Kind.Passage;

        [Tooltip("ㄱ자 계단을 거울처럼 — 앞면 오른쪽으로 들어가 왼쪽으로 꺾인다")]
        public bool mirror;

        [Tooltip("계단 위 지붕 (직선·ㄱ자 계단)")]
        public bool canopy;

        [Tooltip("메모")]
        public string note;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up, $"{name}\n지하 {kind}");
            if (kind == Kind.Hall || kind == Kind.Passage)
                return;
            // 입구는 들어가는 쪽(앞면)을 파란 띠로
            Gizmos.color = new Color(0.15f, 0.45f, 1f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(new Vector3(0f, 0f, -0.5f), new Vector3(1f, 1.05f, 0.04f));
        }

        void OnValidate()
        {
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                return;
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/C/Materials/Greybox/Layout/Layout_Underground.mat");
            if (mat != null && meshRenderer.sharedMaterial != mat)
                meshRenderer.sharedMaterial = mat;
        }
#endif
    }
}
