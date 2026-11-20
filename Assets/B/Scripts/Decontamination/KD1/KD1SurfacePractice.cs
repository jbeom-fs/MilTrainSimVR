using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.B.Decontamination.KD1
{
    // Surface contact experience with a fitted KD-1 pad on one fixed target (a protective glove on a stand): each marked area
    // (KD1SurfaceZone) is recorded the first time the underside (working face) of a fitted pad touches it. A simple VR experience of
    // this scene based on the public description of the hand-worn pad; the areas, their number and every distance here are
    // development settings, not a procedure, an amount of agent, a pressure, a time or an evaluation standard. No angle, approach
    // path, speed or force is judged.
    //
    // Sensor (no collider; a pure box overlap, so it pushes nothing and is never a grab, hover or UI target): a thin box on the
    // underside of the fitted working sheet, in the frame of the pad's Anchor_Contact (+Z out of the working face). Its sides are
    // the sheet's footprint (the bounds of Part_PadContact_Fitted in that frame) grown by sensorSideMargin; it runs from sensorInset
    // behind the face centre (towards the hand) to sensorReach beyond the sheet's outermost point. The top, the back of the hand
    // and the sleeve above the sheet are behind it. The frame comes from the Anchor_Contact and part matrices together, so the
    // left-hand mirror of the fitted model cancels out and both hands get the box on their real underside.
    //
    // Areas: the thin slab of each area (KD1SurfaceZone, radius in the surface plane, zoneDepth each way along its normal). In
    // LateUpdate (order 55: after the hand mounts followed this frame's controller pose in their LateUpdate, and after
    // KD1Equipment's fitting/removal at 50), for each hand that is ready (KD1Equipment.IsReadyForSurface: a pad fitted on it,
    // focus, tracked, unlocked, holding nothing), every area not done yet whose slab the sensor box overlaps now is recorded once.
    // Only the current pose counts: nothing is kept between frames, so motion during a tracking or focus loss and the jump when it
    // returns are never used. Holding the pad on an area never adds to it; touching another area records that area only.
    //
    // Training field hold (KD1Equipment.Suspended, its stop confirm): nothing is recorded meanwhile (the course gate). When it
    // ends, every area not done yet that a fitted pad's sensor overlaps at that moment is re-armed: it is recorded only after no
    // fitted pad overlaps it any more and a ready pad touches it again (the same contact rule). Areas done before the hold stay
    // done; other areas record as usual. A course's own scene never sets the hold, so this never applies there.
    //
    // Markers show only while a pad is fitted on either hand (the state of each area is kept while they are hidden). Records stay
    // through removal and refitting; only the equipment reset (KD1Equipment.EquipmentReset) clears them and hides the markers.
    [DefaultExecutionOrder(55)]
    [DisallowMultipleComponent]
    public sealed class KD1SurfacePractice : MonoBehaviour
    {
        // Working sheet of the fitted pad model (KD-1 model contract, README_KD1.md).
        public const string WorkingSheetPart = "Part_PadContact_Fitted";

        // Oriented box: centre, three unit axes, half sizes along them.
        public readonly struct Box
        {
            public readonly Vector3 Center;
            public readonly Vector3 X;
            public readonly Vector3 Y;
            public readonly Vector3 Z;
            public readonly Vector3 Extents;

            public Box(Vector3 center, Vector3 x, Vector3 y, Vector3 z, Vector3 extents)
            {
                Center = center;
                X = x;
                Y = y;
                Z = z;
                Extents = extents;
            }

            // A box given in a space (local bounds and that space's matrix; a mirrored or scaled matrix is fine).
            public static Box FromLocal(Matrix4x4 space, Bounds local)
            {
                var x = space.MultiplyVector(Vector3.right);
                var y = space.MultiplyVector(Vector3.up);
                var z = space.MultiplyVector(Vector3.forward);
                return new Box(space.MultiplyPoint3x4(local.center), x.normalized, y.normalized, z.normalized,
                    Vector3.Scale(local.extents, new Vector3(x.magnitude, y.magnitude, z.magnitude)));
            }

            Vector3 Axis(int i) => i == 0 ? X : i == 1 ? Y : Z;

            // Separating axis test of two oriented boxes (touching counts as overlapping).
            public static bool Overlap(Box a, Box b)
            {
                const float epsilon = 1e-6f;
                var r = new float[3, 3];
                var absR = new float[3, 3];
                for (var i = 0; i < 3; i++)
                {
                    for (var j = 0; j < 3; j++)
                    {
                        r[i, j] = Vector3.Dot(a.Axis(i), b.Axis(j));
                        absR[i, j] = Mathf.Abs(r[i, j]) + epsilon;
                    }
                }
                var d = b.Center - a.Center;
                var t = new Vector3(Vector3.Dot(d, a.X), Vector3.Dot(d, a.Y), Vector3.Dot(d, a.Z));
                var ea = a.Extents;
                var eb = b.Extents;
                for (var i = 0; i < 3; i++)
                {
                    if (Mathf.Abs(t[i]) > ea[i] + eb[0] * absR[i, 0] + eb[1] * absR[i, 1] + eb[2] * absR[i, 2])
                        return false;
                }
                for (var j = 0; j < 3; j++)
                {
                    if (Mathf.Abs(t[0] * r[0, j] + t[1] * r[1, j] + t[2] * r[2, j]) > ea[0] * absR[0, j] + ea[1] * absR[1, j] + ea[2] * absR[2, j] + eb[j])
                        return false;
                }
                // The nine cross axes A_i x B_j.
                for (var i = 0; i < 3; i++)
                {
                    var i1 = (i + 1) % 3;
                    var i2 = (i + 2) % 3;
                    for (var j = 0; j < 3; j++)
                    {
                        var j1 = (j + 1) % 3;
                        var j2 = (j + 2) % 3;
                        var ra = ea[i1] * absR[i2, j] + ea[i2] * absR[i1, j];
                        var rb = eb[j1] * absR[i, j2] + eb[j2] * absR[i, j1];
                        if (Mathf.Abs(t[i2] * r[i1, j] - t[i1] * r[i2, j]) > ra + rb)
                            return false;
                    }
                }
                return true;
            }
        }

        [SerializeField] KD1Equipment equipment;
        [Tooltip("The target's outer surface (a non-convex mesh collider on its mesh, not a grab collider). Used to place and check the areas.")]
        [SerializeField] MeshCollider surface;
        [SerializeField] KD1SurfaceZone[] zones = new KD1SurfaceZone[0];
        [Tooltip("Engineering setting: the sensor starts this far (m) behind the face centre, towards the hand.")]
        [SerializeField] float sensorInset = 0.002f;
        [Tooltip("Engineering setting: the sensor reaches this far (m) beyond the sheet's outermost point.")]
        [SerializeField] float sensorReach = 0.006f;
        [Tooltip("Engineering setting: the sensor is this much (m) wider than the sheet on each side.")]
        [SerializeField] float sensorSideMargin = 0.005f;
        [Tooltip("Engineering setting: each area's contact slab reaches this far (m) above and below the surface.")]
        [SerializeField] float zoneDepth = 0.006f;

        readonly Dictionary<KD1Item, Bounds> sensors = new Dictionary<KD1Item, Bounds>();
        bool[] done = new bool[0];
        // Areas re-armed when a field hold ended (see the class notes).
        bool[] rearm = new bool[0];
        bool holdSeen;
        bool markersShown;
        bool valid;
        bool subscribed;

        public KD1Equipment Equipment => equipment;
        public MeshCollider Surface => surface;
        public IReadOnlyList<KD1SurfaceZone> Zones => zones;
        public float SensorInset => sensorInset;
        public float SensorReach => sensorReach;
        public float SensorSideMargin => sensorSideMargin;
        public float ZoneDepth => zoneDepth;
        public bool IsValid => valid;
        public int ZoneCount => zones.Length;
        public int DoneCount { get; private set; }
        public bool IsComplete => valid && DoneCount == zones.Length;
        public bool MarkersShown => markersShown;
        // Raised on every record and reset (views rebuild on change).
        public int Version { get; private set; }

        public bool IsDone(int index) => valid && index >= 0 && index < done.Length && done[index];

        public Box ContactBox(KD1SurfaceZone zone) =>
            new Box(zone.Point, zone.transform.right, zone.transform.up, zone.Normal, new Vector3(zone.Radius, zone.Radius, zoneDepth));

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError("[KD1] " + error + " Surface practice is off.", this);
                return;
            }
            done = new bool[zones.Length];
            rearm = new bool[zones.Length];
            equipment.EquipmentReset += OnEquipmentReset;
            subscribed = true;
            markersShown = false;
            ShowMarkers();
        }

        void OnDestroy()
        {
            if (subscribed && equipment != null)
                equipment.EquipmentReset -= OnEquipmentReset;
            subscribed = false;
        }

        void OnEquipmentReset()
        {
            for (var i = 0; i < done.Length; i++)
            {
                done[i] = false;
                rearm[i] = false;
            }
            DoneCount = 0;
            markersShown = false;
            ShowMarkers();
            Version++;
        }

        void LateUpdate()
        {
            if (!valid)
                return;
            var fitted = equipment.FittedOn(KD1Hand.Left) != null || equipment.FittedOn(KD1Hand.Right) != null;
            if (fitted != markersShown)
            {
                markersShown = fitted;
                ShowMarkers();
            }
            if (equipment.Suspended)
            {
                holdSeen = true;
                return;
            }
            if (holdSeen)
            {
                holdSeen = false;
                RearmTouchedAreas();
            }
            ReleaseRearmedAreas();
            Sense(KD1Hand.Left);
            Sense(KD1Hand.Right);
        }

        // Hold just ended: the areas not done that a fitted pad overlaps now wait for that pad to leave them.
        void RearmTouchedAreas()
        {
            for (var i = 0; i < zones.Length; i++)
            {
                if (!done[i] && FittedPadOverlaps(i))
                    rearm[i] = true;
            }
        }

        void ReleaseRearmedAreas()
        {
            for (var i = 0; i < zones.Length; i++)
            {
                if (rearm[i] && !FittedPadOverlaps(i))
                    rearm[i] = false;
            }
        }

        // A pad fitted on either hand overlaps area i now (current pose; readiness not asked, so an untracked hand still counts).
        bool FittedPadOverlaps(int i)
        {
            foreach (var hand in new[] { KD1Hand.Left, KD1Hand.Right })
            {
                var pad = equipment.FittedOn(hand);
                if (pad != null && TryGetSensor(pad, out var local) && Box.Overlap(Box.FromLocal(pad.Contact.localToWorldMatrix, local), ContactBox(zones[i])))
                    return true;
            }
            return false;
        }

        void Sense(KD1Hand hand)
        {
            if (!equipment.IsReadyForSurface(hand, out var pad) || !TryGetSensor(pad, out var local))
                return;
            var sensor = Box.FromLocal(pad.Contact.localToWorldMatrix, local);
            for (var i = 0; i < zones.Length; i++)
            {
                if (done[i] || rearm[i] || !Box.Overlap(sensor, ContactBox(zones[i])))
                    continue;
                done[i] = true;
                DoneCount++;
                zones[i].SetMarkers(markersShown, true);
                Version++;
            }
        }

        // The sensor box in the pad's Anchor_Contact space (see the class notes); false when the pad has no working sheet mesh.
        public bool TryGetSensor(KD1Item pad, out Bounds local)
        {
            if (pad != null && sensors.TryGetValue(pad, out local))
                return true;
            local = default;
            if (pad == null || pad.Contact == null || pad.ChangedModel == null)
                return false;
            var sheet = FindDeep(pad.ChangedModel.transform, WorkingSheetPart);
            if (sheet == null || !sheet.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
                return false;
            var b = Transformed(pad.Contact.worldToLocalMatrix * sheet.localToWorldMatrix, filter.sharedMesh.bounds);
            var min = new Vector3(b.min.x - sensorSideMargin, b.min.y - sensorSideMargin, -sensorInset);
            var max = new Vector3(b.max.x + sensorSideMargin, b.max.y + sensorSideMargin, b.max.z + sensorReach);
            local = new Bounds();
            local.SetMinMax(min, max);
            sensors[pad] = local;
            return true;
        }

        void ShowMarkers()
        {
            for (var i = 0; i < zones.Length; i++)
                zones[i].SetMarkers(markersShown, done[i]);
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

        static Bounds Transformed(Matrix4x4 matrix, Bounds local)
        {
            var bounds = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                bounds.Encapsulate(matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, corner)));
            }
            return bounds;
        }

        static bool InRange(float value, float min, float max) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (equipment == null)
                problems.Add(nameof(equipment) + " missing");
            if (surface == null || surface.convex || surface.isTrigger || surface.sharedMesh == null)
                problems.Add("surface must be a non-convex, non-trigger mesh collider with a mesh");
            else if (surface.attachedRigidbody != null)
                problems.Add("the target must be fixed (no Rigidbody)");
            if (zones == null || zones.Length == 0)
            {
                problems.Add("no practice areas");
            }
            else
            {
                var ids = new HashSet<string>();
                foreach (var zone in zones)
                {
                    if (zone == null)
                        problems.Add("an empty area entry");
                    else if (!zone.TryValidate(out var zoneError))
                        problems.Add(zoneError);
                    else if (!ids.Add(zone.Id))
                        problems.Add($"area id '{zone.Id}' used twice");
                }
            }
            if (!InRange(sensorInset, 0f, 0.01f) || !InRange(sensorReach, 0.0005f, 0.03f) || !InRange(sensorSideMargin, 0f, 0.03f) || !InRange(zoneDepth, 0.0005f, 0.03f))
                problems.Add("sensor settings out of range (inset 0-10 mm, reach 0.5-30 mm, side margin 0-30 mm, area depth 0.5-30 mm)");
            error = problems.Count > 0 ? $"KD1SurfacePractice '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }
    }
}
