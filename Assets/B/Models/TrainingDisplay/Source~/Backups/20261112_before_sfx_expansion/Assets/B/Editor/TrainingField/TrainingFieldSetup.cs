using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MilTrainSimVR.B.AedTutorial;
using MilTrainSimVR.B.BleedingControl.PressureBandage.Tutorial;
using MilTrainSimVR.B.BleedingControl.Tourniquet;
using MilTrainSimVR.B.BleedingControl.Tourniquet.Tutorial;
using MilTrainSimVR.B.Decontamination.KD1.Tutorial;
using MilTrainSimVR.B.K1GasMask.MirrorAvatar;
using MilTrainSimVR.B.K1GasMask.Tutorial;
using MilTrainSimVR.B.ProtectiveClothing;
using MilTrainSimVR.B.ProtectiveClothing.Tutorial;
using MilTrainSimVR.B.Training;
using MilTrainSimVR.B.TrainingUI;
using MilTrainSimVR.B.TrainingUI.EditorSetup;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace MilTrainSimVR.B.TrainingField.EditorSetup
{
    // Read-only configuration checks for the two B training field scenes.
    // Layout, course wiring and presentation are stored in the saved scene assets.
    static class TrainingFieldSetup
    {
        const string Menu = "B/교육장/";
        const string LogTag = "[TrainingField]";
        const string SystemsName = "SceneSystems";
        const string EnvironmentName = "EnvironmentRoot";
        const string StationsName = "StationsRoot";
        const string EntryName = "EntryAnchor";
        const string ExitName = "ExitAnchor";
        const string FloorName = "FloorAnchor";
        const string BlockerPrefix = "Blocker_";
        const string ChairName = "FieldChair";
        const string OriginalAedPath = "Assets/B/Models/Pad_Mannequin/Mannequin_AED_Attached.fbx";
        const string AedBodyName = "Mannequin_Body";

        // Exit: A's hub portal (read only here: found by its script type name and read with SerializedObject, no A type referenced).
        const string HubPortalPrefabPath = "Assets/A/Prefabs/HubPortal_ToHub.prefab";
        const string HubScenePath = "Assets/A/Scenes/A_Hub.unity";
        const string HubPortalType = "MilTrainSimVR.A.Common.HubPortal";
        const string ScreenFaderType = "MilTrainSimVR.A.Common.ScreenFader";
        const string ScreenFadeMaterialPath = "Assets/A/Materials/Common/ScreenFade.mat";
        const string HubPortalLabelName = "Label";
        static readonly string[] HubPortalChildren = { "Lens", "EdgeTint", "Sparkles", HubPortalLabelName };
        const string HubPlaqueName = "HubPlaque";
        const string HubSignName = "HubSign";
        const string SignVisualPrefabPath = "Assets/B/Prefabs/TrainingDisplay/TrainingStationSign_Visual.prefab";
        const string SignMaterialPath = "Assets/B/Materials/TrainingDisplay/TrainingStationSign_PaintedWood.mat";

        // Course ID of the field's exit portal: the hub places the user in front of the hub portal with this ID.
        static string HubCourseId(TrainingFieldKind field) => field == TrainingFieldKind.Cbrn ? "B_CBRN" : "B_FirstAid";

        static readonly string[] AedPadNames = { "Pad01_Attached", "Pad02_Attached" };

        const string MaterialDir = "Assets/B/Materials/TrainingField";

        static readonly string[] MarkerMaterials =
        {
            "Assets/B/Materials/Tourniquet/MAT_TQ_GuideMarker.mat",
            "Assets/B/Materials/K1Training/MAT_K1_GuideMarker.mat",
            "Assets/B/Materials/ProtectiveClothing/PC_TargetMarker.mat",
            "Assets/B/Materials/PressureBandage/MAT_PB_GuideNext.mat",
            "Assets/B/Materials/PressureBandage/MAT_PB_GuidePath.mat",
            "Assets/B/Materials/Decontamination/KD1/KD1_ZoneTodo.mat",
            "Assets/B/Materials/Decontamination/KD1/KD1_ZoneTodoFill.mat",
        };

        // Spatial guide marker style (read-only check). Colour = material colour x vertex colour, so line vertex colours stay white.
        const string OverlayShaderName = "B/Common/StatusMarkerOverlay";
        const string UiOverlayShaderName = "B/Common/UIStatusMarkerOverlay";
        const string TextOverlayShaderName = "TextMeshPro/Distance Field Overlay";
        static readonly Color ActionColour = new Color(0.3f, 0.78f, 1f, 1f);
        static readonly Color DoneColour = new Color(0.25f, 0.9f, 0.35f, 1f);
        static readonly Color AttachReadyColour = new Color(0.2f, 0.95f, 0.4f, 1f);
        static readonly Color ShockColour = new Color(1f, 0.22f, 0.12f, 1f);
        const float FillAlpha = 0.28f;
        const float HandleRingWidth = 0.004f;
        const float BodyRingWidth = 0.006f;
        const float ArrowWidth = 0.006f;
        // Kept by role, not forced to the ring width: PB wound outline, KD-1 zone outline, number and check strokes.
        const float WoundOutlineWidth = 0.004f;
        const float ZoneOutlineWidth = 0.009f;
        const float ZoneNumberWidth = 0.007f;
        const float ZoneCheckWidth = 0.009f;

        static readonly TrainingFieldCourseId[] Courses =
        {
            TrainingFieldCourseId.Aed,
            TrainingFieldCourseId.Tourniquet,
            TrainingFieldCourseId.PressureBandage,
            TrainingFieldCourseId.K1GasMask,
            TrainingFieldCourseId.ProtectiveClothing,
            TrainingFieldCourseId.KD1,
        };

        static IEnumerable<TrainingFieldCourseId> CoursesOf(TrainingFieldKind field) => Courses.Where(id => TrainingFieldIds.FieldOf(id) == field);

        [MenuItem(Menu + "3. 구급법 검사 (읽기 전용)")]
        static void CheckFirstAid() => CheckMenu(TrainingFieldKind.FirstAid);

        [MenuItem(Menu + "4. 화생방 검사 (읽기 전용)")]
        static void CheckCbrn() => CheckMenu(TrainingFieldKind.Cbrn);

        static Component StartEntry(TrainingFieldCourseId course, Transform content) => course switch
        {
            TrainingFieldCourseId.Aed => One<AedTutorialStartController>(content),
            TrainingFieldCourseId.Tourniquet => One<TourniquetTrainingStart>(content),
            TrainingFieldCourseId.PressureBandage => One<PressureBandageTrainingStart>(content),
            TrainingFieldCourseId.K1GasMask => One<K1TrainingGuide>(content),
            TrainingFieldCourseId.ProtectiveClothing => One<ProtectiveClothingTrainingStart>(content),
            TrainingFieldCourseId.KD1 => One<KD1TrainingCourse>(content),
            _ => throw new InvalidOperationException($"unknown course {course}.")
        };

        static Canvas[] CourseBoards(Transform content) =>
            content.GetComponentsInChildren<Canvas>(true)
                .Where(cv => IsTopCanvas(cv) && cv.renderMode == RenderMode.WorldSpace && cv.gameObject.activeSelf && cv.GetComponentsInChildren<Button>(true).Length > 0)
                .ToArray();

        static bool IsTopCanvas(Canvas canvas) => canvas.transform.parent == null || canvas.transform.parent.GetComponentInParent<Canvas>(true) == null;

        static string BlockerName(int index, Canvas board) => $"{BlockerPrefix}{index + 1}_{board.name}";

        sealed class AedPatient
        {
            public Transform Holder;
            public Transform Model;
            public MeshFilter Body;
            public MeshFilter[] Pads;
            public Transform Anchors;
        }

        static readonly string[] TransformPoseProperties = { "m_LocalPosition", "m_LocalRotation", "m_LocalEulerAnglesHint", "m_LocalScale" };

        static MeshFilter ModelPart(Transform root, string name)
        {
            var found = root.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name == name).ToArray();
            if (found.Length != 1)
                throw new InvalidOperationException($"{Path(root)}: expected one '{name}' mesh, found {found.Length}.");
            return found[0];
        }

        static AedPatient FindAedPatient(Transform content)
        {
            var filters = content.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name == AedBodyName).ToArray();
            if (filters.Length != 1)
                throw new InvalidOperationException($"{Path(content)}: expected one {AedBodyName}, found {filters.Length}.");
            var a = new AedPatient { Body = filters[0] };
            a.Holder = PrefabUtility.GetOutermostPrefabInstanceRoot(a.Body.gameObject)?.transform;
            a.Model = PrefabUtility.GetNearestPrefabInstanceRoot(a.Body.gameObject)?.transform;
            if (a.Holder == null || a.Model == null || a.Holder == a.Model)
                throw new InvalidOperationException($"{Path(a.Body.transform)}: expected the casualty model inside the patient prefab instance.");
            a.Pads = AedPadNames.Select(n => ModelPart(a.Model, n)).ToArray();
            var anchors = a.Holder.GetComponentsInChildren<Transform>(true).Where(t => t.name == "PadAnchors").ToArray();
            if (anchors.Length != 1 || anchors[0].IsChildOf(a.Model))
                throw new InvalidOperationException($"{Path(a.Holder)}: expected one PadAnchors under the patient root, outside the model (found {anchors.Length}).");
            a.Anchors = anchors[0];
            return a;
        }

        static bool Overridden(Object target, IEnumerable<string> properties)
        {
            var data = new SerializedObject(target);
            return properties.Any(path => data.FindProperty(path) is SerializedProperty p && p.prefabOverride);
        }

        static List<string> LyingAedProblems(AedPatient a, Transform content)
        {
            var problems = new List<string>();
            void SameMesh(Component part, Mesh mesh, string label)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(part);
                var expected = source is MeshFilter f ? f.sharedMesh : source is MeshCollider m ? m.sharedMesh : null;
                if (expected == null || mesh != expected || AssetDatabase.GetAssetPath(mesh) != OriginalAedPath)
                    problems.Add($"{label} uses '{(mesh != null ? AssetDatabase.GetAssetPath(mesh) + ":" + mesh.name : "none")}', not the prefab's {OriginalAedPath} mesh");
            }
            SameMesh(a.Body, a.Body.sharedMesh, AedBodyName);
            var colliders = a.Body.GetComponents<MeshCollider>();
            if (colliders.Length == 0)
                problems.Add("the body has no contact MeshCollider");
            foreach (var collider in colliders)
                SameMesh(collider, collider.sharedMesh, "contact MeshCollider");
            foreach (var pad in a.Pads)
            {
                SameMesh(pad, pad.sharedMesh, pad.name);
                if (Overridden(pad.transform, TransformPoseProperties))
                    problems.Add($"{pad.name} transform differs from the prefab");
            }
            if (Overridden(a.Anchors, TransformPoseProperties))
                problems.Add("PadAnchors pose differs from the prefab");
            if (a.Holder.Find(ChairName) != null)
                problems.Add($"{ChairName} under the AED patient");
            if (a.Holder.GetComponentsInChildren<TrainingFieldSeatedPose>(true).Length > 0)
                problems.Add("seated record on the AED patient");
            var anchors = a.Anchors.GetComponentsInChildren<PadAttachAnchor>(true);
            if (anchors.Length != AedPadNames.Length)
                problems.Add($"{anchors.Length} PadAttachAnchors under PadAnchors, expected {AedPadNames.Length}");
            foreach (var anchor in anchors)
            {
                if (!anchor.TryValidate(out var error))
                    problems.Add(error);
            }
            var monitors = content.GetComponentsInChildren<PatientContactMonitor>(true);
            var listed = monitors.Length == 1 ? new SerializedObject(monitors[0]).FindProperty("patientColliders") : null;
            if (listed == null || colliders.Any(cl => !Enumerable.Range(0, listed.arraySize).Any(i => listed.GetArrayElementAtIndex(i).objectReferenceValue == cl)))
                problems.Add("the patient contact monitor does not list the body's MeshCollider");
            return problems;
        }

        static void CheckPresentation(TrainingFieldStation station, Transform content, BoardReport r)
        {
            var name = station.name;
            switch (station.Course)
            {
                case TrainingFieldCourseId.Aed:
                {
                    List<string> problems;
                    try
                    {
                        problems = LyingAedProblems(FindAedPatient(content), content);
                    }
                    catch (InvalidOperationException e)
                    {
                        problems = new List<string> { e.Message };
                    }
                    r.Check(problems.Count == 0, $"{name}: AED casualty lying as in its prefab (original body, contact collider and pad meshes; " +
                                                 "PadAnchors and pads at the prefab pose; no chair or seated record; contact and pad references kept)" +
                                                 (problems.Count > 0 ? ":\n      " + string.Join("\n      ", problems) : ""));
                    break;
                }
                case TrainingFieldCourseId.Tourniquet:
                case TrainingFieldCourseId.PressureBandage:
                {
                    var record = content.GetComponentsInChildren<TrainingFieldSeatedPose>(true);
                    r.Check(record.Length == 1 && record[0].Drop > 0.1f && record[0].Chair != null && record[0].MovedAnchors.Length > 0 &&
                            record[0].MovedAnchors.All(a => a != null && a.IsChildOf(record[0].transform)) && record[0].Chair.GetComponentsInChildren<Collider>(true).Length == 0,
                        $"{name}: casualty seated once (drop, chair without colliders, anchors moved with the upper body)");
                    if (record.Length == 1)
                        r.Info($"{name}: seated by {record[0].Method}; drop {record[0].Drop:F3} m; moved: {string.Join(", ", record[0].MovedAnchors.Where(a => a != null).Select(a => a.name))}");
                    if (station.Course == TrainingFieldCourseId.Tourniquet)
                    {
                        var zones = content.GetComponentsInChildren<TourniquetBlockZone>(true);
                        r.Check(zones.Length > 0 && zones.All(z => RefOrNull(z, "visual") is Transform v && !v.gameObject.activeSelf),
                            $"{name}: red/orange block zone visuals hidden, judge boxes kept ({zones.Length})");
                        var targets = content.GetComponentsInChildren<TourniquetLimbTarget>(true);
                        r.Check(targets.Length == 1 && RefOrNull(targets[0], "allowedZoneVisual") is Transform g && g.gameObject.activeSelf,
                            $"{name}: green allowed zone visual shown");
                    }
                    if (station.Course == TrainingFieldCourseId.PressureBandage)
                    {
                        var guide = content.GetComponentsInChildren<PressureBandageTrainingGuide>(true).FirstOrDefault();
                        r.Check(guide != null && RefOrNull(guide, "woundGuide") is Transform w && !w.gameObject.activeSelf && w.IsChildOf(content) &&
                                RefOrNull(guide, "woundOutline") is LineRenderer && w.GetComponentsInChildren<Collider>(true).Length == 0,
                            $"{name}: wound outline and arrow linked to the course guide (saved hidden, no collider)");
                    }
                    break;
                }
                case TrainingFieldCourseId.ProtectiveClothing:
                {
                    var guide = content.GetComponentsInChildren<ProtectiveClothingTrainingGuide>(true).FirstOrDefault();
                    r.Check(guide != null && RefOrNull(guide, "pickupMarker") is Transform m && !m.gameObject.activeSelf && m.IsChildOf(content) &&
                            RefOrNull(guide, "pickupLabel") is TMP_Text && m.GetComponentsInChildren<Collider>(true).Length == 0,
                        $"{name}: pickup arrow and name line linked to the course guide (saved hidden, no collider)");
                    break;
                }
            }
        }

        static string OverlayPathOf(string sourcePath) => $"{MaterialDir}/MAT_Field_{System.IO.Path.GetFileNameWithoutExtension(sourcePath)}_Overlay.mat";

        static void CheckMenu(TrainingFieldKind field)
        {
            var path = TrainingFieldIds.ScenePath(field);
            var r = new BoardReport();
            if (!File.Exists(path))
            {
                r.Section($"{field} field ({path})");
                r.Check(false, $"Scene missing: {path} (restore the saved scene asset)");
                Debug.LogError($"{LogTag} Field check, report only\n{r.Text}Result: {r.Failures} FAIL item(s).");
                return;
            }
            var scene = SceneManager.GetSceneByPath(path);
            var openedHere = false;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Debug.LogWarning($"{LogTag} Field check not run in Play mode for a scene that is not open: {path}. This is not a pass.");
                    return;
                }
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                openedHere = true;
            }
            else if (scene.isDirty)
            {
                r.Info($"{path} is open with unsaved changes: the in-memory scene is checked, not the saved file");
            }
            try
            {
                CheckField(scene, field, r);
            }
            finally
            {
                if (openedHere)
                    EditorSceneManager.CloseScene(scene, true);
            }
            var text = $"{LogTag} Field check, report only (editor static configuration; not a Play, Simulator or headset result)\n" + r.Text;
            if (r.Failures > 0)
                Debug.LogError(text + $"Result: {r.Failures} FAIL item(s).");
            else
                Debug.Log(text + "Result: no FAIL items. Course flows, the stop confirm, teleport, the hub portal and readability still need the user's check.");
        }

        static void CheckField(Scene scene, TrainingFieldKind field, BoardReport r)
        {
            r.Section($"{field} field structure ({scene.path})");
            var roots = scene.GetRootGameObjects();
            Transform Root(string name)
            {
                var found = roots.Where(g => g.name == name).ToArray();
                r.Check(found.Length == 1, $"One root object '{name}' (found {found.Length})");
                return found.Length == 1 ? found[0].transform : null;
            }
            var systems = Root(SystemsName);
            var environment = Root(EnvironmentName);
            var stationsRoot = Root(StationsName);
            var entry = Root(EntryName);
            var exit = Root(ExitName);
            if (systems == null || environment == null || stationsRoot == null || entry == null || exit == null)
                return;
            r.Check(systems.position == Vector3.zero && systems.rotation == Quaternion.identity && systems.lossyScale == Vector3.one,
                $"{SystemsName} at the identity pose (the XR Origin's frame)");
            var others = roots.Where(g => !new[] { SystemsName, EnvironmentName, StationsName, EntryName, ExitName }.Contains(g.name)).Select(g => g.name).ToArray();
            r.Info(others.Length == 0 ? "No other root objects" : "Other root objects (not part of the field structure): " + string.Join(", ", others));

            // XR and input
            r.Section("XR and input (one of each, owned by SceneSystems)");
            var origins = InScene<XROrigin>(scene);
            r.Check(origins.Length == 1, $"One XR Origin in the scene ({origins.Length})");
            var origin = origins.Length == 1 ? origins[0] : null;
            var camera = origin != null ? origin.Camera : null;
            r.Check(origin != null && origin.transform.IsChildOf(systems) && !origin.transform.IsChildOf(stationsRoot),
                "XR Origin under SceneSystems, not under StationsRoot (moving a station never moves the user)");
            r.Check(camera != null && camera.CompareTag("MainCamera"), "XR Origin camera tagged MainCamera (Camera.main of the course markers)");
            // The entry waits for the head's tracking state (position and rotation) before it aligns the user and approves starts.
            var headDriver = camera != null ? camera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>() : null;
            var trackingAction = headDriver != null ? headDriver.trackingStateInput.action : null;
            r.Check(headDriver != null && trackingAction != null && trackingAction.bindings.Count > 0,
                "XR camera TrackedPoseDriver reads a tracking state input (the field entry waits for it before any course can start)");
            var mainCameras = InScene<Camera>(scene).Where(cam => cam.isActiveAndEnabled || cam.enabled).Where(cam => cam.CompareTag("MainCamera")).ToArray();
            r.Check(mainCameras.Length == 1 && mainCameras[0] == camera, $"Only the XR camera is tagged MainCamera ({mainCameras.Length})");
            var listeners = InScene<AudioListener>(scene);
            r.Check(listeners.Length == 1 && camera != null && listeners[0].gameObject == camera.gameObject,
                $"One AudioListener, on the XR camera ({listeners.Length}: {string.Join(", ", listeners.Select(l => l.name))})");
            var others2 = InScene<Camera>(scene).Where(cam => cam != camera && cam.enabled && cam.targetTexture == null && !IsObserverCamera(cam)).ToArray();
            r.Check(others2.Length == 0, "No other camera renders to the display" + (others2.Length > 0 ? ": " + string.Join(", ", others2.Select(x => Path(x.transform))) : ""));
            var managers = InScene<XRInteractionManager>(scene);
            r.Check(managers.Length == 1 && managers[0].transform.IsChildOf(systems), $"One XR Interaction Manager in SceneSystems ({managers.Length})");
            var eventSystems = InScene<EventSystem>(scene);
            r.Check(eventSystems.Length == 1 && eventSystems[0].transform.IsChildOf(systems) && eventSystems[0].GetComponents<BaseInputModule>().Length == 1 &&
                    eventSystems[0].GetComponent<BaseInputModule>() is XRUIInputModule,
                $"One EventSystem in SceneSystems with only XRUIInputModule ({eventSystems.Length})");
            var simulators = roots.SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(m => m != null && m.GetType().Name == "XRInteractionSimulator").ToArray();
            r.Check(simulators.Length <= 1, $"At most one XR Interaction Simulator ({simulators.Length}; editor test input)");
            r.Check(entry.lossyScale == Vector3.one && Mathf.Abs(Vector3.Dot(entry.up, Vector3.up) - 1f) < 1e-4f,
                $"{EntryName} upright with scale 1 (its position and horizontal facing are the start pose)");

            // Raycasters and canvases
            r.Section("Canvases and UI raycasters");
            var canvases = InScene<Canvas>(scene).Where(IsTopCanvas).ToArray();
            var plain = InScene<GraphicRaycaster>(scene);
            r.Check(plain.Length == 0, $"No plain GraphicRaycaster in the scene ({plain.Length}" + (plain.Length > 0 ? ": " + string.Join(", ", plain.Select(x => Path(x.transform))) : "") + ")");
            var badCanvas = canvases.Where(cv => cv.renderMode == RenderMode.WorldSpace && cv.GetComponentsInChildren<Selectable>(true).Length > 0 &&
                                                 (cv.GetComponents<TrackedDeviceGraphicRaycaster>().Length != 1 || cv.worldCamera != camera)).Select(cv => Path(cv.transform)).ToArray();
            r.Check(badCanvas.Length == 0, "Every world canvas with a button: one TrackedDeviceGraphicRaycaster and the XR camera" +
                                          (badCanvas.Length > 0 ? "; wrong: " + string.Join(", ", badCanvas) : ""));

            // Stations
            r.Section("Stations (StationsRoot)");
            var stations = stationsRoot.GetComponentsInChildren<TrainingFieldStation>(true);
            var expected = CoursesOf(field).ToArray();
            r.Check(stations.Length == expected.Length && expected.All(id => stations.Count(s => s.Course == id) == 1),
                $"One station per {field} course: {string.Join(", ", expected)} (found {string.Join(", ", stations.Select(s => s.Course))})");
            r.Check(InScene<TrainingFieldStation>(scene).Length == stations.Length, "No station outside StationsRoot");
            foreach (var station in stations)
                CheckStation(scene, station, stations, systems, environment, r);
            CheckCourseTypes(scene, stations, r);

            // Field operation
            r.Section("Field coordinator and entrance");
            var coordinators = InScene<TrainingFieldCoordinator>(scene);
            r.Check(coordinators.Length == 1 && coordinators[0].transform.IsChildOf(systems), $"One TrainingFieldCoordinator in SceneSystems ({coordinators.Length})");
            if (coordinators.Length == 1)
            {
                var coordinator = coordinators[0];
                string error = null;
                r.Check(coordinator.Field == field && coordinator.TryValidate(out error) && coordinator.Stations.Count == stations.Length && stations.All(s => coordinator.Stations.Contains(s)),
                    $"Coordinator of the {field} field with its {stations.Length} stations" + (error != null ? ": " + error : ""));
                r.Check(RefOrNull(coordinator, "entranceTitleText") is TMP_Text title && title.transform.IsChildOf(entry) && RefOrNull(coordinator, "entranceStationsText") != null,
                    "Entrance board under EntryAnchor, linked to the coordinator");
                r.Check(coordinator.EntryAnchor == entry, $"Coordinator start pose is the {EntryName} root (linked: {(coordinator.EntryAnchor != null ? Path(coordinator.EntryAnchor) : "none")})");
                r.Check(origin != null && coordinator.Origin == origin, "Coordinator moves the scene's one XR Origin at entry" +
                                                                       (coordinator.Origin == null ? " (not linked)" : coordinator.Origin != origin ? " (another object linked)" : ""));
                if (origin != null)
                    r.Info($"Entry: {EntryName} at {entry.position}, facing yaw {entry.eulerAngles.y:F1} deg; the user's head is placed over it once at every scene start " +
                           $"(saved XR Origin pose {origin.transform.position} is not the start pose)");
            }
            CheckHubExit(scene, field, systems, exit, r);
            CheckAudio(scene, field, systems, exit, stations, r);

            // Environment
            r.Section("Environment (replaceable)");
            var teleportAreas = environment.GetComponentsInChildren<TeleportationArea>(true);
            var teleportMask = InteractionLayerMask.GetMask("Teleport");
            r.Check(teleportMask != 0 && teleportAreas.Length > 0 && teleportAreas.All(a => (a.interactionLayers & teleportMask) != 0),
                $"Teleport area(s) on the environment floor with the Teleport interaction layer ({teleportAreas.Length})");
            var envInteractables = environment.GetComponentsInChildren<XRBaseInteractable>(true).Where(i => !(i is BaseTeleportationInteractable)).Select(i => Path(i.transform)).ToArray();
            r.Info(envInteractables.Length == 0 ? "No equipment interactable in EnvironmentRoot" : "Interactables kept in EnvironmentRoot (not course equipment): " + string.Join(", ", envInteractables));
            r.Info("Replacing the background: put the new model and its floor/colliders under EnvironmentRoot (with a TeleportationArea on the walkable floor), " +
                   "then move each StationRoot (position, horizontal rotation). No course reference points into EnvironmentRoot (checked per station).");

            // Text
            r.Section("Field text and font");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TrainingBoardKit.KoreanFontPath);
            if (font == null || font.sourceFontFile == null)
                r.Check(false, "Korean TMP font with source file: " + TrainingBoardKit.KoreanFontPath);
            else
            {
                var missing = TrainingSubtitles.All.Distinct().Where(ch => !char.IsWhiteSpace(ch) && !char.IsControl(ch) && !font.sourceFontFile.HasCharacter(ch)).ToArray();
                r.Check(missing.Length == 0, "Font source has every field text character" + (missing.Length > 0 ? ": missing '" + new string(missing) + "'" : ""));
            }
        }

        static MonoBehaviour[] ScriptsNamed(Scene scene, string fullName) =>
            scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(m => m != null && m.GetType().FullName == fullName).ToArray();

        // Overrides this field's portal instance may have: the instance root pose and name (always recorded by Unity), the root
        // scale, course ID, log name, the hidden name board, the name text its OnValidate writes and the sparkle scaling mode.
        static readonly string[] HubPortalOverrides =
        {
            "m_Name", "m_CourseId", "m_Label", "m_Text", "m_IsActive", "scalingMode",
        };

        static bool AllowedPortalOverride(string path) =>
            HubPortalOverrides.Contains(path) || TransformPoseProperties.Any(p => path.StartsWith(p + ".", StringComparison.Ordinal));

        // Exit: one hub portal (A prefab instance, values overridden only), the wooden sign beside it, the scene's one ScreenFader
        // and no missing script left from the removed exit board. Reads only; nothing is written to the scene or any asset.
        static void CheckHubExit(Scene scene, TrainingFieldKind field, Transform systems, Transform exit, BoardReport r)
        {
            r.Section("Exit: hub portal under ExitAnchor (A prefab instance), its sign and the screen fader");
            var portals = ScriptsNamed(scene, HubPortalType);
            var under = portals.Where(p => p.transform.IsChildOf(exit)).ToArray();
            r.Check(portals.Length == 1 && under.Length == 1,
                $"One HubPortal ({HubPortalType}) in the scene, under {ExitName} ({portals.Length} in the scene, {under.Length} under {ExitName})");
            var portal = under.Length == 1 ? under[0] : null;
            if (portal != null)
            {
                var go = portal.gameObject;
                var source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                r.Check(portal.transform.parent == exit && PrefabUtility.IsOutermostPrefabInstanceRoot(go) &&
                        PrefabUtility.GetPrefabInstanceStatus(go) == PrefabInstanceStatus.Connected && source == HubPortalPrefabPath,
                    $"HubPortal is a connected (not unpacked) instance root of {HubPortalPrefabPath}, directly under {ExitName} (source: {source ?? "none"})");
                var data = new SerializedObject(portal);
                var courseId = data.FindProperty("m_CourseId")?.stringValue;
                r.Check(courseId == HubCourseId(field), $"Portal course ID '{courseId}' is '{HubCourseId(field)}' (the hub stands the user at that course's portal)");
                var scenePath = data.FindProperty("m_ScenePath")?.stringValue;
                r.Check(scenePath == HubScenePath, $"Portal scene path '{scenePath}' is {HubScenePath} ({(File.Exists(HubScenePath) ? "file exists" : "file NOT found")})");
                r.Check(data.FindProperty("m_ReturnPoint")?.objectReferenceValue == null && data.FindProperty("m_Fader")?.objectReferenceValue == null &&
                        data.FindProperty("m_Head")?.objectReferenceValue == null,
                    "Portal return point, fader and head left empty (the fader and Camera.main are found in the scene)");
                var label = portal.transform.Find(HubPortalLabelName);
                r.Check(label != null && !label.gameObject.activeSelf, $"Portal's own name board '{HubPortalLabelName}' switched off in this instance");
                var children = Enumerable.Range(0, portal.transform.childCount).Select(i => portal.transform.GetChild(i).name).ToArray();
                var added = PrefabUtility.GetAddedComponents(go).Count + PrefabUtility.GetRemovedComponents(go).Count + PrefabUtility.GetAddedGameObjects(go).Count;
                r.Check(added == 0 && children.Length == HubPortalChildren.Length && HubPortalChildren.All(children.Contains),
                    $"No object or component added to or removed from the portal instance (children: {string.Join(", ", children)}; added/removed {added})");
                var modifications = PrefabUtility.GetPropertyModifications(go) ?? Array.Empty<PropertyModification>();
                var unexpected = modifications.Select(m => m.propertyPath).Where(p => !AllowedPortalOverride(p)).Distinct().ToArray();
                r.Check(unexpected.Length == 0, "Portal overrides are only the root pose/scale, name, course ID, log name, hidden name board, its text and the sparkle scaling" +
                                                (unexpected.Length > 0 ? "; unexpected: " + string.Join(", ", unexpected) : ""));
                var sparkles = portal.GetComponentsInChildren<ParticleSystem>(true);
                r.Check(sparkles.Length == 1 && sparkles[0].main.scalingMode == ParticleSystemScalingMode.Hierarchy,
                    $"Portal sparkles follow the root scale (scaling mode {(sparkles.Length == 1 ? sparkles[0].main.scalingMode.ToString() : "?")})");
                var t = portal.transform;
                var enterCenter = data.FindProperty("m_EnterCenter")?.vector3Value ?? Vector3.zero;
                var enterSize = data.FindProperty("m_EnterSize")?.vector3Value ?? Vector3.zero;
                var s = t.lossyScale.y;
                r.Info($"Portal at {t.position}, yaw {t.eulerAngles.y:F1} deg, scale {t.lossyScale}; walk-in box height {t.position.y + s * (enterCenter.y - enterSize.y * 0.5f):F3}~" +
                       $"{t.position.y + s * (enterCenter.y + enterSize.y * 0.5f):F3} m, width {s * enterSize.x:F3} m, depth {s * enterSize.z:F3} m (centre {t.TransformPoint(enterCenter)})");
                var lens = t.Find("Lens");
                var lensBox = lens != null ? lens.GetComponent<BoxCollider>() : null;
                if (lensBox != null)
                    r.Info($"Lens collider (ray select) world bounds {lensBox.bounds.min} ~ {lensBox.bounds.max}, layer {LayerMask.LayerToName(lens.gameObject.layer)}, trigger {lensBox.isTrigger}");
                var interactable = portal.GetComponent<XRSimpleInteractable>();
                if (interactable != null)
                    r.Info($"Portal XRSimpleInteractable interaction layers {interactable.interactionLayers.value} (ray hands select it on the same layers)");
            }

            // Wooden sign: a B plaque beside the portal, a sibling under ExitAnchor (not inside the A instance).
            var signs = exit.GetComponentsInChildren<TMP_Text>(true).Where(x => portal == null || !x.transform.IsChildOf(portal.transform)).ToArray();
            var sign = signs.Length == 1 ? signs[0] : null;
            var plaque = sign != null ? sign.transform.parent : null;
            r.Check(sign != null && sign.name == HubSignName && plaque != null && plaque.name == HubPlaqueName && plaque.parent == exit,
                $"One sign text '{HubSignName}' in '{HubPlaqueName}' directly under {ExitName}, beside the portal ({signs.Length} text(s) under {ExitName} outside the portal)");
            if (sign != null)
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TrainingBoardKit.KoreanFontPath);
                r.Check(sign.text == TrainingSubtitles.HubPortalSign && sign.font == font && (sign.fontStyle & FontStyles.Bold) != 0 && sign.color == Color.black,
                    $"Sign text '{sign.text}' is '{TrainingSubtitles.HubPortalSign}', Korean font, black bold ({sign.fontSize:F2})");
                var visual = plaque != null
                    ? Enumerable.Range(0, plaque.childCount).Select(i => plaque.GetChild(i)).FirstOrDefault(c => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(c.gameObject) == SignVisualPrefabPath)
                    : null;
                var board = visual != null ? visual.GetComponentInChildren<MeshRenderer>(true) : null;
                r.Check(board != null && board.sharedMaterial != null && AssetDatabase.GetAssetPath(board.sharedMaterial) == SignMaterialPath,
                    $"Sign board is an instance of {System.IO.Path.GetFileName(SignVisualPrefabPath)} with {System.IO.Path.GetFileName(SignMaterialPath)}");
                if (plaque != null)
                    r.Info($"Sign at {plaque.position}, yaw {plaque.eulerAngles.y:F1} deg (text faces its local -Z)");
            }

            var faders = ScriptsNamed(scene, ScreenFaderType);
            var fadeMaterial = faders.Length == 1 ? new SerializedObject(faders[0]).FindProperty("m_Material")?.objectReferenceValue : null;
            r.Check(faders.Length == 1 && fadeMaterial != null && AssetDatabase.GetAssetPath(fadeMaterial) == ScreenFadeMaterialPath &&
                    faders[0].transform.IsChildOf(systems) && !PrefabUtility.IsPartOfPrefabInstance(faders[0]),
                $"One ScreenFader ({ScreenFaderType}) in the scene, on a B object in {SystemsName}, with {ScreenFadeMaterialPath} ({faders.Length})");

            var missingScripts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Where(x => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(x.gameObject) > 0).Select(Path).ToArray();
            r.Check(missingScripts.Length == 0, "No missing script in the scene" + (missingScripts.Length > 0 ? ": " + string.Join(", ", missingScripts.Take(20)) : ""));

            var buildScenes = EditorBuildSettings.scenes;
            var registered = new[] { scene.path, HubScenePath }.Select(p => (p, buildScenes.Any(b => b.enabled && b.path == p))).ToArray();
            r.Info("Build scene list (not changed by this menu; ProjectSettings is outside Assets/B): " +
                   string.Join(", ", registered.Select(x => $"{x.p} {(x.Item2 ? "registered" : "NOT registered")}")) +
                   ". In a build the portal does not move to the hub while A_Hub is not in the scene list; in the editor it moves anyway. " +
                   "With a Build Profile that has its own scene list, check that list too.");
        }

        static bool IsObserverCamera(Camera camera) =>
            camera.GetComponentInParent<K1ObserverView>(true) != null || camera.GetComponentInParent<ProtectiveClothingMirror>(true) != null || camera.targetTexture != null;

        static void CheckStation(Scene scene, TrainingFieldStation station, TrainingFieldStation[] all, Transform systems, Transform environment, BoardReport r)
        {
            var name = station.name;
            string error = null;
            r.Check(station.TryValidate(out error), $"{name}: station references (course link, content, anchors, display controls, stop confirm in the first blocker, blockers)" + (error != null ? ": " + error : ""));
            var t = station.transform;
            r.Check(Mathf.Abs(Vector3.Dot(t.up, Vector3.up) - 1f) < 1e-4f, $"{name}: StationRoot rotated about the vertical axis only (up {t.up})");
            r.Check((t.lossyScale - Vector3.one).sqrMagnitude < 1e-6f, $"{name}: StationRoot scale 1 ({t.lossyScale})");
            r.Info($"{name}: StationRoot at {t.position}, yaw {t.eulerAngles.y:F1} deg");
            var content = station.Content;
            if (content == null)
                return;

            // References of the course objects: inside this station, or the field's XR Origin / input systems; nothing in another
            // station or in EnvironmentRoot; no missing reference.
            var foreign = new List<string>();
            var missingRefs = new List<string>();
            foreach (var component in content.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform)
                    continue;
                var data = new SerializedObject(component);
                var it = data.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference)
                        continue;
                    var value = it.objectReferenceValue;
                    if (value == null)
                    {
                        if (it.objectReferenceInstanceIDValue != 0)
                            missingRefs.Add($"{Path(component.transform)} {component.GetType().Name}.{it.propertyPath}");
                        continue;
                    }
                    var go = value as GameObject ?? (value as Component)?.gameObject;
                    if (go == null || !go.scene.IsValid())
                        continue;
                    var target = go.transform;
                    if (target.IsChildOf(t) || target.IsChildOf(systems))
                        continue;
                    var where = target.IsChildOf(environment) ? EnvironmentName : all.FirstOrDefault(s => s != station && target.IsChildOf(s.transform))?.name ?? "outside the stations";
                    foreign.Add($"{Path(component.transform)} {component.GetType().Name}.{it.propertyPath} -> {Path(target)} ({where})");
                }
            }
            r.Check(foreign.Count == 0, $"{name}: course references stay in this station or the field's XR Origin and input systems" +
                                        (foreign.Count > 0 ? ":\n      " + string.Join("\n      ", foreign.Take(20)) : ""));
            r.Check(missingRefs.Count == 0, $"{name}: no missing (broken) reference in the course objects" +
                                            (missingRefs.Count > 0 ? ":\n      " + string.Join("\n      ", missingRefs.Take(20)) : ""));

            // Equipment and its restore parents: every course interactable starts under Content, so its course stores its start
            // pose in a parent of this station when it wakes (the restore point moves with the station).
            var interactables = content.GetComponentsInChildren<XRBaseInteractable>(true);
            r.Check(interactables.Length > 0, $"{name}: course interactables under Content ({interactables.Length})");
            var area = station.LocalArea;
            var outside = interactables.Where(i => !area.Contains(t.InverseTransformPoint(i.transform.position))).Select(i => i.name).ToArray();
            r.Info($"{name}: practice range centre {area.center}, size {area.size}" + (outside.Length > 0 ? $"; interactables starting outside it: {string.Join(", ", outside)}" : "; every interactable starts inside it"));

            // Floor of the station and the course's user body on it.
            var floor = station.FloorAnchor;
            r.Check(floor != null && floor.parent == t && !floor.IsChildOf(content) && floor.name == FloorName,
                $"{name}: {FloorName} directly under the StationRoot (not in Content, not from the background)");
            var bodyComponents = content.GetComponentsInChildren<K1UserAvatar>(true).Cast<Component>()
                .Concat(content.GetComponentsInChildren<ProtectiveClothingUserBody>(true)).ToArray();
            foreach (var body in bodyComponents)
            {
                var reference = RefOrNull(body, "floorReference") as Transform;
                var used = body is K1UserAvatar avatar ? avatar.FloorY : ((ProtectiveClothingUserBody)body).FloorY;
                r.Check(floor != null && reference == floor,
                    $"{name}: {body.GetType().Name} stands on this station's {FloorName} (linked: {(reference != null ? Path(reference) : "none, it would use floorHeight " + used.ToString("F3") + " m in world space")})");
                if (floor != null)
                    r.Info($"{name}: floor used by {body.GetType().Name}: world height {used:F3} m ({FloorName} {floor.localPosition.y:F3} m above the StationRoot at {t.position.y:F3} m)");
            }

            // Start approval: the course's start entry point asks this station.
            Component startEntry = null;
            try
            {
                startEntry = StartEntry(station.Course, content);
            }
            catch (InvalidOperationException e)
            {
                r.Check(false, $"{name}: course start entry point: {e.Message}");
            }
            if (startEntry != null)
                r.Check(RefOrNull(startEntry, "startGate") == station,
                    $"{name}: {startEntry.GetType().Name} asks this station before it starts (one course at a time)");

            var practice = station.PracticeAnchor;
            var teleportMask = InteractionLayerMask.GetMask("Teleport");
            var anchor = practice != null ? practice.GetComponent<TeleportationAnchor>() : null;
            r.Check(anchor != null && anchor.teleportAnchorTransform == practice && (anchor.interactionLayers & teleportMask) != 0 &&
                    practice.GetComponentsInChildren<Collider>(true).Length > 0,
                $"{name}: PracticeAnchor is a teleport anchor (Teleport layer, its own collider, facing the course)");

            // Blockers aligned with the course boards.
            var boards = CourseBoards(content);
            var blockers = station.Blockers;
            var aligned = boards.Length == blockers.Count && boards.Select((b, i) => (b, i)).All(x =>
            {
                var (b, i) = x;
                var blocker = blockers[i] != null && blockers[i].name == BlockerName(i, b) ? blockers[i] : null;
                if (blocker == null)
                    return false;
                var bt = blocker.transform;
                var bb = b.transform;
                return Vector3.Distance(bt.position, bb.position - bb.forward * 0.004f) < 0.002f && Quaternion.Angle(bt.rotation, bb.rotation) < 0.5f &&
                       blocker.GetComponent<Canvas>().sortingOrder == b.sortingOrder && !blocker.activeSelf;
            });
            r.Check(aligned, $"{name}: one hidden blocker in front of each course board, same sorting order ({boards.Length} board(s): {string.Join(", ", boards.Select(b => b.name))})");
            CheckPresentation(station, content, r);

            // Observer views
            var cameras = content.GetComponentsInChildren<Camera>(true);
            var bodies = content.GetComponentsInChildren<K1UserAvatar>(true).Length + content.GetComponentsInChildren<ProtectiveClothingUserBody>(true).Length;
            r.Check(station.ObserverCameras.Count == cameras.Length && cameras.All(cam => station.ObserverCameras.Contains(cam)) && (bodies == 0) == (station.UserBodyRenderers.Count == 0),
                $"{name}: observer cameras ({cameras.Length}) and user body renderers ({station.UserBodyRenderers.Count}) listed for the mirror exclusion");

            // Marker overlay
            foreach (var sourcePath in MarkerMaterials)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
                if (source == null)
                    continue;
                var left = content.GetComponentsInChildren<Renderer>(true).Count(rd => rd.sharedMaterials.Contains(source));
                var overlay = AssetDatabase.LoadAssetAtPath<Material>(OverlayPathOf(sourcePath));
                var used = overlay != null ? content.GetComponentsInChildren<Renderer>(true).Count(rd => rd.sharedMaterials.Contains(overlay)) : 0;
                if (left > 0 || used > 0)
                    r.Check(left == 0 && overlay != null && overlay.shader != null && overlay.shader.name == "B/Common/StatusMarkerOverlay",
                        $"{name}: {used} guide marker renderer(s) use the shared StatusMarkerOverlay copy of {System.IO.Path.GetFileName(sourcePath)} ({left} still on the original)");
            }
            var kd1 = content.GetComponentsInChildren<Renderer>(true).SelectMany(rd => rd.sharedMaterials).Where(m => m != null && m.name.StartsWith("KD1_Zone")).Distinct().ToArray();
            if (kd1.Length > 0)
                r.Check(kd1.All(m => m.shader != null && m.shader.name == "B/Common/StatusMarkerOverlay"), $"{name}: KD-1 zone markers already use the shared overlay shader ({kd1.Length} material(s))");
            CheckMarkerStyle(station, content, r);
        }

        // ---------- Spatial guide marker style ----------
        // Display-only markers: on the overlay shader (lines and meshes) or the UI overlay shader (AED Images), in the action
        // cyan or their state colour, line widths by role, white opaque line vertex colours, and no collider, rigidbody,
        // interactable or raycaster. Excluded on purpose (not checked here): equipment and patient models, placement previews and
        // allowed/blocked zones, the real wound look, and the AED lamp, hotspots, labels and input blockers (only checked to stay
        // off the UI overlay material).
        static void CheckMarkerStyle(TrainingFieldStation station, Transform content, BoardReport r)
        {
            var name = station.name;
            var problems = new List<string>();
            var count = 0;
            void Line(LineRenderer line, Color colour, float width, int sorting = -1)
            {
                count++;
                if (LineProblem(line, colour, width, sorting) is string p)
                    problems.Add(p);
            }
            void MeshMarker(Renderer renderer, Color colour)
            {
                count++;
                if (MeshProblem(renderer, colour) is string p)
                    problems.Add(p);
            }
            void Missing(string what) => problems.Add($"{what} not found");

            string roles;
            switch (station.Course)
            {
                case TrainingFieldCourseId.Aed:
                {
                    var guides = new List<Graphic>();
                    void GuideImage(Graphic g, Color colour, string what)
                    {
                        if (g == null)
                        {
                            Missing(what);
                            return;
                        }
                        count++;
                        guides.Add(g);
                        if (ImageProblem(g, colour) is string p)
                            problems.Add(p);
                    }
                    var pads = content.GetComponentsInChildren<AedPad>(true);
                    if (pads.Length == 0)
                        Missing("AedPad");
                    foreach (var pad in pads)
                        GuideImage((RefOrNull(pad, "highlight") as GameObject)?.GetComponent<Graphic>(), ActionColour, $"{pad.name} highlight Image");
                    var controller = content.GetComponentsInChildren<AedTutorialStartController>(true).FirstOrDefault();
                    GuideImage((controller != null ? RefOrNull(controller, "powerButtonHighlight") as GameObject : null)?.GetComponent<Graphic>(), ActionColour,
                        "power button highlight Image");
                    var shock = content.GetComponentsInChildren<AedShockDevice>(true).FirstOrDefault();
                    GuideImage(shock != null ? shock.Highlight : null, ShockColour, "shock button highlight");
                    var anchors = content.GetComponentsInChildren<PadAttachAnchor>(true);
                    if (anchors.Length == 0)
                        Missing("PadAttachAnchor");
                    foreach (var anchor in anchors)
                    {
                        var marker = anchor.Marker != null ? anchor.Marker.transform : null;
                        var ready = RefOrNull(anchor, "readyIndicator") as GameObject;
                        GuideImage(marker != null ? marker.Find("Outline")?.GetComponent<Graphic>() : null, ActionColour, $"{anchor.name} Marker/Outline");
                        var fill = ActionColour;
                        fill.a = FillAlpha;
                        GuideImage(marker != null ? marker.Find("Fill")?.GetComponent<Graphic>() : null, fill, $"{anchor.name} Marker/Fill");
                        GuideImage(ready != null ? ready.transform.Find("ReadyOutline")?.GetComponent<Graphic>() : null, AttachReadyColour,
                            $"{anchor.name} ReadyIndicator/ReadyOutline");
                    }
                    var others = content.GetComponentsInChildren<Graphic>(true).Where(g => !guides.Contains(g) && UiMaterial(g) is Material m && m.shader != null &&
                                                                                            m.shader.name == UiOverlayShaderName).ToArray();
                    r.Check(others.Length == 0, $"{name}: AED lamp, hotspots, labels and blockers keep their own materials (not the guide overlay)" +
                                                (others.Length > 0 ? ": " + string.Join(", ", others.Select(g => Path(g.transform))) : ""));
                    roles = "pad and power highlight, attach outline cyan; attach fill cyan a " + FillAlpha + "; ready outline green; shock orange-red";
                    break;
                }
                case TrainingFieldCourseId.Tourniquet:
                {
                    var markers = content.GetComponentsInChildren<TourniquetGuideMarker>(true);
                    if (markers.Length == 0)
                        Missing("TourniquetGuideMarker");
                    foreach (var m in markers)
                        Line(m.GetComponent<LineRenderer>(), ActionColour, HandleRingWidth);
                    roles = $"handling rings {HandleRingWidth * 1000:F0} mm";
                    break;
                }
                case TrainingFieldCourseId.PressureBandage:
                {
                    var course = content.GetComponentsInChildren<PressureBandageCourseMarker>(true);
                    if (course.Length == 0)
                        Missing("PressureBandageCourseMarker");
                    foreach (var m in course)
                        Line(m.GetComponent<LineRenderer>(), ActionColour, HandleRingWidth);
                    var guide = content.GetComponentsInChildren<MilTrainSimVR.B.BleedingControl.PressureBandage.PressureBandageGuide>(true).FirstOrDefault();
                    if (guide == null)
                        Missing("PressureBandageGuide");
                    else
                        foreach (var field in new[] { "ring", "tapeFrame", "zoneOutline" })
                        {
                            var filter = RefOrNull(guide, field) as MeshFilter;
                            if (filter == null || filter.GetComponent<MeshRenderer>() == null)
                                Missing($"PressureBandageGuide {field} mesh renderer");
                            else
                                MeshMarker(filter.GetComponent<MeshRenderer>(), ActionColour);
                        }
                    var training = content.GetComponentsInChildren<PressureBandageTrainingGuide>(true).FirstOrDefault();
                    var wound = training != null ? RefOrNull(training, "woundGuide") as Transform : null;
                    var outline = training != null ? RefOrNull(training, "woundOutline") as LineRenderer : null;
                    if (wound == null || outline == null)
                        Missing("wound arrow and outline");
                    else
                        foreach (var line in wound.GetComponentsInChildren<LineRenderer>(true))
                            Line(line, ActionColour, line == outline ? WoundOutlineWidth : ArrowWidth);
                    roles = $"course ring {HandleRingWidth * 1000:F0} mm; end target, tape frame and wrap zone meshes; wound arrow " +
                            $"{ArrowWidth * 1000:F0} mm, wound outline {WoundOutlineWidth * 1000:F0} mm";
                    break;
                }
                case TrainingFieldCourseId.K1GasMask:
                {
                    var markers = content.GetComponentsInChildren<K1GuideMarker>(true);
                    if (markers.Length == 0)
                        Missing("K1GuideMarker");
                    foreach (var m in markers)
                    {
                        var chevron = new SerializedObject(m).FindProperty("shape")?.enumValueIndex == (int)K1GuideMarker.Shape.UpChevron;
                        Line(m.GetComponent<LineRenderer>(), ActionColour, chevron ? ArrowWidth : HandleRingWidth);
                    }
                    roles = $"handling rings {HandleRingWidth * 1000:F0} mm, extraction chevron {ArrowWidth * 1000:F0} mm";
                    break;
                }
                case TrainingFieldCourseId.ProtectiveClothing:
                {
                    var training = content.GetComponentsInChildren<ProtectiveClothingTrainingGuide>(true).FirstOrDefault();
                    var equipment = content.GetComponentsInChildren<ProtectiveClothingGuide>(true).FirstOrDefault();
                    if (training == null || training.Marker == null)
                        Missing("course wear ring");
                    else
                        Line(training.Marker, ActionColour, BodyRingWidth);
                    if (equipment == null || equipment.Marker == null)
                        Missing("equipment wear ring");
                    else
                        Line(equipment.Marker, ActionColour, BodyRingWidth);
                    var pickup = training != null ? RefOrNull(training, "pickupMarker") as Transform : null;
                    var arrows = pickup != null ? pickup.GetComponentsInChildren<LineRenderer>(true) : Array.Empty<LineRenderer>();
                    if (arrows.Length == 0)
                        Missing("pickup arrow");
                    foreach (var line in arrows)
                        Line(line, ActionColour, ArrowWidth);
                    var label = training != null ? RefOrNull(training, "pickupLabel") as TMP_Text : null;
                    if (label == null)
                        Missing("pickup name line");
                    else
                    {
                        count++;
                        var m = label.fontSharedMaterial;
                        var atlas = label.font != null ? label.font.atlasTexture : null;
                        if (m == null || m.shader == null || m.shader.name != TextOverlayShaderName || atlas == null || m.mainTexture != atlas)
                            problems.Add($"{Path(label.transform)}: material {(m != null ? m.name + " / " + (m.shader != null ? m.shader.name : "no shader") : "none")}, " +
                                         $"expected {TextOverlayShaderName} on the font's atlas");
                        else if (!Near(label.color, ActionColour))
                            problems.Add($"{Path(label.transform)}: colour {Describe(label.color)}, expected {Describe(ActionColour)}");
                    }
                    roles = $"wear rings {BodyRingWidth * 1000:F0} mm (radius from the body zones at run time), pickup arrow {ArrowWidth * 1000:F0} mm, name line on the text overlay";
                    break;
                }
                case TrainingFieldCourseId.KD1:
                {
                    var lines = content.GetComponentsInChildren<LineRenderer>(true)
                        .Where(l => l.sharedMaterial != null && (l.sharedMaterial.name.StartsWith("KD1_Zone") || l.sharedMaterial.name.StartsWith("MAT_Field_KD1_Zone"))).ToArray();
                    if (lines.Length == 0)
                        Missing("KD-1 zone lines");
                    foreach (var line in lines)
                    {
                        var done = line.transform.GetComponentsInParent<Transform>(true).Any(t => t.name == "Done");
                        var colour = done ? DoneColour : ActionColour;
                        if (line.name == "Fill")
                        {
                            colour.a = FillAlpha;
                            Line(line, colour, -1f, 0);
                            continue;
                        }
                        var group = line.transform.parent != null ? line.transform.parent.name : "";
                        Line(line, colour, group == "Number" ? ZoneNumberWidth : group == "Check" ? ZoneCheckWidth : ZoneOutlineWidth, 1);
                    }
                    roles = $"not done cyan, done green; fills a {FillAlpha} (order 0); outline {ZoneOutlineWidth * 1000:F0} mm, number " +
                            $"{ZoneNumberWidth * 1000:F0} mm, check {ZoneCheckWidth * 1000:F0} mm (order 1)";
                    break;
                }
                default:
                    return;
            }
            r.Check(problems.Count == 0, $"{name}: {count} display-only guide marker(s) on the overlay in the shared style ({roles})" +
                                         (problems.Count > 0 ? ":\n      " + string.Join("\n      ", problems) : ""));
        }

        static bool Near(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 2e-3f && Mathf.Abs(a.g - b.g) < 2e-3f && Mathf.Abs(a.b - b.b) < 2e-3f && Mathf.Abs(a.a - b.a) < 2e-3f;

        static string Describe(Color c) => $"({c.r:F2}, {c.g:F2}, {c.b:F2}, {c.a:F2})";

        static string MaterialName(Material m) => m != null ? $"{m.name} / {(m.shader != null ? m.shader.name : "no shader")}" : "none";

        // Collider, rigidbody, interactable or raycaster on a display-only marker (it must never take rays or grabs).
        static string InputParts(GameObject marker)
        {
            var parts = marker.GetComponentsInChildren<Component>(true)
                .Where(c => c is Collider || c is Rigidbody || c is XRBaseInteractable || c is BaseRaycaster || c is Selectable)
                .Select(c => c.GetType().Name).Distinct().ToArray();
            return parts.Length > 0 ? $"{Path(marker.transform)}: has {string.Join(", ", parts)}" : null;
        }

        // Line marker: overlay shader, white opaque vertex colours and width curve 1 (so the material colour and widthMultiplier are
        // what is drawn), the expected colour, width (negative: not checked) and sorting order (negative: not checked).
        static string LineProblem(LineRenderer line, Color colour, float width, int sorting)
        {
            if (line == null)
                return "a marker without its LineRenderer";
            var at = Path(line.transform);
            var m = line.sharedMaterial;
            if (m == null || m.shader == null || m.shader.name != OverlayShaderName)
                return $"{at}: material {MaterialName(m)}, expected {OverlayShaderName}";
            var vertex = line.colorGradient;
            if (vertex.colorKeys.Any(k => !Near(k.color, Color.white)) || vertex.alphaKeys.Any(k => Mathf.Abs(k.alpha - 1f) > 2e-3f))
                return $"{at}: vertex colours are not white and opaque (they tint the material colour)";
            var drawn = m.GetColor("_BaseColor");
            if (!Near(drawn, colour))
                return $"{at}: colour {Describe(drawn)} ({m.name}), expected {Describe(colour)}";
            if (line.widthCurve.keys.Any(k => Mathf.Abs(k.value - 1f) > 1e-4f))
                return $"{at}: width curve is not 1 along the line";
            if (width >= 0f && Mathf.Abs(line.widthMultiplier - width) > 1e-5f)
                return $"{at}: width {line.widthMultiplier * 1000f:F1} mm, expected {width * 1000f:F1} mm";
            if (sorting >= 0 && line.sortingOrder != sorting)
                return $"{at}: sorting order {line.sortingOrder}, expected {sorting}";
            return InputParts(line.gameObject);
        }

        // Generated mesh marker (PB end target, tape frame, wrap zone): overlay shader and colour on every material slot.
        static string MeshProblem(Renderer renderer, Color colour)
        {
            var at = Path(renderer.transform);
            var materials = renderer.sharedMaterials;
            if (materials.Length == 0 || materials.Any(m => m == null || m.shader == null || m.shader.name != OverlayShaderName))
                return $"{at}: material(s) {string.Join(", ", materials.Select(MaterialName))}, expected {OverlayShaderName}";
            var off = materials.FirstOrDefault(m => !Near(m.GetColor("_BaseColor"), colour));
            if (off != null)
                return $"{at}: colour {Describe(off.GetColor("_BaseColor"))} ({off.name}), expected {Describe(colour)}";
            return InputParts(renderer.gameObject);
        }

        static Material UiMaterial(Graphic g) => RefOrNull(g, "m_Material") as Material;

        // AED guide Image: UI overlay material with a white tint (the Image colour is the drawn colour), raycast target off.
        static string ImageProblem(Graphic g, Color colour)
        {
            var at = Path(g.transform);
            var m = UiMaterial(g);
            if (m == null || m.shader == null || m.shader.name != UiOverlayShaderName)
                return $"{at}: material {MaterialName(m)}, expected {UiOverlayShaderName}";
            if (m.HasProperty("_Color") && !Near(m.GetColor("_Color"), Color.white))
                return $"{at}: {m.name} tint {Describe(m.GetColor("_Color"))} is not white (it would tint the Image colour)";
            if (!Near(g.color, colour))
                return $"{at}: colour {Describe(g.color)}, expected {Describe(colour)}";
            if (g.raycastTarget)
                return $"{at}: raycast target on (a guide must not take UI rays)";
            return InputParts(g.gameObject);
        }

        static void CheckCourseTypes(Scene scene, TrainingFieldStation[] stations, BoardReport r)
        {
            void One<T>(TrainingFieldCourseId id) where T : Component
            {
                var all = InScene<T>(scene);
                var station = stations.FirstOrDefault(s => s.Course == id);
                r.Check(station != null && all.Length == 1 && all[0].transform.IsChildOf(station.transform),
                    $"One {typeof(T).Name}, inside the {id} station ({all.Length})");
            }
            foreach (var station in stations)
            {
                switch (station.Course)
                {
                    case TrainingFieldCourseId.Aed: One<AedTutorialStartController>(station.Course); break;
                    case TrainingFieldCourseId.Tourniquet: One<TourniquetTrainingLink>(station.Course); break;
                    case TrainingFieldCourseId.PressureBandage: One<PressureBandageTrainingLink>(station.Course); break;
                    case TrainingFieldCourseId.K1GasMask: One<K1TrainingLink>(station.Course); break;
                    case TrainingFieldCourseId.ProtectiveClothing: One<ProtectiveClothingTrainingLink>(station.Course); break;
                    case TrainingFieldCourseId.KD1: One<KD1TrainingCourse>(station.Course); break;
                }
            }
            var runners = InScene<TrainingRunner>(scene);
            var outsideRunners = runners.Where(x => !stations.Any(s => x.transform.IsChildOf(s.transform))).ToArray();
            r.Check(outsideRunners.Length == 0, $"Every TrainingRunner inside a station ({runners.Length} runner(s))");
            r.Check(InScene<MilTrainSimVR.B.Decontamination.KD1.KD1Guide>(scene).Length <= 1 && InScene<ProtectiveClothingGuide>(scene).Length <= 1,
                "Course global searches (KD1Guide, ProtectiveClothingGuide) find at most their own course's object");
        }

        // ---------- Training sounds (read-only check) ----------

        const string SfxDir = "Assets/B/Audio/TrainingSfx";
        const string ClickClipPath = SfxDir + "/UI_Click_v1.wav";
        const string CompleteClipPath = SfxDir + "/TrainingComplete_v1.wav";
        const string WrapClipPath = SfxDir + "/BandageWrap_Loop_v2.wav";

        // SHA-256 of the delivered clips (click/completion v1, bandage wrap v2 +6 dB): the copies are used unchanged.
        static readonly (string path, string sha256, float seconds)[] SfxClips =
        {
            (ClickClipPath, "247040181673a3e8355329062c4fe1031a292c0cde049b8bb196240c79b60faf", 0.11f),
            (CompleteClipPath, "73db07a5c72348f517ef9fd027ffa92cd608e595c11d3c42781df9ad4ae21196", 0.78f),
            (WrapClipPath, "b9e3e593141a9ea18607741e9b5e10ec8badc554b384b09000337a44678d36a4", 2.6f),
        };

        // Training UI buttons that click (component type name, its button fields): start, step / final check, stop, continue,
        // abort, retry (KD-1 also its restart), end. Every other button of the field stays silent.
        static readonly (string type, string[] fields)[] ClickButtonFields =
        {
            (nameof(TrainingFieldStation), new[] { "stopButton", "endButton", "continueButton", "abortButton" }),
            ("AedTutorialStartController", new[] { "startButton", "retryButton" }),
            ("TourniquetStartScreen", new[] { "startButton" }),
            ("TourniquetFinalCheck", new[] { "confirmButton" }),
            ("TourniquetCompletionPanel", new[] { "retryButton" }),
            ("PressureBandageStartScreen", new[] { "startButton" }),
            ("PressureBandageTrainingGuide", new[] { "checkButton" }),
            ("PressureBandageCompletionPanel", new[] { "retryButton" }),
            ("K1TrainingGuide", new[] { "startButton", "retryButton" }),
            ("ProtectiveClothingStartScreen", new[] { "startButton" }),
            ("ProtectiveClothingCompletionPanel", new[] { "retryButton" }),
            ("KD1TrainingBoard", new[] { "startButton", "restartButton", "retryButton" }),
        };

        // Buttons that must not click: AED device hotspots and the equipment boards' own reset (not training UI).
        static readonly (string type, string field)[] SilentButtonFields =
        {
            ("AedPowerDevice", "powerButton"),
            ("AedShockDevice", "shockButton"),
            ("ProtectiveClothingGuide", "resetButton"),
            ("KD1Guide", "resetButton"),
        };

        static Button[] ButtonsOf(Scene scene, string type, IEnumerable<string> fields) =>
            ScriptsByName(scene, type).SelectMany(m => fields.Select(f => RefOrNull(m, f) as Button)).Where(b => b != null).ToArray();

        static MonoBehaviour[] ScriptsByName(Scene scene, string name) =>
            scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(m => m != null && m.GetType().Name == name).ToArray();

        static AudioClip SfxClip(string path, float seconds, string sha256, BoardReport r)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (clip == null || importer == null)
            {
                r.Check(false, $"Sound {path} imported as an AudioClip");
                return null;
            }
            string hash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(path))
                hash = string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
            r.Check(hash == sha256, $"{path}: file is the delivered clip, unchanged (SHA-256 {hash})");
            var settings = importer.defaultSampleSettings;
            var normalize = new SerializedObject(importer).FindProperty("normalize");
            r.Check(settings.loadType == AudioClipLoadType.DecompressOnLoad && settings.compressionFormat == AudioCompressionFormat.PCM &&
                    settings.sampleRateSetting == AudioSampleRateSetting.PreserveSampleRate && settings.preloadAudioData && !importer.forceToMono &&
                    normalize != null && !normalize.boolValue && !importer.loadInBackground && !importer.ambisonic &&
                    !importer.ContainsSampleSettingsOverride("Standalone") && !importer.ContainsSampleSettingsOverride("Android"),
                $"{path}: Decompress On Load, PCM, sample rate kept, preloaded, no force to mono, no normalize, no platform override " +
                $"(now {settings.loadType}, {settings.compressionFormat}, {settings.sampleRateSetting}, preload {settings.preloadAudioData}, " +
                $"mono mix {importer.forceToMono}, normalize {(normalize != null ? normalize.boolValue.ToString() : "?")})");
            r.Check(clip.channels == 1 && clip.frequency == 48000 && Mathf.Abs(clip.length - seconds) < 0.005f,
                $"{path}: mono 48 kHz, {seconds:F2} s (now {clip.channels} ch, {clip.frequency} Hz, {clip.length:F3} s)");
            return clip;
        }

        // UI click and completion player in SceneSystems (2D), the bandage loop on the pressure bandage station (3D), no other
        // B audio source; the clicking buttons are exactly the training UI buttons. Reads only.
        static void CheckAudio(Scene scene, TrainingFieldKind field, Transform systems, Transform exit, TrainingFieldStation[] stations, BoardReport r)
        {
            r.Section("Training sounds (UI click, course completion, bandage wrap loop)");
            var click = SfxClip(ClickClipPath, SfxClips[0].seconds, SfxClips[0].sha256, r);
            var complete = SfxClip(CompleteClipPath, SfxClips[1].seconds, SfxClips[1].sha256, r);
            var wrap = field == TrainingFieldKind.FirstAid ? SfxClip(WrapClipPath, SfxClips[2].seconds, SfxClips[2].sha256, r) : null;

            var coordinators = InScene<TrainingFieldCoordinator>(scene);
            var players = InScene<TrainingFieldSfx>(scene);
            r.Check(players.Length == 1 && players[0].transform.parent == systems, $"One TrainingFieldSfx, directly under {SystemsName} ({players.Length})");
            var player = players.Length == 1 ? players[0] : null;
            var loops = InScene<BandageWrapSfx>(scene);
            var pbStation = stations.FirstOrDefault(s => s.Course == TrainingFieldCourseId.PressureBandage);
            r.Check(loops.Length == (pbStation != null ? 1 : 0), $"BandageWrapSfx only on the pressure bandage station ({loops.Length})");
            var loop = loops.Length == 1 ? loops[0] : null;

            var expectedSources = new List<AudioSource>();
            if (player != null)
            {
                var source = player.Source;
                r.Check(coordinators.Length == 1 && player.Coordinator == coordinators[0], "Sound player listens to the scene's coordinator (course completion edge)");
                r.Check(click != null && player.ClickClip == click && complete != null && player.CompleteClip == complete,
                    $"Sound player clips: click {ClickClipPath}, completion {CompleteClipPath}");
                r.Check(player.ClickVolume > 0f && player.CompleteVolume > 0f, $"Click / completion volume above 0 (now {player.ClickVolume:F2} / {player.CompleteVolume:F2})");
                r.Check(source != null && source.gameObject == player.gameObject && source.enabled && !source.playOnAwake && !source.loop &&
                        source.clip == null && Mathf.Approximately(source.spatialBlend, 0f),
                    "UI source on the player: enabled, 2D (spatial blend 0), play on awake off, loop off, no clip of its own (one-shots only)");
                if (source != null)
                    expectedSources.Add(source);
            }
            if (loop != null)
            {
                var source = loop.Source;
                var link = pbStation != null ? pbStation.CourseLink as PressureBandageFieldCourse : null;
                var application = link != null && link.TrainingStart != null && link.TrainingStart.Link != null ? link.TrainingStart.Link.BandageApplication : null;
                r.Check(loop.Station == pbStation && application != null && loop.BandageApplication == application,
                    "Bandage loop reads its station and the course's own PressureBandageApplication");
                r.Check(pbStation != null && loop.transform.IsChildOf(pbStation.transform) && !loop.transform.IsChildOf(pbStation.Content) &&
                        (application == null || !loop.transform.IsChildOf(application.transform)),
                    "Bandage loop object under the station, outside Content (follows the roll; the equipment's parents and grabs unchanged)");
                r.Check(source != null && source.gameObject == loop.gameObject && source.enabled && !source.playOnAwake && source.loop &&
                        wrap != null && source.clip == wrap && Mathf.Approximately(source.spatialBlend, 1f) && Mathf.Approximately(source.dopplerLevel, 0f),
                    $"Loop source on it: enabled, 3D (spatial blend 1), loop on, play on awake off, Doppler 0, clip {WrapClipPath}");
                r.Check(loop.Volume > 0f, $"Bandage loop volume above 0 (now {loop.Volume:F2})");
                if (source != null)
                {
                    r.Info($"Bandage loop source: {source.rolloffMode} rolloff, min {source.minDistance:F2} m, max {source.maxDistance:F2} m, spread {source.spread:F0}, " +
                           $"volume {loop.Volume:F2} (the component writes AudioSource.volume while it fades)");
                    expectedSources.Add(source);
                }
            }
            var sources = InScene<AudioSource>(scene);
            var portalSources = sources.Where(s => s.transform.IsChildOf(exit)).ToArray();
            var extraSources = sources.Where(s => !s.transform.IsChildOf(exit) && !expectedSources.Contains(s)).Select(s => Path(s.transform)).ToArray();
            r.Check(extraSources.Length == 0 && sources.Count(s => !s.transform.IsChildOf(exit)) == expectedSources.Count,
                $"No other B AudioSource ({expectedSources.Count} expected)" + (extraSources.Length > 0 ? ": " + string.Join(", ", extraSources) : ""));
            if (portalSources.Length > 0)
                r.Info($"AudioSource(s) of the hub portal (A, not checked here): {string.Join(", ", portalSources.Select(s => Path(s.transform)))}");
            var awake = sources.Where(s => s.playOnAwake && !s.transform.IsChildOf(exit)).Select(s => Path(s.transform)).ToArray();
            r.Check(awake.Length == 0, "No B AudioSource plays on awake" + (awake.Length > 0 ? ": " + string.Join(", ", awake) : ""));

            // Buttons
            var expected = new HashSet<Button>(ClickButtonFields.SelectMany(e => ButtonsOf(scene, e.type, e.fields)));
            var silent = new HashSet<Button>(SilentButtonFields.SelectMany(e => ButtonsOf(scene, e.type, new[] { e.field })));
            r.Check(expected.Count >= stations.Length * 6, $"Training UI buttons found from the course and station references ({expected.Count})");
            if (player == null)
                return;
            var listed = player.ClickButtons;
            r.Check(listed.All(b => b != null) && listed.Distinct().Count() == listed.Count, $"Click list has no empty or repeated entry ({listed.Count})");
            var missing = expected.Where(b => !listed.Contains(b)).Select(b => Path(b.transform)).ToArray();
            r.Check(missing.Length == 0, "Every training UI button clicks" + (missing.Length > 0 ? "; missing: " + string.Join(", ", missing) : ""));
            var wrong = listed.Where(b => b != null && (!expected.Contains(b) || silent.Contains(b) || b.transform.IsChildOf(exit))).Select(b => Path(b.transform)).ToArray();
            r.Check(wrong.Length == 0, "Only training UI buttons click (no device hotspot, equipment board reset, hub portal or helper)" +
                                       (wrong.Length > 0 ? "; wrong: " + string.Join(", ", wrong) : ""));
            r.Check(listed.All(b => b == null || stations.Any(s => b.transform.IsChildOf(s.transform))), "Every clicking button belongs to a station of this field");
            foreach (var station in stations)
                r.Info($"{station.Course}: {listed.Count(b => b != null && b.transform.IsChildOf(station.transform))} clicking button(s): " +
                       string.Join(", ", listed.Where(b => b != null && b.transform.IsChildOf(station.transform)).Select(b => b.name + " (" + b.transform.parent.name + ")")));
            var quiet = InScene<Button>(scene).Where(b => !listed.Contains(b)).Select(b => Path(b.transform)).ToArray();
            r.Info(quiet.Length == 0 ? "No silent button in the scene objects" : "Silent buttons (scene objects): " + string.Join(", ", quiet));
        }

        static T[] InScene<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();

        static T One<T>(Transform root) where T : Component
        {
            var found = root.GetComponentsInChildren<T>(true);
            if (found.Length != 1)
                throw new InvalidOperationException($"{Path(root)}: expected one {typeof(T).Name}, found {found.Length}.");
            return found[0];
        }

        static Object RefOrNull(Object target, string field) => new SerializedObject(target).FindProperty(field)?.objectReferenceValue;

        static string Path(Transform t) => TrainingBoardKit.Path(t);
    }
}
