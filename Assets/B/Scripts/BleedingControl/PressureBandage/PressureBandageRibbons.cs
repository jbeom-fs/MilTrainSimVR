using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage
{
    // Display of the bandage cloth from PressureBandageApplication (reads its state; never changes it). Runtime work meshes,
    // created once and rewritten in place, replace the model meshes on the three cloth parts while an application is shown;
    // the model mesh assets are never modified and are put back by ResetDisplay (equipment reset) or when the application
    // returns to Ready:
    // - PB_WrapStrip: the wrapped band, along the path up to the application's WrappedAngle (its wrap progress; rebuilt when
    //   Version changes, which it does with every progress step), so the band grows around the arm while the roll is held in
    //   the wrap zone and the display never runs ahead of or behind the progress.
    // - PB_BandageStrip: the loose band from the wrap end to the roll exit (or to the detached end), drawn exactly as the
    //   connection PressureBandageApplication computed and checked this frame: on the arm surface to the tangent point, then
    //   straight. An invalid connection is not drawn (see UpdateStrip). Rebuilt only when its far end or validity changes.
    // - PB_BandageEnd: a flat two-sided copy of the end while loose, the continuation of the wrap after it is laid.
    // Connection points are computed from the path and the application, not read from model empties (empties do not
    // follow deformed meshes): the wrap end is the path point at the wrapped angle; the roll exit is the roll surface
    // point toward the strip (PressureBandageApplication.RollExitToward), so the exit never turns with the roll's own rotation
    // and the strip cannot twist around the roll; the roll mesh itself is not spun (no unrolling animation in this chapter).
    // U runs on from the wrap through the strip and the end (length / band width), like the model strip, so the weave keeps
    // its scale. Width follows the forearm axis on the arm and turns to the roll axis (or the end's width) along the free run.
    [DefaultExecutionOrder(104)]
    [DisallowMultipleComponent]
    public sealed class PressureBandageRibbons : MonoBehaviour
    {
        const int MaxStations = 600;

        [SerializeField] PressureBandageApplication application;
        [SerializeField] PressureBandageKit kit;
        [Tooltip("Front-face-only bandage material for the two-sided work meshes.")]
        [SerializeField] Material ribbonMaterial;
        [Tooltip("Model meshes of the strip, wrap strip and bandage end, put back by the equipment reset.")]
        [SerializeField] Mesh stripSource;
        [SerializeField] Mesh wrapSource;
        [SerializeField] Mesh endSource;
        [Tooltip("Station spacing (m) along the cloth; the model strip uses 0.01 m.")]
        [SerializeField, Min(0.002f)] float stationSpacing = 0.01f;
        [Tooltip("Segments across the width; the model strip uses 4.")]
        [SerializeField, Range(1, 8)] int widthSegments = 4;
        [Tooltip("Height (m) of the loose strip above the bandage surface where it lies on the arm.")]
        [SerializeField, Min(0f)] float stripLift = 0.001f;

        readonly RibbonMeshWriter writer = new RibbonMeshWriter();
        readonly List<RibbonStation> stations = new List<RibbonStation>();
        MeshFilter stripFilter;
        MeshFilter wrapFilter;
        MeshFilter endFilter;
        MeshRenderer stripRenderer;
        MeshRenderer wrapRenderer;
        MeshRenderer endRenderer;
        Material[] stripMaterials;
        Material[] wrapMaterials;
        Material[] endMaterials;
        Mesh wrapMesh;
        Mesh stripMesh;
        Mesh endMesh;
        bool prepared;
        int builtVersion = int.MinValue;
        float wrapEndU;
        float widthSign = 1f;
        bool stripShown;
        bool lastValid;
        bool stripEvaluated;
        int stripVersion = int.MinValue;
        float stripEndU;
        Vector3 lastFarPosition;
        Quaternion lastFarRotation;

        // True while the three cloth parts show the model meshes and materials (no application shown).
        public bool ShowsModelMeshes { get; private set; } = true;
        public Material RibbonMaterial => ribbonMaterial;
        // Height of the loose strip above the bandage surface; PressureBandageApplication checks the connection with it.
        public float StripLift => stripLift;

        void Awake() => Prepare();

        void OnDestroy()
        {
            if (prepared)
                ShowModel();
            Release(wrapMesh);
            Release(stripMesh);
            Release(endMesh);
        }

        bool Prepare()
        {
            if (prepared)
                return true;
            if (kit == null || !kit.TryValidate(out _) || ribbonMaterial == null)
                return false;
            var p = kit.PartTransforms;
            if (!p.bandageStrip.TryGetComponent(out stripFilter) || !p.wrapStrip.TryGetComponent(out wrapFilter) ||
                !p.bandageEnd.TryGetComponent(out endFilter) || !p.bandageStrip.TryGetComponent(out stripRenderer) ||
                !p.wrapStrip.TryGetComponent(out wrapRenderer) || !p.bandageEnd.TryGetComponent(out endRenderer))
                return false;
            if (stripSource == null)
                stripSource = stripFilter.sharedMesh;
            if (wrapSource == null)
                wrapSource = wrapFilter.sharedMesh;
            if (endSource == null)
                endSource = endFilter.sharedMesh;
            stripMaterials = stripRenderer.sharedMaterials;
            wrapMaterials = wrapRenderer.sharedMaterials;
            endMaterials = endRenderer.sharedMaterials;
            wrapMesh = NewMesh("PB_WrapStrip_Work");
            stripMesh = NewMesh("PB_BandageStrip_Work");
            endMesh = NewMesh("PB_BandageEnd_Work");
            prepared = true;
            return true;
        }

        static Mesh NewMesh(string name)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            return mesh;
        }

        static void Release(Mesh mesh)
        {
            if (mesh == null)
                return;
            if (Application.isPlaying)
                Destroy(mesh);
            else
                DestroyImmediate(mesh);
        }

        // Equipment reset step (called by PressureBandageApplication.ResetEquipment): model meshes and materials back on the
        // three cloth parts, work meshes emptied, loose strip and wrap hidden.
        public void ResetDisplay()
        {
            if (!Prepare())
                return;
            ShowModel();
            builtVersion = int.MinValue;
        }

        void LateUpdate()
        {
            if (!Prepare() || application == null || !application.isActiveAndEnabled || application.IsResetting)
                return;
            var rebuilt = false;
            if (application.Version != builtVersion)
            {
                BuildApplied();
                builtVersion = application.Version;
                rebuilt = true;
            }
            UpdateStrip(rebuilt);
        }

        // ---------- Applied parts (wrap and laid end) ----------

        void BuildApplied()
        {
            var stage = application.Stage;
            if (stage == PressureBandageStage.Ready)
            {
                ShowModel();
                return;
            }
            ShowsModelMeshes = false;
            var path = application.Path;
            path.FrameAt(0f, 0f, out _, out var t0, out var n0);
            widthSign = Vector3.Dot(Vector3.Cross(t0, n0), path.Axis) >= 0f ? 1f : -1f;

            var thetaEnd = application.WrappedAngle;
            wrapEndU = 0f;
            if (thetaEnd > 0f)
            {
                stations.Clear();
                wrapEndU = AddArc(path, 0f, thetaEnd, 0f, 0f, float.MaxValue);
                Write(wrapMesh, wrapFilter, wrapRenderer, kit.PartTransforms.wrapStrip);
            }
            else
            {
                wrapMesh.Clear();
                kit.PartTransforms.wrapStrip.gameObject.SetActive(false);
            }

            if (stage >= PressureBandageStage.Taping)
            {
                // Laid end: the wrap continued for the end's length, U continued from the wrap.
                stations.Clear();
                AddArc(path, thetaEnd, thetaEnd + Mathf.PI, application.SurfaceLift, wrapEndU, application.EndLength);
                WriteEnd();
            }
        }

        // Stations along the path from theta0 for at most maxLength (m) or up to theta1; returns the U after the last station.
        float AddArc(PressureBandageWrapPath path, float theta0, float theta1, float lift, float u0, float maxLength)
        {
            var width = path.Axis * widthSign;
            var theta = theta0;
            var length = 0f;
            var previous = path.PointAt(theta, lift);
            Add(previous, width, u0);
            // Generous length estimate (profile bulge over the dressing) so the station count stays bounded.
            var estimate = Mathf.Min(maxLength, (theta1 - theta0) * (path.SurfaceRadius(theta1) + lift) * 1.5f);
            var spacing = Mathf.Max(stationSpacing, estimate / (MaxStations - 2));
            while (theta < theta1 && length < maxLength && stations.Count < MaxStations * 2)
            {
                var step = spacing / Mathf.Max(0.005f, path.SurfaceRadius(theta) + lift);
                theta = Mathf.Min(theta + step, theta1);
                var point = path.PointAt(theta, lift);
                var segment = Vector3.Distance(point, previous);
                if (length + segment > maxLength && segment > 1e-6f)
                {
                    point = Vector3.Lerp(previous, point, (maxLength - length) / segment);
                    segment = maxLength - length;
                }
                length += segment;
                Add(point, width, u0 + length / path.BandWidth);
                previous = point;
            }
            return u0 + length / path.BandWidth;
        }

        // ---------- Loose strip ----------

        // Draws the connection PressureBandageApplication computed this frame (the same tangent point, far point and straight
        // run it checked). Display policy for an invalid connection (roll body or strip into the arm, dressing or layers): it is
        // never drawn. The last valid strip of the current geometry stays as it was (not attached to the roll, so the gap shows
        // that the pose is not accepted; the board names the reason); after the geometry changed (Version) there is no valid
        // strip of it, so the strip is hidden until a valid pose returns. The roll is never moved and no selection is ended.
        void UpdateStrip(bool force)
        {
            var stage = application.Stage;
            var toEnd = stage == PressureBandageStage.EndPlacement;
            if (stage != PressureBandageStage.Wrapping && !toEnd)
            {
                HideStrip();
                stripEvaluated = false;
                return;
            }

            var far = toEnd ? kit.PartTransforms.bandageEnd : kit.PartTransforms.bandageRoll;
            var valid = application.LooseValid;
            var moved = (far.position - lastFarPosition).sqrMagnitude >= 1e-8f || Quaternion.Angle(far.rotation, lastFarRotation) >= 0.05f;
            if (!force && stripEvaluated && !moved && valid == lastValid)
                return;
            stripEvaluated = true;
            lastFarPosition = far.position;
            lastFarRotation = far.rotation;
            lastValid = valid;

            if (valid)
            {
                WriteStrip();
            }
            else if (!stripShown || stripVersion != builtVersion)
            {
                HideStrip();
            }

            if (toEnd)
                WriteLooseEnd();
        }

        void WriteStrip()
        {
            var path = application.Path;
            var theta0 = application.LooseStartAngle;
            var thetaT = application.LooseTangentAngle;
            var axial = path.AxialAt(theta0);
            var farPoint = application.LooseFarPoint;
            var arcWidth = path.Axis * widthSign;
            var farWidth = application.LooseFarWidth;
            if (Vector3.Dot(farWidth, arcWidth) < 0f)
                farWidth = -farWidth;

            // Wrap end, the arc on the arm (at the wrap end's axial position, as checked) to the tangent point, then straight.
            var arcLength = Mathf.Abs(thetaT - theta0) * (path.SurfaceRadius(theta0) + stripLift);
            var total = Mathf.Max(1e-4f, arcLength + Vector3.Distance(path.FreePoint(theta0, thetaT, axial, stripLift), farPoint));
            var spacing = Mathf.Max(stationSpacing, total / (MaxStations - 2));

            stations.Clear();
            var previous = path.PointAt(theta0);
            Add(previous, arcWidth, wrapEndU);
            var travelled = 0f;
            var arcSteps = Mathf.Max(1, Mathf.CeilToInt(arcLength / spacing));
            for (var i = 1; i <= arcSteps; i++)
            {
                var point = path.FreePoint(theta0, Mathf.Lerp(theta0, thetaT, (float)i / arcSteps), axial, stripLift);
                travelled += Vector3.Distance(point, previous);
                Add(point, arcWidth, wrapEndU + travelled / path.BandWidth);
                previous = point;
            }
            var lineStart = previous;
            var lineSteps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(lineStart, farPoint) / spacing));
            for (var i = 1; i <= lineSteps; i++)
            {
                var f = (float)i / lineSteps;
                var point = Vector3.Lerp(lineStart, farPoint, f);
                travelled += Vector3.Distance(point, previous);
                Add(point, Vector3.Slerp(arcWidth, farWidth.normalized, f), wrapEndU + travelled / path.BandWidth);
                previous = point;
            }
            Write(stripMesh, stripFilter, stripRenderer, kit.PartTransforms.bandageStrip);
            stripShown = true;
            stripVersion = builtVersion;
            stripEndU = wrapEndU + travelled / path.BandWidth;
        }

        void HideStrip()
        {
            if (!stripShown)
                return;
            stripMesh.Clear();
            kit.PartTransforms.bandageStrip.gameObject.SetActive(false);
            stripShown = false;
        }

        // Loose end: flat, two-sided, in its own part space (moves with the hand), U continuing from the last drawn strip.
        void WriteLooseEnd()
        {
            stations.Clear();
            var path = application.Path;
            var end = kit.PartTransforms.bandageEnd;
            var length = end.TransformDirection(new Vector3(application.EndLengthSign, 0f, 0f));
            var width = application.EndWidth(end);
            var u = stripShown ? stripEndU : wrapEndU;
            var count = Mathf.Max(2, Mathf.CeilToInt(application.EndLength / stationSpacing) + 1);
            for (var i = 0; i < count; i++)
            {
                var s = application.EndLength * i / (count - 1);
                Add(end.position + length * s, width, u + s / path.BandWidth);
            }
            WriteEnd();
        }

        // ---------- Mesh output ----------

        void Add(Vector3 position, Vector3 width, float u) =>
            stations.Add(new RibbonStation { position = position, width = width, u = u });

        void Write(Mesh mesh, MeshFilter filter, MeshRenderer renderer, Transform part)
        {
            RibbonMeshWriter.CompleteFrames(stations);
            writer.Write(mesh, stations, application.Path.BandWidth * 0.5f, widthSegments, part.worldToLocalMatrix);
            if (filter.sharedMesh != mesh)
                filter.sharedMesh = mesh;
            // The cloth parts have one submesh, so one material.
            if (renderer.sharedMaterial != ribbonMaterial)
                renderer.sharedMaterial = ribbonMaterial;
            if (!part.gameObject.activeSelf)
                part.gameObject.SetActive(true);
        }

        void WriteEnd() => Write(endMesh, endFilter, endRenderer, kit.PartTransforms.bandageEnd);

        void ShowModel()
        {
            Restore(stripFilter, stripRenderer, stripSource, stripMaterials);
            Restore(wrapFilter, wrapRenderer, wrapSource, wrapMaterials);
            Restore(endFilter, endRenderer, endSource, endMaterials);
            if (wrapMesh != null)
                wrapMesh.Clear();
            if (stripMesh != null)
                stripMesh.Clear();
            if (endMesh != null)
                endMesh.Clear();
            if (kit != null && kit.TryValidate(out _))
            {
                kit.PartTransforms.bandageStrip.gameObject.SetActive(false);
                kit.PartTransforms.wrapStrip.gameObject.SetActive(false);
            }
            stripShown = false;
            stripEvaluated = false;
            ShowsModelMeshes = true;
        }

        static void Restore(MeshFilter filter, MeshRenderer renderer, Mesh source, Material[] materials)
        {
            if (filter != null && filter.sharedMesh != source)
                filter.sharedMesh = source;
            if (renderer != null && materials != null)
                renderer.sharedMaterials = materials;
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (application == null)
                missing.Add(nameof(application));
            if (kit == null)
                missing.Add(nameof(kit));
            if (ribbonMaterial == null)
                missing.Add(nameof(ribbonMaterial));
            if (stripSource == null || wrapSource == null || endSource == null)
                missing.Add("model mesh sources");
            error = missing.Count > 0 ? $"PressureBandageRibbons '{name}' missing: {string.Join(", ", missing)}." : null;
            return error == null;
        }
    }
}
