using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MilTrainSimVR.B.ProtectiveClothing;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace MilTrainSimVR.B.ProtectiveClothing.EditorSetup
{
    // Protective clothing equipment chapter (basic wearing; no training order, runner or completion screen): importer settings of
    // the two protective clothing FBX files and their three textures, a derived metallic-smoothness map and the shared URP Lit
    // material, and the work scene B_ProtectiveClothing_Equipment with the XR Origin (inactive Simulator), one EventSystem with the
    // XR UI input module, floor, workbench with the six pickups, the mannequin and the worn model under one mannequin root on a
    // display platform, the guidance board with the development reset button, and the target marker. Editor-only; run by the user.
    //
    // Model use (Assets/B/Models/ProtectiveClothing/Source~/README_Model.md): the worn meshes were fitted to the mannequin's bind
    // T pose, all with the mannequin's common origin. Here the mannequin instance and the worn model instance sit at the identity of
    // the same root (moved, turned or uniformly scaled together through that root only), each worn mesh keeps its imported place
    // and is only shown or hidden. The mannequin's bones are set to the bind pose of its skinned mesh (bindposes) on the scene
    // instance and its Animator is disabled; the FBX, its importer and its rig are not changed. Menu 2 compares the scene's bones
    // and worn Attach points with the model documents (ReferenceInspection.json, AnchorManifest.json).
    //
    // Pickups: the pickup model is instanced and unpacked once; each <Name>_Pickup mesh (with its Grip and Attach Empties, its
    // imported pose kept) goes under its own PC_<Name>_Pickup object with a Rigidbody, a box collider and an XRGrabInteractable.
    //
    // Idempotent: assets by path, scene objects by name, components by type. Layout (root, workbench, platform, mannequin root,
    // pickup slots, board), collider sizes and the wear radius are set only on creation; references, grab and physics settings,
    // materials, the bind pose, the instances' identity under the mannequin root and the saved visibility are rewritten on every
    // run. The FBX files, the .blend and the image files are only read (the derived map is a new file). Other scenes, K1, the
    // shared Training code and project settings are not touched.
    static class ProtectiveClothingSetup
    {
        const string LogTag = "[ProtectiveClothing]";
        const string ModelDir = "Assets/B/Models/ProtectiveClothing";
        const string PickupFbxPath = ModelDir + "/ProtectiveClothing_Pickup.fbx";
        const string WornFbxPath = ModelDir + "/ProtectiveClothing_Worn.fbx";
        const string BaseColorPath = ModelDir + "/Textures/PC_BaseColor_2K.png";
        const string NormalPath = ModelDir + "/Textures/PC_Normal_1K.png";
        const string RoughnessPath = ModelDir + "/Textures/PC_Roughness_1K.png";
        const string ReferenceJsonPath = ModelDir + "/Source~/ReferenceInspection.json";
        const string AnchorJsonPath = ModelDir + "/Source~/AnchorManifest.json";
        const string MaterialDir = "Assets/B/Materials/ProtectiveClothing";
        const string SharedMaterialPath = MaterialDir + "/PC_Shared.mat";
        const string MetallicSmoothnessPath = MaterialDir + "/PC_MetallicSmoothness_1K.png";
        const string FurnitureMaterialPath = MaterialDir + "/PC_Furniture.mat";
        const string MarkerMaterialPath = MaterialDir + "/PC_TargetMarker.mat";
        const string ScenePath = "Assets/B/Scene/B_ProtectiveClothing_Equipment.unity";
        const string XrOriginPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        const string SimulatorPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.4.1/XR Interaction Simulator/XR Interaction Simulator.prefab";
        const string MannequinFbxPath = "Assets/B/Models/Mannequin/Mannequin.fbx";
        const string KoreanFontPath = "Assets/B/Fonts/NotoSansKR-Regular SDF.asset";
        const string MixamoPrefix = "mixamorig1:";

        const string RootName = "PC_Equipment";
        const string FloorName = "PC_Floor";
        const string WorkbenchName = "PC_Workbench";
        const string PickupsName = "PC_Pickups";
        const string MannequinRootName = "PC_MannequinRoot";
        const string DisplayBoardName = "PC_DisplayBoard";
        const string StandName = "PC_Stand";
        const string BoardName = "PC_Board";
        const string MarkerName = "PC_TargetMarker";
        const string EventSystemName = "EventSystem";

        // Layout on creation only (world, metres; the user stands at the XR Origin at the world origin facing +Z). Display: the
        // whole bind T pose mannequin with its worn model (one root) reclined face up by MannequinTiltDegrees from vertical, feet
        // toward the user and head away, on a display board, so the six targets lie between about 0.8 m (feet) and 1.42 m (hands)
        // instead of 0.1 to 1.72 m upright: feet targets about 0.56 m ahead of the user, trousers 1.29 m, jacket 1.58 m, hands
        // 1.72 m ahead at 0.675 m to either side (reached by stepping along the mannequin; the face-up body does not hide them). The
        // workbench is on the user's left; the guidance board stands behind the head end, above the mannequin.
        const float MannequinTiltDegrees = 62f;
        static readonly Vector3 MannequinRootPosition = new Vector3(0f, 0.78f, 0.45f);
        static Quaternion MannequinRootRotation => Quaternion.AngleAxis(MannequinTiltDegrees, Vector3.right) * Quaternion.Euler(0f, 180f, 0f);
        // Display board behind the mannequin's back, in the mannequin root's frame (moves and tilts with it; clear of the body,
        // whose back is at local z -0.114 m, and of the arms in front of it). The stand under it is only created with the default.
        static readonly Vector3 DisplayBoardLocalCenter = new Vector3(0f, 0.85f, -0.15f);
        static readonly Vector3 DisplayBoardSize = new Vector3(0.55f, 1.9f, 0.04f);
        static readonly Vector2 StandFootprint = new Vector2(0.3f, 0.3f);
        const float StandGap = 0.12f;
        static readonly Vector3 WorkbenchCenter = new Vector3(-0.85f, 0.425f, 0.6f);
        static readonly Vector3 WorkbenchSize = new Vector3(0.85f, 0.85f, 1.3f);
        static readonly Vector3 BoardPosition = new Vector3(0f, 1.85f, 2.45f);
        const float StandingEyeHeight = 1.6f;
        // Development reach range of the target centres (reported by menu 2; not a comfort guarantee, never enforced).
        const float TargetHeightLow = 0.75f;
        const float TargetHeightHigh = 1.45f;
        // The first layout of this chapter (upright on a 0.3 m platform, board on the right). A scene object still exactly at one of
        // these defaults is updated once to the current default; any other pose is the user's and is kept.
        static readonly Vector3 EarlierMannequinRootPosition = new Vector3(0f, 0.3f, 1.25f);
        static readonly Quaternion EarlierMannequinRootRotation = Quaternion.Euler(0f, 180f, 0f);
        const string EarlierPlatformName = "PC_Platform";
        static readonly Vector3 EarlierPlatformCenter = new Vector3(0f, 0.15f, 1.25f);
        static readonly Vector3 EarlierPlatformSize = new Vector3(1.0f, 0.3f, 0.6f);
        static readonly Vector3 EarlierBoardPosition = new Vector3(0.95f, 1.45f, 0.75f);
        // Pickup slots on the workbench top (local to PC_Pickups at the top centre): left items on -X, nearest row gloves, then
        // jacket and trousers, then overboots.
        static readonly (ProtectiveClothingPart part, Vector3 slot)[] Slots =
        {
            (ProtectiveClothingPart.GloveLeft, new Vector3(-0.21f, 0f, -0.42f)), (ProtectiveClothingPart.GloveRight, new Vector3(0.21f, 0f, -0.42f)),
            (ProtectiveClothingPart.Jacket, new Vector3(-0.21f, 0f, 0f)), (ProtectiveClothingPart.Trousers, new Vector3(0.21f, 0f, 0f)),
            (ProtectiveClothingPart.OverbootLeft, new Vector3(-0.21f, 0f, 0.42f)), (ProtectiveClothingPart.OverbootRight, new Vector3(0.21f, 0f, 0.42f))
        };
        const float PickupMass = 0.6f;

        static readonly Vector2 BoardSize = new Vector2(640f, 300f);
        const float CanvasScale = 0.001f;
        const int UiLayer = 5;
        static readonly Color BoardBackground = new Color(0.07f, 0.09f, 0.11f, 1f);
        static readonly Color BoardText = new Color(0.94f, 0.95f, 0.96f, 1f);
        static readonly Color ButtonColor = new Color(0.16f, 0.42f, 0.7f, 1f);
        static readonly Color FurnitureColor = new Color(0.5f, 0.52f, 0.55f, 1f);
        static readonly Color MarkerColor = new Color(0.25f, 0.85f, 1f, 0.6f);
        const float MarkerWidth = 0.006f;

        static readonly ProtectiveClothingPart[] Parts = (ProtectiveClothingPart[])Enum.GetValues(typeof(ProtectiveClothingPart));

        // ---------- Menu 1 ----------

        [MenuItem("B/Protective Clothing/1. Build Equipment Scene", true)]
        static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // Checked before anything is changed (no asset, importer or scene work happens before these return): not in or entering
        // Play mode, not compiling, and no open scene with unsaved changes. No save dialog is shown and no scene is saved, reverted,
        // closed or switched on the user's behalf; the user tidies the scenes and runs the menu again. A normal run saves only
        // the protective clothing scene.
        static bool CanChange(out string reason)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                reason = "Play mode is running or starting. Exit Play mode, then run the menu again.";
                return false;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                reason = "scripts are compiling or assets are importing. Wait until it finishes, then run the menu again.";
                return false;
            }
            var dirty = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isDirty).ToArray();
            if (dirty.Length > 0)
            {
                reason = "unsaved changes in the open scene(s) " + string.Join(", ", dirty.Select(s => "'" + (string.IsNullOrEmpty(s.path) ? s.name : s.path) + "'")) +
                         ". This menu opens and saves its own scene, so it does not run over unsaved work. Save or revert them yourself, " +
                         "then run the menu again.";
                return false;
            }
            reason = null;
            return true;
        }

        [MenuItem("B/Protective Clothing/1. Build Equipment Scene")]
        static void Build()
        {
            if (!CanChange(out var reason))
            {
                Debug.LogError($"{LogTag} Build stopped before any change: {reason} Nothing was changed.");
                return;
            }

            var log = new StringBuilder(LogTag + " Build (equipment chapter: grab, bring to the body target, automatic wearing, reset)\n");
            try
            {
                ConfigureModel(PickupFbxPath, log);
                ConfigureModel(WornFbxPath, log);
                ConfigureTexture(BaseColorPath, TextureImporterType.Default, true, log);
                ConfigureTexture(NormalPath, TextureImporterType.NormalMap, false, log);
                ConfigureTexture(RoughnessPath, TextureImporterType.Default, false, log);
                EnsureFolder(MaterialDir);
                var metallicSmoothness = EnsureMetallicSmoothness(log);
                var shared = EnsureSharedMaterial(metallicSmoothness, log);
                var furniture = EnsureLitMaterial(FurnitureMaterialPath, FurnitureColor, log);
                var markerMaterial = EnsureMarkerMaterial(log);

                var scene = OpenOrCreateScene(log);
                EnsureSceneContent(scene, shared, furniture, markerMaterial, log);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("Saving scene failed: " + ScenePath);
                log.AppendLine("Scene saved: " + ScenePath + " (Build Settings unchanged)");
            }
            catch (Exception e)
            {
                Debug.LogError(log + "\nFAILED: " + e);
                return;
            }
            Debug.Log(log.ToString());
            Validate();
        }

        // ---------- Assets ----------

        // Importer settings of the new protective clothing FBX files only: metres from the file (no 100 / 0.01 fix), axis
        // conversion kept on the objects (not baked), an explicit prefab root so the FBX root node keeps its own transform, no
        // imported materials (the shared material is assigned explicitly in the scene), no cameras, lights, animation, rig or
        // blend shapes.
        static void ConfigureModel(string path, StringBuilder log)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
                throw new InvalidOperationException("Model importer not found (let Unity import the model folder first): " + path);
            var changes = new List<string>();
            void Set<T>(string label, T current, T wanted, Action<T> apply)
            {
                if (EqualityComparer<T>.Default.Equals(current, wanted))
                    return;
                apply(wanted);
                changes.Add($"{label} {wanted}");
            }
            Set("globalScale", importer.globalScale, 1f, v => importer.globalScale = v);
            Set("useFileScale", importer.useFileScale, true, v => importer.useFileScale = v);
            Set("bakeAxisConversion", importer.bakeAxisConversion, false, v => importer.bakeAxisConversion = v);
            Set("preserveHierarchy", importer.preserveHierarchy, true, v => importer.preserveHierarchy = v);
            Set("materialImportMode", importer.materialImportMode, ModelImporterMaterialImportMode.None, v => importer.materialImportMode = v);
            Set("importCameras", importer.importCameras, false, v => importer.importCameras = v);
            Set("importLights", importer.importLights, false, v => importer.importLights = v);
            Set("importAnimation", importer.importAnimation, false, v => importer.importAnimation = v);
            Set("animationType", importer.animationType, ModelImporterAnimationType.None, v => importer.animationType = v);
            Set("importBlendShapes", importer.importBlendShapes, false, v => importer.importBlendShapes = v);
            Set("isReadable", importer.isReadable, false, v => importer.isReadable = v);
            Set("addCollider", importer.addCollider, false, v => importer.addCollider = v);
            if (changes.Count == 0)
            {
                log.AppendLine("Model importer kept: " + path);
                return;
            }
            importer.SaveAndReimport();
            log.AppendLine($"Model importer updated ({path}; FBX file unchanged): " + string.Join(", ", changes));
        }

        static void ConfigureTexture(string path, TextureImporterType type, bool sRGB, StringBuilder log)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                throw new InvalidOperationException("Texture importer not found (let Unity import the model folder first): " + path);
            var changes = new List<string>();
            if (importer.textureType != type)
            {
                importer.textureType = type;
                changes.Add("type " + type);
            }
            if (type != TextureImporterType.NormalMap && importer.sRGBTexture != sRGB)
            {
                importer.sRGBTexture = sRGB;
                changes.Add("sRGB " + (sRGB ? "on" : "off"));
            }
            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                changes.Add("wrap Repeat");
            }
            if (importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = false;
                changes.Add("alpha is not transparency");
            }
            if (changes.Count == 0)
            {
                log.AppendLine("Texture importer kept: " + path);
                return;
            }
            importer.SaveAndReimport();
            log.AppendLine($"Texture importer updated ({path}; image file unchanged): " + string.Join(", ", changes));
        }

        // URP Lit takes smoothness from the alpha of its metallic map. Derived from PC_Roughness_1K (read from the PNG file, which
        // is not changed): RGB 0 (not metallic), A = 1 - roughness. Rewritten only when its content would change.
        static Texture2D EnsureMetallicSmoothness(StringBuilder log)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            var output = (Texture2D)null;
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(RoughnessPath)))
                    throw new InvalidOperationException("Could not read " + RoughnessPath);
                var pixels = source.GetPixels32();
                for (var i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(0, 0, 0, (byte)(255 - pixels[i].r));
                output = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                output.SetPixels32(pixels);
                output.Apply();
                var png = output.EncodeToPNG();
                var same = File.Exists(MetallicSmoothnessPath) && File.ReadAllBytes(MetallicSmoothnessPath).SequenceEqual(png);
                if (!same)
                {
                    File.WriteAllBytes(MetallicSmoothnessPath, png);
                    AssetDatabase.ImportAsset(MetallicSmoothnessPath, ImportAssetOptions.ForceUpdate);
                }
                log.AppendLine($"Derived metallic-smoothness map {(same ? "kept" : "written")}: {MetallicSmoothnessPath} ({source.width}x{source.height}; " +
                               "RGB 0, alpha = 1 - roughness of " + RoughnessPath + ")");
            }
            finally
            {
                Object.DestroyImmediate(source);
                if (output != null)
                    Object.DestroyImmediate(output);
            }

            if (!(AssetImporter.GetAtPath(MetallicSmoothnessPath) is TextureImporter importer))
                throw new InvalidOperationException("Derived map importer not found: " + MetallicSmoothnessPath);
            if (importer.textureType != TextureImporterType.Default || importer.sRGBTexture || importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
                log.AppendLine("Derived map importer set: linear (sRGB off), alpha from input");
            }
            return LoadRequired<Texture2D>(MetallicSmoothnessPath);
        }

        // One shared opaque URP Lit material for all twelve meshes. Required settings rewritten every run; colour, normal scale and
        // smoothness strength only on creation.
        static Material EnsureSharedMaterial(Texture2D metallicSmoothness, StringBuilder log)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit shader not found.");
            var m = AssetDatabase.LoadAssetAtPath<Material>(SharedMaterialPath);
            var created = m == null;
            if (created)
            {
                m = new Material(lit) { name = "PC_Shared" };
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_BumpScale", 1f);
                m.SetFloat("_Smoothness", 1f);
                AssetDatabase.CreateAsset(m, SharedMaterialPath);
            }
            else if (m.shader != lit)
            {
                m.shader = lit;
            }
            m.SetFloat("_WorkflowMode", (float)LitGUI.WorkflowMode.Metallic);
            m.SetFloat("_Surface", (float)BaseShaderGUI.SurfaceType.Opaque);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_Cull", (float)BaseShaderGUI.RenderFace.Front);
            m.SetTexture("_BaseMap", LoadRequired<Texture2D>(BaseColorPath));
            m.SetTexture("_BumpMap", LoadRequired<Texture2D>(NormalPath));
            m.SetTexture("_MetallicGlossMap", metallicSmoothness);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetFloat("_Metallic", 0f);
            BaseShaderGUI.SetMaterialKeywords(m, LitGUI.SetMaterialKeywords);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            log.AppendLine($"Shared material {(created ? "created" : "kept and required settings rewritten")}: {SharedMaterialPath} (URP Lit opaque, " +
                           "base colour sRGB, normal map, smoothness from the derived map alpha)");
            return m;
        }

        static Material EnsureLitMaterial(string path, Color color, StringBuilder log)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit shader not found.");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null)
            {
                log.AppendLine("Scene material kept: " + path);
                return m;
            }
            m = new Material(lit) { name = Path.GetFileNameWithoutExtension(path) };
            m.SetFloat("_WorkflowMode", (float)LitGUI.WorkflowMode.Metallic);
            m.SetFloat("_Surface", (float)BaseShaderGUI.SurfaceType.Opaque);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.2f);
            BaseShaderGUI.SetMaterialKeywords(m, LitGUI.SetMaterialKeywords);
            AssetDatabase.CreateAsset(m, path);
            log.AppendLine("Scene material created: " + path);
            return m;
        }

        // Target ring: URP Unlit, transparent (alpha blend), both faces; colour only on creation.
        static Material EnsureMarkerMaterial(StringBuilder log)
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? throw new InvalidOperationException("URP Unlit shader not found.");
            var m = AssetDatabase.LoadAssetAtPath<Material>(MarkerMaterialPath);
            var created = m == null;
            if (created)
            {
                m = new Material(unlit) { name = Path.GetFileNameWithoutExtension(MarkerMaterialPath) };
                m.SetColor("_BaseColor", MarkerColor);
                AssetDatabase.CreateAsset(m, MarkerMaterialPath);
            }
            else if (m.shader != unlit)
            {
                m.shader = unlit;
            }
            m.SetFloat("_Surface", (float)BaseShaderGUI.SurfaceType.Transparent);
            m.SetFloat("_Blend", (float)BaseShaderGUI.BlendMode.Alpha);
            m.SetFloat("_Cull", (float)BaseShaderGUI.RenderFace.Both);
            BaseShaderGUI.SetMaterialKeywords(m);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            log.AppendLine($"Marker material {(created ? "created" : "kept and required settings rewritten")}: {MarkerMaterialPath} (URP Unlit transparent)");
            return m;
        }

        // ---------- Scene ----------

        static Scene OpenOrCreateScene(StringBuilder log)
        {
            Scene scene;
            if (!File.Exists(ScenePath))
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var light = new GameObject("Directional Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("Saving scene failed: " + ScenePath);
                log.AppendLine("Scene created: " + ScenePath);
            }
            else
            {
                var active = EditorSceneManager.GetActiveScene();
                scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                log.AppendLine("Scene opened: " + ScenePath);
            }

            var origins = InScene<XROrigin>(scene);
            if (origins.Length == 0)
            {
                var origin = (GameObject)PrefabUtility.InstantiatePrefab(LoadRequired<GameObject>(XrOriginPrefabPath), scene);
                origin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                log.AppendLine("XR Origin (Starter Assets prefab, unchanged) added at the world origin");
            }
            else
            {
                log.AppendLine($"XR Origin kept ({origins.Length})");
            }
            if (InScene<XRInteractionManager>(scene).Length == 0)
            {
                var manager = new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
                if (manager.gameObject.scene != scene)
                    SceneManager.MoveGameObjectToScene(manager.gameObject, scene);
                log.AppendLine("XR Interaction Manager added");
            }
            var simulatorAsset = LoadRequired<GameObject>(SimulatorPrefabPath);
            var simulators = SceneInstances(scene, simulatorAsset);
            if (simulators.Length == 0)
            {
                // Desktop checks only; saved inactive so it never overrides headset input (same as the other B scenes).
                var simulator = (GameObject)PrefabUtility.InstantiatePrefab(simulatorAsset, scene);
                simulator.SetActive(false);
                log.AppendLine("XR Interaction Simulator added (inactive)");
            }
            foreach (var simulator in simulators.Where(s => s.activeSelf))
            {
                simulator.SetActive(false);
                log.AppendLine("XR Interaction Simulator set inactive for saving (activate it only for desktop checks)");
            }
            EnsureEventSystem(scene, log);
            return scene;
        }

        static void EnsureEventSystem(Scene scene, StringBuilder log)
        {
            var systems = InScene<EventSystem>(scene);
            if (systems.Length > 1)
                throw new InvalidOperationException($"{systems.Length} EventSystems in {ScenePath}; keep one and run again.");
            if (systems.Length == 1)
            {
                var modules = systems[0].GetComponents<BaseInputModule>();
                if (modules.Length != 1 || !(modules[0] is XRUIInputModule))
                    throw new InvalidOperationException($"EventSystem '{systems[0].name}' must use only XRUIInputModule (found " +
                                                        $"{string.Join(", ", modules.Select(m => m.GetType().Name))}); not changed.");
                log.AppendLine($"EventSystem '{systems[0].name}' with XRUIInputModule kept");
                return;
            }
            var go = new GameObject(EventSystemName, typeof(EventSystem), typeof(XRUIInputModule));
            if (go.scene != scene)
                SceneManager.MoveGameObjectToScene(go, scene);
            log.AppendLine("EventSystem created with XRUIInputModule");
        }

        static void EnsureSceneContent(Scene scene, Material shared, Material furniture, Material markerMaterial, StringBuilder log)
        {
            var origins = InScene<XROrigin>(scene);
            if (origins.Length != 1 || origins[0].Camera == null)
                throw new InvalidOperationException($"Expected one XR Origin with a camera in {ScenePath} (found {origins.Length}).");
            var origin = origins[0];
            var eye = origin.transform.position + Vector3.up * StandingEyeHeight;

            var roots = scene.GetRootGameObjects().Where(g => g.name == RootName).ToArray();
            if (roots.Length > 1)
                throw new InvalidOperationException($"{roots.Length} root objects named {RootName}; remove the extra one and run again.");
            var root = roots.Length == 1 ? roots[0].transform : new GameObject(RootName).transform;
            log.AppendLine(RootName + (roots.Length == 1 ? " kept" : " created at the world origin"));

            EnsureFloor(root, log);
            EnsureBox(root, WorkbenchName, WorkbenchCenter, WorkbenchSize, furniture, log);

            var mannequinRoot = root.Find(MannequinRootName);
            var atDefault = false;
            if (mannequinRoot == null)
            {
                mannequinRoot = new GameObject(MannequinRootName).transform;
                mannequinRoot.SetParent(root, false);
                mannequinRoot.SetPositionAndRotation(MannequinRootPosition, MannequinRootRotation);
                atDefault = true;
                log.AppendLine($"{MannequinRootName} created: reclined {MannequinTiltDegrees:0} deg face up, feet toward the user " +
                               "(move, turn or scale uniformly only this root)");
            }
            else if (IsPose(mannequinRoot, EarlierMannequinRootPosition, EarlierMannequinRootRotation, Vector3.one))
            {
                mannequinRoot.SetPositionAndRotation(MannequinRootPosition, MannequinRootRotation);
                atDefault = true;
                log.AppendLine($"{MannequinRootName} was exactly at the earlier upright default: updated once to the reclined display default");
            }
            else
            {
                atDefault = IsPose(mannequinRoot, MannequinRootPosition, MannequinRootRotation, Vector3.one);
                log.AppendLine($"{MannequinRootName} kept ({(atDefault ? "current default" : "user pose")}; the current default is position " +
                               $"{Vec(MannequinRootPosition)}, reclined {MannequinTiltDegrees:0} deg)");
            }
            RemoveEarlierPlatform(root, atDefault, log);
            EnsureDisplayBoard(root, mannequinRoot, furniture, atDefault, log);
            var mannequin = EnsureModelInstance(mannequinRoot, LoadRequired<GameObject>(MannequinFbxPath), log);
            var wornModel = EnsureModelInstance(mannequinRoot, LoadRequired<GameObject>(WornFbxPath), log);
            ApplyBindPose(mannequin, log);
            DisableAnimators(mannequin, log);
            foreach (var c in mannequin.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);

            var worn = new Dictionary<ProtectiveClothingPart, GameObject>();
            foreach (var part in Parts)
            {
                var mesh = RequireDeep(wornModel, ProtectiveClothingPiece.WornName(part));
                RequireDeep(mesh, ProtectiveClothingPiece.WornAttachName(part));
                SetSharedMaterial(mesh, shared);
                if (mesh.gameObject.activeSelf)
                    mesh.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(mesh.gameObject);
                worn[part] = mesh.gameObject;
            }
            log.AppendLine("Worn meshes: shared material, saved hidden, kept at their imported place under the worn model (shown by wearing only)");

            var pieces = EnsurePickups(root, worn, shared, log);
            var equipment = GetOrAdd<ProtectiveClothingEquipment>(root.gameObject);
            var equipmentData = new SerializedObject(equipment);
            SetObjects(Property(equipmentData, "pieces"), pieces.Cast<Object>().ToArray());
            equipmentData.ApplyModifiedPropertiesWithoutUndo();
            if (!equipment.TryValidate(out var error))
                throw new InvalidOperationException(error);
            log.AppendLine($"ProtectiveClothingEquipment on {RootName}: six parts; wear radius {equipment.WearRadius:0.###} m (kept; development value)");

            var marker = EnsureMarker(root, markerMaterial, log);
            EnsureBoard(root, equipment, marker, origin, eye, log);
        }

        static void EnsureFloor(Transform root, StringBuilder log)
        {
            var floor = root.Find(FloorName);
            if (floor != null)
            {
                log.AppendLine(FloorName + " kept");
                return;
            }
            floor = GameObject.CreatePrimitive(PrimitiveType.Plane).transform;
            floor.name = FloorName;
            floor.SetParent(root, false);
            floor.localPosition = Vector3.zero;
            log.AppendLine(FloorName + " created (10 x 10 m plane with its collider)");
        }

        // Display furniture with its box collider (dropped pickups rest on it). Pose and size only on creation.
        static void EnsureBox(Transform root, string name, Vector3 center, Vector3 size, Material material, StringBuilder log)
        {
            var box = root.Find(name);
            if (box == null)
            {
                box = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                box.name = name;
                box.SetParent(root, false);
                box.position = center;
                box.localScale = size;
                log.AppendLine($"{name} created ({size.x:0.##} x {size.y:0.##} x {size.z:0.##} m, box collider)");
            }
            else
            {
                log.AppendLine(name + " kept (pose and size unchanged)");
            }
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!box.TryGetComponent<BoxCollider>(out _))
                box.gameObject.AddComponent<BoxCollider>();
        }

        // One instance of the model under the mannequin root at its identity (the common frame of the mannequin and the worn
        // meshes). The local pose is rewritten every run: the root alone carries the user's move, turn and uniform scale.
        static Transform EnsureModelInstance(Transform parent, GameObject asset, StringBuilder log)
        {
            var found = parent.Cast<Transform>().Where(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) == asset).ToArray();
            if (found.Length > 1)
                throw new InvalidOperationException($"{found.Length} {asset.name} instances under {parent.name}; keep one.");
            var t = found.Length == 1 ? found[0] : ((GameObject)PrefabUtility.InstantiatePrefab(asset, parent)).transform;
            var moved = t.localPosition != Vector3.zero || t.localRotation != Quaternion.identity || t.localScale != Vector3.one;
            t.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            t.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            log.AppendLine($"{asset.name} instance {(found.Length == 1 ? "kept" : "created")} at the identity of {parent.name}" + (moved ? " (local pose reset)" : ""));
            return t;
        }

        // Bones of the scene instance to the skinned mesh's bind pose (world = renderer.localToWorld * bindpose^-1), parents first.
        // The FBX, its importer and its rig are not changed; only this instance's bone transforms are overridden.
        static void ApplyBindPose(Transform model, StringBuilder log)
        {
            var targets = BindTargets(model);
            var moved = 0f;
            var turned = 0f;
            foreach (var pair in targets.OrderBy(p => Depth(p.Key)))
            {
                var bone = pair.Key;
                var m = pair.Value;
                var position = (Vector3)m.GetColumn(3);
                moved = Mathf.Max(moved, Vector3.Distance(bone.position, position));
                turned = Mathf.Max(turned, Quaternion.Angle(bone.rotation, m.rotation));
                bone.SetPositionAndRotation(position, m.rotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
            }
            log.AppendLine($"Mannequin bones set to the bind pose of its skinned mesh ({targets.Count} bones; largest change {moved * 1000f:F1} mm, {turned:F1} deg)");
        }

        static Dictionary<Transform, Matrix4x4> BindTargets(Transform model)
        {
            var targets = new Dictionary<Transform, Matrix4x4>();
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length == 0)
                throw new InvalidOperationException("Mannequin has no skinned mesh renderer.");
            foreach (var skin in skins)
            {
                var bones = skin.bones;
                var binds = skin.sharedMesh != null ? skin.sharedMesh.bindposes : Array.Empty<Matrix4x4>();
                if (bones.Length != binds.Length)
                    throw new InvalidOperationException($"{skin.name}: {bones.Length} bones but {binds.Length} bind poses.");
                for (var i = 0; i < bones.Length; i++)
                {
                    if (bones[i] == null)
                        continue;
                    var m = skin.transform.localToWorldMatrix * binds[i].inverse;
                    if (targets.TryGetValue(bones[i], out var other) && Vector3.Distance(other.GetColumn(3), m.GetColumn(3)) > 1e-4f)
                        throw new InvalidOperationException($"Bind poses of {bones[i].name} disagree between skinned meshes.");
                    targets[bones[i]] = m;
                }
            }
            return targets;
        }

        static void DisableAnimators(Transform model, StringBuilder log)
        {
            var animators = model.GetComponentsInChildren<Animator>(true);
            foreach (var animator in animators)
            {
                animator.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            }
            log.AppendLine($"Mannequin Animator disabled on the scene instance ({animators.Length}); the bind pose is not animated");
        }

        static int Depth(Transform t)
        {
            var depth = 0;
            for (var p = t.parent; p != null; p = p.parent)
                depth++;
            return depth;
        }

        static void SetSharedMaterial(Transform mesh, Material material)
        {
            if (!mesh.TryGetComponent<Renderer>(out var renderer))
                throw new InvalidOperationException($"'{mesh.name}' has no renderer.");
            renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        // Pickups: missing ones come from one unpacked pickup model instance (their imported pose relative to the model root kept,
        // Grip and Attach with them); each under PC_<Name>_Pickup at its workbench slot (slot pose and collider size only on
        // creation). Grab, physics, material and references rewritten every run.
        static ProtectiveClothingPiece[] EnsurePickups(Transform root, Dictionary<ProtectiveClothingPart, GameObject> worn, Material shared, StringBuilder log)
        {
            var holder = root.Find(PickupsName);
            if (holder == null)
            {
                holder = new GameObject(PickupsName).transform;
                holder.SetParent(root, false);
                holder.position = WorkbenchCenter + Vector3.up * (WorkbenchSize.y * 0.5f + 0.002f);
                log.AppendLine(PickupsName + " created on the workbench top");
            }

            var missing = Parts.Where(p => holder.Find(WrapperName(p)) == null).ToArray();
            if (missing.Length > 0)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(LoadRequired<GameObject>(PickupFbxPath));
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                try
                {
                    foreach (var part in missing)
                    {
                        var mesh = RequireDeep(instance.transform, ProtectiveClothingPiece.PickupMeshName(part));
                        var relative = instance.transform.worldToLocalMatrix * mesh.localToWorldMatrix;
                        var wrapper = new GameObject(WrapperName(part)).transform;
                        wrapper.SetParent(holder, false);
                        wrapper.localPosition = Slots.First(s => s.part == part).slot;
                        wrapper.localRotation = Quaternion.identity;
                        mesh.SetParent(wrapper, false);
                        mesh.localPosition = relative.GetColumn(3);
                        mesh.localRotation = relative.rotation;
                        mesh.localScale = relative.lossyScale;
                        var box = wrapper.gameObject.AddComponent<BoxCollider>();
                        var bounds = BoundsIn(wrapper, mesh);
                        box.center = bounds.center;
                        box.size = bounds.size;
                        log.AppendLine($"{WrapperName(part)} created at its workbench slot ({ProtectiveClothingPiece.PickupMeshName(part)} with its imported pose, " +
                                       $"box collider {Vec(bounds.size)} m)");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            var pieces = new ProtectiveClothingPiece[Parts.Length];
            foreach (var part in Parts)
            {
                var wrapper = holder.Find(WrapperName(part));
                var mesh = RequireDeep(wrapper, ProtectiveClothingPiece.PickupMeshName(part));
                var grip = RequireDeep(mesh, ProtectiveClothingPiece.GripName(part));
                var attach = RequireDeep(mesh, ProtectiveClothingPiece.PickupAttachName(part));
                SetSharedMaterial(mesh, shared);
                foreach (var extra in wrapper.GetComponentsInChildren<Collider>(true).Where(x => x.transform != wrapper))
                    Object.DestroyImmediate(extra);
                if (!wrapper.TryGetComponent<BoxCollider>(out var box))
                    box = wrapper.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = false;
                var body = ConfigureBody(wrapper);
                var grab = ConfigureGrab(wrapper, box, grip);

                var piece = GetOrAdd<ProtectiveClothingPiece>(wrapper.gameObject);
                var data = new SerializedObject(piece);
                Property(data, "part").enumValueIndex = (int)part;
                Property(data, "grab").objectReferenceValue = grab;
                Property(data, "body").objectReferenceValue = body;
                Property(data, "grabCollider").objectReferenceValue = box;
                Property(data, "grip").objectReferenceValue = grip;
                Property(data, "pickupAttach").objectReferenceValue = attach;
                Property(data, "worn").objectReferenceValue = worn[part];
                Property(data, "wornAttach").objectReferenceValue = RequireDeep(worn[part].transform, ProtectiveClothingPiece.WornAttachName(part));
                data.ApplyModifiedPropertiesWithoutUndo();
                if (!piece.gameObject.activeSelf)
                    piece.gameObject.SetActive(true);
                if (!piece.TryValidate(out var error))
                    throw new InvalidOperationException(error);
                pieces[(int)part] = piece;
            }
            log.AppendLine("Pickups: XRGrabInteractable (Instantaneous, Grip attach, single select, no throw), dynamic Rigidbody with gravity, " +
                           "one box collider each, shared material; saved shown on the workbench");
            return pieces;
        }

        static string WrapperName(ProtectiveClothingPart part) => $"PC_{ProtectiveClothingPiece.ModelName(part)}_Pickup";

        static Rigidbody ConfigureBody(Transform wrapper)
        {
            var created = !wrapper.TryGetComponent<Rigidbody>(out var body);
            if (created)
            {
                body = wrapper.gameObject.AddComponent<Rigidbody>();
                body.mass = PickupMass;
            }
            // Rests on the workbench and falls onto the floor or the platform when released; XRI makes it kinematic while held.
            body.isKinematic = false;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            return body;
        }

        static XRGrabInteractable ConfigureGrab(Transform wrapper, BoxCollider box, Transform grip)
        {
            var grab = GetOrAdd<XRGrabInteractable>(wrapper.gameObject);
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.trackPosition = true;
            grab.trackRotation = true;
            grab.trackScale = false;
            grab.throwOnDetach = false;
            grab.forceGravityOnDetach = false;
            grab.attachEaseInTime = 0f;
            grab.smoothPosition = false;
            grab.smoothRotation = false;
            // The Grip point comes to the hand.
            grab.attachTransform = grip;
            grab.useDynamicAttach = false;
            grab.farAttachMode = InteractableFarAttachMode.DeferToInteractor;
            grab.selectMode = InteractableSelectMode.Single;
            grab.retainTransformParent = true;
            grab.addDefaultGrabTransformers = true;
            grab.colliders.Clear();
            grab.colliders.Add(box);
            if (grab.interactionLayers.value == 0)
                grab.interactionLayers = InteractionLayerMask.GetMask("Default");
            return grab;
        }

        // Target ring (one, display only: no collider), saved hidden; its points are written by the guide at runtime.
        static LineRenderer EnsureMarker(Transform root, Material material, StringBuilder log)
        {
            var t = root.Find(MarkerName);
            var created = t == null;
            if (created)
            {
                t = new GameObject(MarkerName).transform;
                t.SetParent(root, false);
            }
            t.localScale = Vector3.one;
            var line = GetOrAdd<LineRenderer>(t.gameObject);
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = MarkerWidth;
            line.positionCount = 0;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            foreach (var c in t.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
            t.gameObject.SetActive(false);
            log.AppendLine($"{MarkerName} {(created ? "created" : "kept")}: one target ring (wear radius), display only, saved hidden");
            return line;
        }

        // World-space board: one instruction line and the development reset button; pose only on creation. Only the button image
        // takes rays.
        static void EnsureBoard(Transform root, ProtectiveClothingEquipment equipment, LineRenderer marker, XROrigin origin, Vector3 eye, StringBuilder log)
        {
            var font = LoadRequired<TMP_FontAsset>(KoreanFontPath);
            var t = root.Find(BoardName);
            var created = t == null;
            // The board is a canvas at CanvasScale (0.001 per unit), so its defaults are checked at that scale; the scale is kept.
            var boardScale = Vector3.one * CanvasScale;
            string pose;
            if (created)
            {
                t = new GameObject(BoardName, typeof(RectTransform)).transform;
                t.SetParent(root, false);
                t.gameObject.layer = UiLayer;
                t.localScale = boardScale;
                t.SetPositionAndRotation(BoardPosition, FacingEye(BoardPosition, eye));
                pose = $"created at the default {Vec(BoardPosition)}, scale {CanvasScale}";
            }
            else if (IsPose(t, EarlierBoardPosition, FacingEye(EarlierBoardPosition, eye), boardScale))
            {
                // Exactly the earlier default beside the user's path along the mannequin: moved once behind the head end (position and
                // rotation only; the scale stays). The next run finds it at the current default and leaves it.
                t.SetPositionAndRotation(BoardPosition, FacingEye(BoardPosition, eye));
                pose = $"was exactly at the earlier default {Vec(EarlierBoardPosition)}: moved once to {Vec(BoardPosition)} (scale {CanvasScale} kept)";
            }
            else if (IsPose(t, BoardPosition, FacingEye(BoardPosition, eye), boardScale))
            {
                pose = "kept at the current default";
            }
            else
            {
                pose = $"kept (user position, rotation or scale; the current default is {Vec(BoardPosition)}, scale {CanvasScale})";
            }
            ((RectTransform)t).sizeDelta = BoardSize;
            var canvas = GetOrAdd<Canvas>(t.gameObject);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = origin.Camera;
            GetOrAdd<CanvasScaler>(t.gameObject);
            foreach (var raycaster in t.GetComponents<GraphicRaycaster>().Where(r => !(r is TrackedDeviceGraphicRaycaster)))
                Object.DestroyImmediate(raycaster);
            GetOrAdd<TrackedDeviceGraphicRaycaster>(t.gameObject);

            var background = EnsureRect(t, "Background", Vector2.zero, BoardSize);
            var image = GetOrAdd<Image>(background.gameObject);
            if (image.color == Color.white)
                image.color = BoardBackground;
            var instruction = EnsureText(t, "Instruction", new Vector2(0f, 55f), new Vector2(580f, 120f), 30f, font);
            var buttonRect = EnsureRect(t, "ResetButton", new Vector2(0f, -85f), new Vector2(240f, 70f));
            var buttonImage = GetOrAdd<Image>(buttonRect.gameObject);
            if (buttonImage.color == Color.white)
                buttonImage.color = ButtonColor;
            var button = GetOrAdd<Button>(buttonRect.gameObject);
            button.targetGraphic = buttonImage;
            var label = EnsureText(buttonRect, "Label", Vector2.zero, new Vector2(240f, 70f), 28f, font);
            foreach (var graphic in t.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = graphic == buttonImage;

            var guide = GetOrAdd<ProtectiveClothingGuide>(t.gameObject);
            var data = new SerializedObject(guide);
            Property(data, "equipment").objectReferenceValue = equipment;
            Property(data, "koreanFont").objectReferenceValue = font;
            Property(data, "instructionText").objectReferenceValue = instruction;
            Property(data, "resetButton").objectReferenceValue = button;
            Property(data, "resetButtonText").objectReferenceValue = label;
            Property(data, "marker").objectReferenceValue = marker;
            Property(data, "viewer").objectReferenceValue = origin.Camera.transform;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (!guide.TryValidate(out var error))
                throw new InvalidOperationException(error);
            log.AppendLine($"{BoardName} {pose}: instruction line, reset button (code listener only), " +
                           "tracked device raycaster, ProtectiveClothingGuide");
        }

        static Quaternion FacingEye(Vector3 position, Vector3 eye) => Quaternion.LookRotation(Vector3.ProjectOnPlane(position - eye, Vector3.up), Vector3.up);

        // World position and rotation, and the local scale each caller expects (1 for the mannequin root, CanvasScale for the board).
        static bool IsPose(Transform t, Vector3 position, Quaternion rotation, Vector3 localScale) =>
            (t.position - position).sqrMagnitude < 1e-8f && Quaternion.Angle(t.rotation, rotation) < 0.01f && (t.localScale - localScale).sqrMagnitude < 1e-12f;

        // The upright layout's platform is removed only when it is exactly the earlier default and the mannequin root is at a
        // default; otherwise it is the user's and is kept (reported).
        static void RemoveEarlierPlatform(Transform root, bool mannequinAtDefault, StringBuilder log)
        {
            var platform = root.Find(EarlierPlatformName);
            if (platform == null)
                return;
            var unchanged = (platform.position - EarlierPlatformCenter).sqrMagnitude < 1e-8f && (platform.localScale - EarlierPlatformSize).sqrMagnitude < 1e-8f &&
                            Quaternion.Angle(platform.rotation, Quaternion.identity) < 0.01f;
            if (unchanged && mannequinAtDefault)
            {
                Object.DestroyImmediate(platform.gameObject);
                log.AppendLine($"{EarlierPlatformName} (earlier upright layout, unchanged) removed");
            }
            else
            {
                log.AppendLine($"{EarlierPlatformName} kept (adjusted by the user or the mannequin is not at a default); remove it yourself if it is no longer needed");
            }
        }

        // Display board under the mannequin root (tilts with it) and, only with the default mannequin pose, a stand from the floor
        // under the board centre. Both: furniture material, box collider, pose only on creation.
        static void EnsureDisplayBoard(Transform root, Transform mannequinRoot, Material furniture, bool atDefault, StringBuilder log)
        {
            var board = mannequinRoot.Find(DisplayBoardName);
            if (board == null)
            {
                board = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                board.name = DisplayBoardName;
                board.SetParent(mannequinRoot, false);
                board.SetLocalPositionAndRotation(DisplayBoardLocalCenter, Quaternion.identity);
                board.localScale = DisplayBoardSize;
                log.AppendLine($"{DisplayBoardName} created behind the mannequin's back (under {MannequinRootName}, tilts with it)");
            }
            board.GetComponent<MeshRenderer>().sharedMaterial = furniture;

            var stand = root.Find(StandName);
            if (stand == null && atDefault)
            {
                var top = board.position.y - StandGap;
                stand = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                stand.name = StandName;
                stand.SetParent(root, false);
                stand.position = new Vector3(board.position.x, top * 0.5f, board.position.z);
                stand.localScale = new Vector3(StandFootprint.x, top, StandFootprint.y);
                log.AppendLine($"{StandName} created under the display board ({top:F2} m high)");
            }
            else if (stand == null)
            {
                log.AppendLine($"{StandName} not created: the mannequin root is the user's pose (add a stand by hand if wanted)");
            }
            if (stand != null)
                stand.GetComponent<MeshRenderer>().sharedMaterial = furniture;
        }

        static RectTransform EnsureRect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var t = parent.Find(name);
            if (t != null)
                return (RectTransform)t;
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.gameObject.layer = UiLayer;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        // Layout and style only on creation; text stays empty (filled at runtime).
        static TMP_Text EnsureText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize, TMP_FontAsset font)
        {
            var created = parent.Find(name) == null;
            var rect = EnsureRect(parent, name, position, size);
            var text = GetOrAdd<TextMeshProUGUI>(rect.gameObject);
            text.raycastTarget = false;
            if (!created)
                return text;
            text.font = font;
            text.text = string.Empty;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = BoardText;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = false;
            return text;
        }

        // ---------- Menu 2 ----------

        [MenuItem("B/Protective Clothing/2. Validate Equipment Scene (report only)")]
        static void Validate()
        {
            var r = new Report();
            r.Section("Model files and importers");
            var pickupModel = AssetDatabase.LoadAssetAtPath<GameObject>(PickupFbxPath);
            var wornModel = AssetDatabase.LoadAssetAtPath<GameObject>(WornFbxPath);
            ValidateModelImporter(PickupFbxPath, r);
            ValidateModelImporter(WornFbxPath, r);
            ValidateModelContent(pickupModel, wornModel, r);
            r.Section("Textures and material");
            ValidateTexturesAndMaterial(r);
            r.Section("Scene");
            if (!File.Exists(ScenePath))
                r.Check(false, "Scene missing: " + ScenePath);
            else if (EditorSceneManager.GetActiveScene().path != ScenePath)
                r.NotRun($"Scene checks: open {ScenePath} and run '2. Validate Equipment Scene' again.");
            else
                ValidateScene(EditorSceneManager.GetActiveScene(), wornModel, r);
            r.Flush();
        }

        static void ValidateModelImporter(string path, Report r)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            r.Check(importer != null, "Model importer present: " + path);
            if (importer == null)
                return;
            r.Check(Mathf.Approximately(importer.globalScale, 1f) && importer.useFileScale && !importer.bakeAxisConversion && importer.preserveHierarchy,
                $"{Path.GetFileName(path)}: scale 1 with file units (no 100 / 0.01 fix), axis conversion not baked, explicit prefab root");
            r.Check(importer.materialImportMode == ModelImporterMaterialImportMode.None && !importer.importCameras && !importer.importLights &&
                    !importer.importAnimation && importer.animationType == ModelImporterAnimationType.None,
                $"{Path.GetFileName(path)}: no imported materials, cameras, lights, animation or rig");
        }

        // Contract names (README_Model.md): six meshes and their Empties, each exactly once; worn meshes at the common origin.
        static void ValidateModelContent(GameObject pickupModel, GameObject wornModel, Report r)
        {
            r.Check(pickupModel != null && wornModel != null, "Pickup and worn models load");
            if (pickupModel == null || wornModel == null)
                return;
            var pickupNames = pickupModel.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            var wornNames = wornModel.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            var problems = new List<string>();
            foreach (var part in Parts)
            {
                foreach (var name in new[] { ProtectiveClothingPiece.PickupMeshName(part), ProtectiveClothingPiece.GripName(part), ProtectiveClothingPiece.PickupAttachName(part) })
                {
                    if (pickupNames.Count(n => n == name) != 1)
                        problems.Add($"pickup {name} x{pickupNames.Count(n => n == name)}");
                }
                foreach (var name in new[] { ProtectiveClothingPiece.WornName(part), ProtectiveClothingPiece.WornAttachName(part) })
                {
                    if (wornNames.Count(n => n == name) != 1)
                        problems.Add($"worn {name} x{wornNames.Count(n => n == name)}");
                }
            }
            r.Check(problems.Count == 0, "Six parts: each pickup mesh, Grip, pickup Attach, worn mesh and worn Attach exactly once" +
                                         (problems.Count > 0 ? ": " + string.Join(", ", problems) : ""));
            var notCommon = Parts.Select(p => FindDeep(wornModel.transform, ProtectiveClothingPiece.WornName(p)))
                .Where(t => t != null && (t.localPosition.sqrMagnitude > 1e-10f || Quaternion.Angle(t.localRotation, Quaternion.identity) > 0.01f ||
                                          (t.localScale - Vector3.one).sqrMagnitude > 1e-8f))
                .Select(t => t.name).ToArray();
            r.Check(notCommon.Length == 0, "Worn meshes share the worn model's origin (local identity under the FBX root)" +
                                           (notCommon.Length > 0 ? ": " + string.Join(", ", notCommon) : ""));
            var mats = wornModel.GetComponentsInChildren<Renderer>(true).Concat(pickupModel.GetComponentsInChildren<Renderer>(true))
                .Select(x => x.sharedMaterials.Length).ToArray();
            r.Info($"Model renderers: {mats.Length}, material slots each: {string.Join("/", mats.Distinct())}");
        }

        static void ValidateTexturesAndMaterial(Report r)
        {
            var baseColor = AssetImporter.GetAtPath(BaseColorPath) as TextureImporter;
            var normal = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
            var roughness = AssetImporter.GetAtPath(RoughnessPath) as TextureImporter;
            var derived = AssetImporter.GetAtPath(MetallicSmoothnessPath) as TextureImporter;
            r.Check(baseColor != null && baseColor.textureType == TextureImporterType.Default && baseColor.sRGBTexture, "PC_BaseColor_2K: colour texture, sRGB on");
            r.Check(normal != null && normal.textureType == TextureImporterType.NormalMap, "PC_Normal_1K: Normal Map");
            r.Check(roughness != null && !roughness.sRGBTexture, "PC_Roughness_1K: sRGB off (linear data)");
            r.Check(derived != null && !derived.sRGBTexture && derived.alphaSource == TextureImporterAlphaSource.FromInput,
                "Derived PC_MetallicSmoothness_1K: linear, alpha from input (smoothness = 1 - roughness)");
            foreach (var path in new[] { BaseColorPath, NormalPath, RoughnessPath, MetallicSmoothnessPath })
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                    r.Info($"{Path.GetFileName(path)} {tex.width}x{tex.height}");
            }

            var m = AssetDatabase.LoadAssetAtPath<Material>(SharedMaterialPath);
            r.Check(m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Lit", "Shared material PC_Shared uses URP Lit");
            if (m == null)
                return;
            r.Check(m.GetTexture("_BaseMap") == AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath) &&
                    m.GetTexture("_BumpMap") == AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath) &&
                    m.GetTexture("_MetallicGlossMap") == AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicSmoothnessPath) &&
                    Mathf.Approximately(m.GetFloat("_SmoothnessTextureChannel"), 0f),
                "PC_Shared: base colour, normal map and metallic-smoothness map (smoothness from its alpha) assigned");
            r.Check(m.IsKeywordEnabled("_NORMALMAP") && m.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), "PC_Shared keywords: _NORMALMAP, _METALLICSPECGLOSSMAP");
            r.Info($"PC_Shared: smoothness strength {m.GetFloat("_Smoothness"):0.##}, normal scale {m.GetFloat("_BumpScale"):0.##}, render face front");
        }

        static void ValidateScene(Scene scene, GameObject wornAsset, Report r)
        {
            var origins = InScene<XROrigin>(scene);
            r.Check(origins.Length == 1 && origins[0].Camera != null, $"One XR Origin with a camera ({origins.Length})");
            r.Check(Active(InScene<Camera>(scene)).Length == 1 && Active(InScene<AudioListener>(scene)).Length == 1,
                $"One active camera and one active AudioListener ({Active(InScene<Camera>(scene)).Length}, {Active(InScene<AudioListener>(scene)).Length})");
            r.Check(InScene<XRInteractionManager>(scene).Length == 1, "One XR Interaction Manager");
            var systems = Active(InScene<EventSystem>(scene));
            r.Check(systems.Length == 1 && systems[0].GetComponents<BaseInputModule>().Length == 1 && systems[0].GetComponent<BaseInputModule>() is XRUIInputModule,
                $"One active EventSystem with only XRUIInputModule ({systems.Length})");
            var simulators = SceneInstances(scene, AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPrefabPath));
            r.Check(simulators.All(s => !s.activeSelf), $"XR Interaction Simulator saved inactive ({simulators.Length})");
            var nearFars = origins.Length == 1 ? origins[0].GetComponentsInChildren<NearFarInteractor>(true) : Array.Empty<NearFarInteractor>();
            r.Check(nearFars.Any(n => n.handedness == InteractorHandedness.Left) && nearFars.Any(n => n.handedness == InteractorHandedness.Right),
                "Both hands have a NearFar interactor (either hand can grab)");

            var roots = scene.GetRootGameObjects().Where(g => g.name == RootName).ToArray();
            r.Check(roots.Length == 1, $"One {RootName}");
            if (roots.Length != 1)
                return;
            var root = roots[0].transform;
            var equipments = InScene<ProtectiveClothingEquipment>(scene);
            r.Check(equipments.Length == 1 && equipments[0].transform == root, "One ProtectiveClothingEquipment on " + RootName);
            if (equipments.Length != 1)
                return;
            var equipment = equipments[0];
            r.Check(equipment.TryValidate(out var error), "Equipment references, parts and wear radius valid" + (error != null ? ": " + error : ""));
            r.Info($"Wear radius {equipment.WearRadius:0.###} m (pickup Attach to worn Attach, world distance only; development value)");

            ValidateLock(root, equipment, r);

            r.Section("Mannequin and worn alignment");
            Guarded(r, "alignment", () => ValidateAlignment(scene, root, wornAsset, r));

            r.Section("Display layout and targets (development values; reach is the user's check)");
            Guarded(r, "layout", () => ValidateLayout(root, equipment, r));

            r.Section("Pickups");
            Guarded(r, "pickups", () => ValidatePickups(scene, root, equipment, r));

            r.Section("Guidance, marker and reset");
            Guarded(r, "guidance", () => ValidateGuide(scene, root, equipment, origins.Length == 1 ? origins[0] : null, r));
            r.Info("Grabbing, wearing at the target, angle independence, wrong side and released pickups, tracking and focus loss, two-hand " +
                   "use, reset and reach are runtime checks for the user.");
        }

        // A check group that throws (for example on a broken reference) is reported as a FAIL; the other groups still run.
        static void Guarded(Report r, string group, Action check)
        {
            try
            {
                check();
            }
            catch (Exception e)
            {
                r.Check(false, $"{group} checks stopped by {e.GetType().Name}: {e.Message}");
            }
        }

        // The lock path of an invalid setup: Awake disables every grab it can reach (the pieces' grabs and every grab under the
        // equipment), so each pickup must carry its grab on itself under the equipment; static, not a Play result.
        static void ValidateLock(Transform root, ProtectiveClothingEquipment equipment, Report r)
        {
            var under = root.GetComponentsInChildren<XRGrabInteractable>(true);
            var pieces = root.GetComponentsInChildren<ProtectiveClothingPiece>(true);
            var withoutGrab = pieces.Where(p => !p.TryGetComponent<XRGrabInteractable>(out _)).Select(p => p.name).ToArray();
            var outside = equipment.Pieces.Where(p => p != null && !p.transform.IsChildOf(root)).Select(p => p.name).ToArray();
            r.Check(withoutGrab.Length == 0 && outside.Length == 0,
                $"Invalid-setup lock reaches every pickup: {under.Length} grab(s) under {RootName}, each piece carries its own grab" +
                (withoutGrab.Length > 0 ? "; no grab on " + string.Join(", ", withoutGrab) : "") +
                (outside.Length > 0 ? "; outside the equipment: " + string.Join(", ", outside) : ""));
            r.Check(under.All(g => g.enabled), "Saved grabs enabled (only an invalid setup disables them at Awake)");
            r.Info("Locks at runtime: invalid setup -> grabs disabled at Awake and the setup error line; reset running, failed reset or the " +
                   "equipment disabled -> its select filter refuses new grabs (registered from Awake to OnDestroy) and wearing is not judged");
        }

        // Target centres in world space (default layout values are development reach aims only), side pairs against the wear radius,
        // and targets clear of the furniture colliders.
        static void ValidateLayout(Transform root, ProtectiveClothingEquipment equipment, Report r)
        {
            var pieces = equipment.Pieces.Where(p => p != null && p.WornAttach != null).ToArray();
            var origin = Vector3.zero;
            foreach (var p in pieces)
            {
                var t = p.WornAttach.position;
                var horizontal = Vector2.Distance(new Vector2(t.x, t.z), new Vector2(origin.x, origin.z));
                var inRange = t.y >= TargetHeightLow && t.y <= TargetHeightHigh;
                r.Info($"{p.Part} target {Vec(t)}: height {t.y:F2} m, {horizontal:F2} m from the XR Origin on the floor" +
                       (inRange ? "" : $" - NOTE outside the development range {TargetHeightLow:0.00}-{TargetHeightHigh:0.00} m (kept; check reach)"));
            }

            var radius = equipment.WearRadius;
            foreach (var (left, right) in new[] { (ProtectiveClothingPart.GloveLeft, ProtectiveClothingPart.GloveRight), (ProtectiveClothingPart.OverbootLeft, ProtectiveClothingPart.OverbootRight) })
            {
                var a = pieces.FirstOrDefault(p => p.Part == left);
                var b = pieces.FirstOrDefault(p => p.Part == right);
                if (a == null || b == null)
                {
                    r.Check(false, $"{left} and {right} targets present");
                    continue;
                }
                var d = Vector3.Distance(a.WornAttach.position, b.WornAttach.position);
                r.Check(d > radius, $"{left} / {right} targets {d:F3} m apart, more than the wear radius {radius:0.###} m (a part held at the other side's " +
                                    $"target centre is not worn)" + (d >= 2f * radius ? "; the two wear spheres do not overlap" : "; NOTE the wear spheres overlap near the middle"));
            }

            Physics.SyncTransforms();
            var furniture = root.GetComponentsInChildren<Collider>(true)
                .Where(c => c.GetComponentInParent<ProtectiveClothingPiece>(true) == null && c.enabled && c.gameObject.activeInHierarchy &&
                            !(c is MeshCollider mesh && !mesh.convex)).ToArray();
            var inside = pieces.Where(p => furniture.Any(c => (c.ClosestPoint(p.WornAttach.position) - p.WornAttach.position).sqrMagnitude < 1e-8f))
                .Select(p => p.Part.ToString()).ToArray();
            r.Check(inside.Length == 0, "No target centre inside the workbench, display board or stand colliders" +
                                        (inside.Length > 0 ? ": " + string.Join(", ", inside) : ""));
            var bench = root.Find(WorkbenchName);
            if (bench != null && bench.TryGetComponent<Collider>(out var benchCollider))
            {
                var nearest = pieces.Select(p => Vector3.Distance(benchCollider.ClosestPoint(p.WornAttach.position), p.WornAttach.position)).DefaultIfEmpty(0f).Min();
                r.Info($"Nearest target to the workbench: {nearest:F2} m (pickups are taken from the workbench and carried to the mannequin)");
            }
        }

        static void ValidateAlignment(Scene scene, Transform root, GameObject wornAsset, Report r)
        {
            var mannequinAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MannequinFbxPath);
            var mannequins = SceneInstances(scene, mannequinAsset);
            var worns = SceneInstances(scene, wornAsset);
            var mannequinRoot = root.Find(MannequinRootName);
            r.Check(mannequins.Length == 1 && worns.Length == 1, $"One mannequin and one worn model instance ({mannequins.Length}, {worns.Length})");
            r.Check(SceneInstances(scene, AssetDatabase.LoadAssetAtPath<GameObject>(PickupFbxPath)).Length == 0, "No pickup model instance left (pickups are unpacked objects)");
            if (mannequinRoot == null || mannequins.Length != 1 || worns.Length != 1)
            {
                r.Check(false, MannequinRootName + " with the two instances");
                return;
            }
            var mannequin = mannequins[0].transform;
            var worn = worns[0].transform;
            bool AtIdentity(Transform t) => t.parent == mannequinRoot && t.localPosition.sqrMagnitude < 1e-10f &&
                                            Quaternion.Angle(t.localRotation, Quaternion.identity) < 0.01f && (t.localScale - Vector3.one).sqrMagnitude < 1e-8f;
            r.Check(AtIdentity(mannequin) && AtIdentity(worn), $"Mannequin and worn model at the identity of {MannequinRootName} (they move together through it)");
            var s = mannequinRoot.lossyScale;
            r.Check(Mathf.Abs(s.x - s.y) < 1e-4f && Mathf.Abs(s.y - s.z) < 1e-4f && s.x > 0f, $"{MannequinRootName} scale uniform and positive ({Vec(s)})");
            r.Check(mannequin.GetComponentsInChildren<Animator>(true).All(a => !a.enabled), "Mannequin Animator disabled (no clip pose)");
            r.Check(mannequin.GetComponentsInChildren<Collider>(true).Length == 0, "No collider on the mannequin (it never blocks bringing a part to the body)");

            var targets = BindTargets(mannequin);
            var worstMove = 0f;
            var worstTurn = 0f;
            foreach (var pair in targets)
            {
                worstMove = Mathf.Max(worstMove, Vector3.Distance(pair.Key.position, pair.Value.GetColumn(3)));
                worstTurn = Mathf.Max(worstTurn, Quaternion.Angle(pair.Key.rotation, pair.Value.rotation));
            }
            r.Check(worstMove < 0.001f && worstTurn < 0.1f, $"Mannequin bones in the skinned mesh's bind pose ({targets.Count} bones; worst {worstMove * 1000f:F2} mm, {worstTurn:F2} deg)");

            // Model documents (Blender metres, +Z up, -Y front, +X the mannequin's left) in the mannequin root's frame: (-x, z, -y).
            var reference = ReadText(ReferenceJsonPath);
            var anchors = ReadText(AnchorJsonPath);
            if (reference == null || anchors == null)
            {
                r.Check(false, "Model documents readable: " + ReferenceJsonPath + ", " + AnchorJsonPath);
                return;
            }
            var boneProblems = new List<string>();
            var worstBone = 0f;
            foreach (var bone in new[] { "Hips", "LeftHand", "RightHand", "LeftFoot", "RightFoot", "Head" })
            {
                var t = FindDeep(mannequin, MixamoPrefix + bone);
                var referenceBoneHead = BoneHead(reference, MixamoPrefix + bone);
                if (t == null || referenceBoneHead == null)
                {
                    boneProblems.Add(bone + " missing");
                    continue;
                }
                var d = Vector3.Distance(mannequinRoot.InverseTransformPoint(t.position), ToUnity(referenceBoneHead.Value));
                worstBone = Mathf.Max(worstBone, d);
                if (d > 0.005f)
                    boneProblems.Add($"{bone} {d * 1000f:F1} mm");
            }
            r.Check(boneProblems.Count == 0, $"Bones match ReferenceInspection.json (bind T pose; worst {worstBone * 1000f:F1} mm, limit 5 mm)" +
                                             (boneProblems.Count > 0 ? ": " + string.Join(", ", boneProblems) : ""));

            var attachProblems = new List<string>();
            var worstAttach = 0f;
            foreach (var part in Parts)
            {
                var t = FindDeep(worn, ProtectiveClothingPiece.WornAttachName(part));
                var doc = AnchorTranslation(anchors, ProtectiveClothingPiece.WornAttachName(part));
                if (t == null || doc == null)
                {
                    attachProblems.Add(part + " missing");
                    continue;
                }
                var d = Vector3.Distance(mannequinRoot.InverseTransformPoint(t.position), ToUnity(doc.Value));
                worstAttach = Mathf.Max(worstAttach, d);
                if (d > 0.001f)
                    attachProblems.Add($"{part} {d * 1000f:F1} mm");
            }
            r.Check(attachProblems.Count == 0, $"Worn Attach points match AnchorManifest.json (worst {worstAttach * 1000f:F2} mm, limit 1 mm)" +
                                               (attachProblems.Count > 0 ? ": " + string.Join(", ", attachProblems) : ""));

            // Sides: each left / right target is nearer the mannequin's own same-side hand or foot.
            var sides = new[]
            {
                (ProtectiveClothingPart.GloveLeft, "LeftHand", "RightHand"), (ProtectiveClothingPart.GloveRight, "RightHand", "LeftHand"),
                (ProtectiveClothingPart.OverbootLeft, "LeftFoot", "RightFoot"), (ProtectiveClothingPart.OverbootRight, "RightFoot", "LeftFoot")
            };
            var sideProblems = sides.Where(x =>
            {
                var target = FindDeep(worn, ProtectiveClothingPiece.WornAttachName(x.Item1));
                var same = FindDeep(mannequin, MixamoPrefix + x.Item2);
                var other = FindDeep(mannequin, MixamoPrefix + x.Item3);
                return target == null || same == null || other == null ||
                       Vector3.Distance(target.position, same.position) >= Vector3.Distance(target.position, other.position);
            }).Select(x => x.Item1.ToString()).ToArray();
            r.Check(sideProblems.Length == 0, "Left and right targets on the mannequin's own left and right hand / foot" +
                                              (sideProblems.Length > 0 ? ": " + string.Join(", ", sideProblems) : ""));

            // Body front from the bind pose: the spine (hips to head) crossed with the shoulders (mannequin's right to left); for an
            // upright mannequin facing -Z this is -Z (Unity's left-handed cross product).
            var right = FindDeep(mannequin, MixamoPrefix + "RightArm");
            var left = FindDeep(mannequin, MixamoPrefix + "LeftArm");
            var hips = FindDeep(mannequin, MixamoPrefix + "Hips");
            var head = FindDeep(mannequin, MixamoPrefix + "Head");
            if (right != null && left != null && hips != null && head != null)
            {
                var front = Vector3.Cross(head.position - hips.position, left.position - right.position).normalized;
                var toUser = Vector3.ProjectOnPlane(-mannequinRoot.position, Vector3.up).normalized;
                var spine = (head.position - hips.position).normalized;
                r.Info($"Mannequin front {Vec(front)} (up {Vector3.Dot(front, Vector3.up):F2}, toward the XR Origin {Vector3.Dot(front, toUser):F2}); " +
                       $"spine {Vector3.Angle(spine, Vector3.up):F0} deg from vertical. The front must face the user or up so no target is hidden behind the body.");
                r.Check(Vector3.Dot(front, Vector3.up) > 0.2f || Vector3.Dot(front, toUser) > 0.2f, "Mannequin front faces up or toward the user (targets on the visible side)");
            }

            var wornMeshes = Parts.Select(p => FindDeep(worn, ProtectiveClothingPiece.WornName(p))).ToArray();
            var shared = AssetDatabase.LoadAssetAtPath<Material>(SharedMaterialPath);
            r.Check(wornMeshes.All(t => t != null && !t.gameObject.activeSelf), "Worn meshes saved hidden (shown by wearing only)");
            r.Check(wornMeshes.All(t => t != null && t.TryGetComponent<Renderer>(out var x) && x.sharedMaterials.All(m => m == shared)),
                "Worn meshes use the shared material PC_Shared");
        }

        static void ValidatePickups(Scene scene, Transform root, ProtectiveClothingEquipment equipment, Report r)
        {
            var pieces = equipment.Pieces;
            var shared = AssetDatabase.LoadAssetAtPath<Material>(SharedMaterialPath);
            r.Check(InScene<ProtectiveClothingPiece>(scene).Length == Parts.Length && pieces.Count == Parts.Length &&
                    pieces.Select(p => p == null ? -1 : (int)p.Part).SequenceEqual(Parts.Select(p => (int)p)),
                "Six pieces, one per part, in part order (Jacket, Trousers, Glove_L, Glove_R, Overboot_L, Overboot_R)");
            if (pieces.Any(p => p == null))
                return;
            r.Check(pieces.All(p => p.Grab != null && p.Grip != null && p.PickupAttach != null && p.WornAttach != null && p.Worn != null),
                "Every piece has its grab, Grip, Attach points and worn mesh");
            r.Check(pieces.Select(p => p.Grab).Distinct().Count() == pieces.Count && pieces.Select(p => p.Grip).Distinct().Count() == pieces.Count &&
                    pieces.Select(p => p.PickupAttach).Distinct().Count() == pieces.Count && pieces.Select(p => p.WornAttach).Distinct().Count() == pieces.Count &&
                    pieces.Select(p => p.Worn).Distinct().Count() == pieces.Count,
                "No grab, Grip, Attach or worn mesh shared between parts");
            foreach (var p in pieces)
            {
                var name = ProtectiveClothingPiece.ModelName(p.Part);
                r.Check(p.TryValidate(out var error), $"{name}: references, names and structure" + (error != null ? ": " + error : ""));
                var g = p.Grab;
                var b = p.Body;
                r.Check(g != null && g.selectMode == InteractableSelectMode.Single && g.attachTransform == p.Grip && !g.useDynamicAttach && !g.throwOnDetach &&
                        g.movementType == XRBaseInteractable.MovementType.Instantaneous && g.colliders.Count == 1 && g.colliders[0] == p.GrabCollider,
                    $"{name}: grab single select, Grip attach, one collider, no throw");
                r.Check(b != null && !b.isKinematic && b.useGravity && p.GrabCollider is BoxCollider box && !box.isTrigger &&
                        p.GetComponentsInChildren<Collider>(true).Length == 1,
                    $"{name}: dynamic Rigidbody with gravity, one solid box collider");
                r.Check(p.gameObject.activeSelf && p.GetComponentsInChildren<Renderer>(true).All(x => x.sharedMaterials.All(m => m == shared)),
                    $"{name}: pickup saved shown with the shared material");
                r.Check(g != null && g.selectExited.GetPersistentEventCount() == 0 && g.selectEntered.GetPersistentEventCount() == 0,
                    $"{name}: no persistent grab events (the equipment subscribes in code)");
            }

            var bounds = pieces.Select(p => RendererBounds(p.transform)).ToArray();
            var overlaps = new List<string>();
            for (var i = 0; i < bounds.Length; i++)
            {
                for (var j = i + 1; j < bounds.Length; j++)
                {
                    if (bounds[i].Intersects(bounds[j]))
                        overlaps.Add($"{pieces[i].Part}/{pieces[j].Part}");
                }
            }
            r.Check(overlaps.Count == 0, "Pickups do not overlap on the workbench" + (overlaps.Count > 0 ? ": " + string.Join(", ", overlaps) : ""));
            var bench = root.Find(WorkbenchName);
            if (bench != null && bench.TryGetComponent<Collider>(out var benchCollider))
            {
                var top = benchCollider.bounds.max.y;
                var off = pieces.Where((p, i) => Mathf.Abs(bounds[i].min.y - top) > 0.02f || bounds[i].min.x < benchCollider.bounds.min.x ||
                                                 bounds[i].max.x > benchCollider.bounds.max.x || bounds[i].min.z < benchCollider.bounds.min.z ||
                                                 bounds[i].max.z > benchCollider.bounds.max.z)
                    .Select(p => p.Part.ToString()).ToArray();
                r.Check(off.Length == 0, $"Pickups rest on the workbench top ({top:F2} m) within its edges" + (off.Length > 0 ? ": " + string.Join(", ", off) : ""));
            }
            else
            {
                r.Check(false, WorkbenchName + " with a collider");
            }
            var floor = root.Find(FloorName);
            r.Check(floor != null && floor.TryGetComponent<Collider>(out _), $"{FloorName} has a collider (released pickups never fall through)");
        }

        static void ValidateGuide(Scene scene, Transform root, ProtectiveClothingEquipment equipment, XROrigin origin, Report r)
        {
            var guides = InScene<ProtectiveClothingGuide>(scene);
            r.Check(guides.Length == 1 && guides[0].TryValidate(out _) && guides[0].Equipment == equipment, "One guide reading the equipment, with its texts, button and marker");
            if (guides.Length != 1)
                return;
            var guide = guides[0];
            var data = new SerializedObject(guide);
            r.Check(origin != null && data.FindProperty("viewer")?.objectReferenceValue == origin.Camera.transform, "Marker faces the XR camera");
            var marker = guide.Marker;
            r.Check(marker != null && !marker.gameObject.activeSelf && marker.GetComponentsInChildren<Collider>(true).Length == 0 && marker.sharedMaterial != null &&
                    marker.transform.lossyScale == Vector3.one && !marker.useWorldSpace,
                "One target ring: saved hidden, no collider, material set, unit scale (its radius is the wear radius)");
            var board = guide.transform;
            r.Check(board.TryGetComponent<Canvas>(out var canvas) && canvas.renderMode == RenderMode.WorldSpace && board.GetComponent<TrackedDeviceGraphicRaycaster>() != null,
                "Board: world-space canvas with a tracked device raycaster");
            var button = guide.ResetButton;
            var targets = board.GetComponentsInChildren<Graphic>(true).Where(x => x.raycastTarget).ToArray();
            r.Check(button != null && targets.Length == 1 && targets[0] == button.targetGraphic && button.onClick.GetPersistentEventCount() == 0,
                "Only the reset button image takes rays; no persistent OnClick entries (code listener only)");
            var filled = board.GetComponentsInChildren<TMP_Text>(true).Where(t => !string.IsNullOrEmpty(t.text)).Select(t => t.name).ToArray();
            r.Check(filled.Length == 0, "Board texts saved empty (text comes from ProtectiveClothingSubtitles at runtime)" + (filled.Length > 0 ? ": " + string.Join(", ", filled) : ""));

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font == null || font.sourceFontFile == null)
            {
                r.Check(false, "Korean TMP font with source file: " + KoreanFontPath);
                return;
            }
            var missing = string.Concat(ProtectiveClothingSubtitles.All).Distinct()
                .Where(c => !char.IsWhiteSpace(c) && !char.IsControl(c) && !font.sourceFontFile.HasCharacter(c)).ToArray();
            r.Check(missing.Length == 0, "Font source has every guidance character" + (missing.Length > 0 ? ": missing '" + new string(missing) + "'" : ""));
            var instruction = board.Find("Instruction") is { } instructionObject && instructionObject.TryGetComponent<TMP_Text>(out var text) ? text : null;
            if (instruction != null)
            {
                // Rough fit: Hangul 1 em, other characters 0.6 em; one line each at the instruction's font size and width.
                var width = ((RectTransform)instruction.transform).rect.width;
                var longest = ProtectiveClothingSubtitles.Lines.Max(l => l.Sum(c => c >= 0x1100 ? 1f : 0.6f) * instruction.fontSize);
                r.Check(longest <= width * 2f, $"Instruction lines fit in two lines (longest {longest:F0} of {width:F0} per line)");
            }
        }

        // ---------- Model documents ----------

        static string ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

        const string Number = @"(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)";

        // "mixamorig1:<Bone>": { "head": [x, y, z] ... } in ReferenceInspection.json (Blender metres).
        static Vector3? BoneHead(string json, string bone)
        {
            var m = Regex.Match(json, "\"" + Regex.Escape(bone) + "\"\\s*:\\s*\\{\\s*\"head\"\\s*:\\s*\\[\\s*" + Number + "\\s*,\\s*" + Number + "\\s*,\\s*" + Number);
            return m.Success ? new Vector3(Parse(m.Groups[1].Value), Parse(m.Groups[2].Value), Parse(m.Groups[3].Value)) : (Vector3?)null;
        }

        // Translation of "matrix_world_blender_m" (rows) of an object in AnchorManifest.json (Blender metres).
        static Vector3? AnchorTranslation(string json, string objectName)
        {
            var start = json.IndexOf("\"" + objectName + "\"", StringComparison.Ordinal);
            if (start < 0)
                return null;
            var matrix = json.IndexOf("\"matrix_world_blender_m\"", start, StringComparison.Ordinal);
            if (matrix < 0)
                return null;
            var numbers = Regex.Matches(json.Substring(matrix), Number).Cast<Match>().Take(16).Select(x => Parse(x.Value)).ToArray();
            return numbers.Length == 16 ? new Vector3(numbers[3], numbers[7], numbers[11]) : (Vector3?)null;
        }

        static float Parse(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        // Blender (+X the mannequin's left, -Y front, +Z up) to the Unity frame of both FBX instances (FBX Y up, X mirrored).
        static Vector3 ToUnity(Vector3 blender) => new Vector3(-blender.x, blender.z, -blender.y);

        // ---------- Helpers ----------

        sealed class Report
        {
            readonly StringBuilder text = new StringBuilder(LogTag + " Validation report (editor static check; not a Play, Simulator or headset result)\n");
            int failures;
            int notRun;

            public void Check(bool ok, string message)
            {
                text.AppendLine((ok ? "PASS  " : "FAIL  ") + message);
                if (!ok) failures++;
            }

            public void Info(string message) => text.AppendLine("INFO  " + message);

            public void Section(string title) => text.AppendLine("== " + title + " ==");

            public void NotRun(string message)
            {
                text.AppendLine("NOT RUN  " + message);
                notRun++;
            }

            public void Flush()
            {
                if (failures > 0)
                {
                    text.AppendLine($"Result: {failures} FAIL item(s)." + (notRun > 0 ? $" {notRun} required check group(s) not run." : ""));
                    Debug.LogError(text.ToString());
                }
                else if (notRun > 0)
                {
                    text.AppendLine($"Result: INCOMPLETE - {notRun} required check group(s) not run. This is not a pass.");
                    Debug.LogWarning(text.ToString());
                }
                else
                {
                    text.AppendLine("Result: no FAIL items. This is static configuration only: grabbing, automatic wearing, the look of the worn " +
                                    "parts, readability, reach and reset still need the user's Unity and Simulator / headset check.");
                    Debug.Log(text.ToString());
                }
            }
        }

        static Bounds RendererBounds(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(t.position, Vector3.zero);
            foreach (var x in renderers)
                bounds.Encapsulate(x.bounds);
            return bounds;
        }

        // Axis-aligned bounds, in space, of a part's mesh bounds.
        static Bounds BoundsIn(Transform space, Transform part)
        {
            if (!part.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
                throw new InvalidOperationException($"'{part.name}' has no mesh.");
            var local = filter.sharedMesh.bounds;
            var matrix = space.worldToLocalMatrix * part.localToWorldMatrix;
            var bounds = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                bounds.Encapsulate(matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, corner)));
            }
            return bounds;
        }

        static T[] InScene<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();

        static T[] Active<T>(T[] components) where T : Component => components.Where(c => c.gameObject.activeInHierarchy).ToArray();

        static GameObject[] SceneInstances(Scene scene, GameObject prefab) =>
            prefab == null ? Array.Empty<GameObject>() :
            scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject)
                .Where(g => PrefabUtility.IsOutermostPrefabInstanceRoot(g) && PrefabUtility.GetCorrespondingObjectFromSource(g) == prefab).ToArray();

        static T GetOrAdd<T>(GameObject go) where T : Component =>
            go.TryGetComponent<T>(out var component) ? component : go.AddComponent<T>();

        static SerializedProperty Property(SerializedObject data, string name) =>
            data.FindProperty(name) ?? throw new InvalidOperationException($"Serialized field '{name}' not found on {data.targetObject.GetType().Name}.");

        static void SetObjects(SerializedProperty array, Object[] values)
        {
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
                return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        static Transform RequireDeep(Transform root, string name) =>
            FindDeep(root, name) ?? throw new InvalidOperationException($"'{name}' not found under {root.name}");

        static string Vec(Vector3 v) => $"({v.x:F3}, {v.y:F3}, {v.z:F3})";

        static T LoadRequired<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Asset not found: " + path);

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
