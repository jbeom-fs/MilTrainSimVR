using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.A.EditorTools
{
    // 내무반 천장 (10/29) — 석고 텍스 천장처럼. 텍스 한 장 0.9 × 0.45 m (300 × 600 의 1.5배, 사용자), 줄마다 반 장씩 어긋나게 (사용자 그림: 엇갈려 쌓기).
    //   · 무늬(Ceiling.mat, CeilingTile.png)는 텍스 한 장 긴 변(k_TileLong)마다 한 번 — 장마다 같은 자리에서 시작
    //   · 줄눈: 천장 아래에 얇은 띠. 줄(짧은 변 z 간격)을 가르는 띠는 방을 가로질러 이어지고, 장을 가르는 띠(x 간격)는 줄 안에서만 — 이웃 줄과 반 장 어긋난다.
    //     방 가운데 기준. 띠를 메시 하나로 합쳐 Models/Ceiling/Meshes 에 저장
    //   · 조명: 형광등 자리에 맞춘다 (BuildLights)
    // 방 안쪽 크기는 바닥(Shell/Floor), 천장 높이는 천장(Shell/Ceiling) 아래면에서 잰다. 방 크기를 바꾸면 다시 누른다.
    // 사격장은 사수 쪽 천장(Shell/Ceiling_Firing)만 같은 텍스로 (11/02 사용자) — 무늬 반복 수가 재질에 들어가므로 Ceiling.mat 을 복사한 사격장 전용 재질·줄눈 메시를 쓴다.
    //   표적 쪽 천장(Shell/Ceiling)은 다른 재질로 할 예정이라 건드리지 않는다. 조명은 짓지 않는다
    static class CeilingBuilder
    {
        const string k_Shell = "Room_Assembly/Shell";
        const string k_Room = "Room_Assembly";
        const string k_CeilingMaterialPath = "Assets/A/Models/Ceiling/Materials/Ceiling.mat";
        const string k_JointMaterialPath = "Assets/A/Models/Ceiling/Materials/CeilingJoint.mat";
        const string k_MeshFolder = "Assets/A/Models/Ceiling/Meshes";
        const string k_MeshPath = k_MeshFolder + "/CeilingGrid.asset";
        const string k_RangeShell = "Room_Range/Shell";
        const string k_RangeMaterialPath = "Assets/A/Materials/Range/Range_CeilingTile.mat";
        const string k_RangeMeshPath = k_MeshFolder + "/CeilingGrid_Range.asset";

        const float k_TileLong = 0.9f;     // x — 10/29 0.6 → 0.9 (1.5배)
        const float k_TileShort = 0.45f;   // z — 0.3 → 0.45
        const float k_JointWidth = 0.006f;
        const float k_JointDepth = 0.002f; // 천장 아래로 나온 만큼

        // 조명 (10/30) — 형광등(Barracks/CeilingPanel*)마다 아래로 넓게 비추는 스포트 + 천장·벽을 채우는 가운데 줄 점조명(반사광 흉내). 그림자 없음
        const string k_Barracks = "Barracks";
        const string k_PanelPrefix = "CeilingPanel";
        const float k_PanelLightBelowCeiling = 0.04f;   // 형광등 발광면(0.032) 바로 아래
        const float k_PanelLightRange = 8f;
        const float k_PanelLightIntensity = 2.5f;
        const float k_PanelLightOuterAngle = 160f;
        const float k_PanelLightInnerAngle = 100f;
        const float k_FillY = 1.3f;
        const float k_FillRange = 7f;
        const float k_FillIntensity = 0.5f;
        static readonly Color k_LightColor = new Color(1f, 0.97f, 0.92f);

        [MenuItem("A/만들기/내무반 천장 (텍스 줄눈 + 임시 조명)")]
        static void Build()
        {
            var shell = GameObject.Find(k_Shell);
            var room = GameObject.Find(k_Room);
            var ceilingMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_CeilingMaterialPath);
            if (shell == null || room == null || ceilingMaterial == null)
            {
                Debug.LogError($"[A] {k_Shell} 또는 {k_CeilingMaterialPath} 이 없다");
                return;
            }
            Undo.SetCurrentGroupName("A: 내무반 천장");
            int group = Undo.GetCurrentGroup();

            Transform floor = shell.transform.Find("Floor");
            Transform ceiling = shell.transform.Find("Ceiling");
            if (floor == null || ceiling == null)
            {
                Debug.LogError($"[A] {k_Shell}/Floor · Ceiling 이 없다");
                return;
            }
            Tile(shell.transform, ceiling, floor.GetComponent<Renderer>().bounds, ceilingMaterial, k_MeshPath, out float ceilingY, out string summary);

            // 3) 조명
            int lightCount = BuildLights(room.transform, ceilingY);

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log($"[A] 내무반 천장: {summary}, 조명 {lightCount}개");
        }

        // 사격장 사수 쪽 천장 — 내무반과 같은 텍스·줄눈 (실내 사격장 메뉴도 마지막에 부른다). 천장이 아직 하나면 둘로 나눈다
        [MenuItem("A/만들기/사격장 사수 쪽 천장 (내무반과 같은 텍스)")]
        static void BuildRangeMenu()
        {
            Undo.SetCurrentGroupName("A: 사격장 천장");
            int group = Undo.GetCurrentGroup();
            BuildRange();
            Undo.CollapseUndoOperations(group);
        }

        internal static void BuildRange()
        {
            var shell = GameObject.Find(k_RangeShell);
            var source = AssetDatabase.LoadAssetAtPath<Material>(k_CeilingMaterialPath);
            if (shell == null || source == null)
            {
                Debug.LogError($"[A] {k_RangeShell} 또는 {k_CeilingMaterialPath} 이 없다");
                return;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_RangeMaterialPath);
            if (material == null)
            {
                material = new Material(source) { name = "Range_CeilingTile" };
                AssetDatabase.CreateAsset(material, k_RangeMaterialPath);
            }
            Transform firing = shell.transform.Find(IndoorRangeBuilder.k_FiringCeiling);
            if (firing == null)
            {
                IndoorRangeBuilder.BuildCeilings(shell.transform);
                firing = shell.transform.Find(IndoorRangeBuilder.k_FiringCeiling);
            }
            Transform floor = shell.transform.Find("Floor");
            if (floor == null)
            {
                Debug.LogError($"[A] {k_RangeShell}/Floor 가 없다");
                return;
            }
            var renderer = firing.GetComponent<MeshRenderer>();
            if (renderer.sharedMaterial != material)
            {
                Undo.RecordObject(renderer, "A: 사격장 천장");
                renderer.sharedMaterial = material;
            }
            // 줄눈은 바닥 폭 × 사수 쪽 천장 길이만큼. 바닥은 사수 쪽(Floor_Firing)·표적 쪽(Floor)으로 나뉘어 있을 수 있다 (11/02)
            Bounds inner = floor.GetComponent<Renderer>().bounds;
            Transform firingFloor = shell.transform.Find("Floor_Firing");
            if (firingFloor != null)
                inner.Encapsulate(firingFloor.GetComponent<Renderer>().bounds);
            Bounds area = renderer.bounds;
            inner.SetMinMax(new Vector3(inner.min.x, inner.min.y, Mathf.Max(inner.min.z, area.min.z)),
                            new Vector3(inner.max.x, inner.max.y, Mathf.Min(inner.max.z, area.max.z)));
            Tile(shell.transform, firing, inner, material, k_RangeMeshPath, out _, out string summary);
            Debug.Log($"[A] 사격장 사수 쪽 천장: {summary}");
            AssetDatabase.SaveAssets();
        }

        // 천장(ceiling)에 텍스 무늬를 맞추고 줄눈(shell/Ceiling_Joints)을 inner(방 안쪽 넓이, 줄눈 기준 = 가운데)에 다시 짓는다. ceilingY = 천장 아래면 높이
        static void Tile(Transform shell, Transform ceiling, Bounds inner, Material ceilingMaterial, string meshPath, out float ceilingY, out string summary)
        {
            Bounds ceilingBounds = ceiling.GetComponent<Renderer>().bounds;
            ceilingY = ceilingBounds.min.y;

            // 1) 무늬 — 텍스 긴 변마다 한 번, 장의 모서리(줄눈)에서 시작하도록 맞춘다
            Undo.RecordObject(ceilingMaterial, "A: 천장");
            Vector2 tiling = new Vector2(ceilingBounds.size.x / k_TileLong, ceilingBounds.size.z / k_TileLong);
            Vector2 offset = new Vector2(Frac(-(ceilingBounds.min.x - inner.center.x) / k_TileLong + 0.5f),
                                         Frac(-(ceilingBounds.min.z - inner.center.z) / k_TileLong));
            foreach (string prop in new[] { "_BaseMap", "_MainTex" })
            {
                ceilingMaterial.SetTextureScale(prop, tiling);
                ceilingMaterial.SetTextureOffset(prop, offset);
            }
            EditorUtility.SetDirty(ceilingMaterial);

            // 2) 줄눈 — 방 가운데 기준, 줄마다 반 장 엇갈림
            if (!AssetDatabase.IsValidFolder(k_MeshFolder))
                AssetDatabase.CreateFolder("Assets/A/Models/Ceiling", "Meshes");
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved == null)
            {
                saved = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(meshPath) };
                AssetDatabase.CreateAsset(saved, meshPath);
            }
            // 있던 메시에 직접 채운다 — CopySerialized 로 덮으면 에디터 화면에 예전 모양이 남는다
            FillGridMesh(saved, inner, ceilingY);
            EditorUtility.SetDirty(saved);

            var old = shell.Find("Ceiling_Joints");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var joints = new GameObject("Ceiling_Joints");
            Undo.RegisterCreatedObjectUndo(joints, "A: 천장");
            joints.transform.SetParent(shell, false);
            joints.transform.position = Vector3.zero;
            joints.transform.rotation = Quaternion.identity;
            joints.AddComponent<MeshFilter>().sharedMesh = saved;
            var renderer = joints.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = JointMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            summary = $"텍스 {k_TileLong} × {k_TileShort} m, 줄눈 {saved.vertexCount / 8}개, 무늬 반복 {tiling.x:0.#} × {tiling.y:0.#}";
        }

        // 형광등 자리에서 조명을 다시 짓는다 — 형광등을 옮겼으면 메뉴를 다시 누른다
        static int BuildLights(Transform room, float ceilingY)
        {
            var oldLights = room.Find("Lights_Temp");
            if (oldLights != null)
                Undo.DestroyObjectImmediate(oldLights.gameObject);
            var lights = new GameObject("Lights_Temp").transform;
            Undo.RegisterCreatedObjectUndo(lights.gameObject, "A: 내무반 천장");
            lights.SetParent(room, false);

            int count = 0;
            var rowsZ = new SortedSet<float>();
            foreach (Transform panel in room.Find(k_Barracks))
            {
                if (!panel.name.StartsWith(k_PanelPrefix))
                    continue;
                Vector3 p = panel.position;
                rowsZ.Add(p.z);
                var light = AddLight(lights, $"Light_{p.x:0.#}_{p.z:0.#}", new Vector3(p.x, ceilingY - k_PanelLightBelowCeiling, p.z), LightType.Spot, k_PanelLightRange, k_PanelLightIntensity);
                light.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                light.spotAngle = k_PanelLightOuterAngle;
                light.innerSpotAngle = k_PanelLightInnerAngle;
                count++;
            }
            foreach (float z in rowsZ)
            {
                AddLight(lights, $"Fill_{z:0.#}", new Vector3(0f, k_FillY, z), LightType.Point, k_FillRange, k_FillIntensity);
                count++;
            }
            return count;
        }

        static Light AddLight(Transform parent, string name, Vector3 position, LightType type, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = type;
            light.range = range;
            light.intensity = intensity;
            light.color = k_LightColor;
            light.shadows = LightShadows.None;
            return light;
        }

        // 천장 아래면(y)에 붙는 얇은 띠들 — 줄 경계(z 간격 k_TileShort)는 방을 가로지르고, 장 경계(x 간격 k_TileLong)는 줄마다 반 장씩 엇갈린다
        static void FillGridMesh(Mesh mesh, Bounds inner, float y)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float x0 = inner.min.x, x1 = inner.max.x, z0 = inner.min.z, z1 = inner.max.z;
            float cx = inner.center.x, cz = inner.center.z;
            float half = k_JointWidth * 0.5f;
            float bottom = y - k_JointDepth;

            // 줄 경계 (방을 가로지르는 띠)
            int first = Mathf.FloorToInt((z0 - cz) / k_TileShort);
            int last = Mathf.CeilToInt((z1 - cz) / k_TileShort);
            for (int n = first; n <= last; n++)
            {
                float z = cz + n * k_TileShort;
                if (z > z0 + 0.01f && z < z1 - 0.01f)
                    Strip(vertices, triangles, new Vector3(x0, bottom, z - half), new Vector3(x1, bottom, z + half), y);
            }
            // 줄 n (z 가 cz + n·짧은 변 ~ 그다음) 안의 장 경계 — 홀수 줄은 반 장 밀린다
            for (int n = first; n < last; n++)
            {
                float rz0 = Mathf.Max(z0, cz + n * k_TileShort);
                float rz1 = Mathf.Min(z1, cz + (n + 1) * k_TileShort);
                if (rz1 - rz0 < 0.01f)
                    continue;
                float shift = (n & 1) == 0 ? 0f : k_TileLong * 0.5f;
                float start = cx + shift + Mathf.Ceil((x0 - cx - shift) / k_TileLong) * k_TileLong;
                for (float x = start; x < x1 - 0.01f; x += k_TileLong)
                    if (x > x0 + 0.01f)
                        Strip(vertices, triangles, new Vector3(x - half, bottom, rz0), new Vector3(x + half, bottom, rz1), y);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        // 아래면 + 긴 두 옆면 (위는 천장에 붙어 안 보인다). a = 아래 한 모서리, b = 맞은편 모서리, top = 천장 높이
        static void Strip(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, float top)
        {
            int i = v.Count;
            v.Add(new Vector3(a.x, a.y, a.z)); v.Add(new Vector3(b.x, a.y, a.z));
            v.Add(new Vector3(b.x, a.y, b.z)); v.Add(new Vector3(a.x, a.y, b.z));
            v.Add(new Vector3(a.x, top, a.z)); v.Add(new Vector3(b.x, top, a.z));
            v.Add(new Vector3(b.x, top, b.z)); v.Add(new Vector3(a.x, top, b.z));
            // 아래면 (아래에서 보이게)
            t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            // 옆면 넷
            t.AddRange(new[] { i, i + 4, i + 5, i, i + 5, i + 1 });
            t.AddRange(new[] { i + 1, i + 5, i + 6, i + 1, i + 6, i + 2 });
            t.AddRange(new[] { i + 2, i + 6, i + 7, i + 2, i + 7, i + 3 });
            t.AddRange(new[] { i + 3, i + 7, i + 4, i + 3, i + 4, i });
        }

        static Material JointMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_JointMaterialPath);
            if (material != null)
                return material;   // 손으로 고친 재질은 덮어쓰지 않는다
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CeilingJoint" };
            material.SetColor("_BaseColor", new Color(0.72f, 0.72f, 0.7f));
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, k_JointMaterialPath);
            return material;
        }

        static float Frac(float v) => v - Mathf.Floor(v);
    }
}
