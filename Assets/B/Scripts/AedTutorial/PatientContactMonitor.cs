using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace MilTrainSimVR.B.AedTutorial
{
    public enum PatientContactState
    {
        Unavailable = 0,
        Touching = 1,
        Clear = 2
    }

    // Why a hand could not be checked: the user's controller tracking, or the patient-side check itself.
    public enum ContactCheckIssue
    {
        None = 0,
        HandTracking = 1,
        PatientCheck = 2
    }

    // Probe sphere that follows one tracked controller.
    [Serializable]
    public struct HandContactProbe
    {
        [Tooltip("Controller pose driver of the B scene's XR Rig instance. Its tracking state decides whether the hand can be checked.")]
        public TrackedPoseDriver controller;
        [Tooltip("Probe center in the controller's local space, meters.")]
        public Vector3 localOffset;
        [Tooltip("Probe radius in meters.")]
        public float radius;
    }

    // Training approximation of "hands off the patient": a sphere around each tracked controller against the registered patient colliders.
    // It does not measure the user's whole body or bystanders. UI rays never count; only controller positions do.
    // Queries run on demand against current poses, so an overlap present from the start or a collider disabled meanwhile is always seen.
    // Reports state only; tutorial progression decides what a result means.
    public sealed class PatientContactMonitor : MonoBehaviour
    {
        const int OverlapBufferSize = 32;

        [SerializeField] HandContactProbe leftHand = new HandContactProbe { radius = 0.08f };
        [SerializeField] HandContactProbe rightHand = new HandContactProbe { radius = 0.08f };
        [Tooltip("Patient body colliders only. Desk, floor, AED, pads and guide markers must not be listed.")]
        [SerializeField] Collider[] patientColliders = Array.Empty<Collider>();
        readonly Collider[] overlaps = new Collider[OverlapBufferSize];

        public PatientContactState Left { get; private set; }
        public PatientContactState Right { get; private set; }
        // First reason a hand could not be checked in the last evaluation; null when both hands were checked.
        public string Issue { get; private set; }
        public ContactCheckIssue IssueKind { get; private set; }

        // Both hands must be validly clear for Clear. Any touch wins over an unavailable hand.
        public PatientContactState Evaluate()
        {
            Issue = null;
            IssueKind = ContactCheckIssue.None;
            var patientIssue = !isActiveAndEnabled ? "contact monitor disabled" : PatientIssue();
            Left = EvaluateHand(leftHand, "left", patientIssue);
            Right = EvaluateHand(rightHand, "right", patientIssue);

            if (Left == PatientContactState.Touching || Right == PatientContactState.Touching)
                return PatientContactState.Touching;
            return Left == PatientContactState.Clear && Right == PatientContactState.Clear
                ? PatientContactState.Clear
                : PatientContactState.Unavailable;
        }

        PatientContactState EvaluateHand(HandContactProbe probe, string hand, string patientIssue)
        {
            var issue = patientIssue ?? TrackingIssue(probe);
            var kind = patientIssue != null ? ContactCheckIssue.PatientCheck : ContactCheckIssue.HandTracking;
            if (issue == null)
            {
                var center = probe.controller.transform.TransformPoint(probe.localOffset);
                var count = Physics.OverlapSphereNonAlloc(center, probe.radius, overlaps, PatientLayers(), QueryTriggerInteraction.Ignore);
                var touching = false;
                for (var i = 0; i < count; i++)
                    touching |= Array.IndexOf(patientColliders, overlaps[i]) >= 0;
                Array.Clear(overlaps, 0, count);
                foreach (var patientCollider in patientColliders)
                    touching |= IsUnderSurface(center, patientCollider);

                // A full buffer may have dropped the patient collider; never report that as clear.
                if (touching)
                    return PatientContactState.Touching;
                if (count < overlaps.Length)
                    return PatientContactState.Clear;
                issue = "overlap buffer full";
                kind = ContactCheckIssue.PatientCheck;
            }

            if (Issue == null)
            {
                Issue = $"{hand} hand: {issue}";
                IssueKind = kind;
            }

            return PatientContactState.Unavailable;
        }

        // A non-convex mesh only overlaps at its surface, so a probe pushed deep into the body would touch no triangle.
        // Patient surface directly above the probe (the patient lies face up) counts as touching.
        static bool IsUnderSurface(Vector3 center, Collider patientCollider)
        {
            var bounds = patientCollider.bounds;
            if (!bounds.Contains(center))
                return false;

            var top = bounds.max.y + 0.01f;
            return patientCollider.Raycast(new Ray(new Vector3(center.x, top, center.z), Vector3.down), out _, top - center.y);
        }

        // A transform alone is not tracking: the pose driver must be live and report a tracked position.
        static string TrackingIssue(HandContactProbe probe)
        {
            var driver = probe.controller;
            if (driver == null)
                return "controller reference missing";
            if (!driver.isActiveAndEnabled)
                return "controller inactive";
            if (!IsValidProbe(probe))
                return "invalid probe offset or radius";

            var tracking = driver.trackingStateInput.action;
            if (tracking == null || !tracking.enabled || tracking.controls.Count == 0)
                return "tracking state input unavailable";
            if (((InputTrackingState)tracking.ReadValue<int>() & InputTrackingState.Position) == 0)
                return "position not tracked";

            var position = driver.positionInput.action;
            if (position == null || !position.enabled || position.controls.Count == 0)
                return "position input unavailable";
            return null;
        }

        string PatientIssue()
        {
            if (patientColliders == null || patientColliders.Length == 0)
                return "no patient collider";

            foreach (var patientCollider in patientColliders)
            {
                if (patientCollider == null)
                    return "patient collider missing";
                if (!patientCollider.enabled || !patientCollider.gameObject.activeInHierarchy)
                    return $"patient collider '{patientCollider.name}' disabled";
            }

            return null;
        }

        int PatientLayers()
        {
            var mask = 0;
            foreach (var patientCollider in patientColliders)
                mask |= 1 << patientCollider.gameObject.layer;
            return mask;
        }

        // Configuration check before training starts; tracking is checked at runtime by Evaluate.
        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (leftHand.controller == null)
                missing.Add($"{nameof(leftHand)}.{nameof(HandContactProbe.controller)}");
            if (rightHand.controller == null)
                missing.Add($"{nameof(rightHand)}.{nameof(HandContactProbe.controller)}");
            if (patientColliders == null || patientColliders.Length == 0)
                missing.Add(nameof(patientColliders));

            error = missing.Count > 0 ? $"PatientContactMonitor '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && leftHand.controller == rightHand.controller)
                error = $"PatientContactMonitor '{name}': left and right hands use the same controller.";
            if (error == null && (!IsValidProbe(leftHand) || !IsValidProbe(rightHand)))
                error = $"PatientContactMonitor '{name}': probe radius must be a positive finite value and offsets finite.";

            for (var i = 0; error == null && i < patientColliders.Length; i++)
            {
                var patientCollider = patientColliders[i];
                if (patientCollider == null)
                    error = $"PatientContactMonitor '{name}': {nameof(patientColliders)}[{i}] is missing.";
                else if (patientCollider.isTrigger)
                    error = $"PatientContactMonitor '{name}': patient collider '{patientCollider.name}' must not be a trigger.";
                else if (patientCollider.GetComponentInParent<AedPad>(true) != null)
                    error = $"PatientContactMonitor '{name}': '{patientCollider.name}' belongs to an AED pad, not the patient.";
                else if (Array.IndexOf(patientColliders, patientCollider) != i)
                    error = $"PatientContactMonitor '{name}': patient collider '{patientCollider.name}' is listed twice.";
            }

            return error == null;
        }

        static bool IsValidProbe(HandContactProbe probe) =>
            IsFinite(probe.radius) && probe.radius > 0f &&
            IsFinite(probe.localOffset.x) && IsFinite(probe.localOffset.y) && IsFinite(probe.localOffset.z);

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
