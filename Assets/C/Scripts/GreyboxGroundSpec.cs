using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 회색 박스 맵 '배치'의 바닥 한 칸 — 차도·인도(대지)·샛길·주차장. 이 상자의 위치·가로·세로·회전이 범위다.
    // 차도는 바탕 땅 위에 도로 방향을 맞춘 노면(중앙선)을 깐다. 인도(대지)는 0.15m 높게(턱) — 건물은 이 위에 선다.
    // 샛길은 대지 위 건물 사이 걷는 길(인도 높이에 색만 다르게), 주차장은 차도 높이 — 바탕 땅(공터)과 같은 중앙선 없는 아스팔트 (11/05).
    public class GreyboxGroundSpec : MonoBehaviour
    {
        public enum Kind { Road, Sidewalk, Alley, Lot }

        [Tooltip("차도(안길 포함) · 인도·대지(턱 0.15m) · 샛길(대지 위) · 주차장(차도 높이)")]
        public Kind kind = Kind.Road;

        [Tooltip("차도만 — 노란 중앙선을 도로 가운데로. 끄면 중앙선 없는 아스팔트(Textures/Map/Asphalt) — 교차로 조각 등 (사용자 11/05). " +
                 "중앙선 있는 길끼리 만나는 모서리·교차로 칸은 맵을 만들 때 저절로 중앙선 없는 아스팔트로 덮인다")]
        public bool centerLine = true;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up, name);
        }

        // 종류를 바꾸면 색이 따라온다 (차도 파랑 · 인도 흰색 · 샛길 노랑 · 주차장 초록)
        void OnValidate()
        {
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                return;
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/C/Materials/Greybox/Layout/Layout_{kind}.mat");
            if (mat != null && meshRenderer.sharedMaterial != mat)
                meshRenderer.sharedMaterial = mat;
        }
#endif
    }
}
