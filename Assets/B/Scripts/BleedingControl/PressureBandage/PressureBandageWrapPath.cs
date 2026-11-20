using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage
{
    // Wrap path around the training forearm for the bandage chapter (display geometry of the wrapped band and its wrap zone, not
    // a taught wrapping method). Frame of this object: position = forearm axis point under the wound, forward = along the forearm toward
    // the wrist, up = toward the wound. Frame angle alpha is measured from up toward this object's right (+X).
    //
    // Path angle theta (>= 0) runs from the start (alpha = startAngle) in wrapDirection for `turns` turns; waypoint i lies at
    // theta = i * 360 / pointsPerTurn. Along the arm the path advances advancePerTurn per turn from startAxialOffset. Its
    // radius follows the wrap profile (convex hull of the measured skin profile plus clearance, widened over the dressing's
    // corners while an application runs) plus layerOffset per turn, so each later layer lies outside the earlier one.
    //
    // The path is the display geometry of the wrap: the trainee does not follow it. Wrapping is driven by holding the roll in
    // the wrap zone (PressureBandageApplication owns the time and the progress); the wrapped band grows along this path to the
    // reached angle. skinProfile (radius per frame-angle bin, measured from the baked mannequin skin) is the
    // "inside the arm" boundary (IsInsideArm). The roll body clearance against the outer boundary (wrap profile plus all
    // layers; RollClear) and the loose strip's tangent point and straight run (TryTangent, FreeVisible, SegmentClearOfWrap)
    // decide what the cloth display draws, so judging and display use one geometry.
    //
    // Wrap zone: a box in this frame (zoneCentre, zoneSize; x toward +X, y toward the wound, z along the forearm) outside the
    // arm on the wound side, so the roll is brought there from outside without going round, under or behind the arm. It moves
    // and turns with this frame (a child of the wound judge frame). The roll centre (Anchor_RollAxis) is the judged point; a
    // point inside the outer boundary (arm, dressing or bandage layers) is never in the zone, whatever the box values.
    // All values are development handling values chosen so the operation can be checked end to end; they are not a taught
    // wrapping direction, number of turns, overlap, tension, time or tolerance.
    [DisallowMultipleComponent]
    public sealed class PressureBandageWrapPath : MonoBehaviour
    {
        public const int ProfileBins = 72;
        const float TwoPi = Mathf.PI * 2f;
        const int CircleHullPoints = 72;

        [Header("Arm cross-section (measured from the mannequin skin; development values)")]
        [Tooltip("Skin radius (m) from the forearm axis per frame angle bin (72 bins of 5 deg from up toward +X). Empty: a " +
                 "circle of skinRadius.")]
        [SerializeField] float[] skinProfile = new float[0];
        [Tooltip("Largest skin radius (m) around the wrap section; used when skinProfile is empty.")]
        [SerializeField, Min(0.005f)] float skinRadius = 0.045f;
        [Tooltip("Gap (m) between the skin profile and the first bandage layer.")]
        [SerializeField, Min(0f)] float clearance = 0.003f;
        [Header("Path shape (development values, not a taught method)")]
        [SerializeField, Range(1, 6)] int turns = 3;
        [SerializeField, Range(4, 16)] int pointsPerTurn = 8;
        [Tooltip("Advance (m) along the forearm per turn.")]
        [SerializeField] float advancePerTurn = 0.03f;
        [Tooltip("Axial position (m) of the path start from this frame (negative: toward the elbow).")]
        [SerializeField] float startAxialOffset = -0.045f;
        [Tooltip("Frame angle (deg) of the path start; 0 = toward the wound.")]
        [SerializeField] float startAngle;
        [Tooltip("+1: turns from up toward this frame's +X; -1: the other way.")]
        [SerializeField] int wrapDirection = 1;
        [Tooltip("Extra radius (m) per turn so overlapping layers do not coincide.")]
        [SerializeField, Min(0f)] float layerOffset = 0.0015f;
        [Tooltip("Bandage width (m), from the model strip.")]
        [SerializeField, Min(0.01f)] float bandWidth = 0.08f;
        [Tooltip("Margin (m) added to the skin profile for the inside-the-arm check.")]
        [SerializeField, Min(0f)] float insideMargin;
        [Header("Wrap zone (hold the roll here to wrap; development values)")]
        [Tooltip("Centre (m) of the wrap zone in this frame: x toward this frame's +X, y toward the wound (away from the arm " +
                 "axis), z along the forearm toward the wrist.")]
        [SerializeField] Vector3 zoneCentre = new Vector3(0f, 0.18f, 0f);
        [Tooltip("Size (m) of the wrap zone box along this frame's X, up and forearm axes.")]
        [SerializeField] Vector3 zoneSize = new Vector3(0.2f, 0.12f, 0.2f);

        const int RollRings = 5;
        const int RollRimPoints = 12;
        const float SampleStep = 0.005f;
        const float MinZoneSize = 0.02f;
        const float SearchStep = Mathf.Deg2Rad;

        // Runtime wrap profile per frame-angle bin; rebuilt by PrepareProfile / UpdateProfile.
        readonly float[] wrapProfile = new float[ProfileBins];
        readonly float[] candidate = new float[ProfileBins];
        readonly List<Vector2> hullInput = new List<Vector2>();
        readonly List<Vector2> hull = new List<Vector2>();
        // Hot reload restores private fields but not the readonly profile arrays. Keep the cache flags transient too,
        // otherwise a restored prepared flag makes the newly zeroed profile look ready.
        [System.NonSerialized] bool prepared;
        [System.NonSerialized] bool widened;

        public int Turns => turns;
        public int PointsPerTurn => pointsPerTurn;
        public int WaypointCount => turns * pointsPerTurn + 1;
        public int LastWaypoint => turns * pointsPerTurn;
        public float StepAngle => TwoPi / pointsPerTurn;
        public float TotalAngle => turns * TwoPi;
        public float AdvancePerTurn => advancePerTurn;
        public float StartAxialOffset => startAxialOffset;
        public int WrapDirection => wrapDirection;
        public float LayerOffset => layerOffset;
        public float BandWidth => bandWidth;
        public float Clearance => clearance;
        public float SkinRadius => skinRadius;
        public bool HasSkinProfile => skinProfile != null && skinProfile.Length == ProfileBins;
        public bool IsWidenedForDressing => widened;
        public Vector3 ZoneLocalCentre => zoneCentre;
        public Vector3 ZoneSize => zoneSize;
        // Wrap zone centre (world) and its axes: this frame's rotation (x = Right, y = Up, z = Axis).
        public Vector3 ZoneCentre => Origin + Right * zoneCentre.x + Up * zoneCentre.y + Axis * zoneCentre.z;
        public Quaternion ZoneRotation => transform.rotation;

        public Vector3 Origin => transform.position;
        public Vector3 Axis => transform.forward;
        public Vector3 Up => transform.up;
        public Vector3 Right => transform.right;

        void Awake() => PrepareProfile(null);

        // Serialized profile/path edits must invalidate the derived cache without doing geometry work in OnValidate.
        void OnValidate()
        {
            prepared = false;
            widened = false;
        }

        // Wrap profile = convex hull of the skin profile pushed out by clearance and, when given, the dressing corners (world)
        // pushed out by clearance, so the strip passes over the pad instead of through it. Called without corners to clear.
        public void PrepareProfile(IReadOnlyList<Vector3> dressingCorners)
        {
            widened = ComputeProfile(dressingCorners, wrapProfile);
            prepared = true;
        }

        // Profile for the dressing's current corners; applied only when some bin differs by more than tolerance (m) or the
        // dressing is newly in or out, so hand jitter does not rebuild the bandage every frame. True when applied.
        public bool UpdateProfile(IReadOnlyList<Vector3> dressingCorners, float tolerance)
        {
            var nowWidened = ComputeProfile(dressingCorners, candidate);
            var changed = !prepared || nowWidened != widened;
            for (var i = 0; i < ProfileBins && !changed; i++)
                changed = Mathf.Abs(candidate[i] - wrapProfile[i]) > tolerance;
            if (!changed)
                return false;
            System.Array.Copy(candidate, wrapProfile, ProfileBins);
            widened = nowWidened;
            prepared = true;
            return true;
        }

        bool ComputeProfile(IReadOnlyList<Vector3> dressingCorners, float[] output)
        {
            hullInput.Clear();
            for (var i = 0; i < CircleHullPoints; i++)
            {
                var alpha = TwoPi * i / CircleHullPoints;
                var r = SkinAt(alpha) + clearance;
                hullInput.Add(new Vector2(Mathf.Cos(alpha), Mathf.Sin(alpha)) * r);
            }
            var withDressing = false;
            if (dressingCorners != null)
            {
                foreach (var corner in dressingCorners)
                {
                    var p = ToFrame2D(corner);
                    var length = p.magnitude;
                    if (length < 1e-4f)
                        continue;
                    hullInput.Add(p * ((length + clearance) / length));
                    withDressing = true;
                }
            }
            ConvexHull(hullInput, hull);
            for (var i = 0; i < ProfileBins; i++)
            {
                var alpha = TwoPi * i / ProfileBins;
                output[i] = RayToHull(new Vector2(Mathf.Cos(alpha), Mathf.Sin(alpha)), hull, SkinAt(alpha) + clearance);
            }
            return withDressing;
        }

        // ---------- Path geometry (theta = path angle from the start, radians) ----------

        public float AngleOf(int waypoint) => waypoint * StepAngle;
        public float FrameAngle(float theta) => startAngle * Mathf.Deg2Rad + Sign * theta;
        public float AxialAt(float theta) => startAxialOffset + advancePerTurn * theta / TwoPi;
        public float SurfaceRadius(float theta) => WrapProfileAt(FrameAngle(theta)) + layerOffset * Mathf.Max(0f, theta) / TwoPi;
        public Vector3 Radial(float alpha) => Mathf.Cos(alpha) * Up + Mathf.Sin(alpha) * Right;

        public Vector3 PointAt(float theta, float lift = 0f) =>
            Origin + Axis * AxialAt(theta) + Radial(FrameAngle(theta)) * (SurfaceRadius(theta) + lift);

        // Frame of the bandage surface at theta: tangent along the wrap direction, outward normal, width = cross(tangent, normal).
        public void FrameAt(float theta, float lift, out Vector3 position, out Vector3 tangent, out Vector3 normal)
        {
            position = PointAt(theta, lift);
            const float h = 0.01f;
            tangent = (PointAt(theta + h, lift) - PointAt(theta - h, lift)).normalized;
            normal = OutwardNormal(position, tangent);
        }

        // Normal of a surface around the axis at a point with this tangent, pointing away from the axis.
        public Vector3 OutwardNormal(Vector3 point, Vector3 tangent)
        {
            var n = Vector3.Cross(tangent, Axis);
            var radial = Vector3.ProjectOnPlane(point - Origin, Axis);
            if (Vector3.Dot(n, radial) < 0f)
                n = -n;
            n = Vector3.ProjectOnPlane(n, tangent);
            return n.sqrMagnitude > 1e-8f ? n.normalized : radial.normalized;
        }

        // Path angle reached after an arc length s (m) from theta along the surface.
        public float AngleAfter(float theta, float arc) => theta + arc / Mathf.Max(0.005f, SurfaceRadius(theta));

        // ---------- Locating the roll ----------

        // Path angle (wrapped to -pi..pi from the start), axial position and distance from the axis of a world point.
        public void Locate(Vector3 point, out float pathAngle, out float axial, out float distance, out float frameAngle)
        {
            var d = point - Origin;
            axial = Vector3.Dot(d, Axis);
            var r = d - Axis * axial;
            distance = r.magnitude;
            frameAngle = Mathf.Atan2(Vector3.Dot(r, Right), Vector3.Dot(r, Up));
            pathAngle = WrapAngle(Sign * (frameAngle - startAngle * Mathf.Deg2Rad));
        }

        public bool IsInsideArm(Vector3 point)
        {
            Locate(point, out _, out _, out var distance, out var alpha);
            return distance < SkinAt(alpha) + insideMargin;
        }

        // Roll centre (or any point) inside the wrap zone box and outside the outer boundary (skin, dressing hull and every
        // layer): a point in the arm is never in the zone, even if the box is set to reach into it.
        public bool InWrapZone(Vector3 point)
        {
            var d = point - Origin;
            var local = new Vector3(Vector3.Dot(d, Right), Vector3.Dot(d, Up), Vector3.Dot(d, Axis)) - zoneCentre;
            var half = zoneSize * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z && IsClearOfOuter(point, 0f);
        }

        // ---------- Roll body and loose strip (development clearance geometry) ----------

        // Boundary for the roll body: the wrap profile (skin + clearance, dressing hull) plus every layer the path can lay, so a
        // roll clear of it does not sink into the skin, the dressing or any bandage layer there.
        public float OuterRadiusAt(float alpha) => WrapProfileAt(alpha) + layerOffset * turns;

        public bool IsClearOfOuter(Vector3 point, float margin)
        {
            Locate(point, out _, out _, out var distance, out var alpha);
            return distance >= OuterRadiusAt(alpha) + margin;
        }

        // Roll as a cylinder (centre, axis, radius, half width): 5 cross-sections along the axis, each its centre and 12 rim
        // points, all clear of the outer boundary by margin. Section spacing 0.02 m and 30 deg rim steps keep the gap between
        // samples about 1 mm for the 0.03 m x 0.08 m roll (below the default margin). A sampled approximation, not a mesh test.
        public bool RollClear(Vector3 centre, Vector3 axis, float radius, float halfWidth, float margin)
        {
            var a = axis.sqrMagnitude > 1e-8f ? axis.normalized : Axis;
            var u = Vector3.Cross(a, Mathf.Abs(Vector3.Dot(a, Up)) < 0.9f ? Up : Right).normalized;
            var v = Vector3.Cross(a, u);
            for (var ring = 0; ring < RollRings; ring++)
            {
                var c = centre + a * Mathf.Lerp(-halfWidth, halfWidth, ring / (RollRings - 1f));
                if (!IsClearOfOuter(c, margin))
                    return false;
                for (var k = 0; k < RollRimPoints; k++)
                {
                    var angle = TwoPi * k / RollRimPoints;
                    if (!IsClearOfOuter(c + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * radius, margin))
                        return false;
                }
            }
            return true;
        }

        // Loose strip surface point: ahead of the wrap end on the next layer position, behind it one layer above the wrap.
        public Vector3 FreePoint(float theta0, float theta, float axial, float lift)
        {
            var radius = SurfaceRadius(theta) + (theta < theta0 ? layerOffset : 0f) + lift;
            return Origin + Axis * axial + Radial(FrameAngle(theta)) * radius;
        }

        // Target lies on the outer side of the loose strip surface at theta (so, the profile being convex, the straight run from
        // there to the target stays outside it).
        public bool FreeVisible(float theta0, float theta, Vector3 target, float axial, float lift)
        {
            var point = FreePoint(theta0, theta, axial, lift);
            var tangent = FreePoint(theta0, theta + 0.01f, axial, lift) - FreePoint(theta0, theta - 0.01f, axial, lift);
            return Vector3.Dot(target - point, OutwardNormal(point, tangent.normalized)) > 0f;
        }

        // Where the loose strip leaves the arm toward target: the wrap end if visible there, otherwise the first visible surface
        // point stepping 1 deg forward and backward (nearer first). False when none (target inside the profile).
        public bool TryTangent(float theta0, Vector3 target, float lift, out float thetaT)
        {
            var axial = AxialAt(theta0);
            thetaT = theta0;
            if (FreeVisible(theta0, theta0, target, axial, lift))
                return true;
            for (var k = 1; k * SearchStep <= Mathf.PI; k++)
            {
                thetaT = theta0 + k * SearchStep;
                if (FreeVisible(theta0, thetaT, target, axial, lift))
                    return true;
                thetaT = theta0 - k * SearchStep;
                if (FreeVisible(theta0, thetaT, target, axial, lift))
                    return true;
            }
            thetaT = theta0;
            return false;
        }

        // Straight run clear of the wrap profile (skin + clearance and dressing hull, without layers), sampled every 5 mm, each
        // sample at least the profile radius minus tolerance from the axis.
        public bool SegmentClearOfWrap(Vector3 a, Vector3 b, float tolerance)
        {
            var steps = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) / SampleStep), 1, 256);
            for (var i = 0; i <= steps; i++)
            {
                Locate(Vector3.Lerp(a, b, (float)i / steps), out _, out _, out var distance, out var alpha);
                if (distance < WrapProfileAt(alpha) - tolerance)
                    return false;
            }
            return true;
        }

        // ---------- Profiles ----------

        public float SkinAt(float alpha)
        {
            if (!HasSkinProfile)
                return skinRadius;
            return Sample(skinProfile, alpha);
        }

        public float WrapProfileAt(float alpha)
        {
            if (!prepared)
                PrepareProfile(null);
            return Sample(wrapProfile, alpha);
        }

        static float Sample(float[] bins, float alpha)
        {
            var x = Mathf.Repeat(alpha, TwoPi) / TwoPi * ProfileBins;
            var i = Mathf.FloorToInt(x) % ProfileBins;
            var j = (i + 1) % ProfileBins;
            return Mathf.Lerp(bins[i], bins[j], x - Mathf.Floor(x));
        }

        Vector2 ToFrame2D(Vector3 world)
        {
            var d = world - Origin;
            return new Vector2(Vector3.Dot(d, Up), Vector3.Dot(d, Right));
        }

        float Sign => wrapDirection >= 0 ? 1f : -1f;

        public static float WrapAngle(float angle) => Mathf.Repeat(angle + Mathf.PI, TwoPi) - Mathf.PI;
        public static float PositiveAngle(float angle) => Mathf.Repeat(angle, TwoPi);

        // Andrew's monotone chain; output counter-clockwise without the closing point.
        static void ConvexHull(List<Vector2> points, List<Vector2> output)
        {
            output.Clear();
            points.Sort((p, q) => p.x != q.x ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y));
            for (var pass = 0; pass < 2; pass++)
            {
                var start = output.Count;
                for (var k = 0; k < points.Count; k++)
                {
                    var p = points[pass == 0 ? k : points.Count - 1 - k];
                    while (output.Count >= start + 2 && Cross(output[output.Count - 2], output[output.Count - 1], p) <= 0f)
                        output.RemoveAt(output.Count - 1);
                    output.Add(p);
                }
                output.RemoveAt(output.Count - 1);
            }
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        // Distance from the origin along direction to the hull boundary (the origin is inside the hull).
        static float RayToHull(Vector2 direction, List<Vector2> polygon, float fallback)
        {
            var best = 0f;
            for (var i = 0; i < polygon.Count; i++)
            {
                var p = polygon[i];
                var e = polygon[(i + 1) % polygon.Count] - p;
                var denominator = direction.x * e.y - direction.y * e.x;
                if (Mathf.Abs(denominator) < 1e-9f)
                    continue;
                var t = (p.x * e.y - p.y * e.x) / denominator;
                var s = (p.x * direction.y - p.y * direction.x) / denominator;
                if (t > 0f && s >= -1e-5f && s <= 1f + 1e-5f)
                    best = Mathf.Max(best, t);
            }
            return best > 0f ? best : fallback;
        }

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (skinProfile != null && skinProfile.Length != 0 && skinProfile.Length != ProfileBins)
                problems.Add($"skinProfile has {skinProfile.Length} bins (0 or {ProfileBins})");
            if (HasSkinProfile)
            {
                foreach (var r in skinProfile)
                {
                    if (!IsFinite(r) || r <= 0f)
                    {
                        problems.Add("skinProfile radius not finite and above zero");
                        break;
                    }
                }
            }
            if (!IsFinite(skinRadius) || skinRadius <= 0f || !IsFinite(clearance) || clearance < 0f)
                problems.Add("skin radius or clearance");
            if (turns < 1 || pointsPerTurn < 4)
                problems.Add("turns or points per turn");
            if (!IsFinite(advancePerTurn) || advancePerTurn < 0f || !IsFinite(startAxialOffset) || !IsFinite(startAngle))
                problems.Add("advance, start offset or start angle");
            if (wrapDirection != 1 && wrapDirection != -1)
                problems.Add("wrapDirection must be +1 or -1");
            if (!IsFinite(layerOffset) || layerOffset < 0f || !IsFinite(bandWidth) || bandWidth <= 0f)
                problems.Add("layer offset or band width");
            if (!IsFinite(insideMargin) || insideMargin < 0f)
                problems.Add("inside margin");
            if (!IsFinite(zoneCentre.x) || !IsFinite(zoneCentre.y) || !IsFinite(zoneCentre.z) || !IsFinite(zoneSize.x) || !IsFinite(zoneSize.y) ||
                !IsFinite(zoneSize.z) || zoneSize.x < MinZoneSize || zoneSize.y < MinZoneSize || zoneSize.z < MinZoneSize)
                problems.Add($"wrap zone centre finite and size at least {MinZoneSize} m on each axis");
            error = problems.Count > 0 ? $"PressureBandageWrapPath '{name}' invalid: {string.Join(", ", problems)}." : null;
            return error == null;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        // Editor aid for adjusting the zone values (selection gizmo only).
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.55f, 0.85f, 1f, 1f);
            Gizmos.matrix = Matrix4x4.TRS(ZoneCentre, ZoneRotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, zoneSize);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
