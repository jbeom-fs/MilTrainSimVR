using UnityEngine;
using UnityEngine.Serialization;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage
{
    // Display-only guide of the bandage chapter (reads PressureBandageApplication; never changes it). Markers have a mesh
    // renderer only (no collider, interactable or UI graphic), so they never take input. Meshes are generated at runtime;
    // shapes carry the meaning, colour only repeats it:
    // - Ready (dressing placed) and Wrapping: the wrap zone outline (box edges) where the roll is held to wrap, from the wrap
    //   path's zone values (it follows the wound frame and is rebuilt when its size changes). The trainee does not follow the
    //   path, so no gate, arrow or path dots are shown.
    // - End placement: the attach target on the end's mark, flat on the bandage surface: an outer ring of the application's
    //   attach distance (EndSnapRadius) and a small centre ring. No direction arrow: the end attaches by distance alone and is
    //   turned into place by the attach.
    // - Taping: the tape outline (frame) on its mark, long side across the band.
    // - Fixed, resetting or no valid application: hidden.
    [DefaultExecutionOrder(106)]
    [DisallowMultipleComponent]
    public sealed class PressureBandageGuide : MonoBehaviour
    {
        [SerializeField] PressureBandageApplication application;
        [Tooltip("End attach target (ring of the attach distance and the mark). Marker objects have a mesh renderer only; their " +
                 "meshes are generated at runtime.")]
        [SerializeField] MeshFilter ring;
        [SerializeField] MeshFilter tapeFrame;
        [Tooltip("Wrap zone outline (box edges).")]
        [FormerlySerializedAs("pathDots")]
        [SerializeField] MeshFilter zoneOutline;
        [Header("Marker sizes (m)")]
        [FormerlySerializedAs("gateLine")]
        [SerializeField, Min(0.001f)] float ringLine = 0.005f;
        [Tooltip("Radius (m) of the centre ring that marks the attach point.")]
        [SerializeField, Min(0.002f)] float centreRadius = 0.012f;
        [SerializeField, Min(0.001f)] float zoneLine = 0.004f;
        [SerializeField] Vector2 tapeOutline = new Vector2(0.1f, 0.025f);

        readonly FlatMeshWriter writer = new FlatMeshWriter();
        Mesh ringMesh;
        Mesh frameMesh;
        Mesh zoneMesh;
        bool prepared;
        Vector3 zoneMeshSize;
        float ringMeshRadius;

        void OnDestroy()
        {
            Release(ringMesh);
            Release(frameMesh);
            Release(zoneMesh);
        }

        bool Prepare()
        {
            if (prepared)
                return true;
            if (ring == null || tapeFrame == null || zoneOutline == null)
                return false;
            ringMesh = NewMesh("PB_Guide_EndTarget");
            frameMesh = NewMesh("PB_Guide_TapeFrame");
            zoneMesh = NewMesh("PB_Guide_WrapZone");
            FlatMeshWriter.Frame(frameMesh, writer, tapeOutline, 0.003f);
            ring.sharedMesh = ringMesh;
            tapeFrame.sharedMesh = frameMesh;
            zoneOutline.sharedMesh = zoneMesh;
            zoneMeshSize = Vector3.zero;
            ringMeshRadius = 0f;
            prepared = true;
            return true;
        }

        static Mesh NewMesh(string name) => new Mesh { name = name, hideFlags = HideFlags.DontSave };

        static void Release(Mesh mesh)
        {
            if (mesh == null)
                return;
            if (Application.isPlaying)
                Destroy(mesh);
            else
                DestroyImmediate(mesh);
        }

        // Set by the training course guide (before this LateUpdate) while the current course step does not use these markers
        // (start wait, earlier steps, post-wrap check, completion, reset). Without a course it stays false.
        public bool Suppressed { get; set; }

        void LateUpdate()
        {
            if (Suppressed || !Prepare() || application == null || !application.isActiveAndEnabled || application.IsResetting || application.Path == null)
            {
                Show(false, false, false);
                return;
            }

            var path = application.Path;
            switch (application.Stage)
            {
                case PressureBandageStage.Ready:
                    if (!application.Dressing.IsPlacementValid)
                    {
                        Show(false, false, false);
                        return;
                    }
                    PlaceZone(path);
                    Show(false, false, true);
                    return;
                case PressureBandageStage.Wrapping:
                    PlaceZone(path);
                    Show(false, false, true);
                    return;
                case PressureBandageStage.EndPlacement:
                {
                    // The same mark and distance the application attaches with (Anchor_EndFix to EndMark, EndSnapRadius).
                    var radius = application.EndSnapRadius;
                    if (!Mathf.Approximately(radius, ringMeshRadius))
                    {
                        FlatMeshWriter.Target(ringMesh, writer, radius, ringLine, Mathf.Min(centreRadius, radius * 0.5f), 48);
                        ringMeshRadius = radius;
                    }
                    application.EndMark(out var mark, out var tangent, out var normal);
                    ring.transform.SetPositionAndRotation(mark + normal * 0.002f, Quaternion.LookRotation(normal, tangent));
                    Show(true, false, false);
                    return;
                }
                case PressureBandageStage.Taping:
                {
                    application.TapeMark(out var mark, out var tangent, out var normal, out _);
                    tapeFrame.transform.SetPositionAndRotation(mark + normal * 0.002f, Quaternion.LookRotation(normal, tangent));
                    Show(false, true, false);
                    return;
                }
                default:
                    Show(false, false, false);
                    return;
            }
        }

        // Outline of the zone the application judges (PressureBandageWrapPath.InWrapZone): same centre, axes and size.
        void PlaceZone(PressureBandageWrapPath path)
        {
            var size = path.ZoneSize;
            if (size != zoneMeshSize)
            {
                FlatMeshWriter.BoxEdges(zoneMesh, writer, size, zoneLine);
                zoneMeshSize = size;
            }
            zoneOutline.transform.SetPositionAndRotation(path.ZoneCentre, path.ZoneRotation);
        }

        void Show(bool showRing, bool showFrame, bool showZone)
        {
            SetActive(ring, showRing);
            SetActive(tapeFrame, showFrame);
            SetActive(zoneOutline, showZone);
        }

        static void SetActive(Component c, bool on)
        {
            if (c != null && c.gameObject.activeSelf != on)
                c.gameObject.SetActive(on);
        }

        public bool TryValidate(out string error)
        {
            error = application == null || ring == null || tapeFrame == null || zoneOutline == null
                ? $"PressureBandageGuide '{name}' has a missing reference (application or a marker)."
                : null;
            return error == null;
        }
    }
}
