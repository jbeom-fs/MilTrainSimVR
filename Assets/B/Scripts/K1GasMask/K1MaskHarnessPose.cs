using System;
using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.B.K1GasMask
{
    // Harness pose along the validated donning route of K1_Validation.json as one progress value:
    // 0 Loose_Front, 1 Spread, 2 Lift_Above, 3 Over_Crown, 4 Behind_Head, 5 Seated.
    // Samples hold every driven bone (RearCenter, RearGrab_L/R and all 16 spans) as parent-relative local TRS, already
    // retargeted onto the imported rig (positive span length scale included), so the spans stay connected
    // because the whole sample is applied, not because two grab bones moved. Runtime never reads source files.
    // The only writer of these bones after extraction. HarnessRoot and the Attach bones are never driven.
    // Also owns the harness rendering bounds: precomputed from the supported poses and stored here, because a
    // custom Renderer.localBounds is not saved into scenes or prefabs; OnEnable applies it to the renderer (Unity docs,
    // Renderer.localBounds). Stored in the space Unity reads a skinned renderer's localBounds in: the root bone if set,
    // else the renderer, which must be body-fixed so only the mask's own motion moves the bounds.
    public sealed class K1MaskHarnessPose : MonoBehaviour
    {
        public const float RouteEnd = 5f;

        [Serializable]
        public sealed class Sample
        {
            public float progress;
            public Vector3[] localPositions = Array.Empty<Vector3>();
            public Quaternion[] localRotations = Array.Empty<Quaternion>();
            public Vector3[] localScales = Array.Empty<Vector3>();
        }

        [Tooltip("Driven harness bones: RearCenter, RearGrab_L/R and the 16 span bones.")]
        [SerializeField] Transform[] bones = Array.Empty<Transform>();
        [Tooltip("Ascending progress from 0 to 5, from the route samples.")]
        [SerializeField] Sample[] samples = Array.Empty<Sample>();
        [Tooltip("Part_Harness skinned renderer that receives the stored rendering bounds.")]
        [SerializeField] SkinnedMeshRenderer harnessRenderer;
        [Tooltip("Space of the stored bounds: the renderer's root bone if set, else the renderer itself. Body-fixed.")]
        [SerializeField] Transform boundsSpace;
        [Tooltip("Deformed harness range of the supported poses plus margin, in boundsSpace.")]
        [SerializeField] Bounds renderBounds;

        Vector3[] capturedPositions = Array.Empty<Vector3>();
        Quaternion[] capturedRotations = Array.Empty<Quaternion>();
        Vector3[] capturedScales = Array.Empty<Vector3>();

        public float Progress { get; private set; }
        public IReadOnlyList<Transform> Bones => bones;
        public IReadOnlyList<Sample> Samples => samples;

        // Runs on first enable and on every re-enable. Only the renderer's bounds change; bone pose and wear state are untouched.
        void OnEnable()
        {
            if (TryValidateRenderBounds(out var error))
                harnessRenderer.localBounds = renderBounds;
            else
                Debug.LogError(error + " Harness rendering bounds not applied.", this);
        }

        public void ApplyRoute(float progress)
        {
            Progress = Mathf.Clamp(progress, 0f, RouteEnd);
            var i = 1;
            while (i < samples.Length - 1 && samples[i].progress < Progress)
                i++;

            var a = samples[i - 1];
            var b = samples[i];
            var t = Mathf.InverseLerp(a.progress, b.progress, Progress);
            for (var k = 0; k < bones.Length; k++)
                Set(k, Vector3.Lerp(a.localPositions[k], b.localPositions[k], t), Quaternion.Slerp(a.localRotations[k], b.localRotations[k], t),
                    Vector3.Lerp(a.localScales[k], b.localScales[k], t));
        }

        // Remembers the current bone pose (the folded storage pose before unfolding).
        public void CaptureCurrent()
        {
            capturedPositions = new Vector3[bones.Length];
            capturedRotations = new Quaternion[bones.Length];
            capturedScales = new Vector3[bones.Length];
            for (var k = 0; k < bones.Length; k++)
            {
                capturedPositions[k] = bones[k].localPosition;
                capturedRotations[k] = bones[k].localRotation;
                capturedScales[k] = bones[k].localScale;
            }
        }

        // Direct per-bone blend from the captured pose to the route start (Loose_Front). Only the end pose is validated data.
        public void ApplyCapturedToRouteStart(float t)
        {
            t = Mathf.Clamp01(t);
            var start = samples[0];
            for (var k = 0; k < bones.Length; k++)
                Set(k, Vector3.Lerp(capturedPositions[k], start.localPositions[k], t), Quaternion.Slerp(capturedRotations[k], start.localRotations[k], t),
                    Vector3.Lerp(capturedScales[k], start.localScales[k], t));
            Progress = 0f;
        }

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (bones == null || bones.Length == 0 || Array.IndexOf(bones, null) >= 0)
                problems.Add("bones missing");
            if (samples == null || samples.Length < 2)
            {
                problems.Add("fewer than 2 samples");
            }
            else
            {
                if (samples[0].progress != 0f || samples[samples.Length - 1].progress != RouteEnd)
                    problems.Add("samples must start at 0 and end at " + RouteEnd);
                for (var i = 0; i < samples.Length; i++)
                {
                    var s = samples[i];
                    if (i > 0 && s.progress <= samples[i - 1].progress)
                        problems.Add($"sample {i} not ascending");
                    if (bones != null && (s.localPositions == null || s.localPositions.Length != bones.Length || s.localRotations == null
                                          || s.localRotations.Length != bones.Length || s.localScales == null || s.localScales.Length != bones.Length))
                        problems.Add($"sample {i} bone count mismatch");
                }
            }

            if (!TryValidateRenderBounds(out var boundsError))
                problems.Add(boundsError);

            error = problems.Count > 0 ? $"K1MaskHarnessPose '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }

        // Stored data only (no dependence on whether OnEnable already applied it), so other components may check it in Awake.
        public bool TryValidateRenderBounds(out string error)
        {
            error = null;
            if (harnessRenderer == null || boundsSpace == null)
                error = "rendering bounds renderer or space missing";
            else if (boundsSpace != (harnessRenderer.rootBone != null ? harnessRenderer.rootBone : harnessRenderer.transform))
                error = $"rendering bounds space {boundsSpace.name} is not the renderer's root bone (or renderer)";
            else if (bones != null && Array.Exists(bones, b => b != null && (boundsSpace == b || boundsSpace.IsChildOf(b))))
                error = $"rendering bounds space {boundsSpace.name} is a driven harness bone";
            else if (!IsFinite(renderBounds.center) || !IsFinite(renderBounds.size)
                     || renderBounds.size.x <= 0f || renderBounds.size.y <= 0f || renderBounds.size.z <= 0f)
                error = "rendering bounds not finite or not positive in size";
            return error == null;
        }

        static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        void Set(int k, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            bones[k].localPosition = position;
            bones[k].localRotation = rotation;
            bones[k].localScale = scale;
        }
    }
}
