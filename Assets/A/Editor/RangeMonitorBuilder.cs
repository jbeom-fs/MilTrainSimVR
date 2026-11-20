using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MilTrainSimVR.A.EditorTools
{
    // 사격장 사로 패널 모니터 (11/19) — 공중에 떠 있던 사로 패널(RangePanel)을 4번 부스 오른쪽 유리 칸막이에 건 모니터 화면 안으로 옮긴다.
    // 모니터 모델은 B 교육 디스플레이(Display_Entrance_760x560 · 케이스 · 브래킷 재질)를 A 폴더로 복사해 쓴다 — B 파일은 고치지 않는다.
    // 모델 원점 = 화면 가운데, 앞면 z 0, 벽에 닿는 브래킷 뒤 z 0.07. 패널(900×560, 0.001 배율 = 0.90×0.56 m)에 맞게 가로만 늘린다.
    // 사격장을 다시 지어도(IndoorRangeBuilder) Lane4 아래는 지우지 않고, 패널 자리는 같은 값(PanelLocalPosition)으로 둔다.
    static class RangeMonitorBuilder
    {
        const string k_SourceModel = "Assets/B/Models/TrainingDisplay/Display_Entrance_760x560.fbx";
        const string k_SourceCase = "Assets/B/Materials/TrainingDisplay/TrainingDisplay_Case.mat";
        const string k_SourceMetal = "Assets/B/Materials/TrainingMirror/TrainingMirror_Metal.mat";
        const string k_Model = "Assets/A/Models/Monitor/A_Monitor_760x560.fbx";
        const string k_CaseMaterial = "Assets/A/Materials/Monitor/A_Monitor_Case.mat";
        const string k_MetalMaterial = "Assets/A/Materials/Monitor/A_Monitor_Metal.mat";

        const string k_LanePath = "Room_Range/Lane4";
        const string k_MonitorName = "Monitor_RangePanel";

        // Lane4 기준. 오른쪽 칸막이 유리(Partition_4, 안쪽 면 x 1.09)에 브래킷 뒤(0.07)가 닿고, 사수(z 3.4)의 오른쪽 앞에서 부스 안(-x)을 본다
        // z 4.529 — 11/19 사용자가 씬에서 앞으로 옮긴 자리 (처음 3.95)
        public static readonly Vector3 MonitorLocalPosition = new Vector3(1.02f, 1.45f, 4.529f);
        public static readonly Vector3 MonitorLocalRotation = new Vector3(0f, 90f, 0f);
        static readonly Vector3 k_MonitorScale = new Vector3(900f / 760f, 1f, 1f);
        // 패널은 테두리 앞면에서 3 mm 안쪽 — 테두리 안에 들어간 화면처럼
        public static readonly Vector3 PanelLocalPosition = MonitorLocalPosition + Quaternion.Euler(MonitorLocalRotation) * new Vector3(0f, 0f, 0.003f);
        public static Vector3 PanelLocalRotation => MonitorLocalRotation;

        [MenuItem("A/만들기/사격장 사로 모니터")]
        static void Build()
        {
            if (!CopyAssets())
                return;
            var laneObject = GameObject.Find(k_LanePath);
            if (laneObject == null)
            {
                Debug.LogError($"[A] {k_LanePath} 이 없습니다 — A_Sandbox 씬을 여세요");
                return;
            }
            Transform lane = laneObject.transform;

            Transform old = lane.Find(k_MonitorName);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_Model);
            var monitor = (GameObject)PrefabUtility.InstantiatePrefab(model);
            Undo.RegisterCreatedObjectUndo(monitor, "A: 사격장 사로 모니터");
            monitor.name = k_MonitorName;
            monitor.transform.SetParent(lane, false);
            monitor.transform.localPosition = MonitorLocalPosition;
            monitor.transform.localRotation = Quaternion.Euler(MonitorLocalRotation);
            monitor.transform.localScale = k_MonitorScale;

            var caseMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_CaseMaterial);
            var metalMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_MetalMaterial);
            foreach (var renderer in monitor.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sharedMaterial = renderer.name.Contains("Bracket") ? metalMaterial : caseMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Transform panel = lane.Find("RangePanel");
            if (panel != null)
            {
                Undo.RecordObject(panel, "A: 사격장 사로 모니터");
                panel.localPosition = PanelLocalPosition;
                panel.localRotation = Quaternion.Euler(PanelLocalRotation);
            }
            else
                Debug.LogWarning("[A] RangePanel 이 없어 모니터만 달았습니다");

            EditorSceneManager.MarkSceneDirty(lane.gameObject.scene);
            Debug.Log($"[A] 사격장 사로 모니터: {k_LanePath}/{k_MonitorName}, 사로 패널을 화면 안으로");
        }

        // B 모델 · 재질을 A 폴더로 복사 (이미 있으면 그대로)
        static bool CopyAssets()
        {
            return Copy(k_SourceModel, k_Model) && Copy(k_SourceCase, k_CaseMaterial) && Copy(k_SourceMetal, k_MetalMaterial);
        }

        static bool Copy(string from, string to)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(to) != null)
                return true;
            string folder = System.IO.Path.GetDirectoryName(to).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
            }
            if (AssetDatabase.CopyAsset(from, to))
                return true;
            Debug.LogError($"[A] 복사 실패: {from} → {to}");
            return false;
        }
    }
}
