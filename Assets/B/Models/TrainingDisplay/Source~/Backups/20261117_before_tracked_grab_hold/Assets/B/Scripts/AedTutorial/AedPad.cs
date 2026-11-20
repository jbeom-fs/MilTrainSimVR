using System;
using System.Collections.Generic;
using MilTrainSimVR.B.Interaction;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.B.AedTutorial
{
    // One AED pad: grab state from the real XR selection, attachment state, highlight and cable visuals.
    // On attach only the flat visual child and its collider are hidden; this logical root stays active.
    // Reports Grabbed/Released in pairs; tutorial progression decides what they mean.
    // Grab near or far by ray: a far grab brings the pad to the hand with its rotation kept (XRI dynamic attach and attach ease-in).
    // A pad let go while it is still being brought (GrabBring, read in LateUpdate) never attaches; it is grabbed again.
    public sealed class AedPad : MonoBehaviour
    {
        [SerializeField] string padId;
        [SerializeField] XRGrabInteractable grab;
        [SerializeField] Rigidbody body;
        [SerializeField] GameObject highlight;
        [SerializeField] Renderer cable;
        [Tooltip("Center of the adhesive face. Forward points out of the adhesive face (toward the skin when attached).")]
        [SerializeField] Transform attachPoint;
        [Tooltip("Flat pad visual child hidden once attached (the patient shows its conforming pad instead).")]
        [SerializeField] GameObject visual;
        [Tooltip("Grab collider disabled once attached so the hidden pad never blocks rays or physics.")]
        [SerializeField] Collider padCollider;
        XRGrabInteractable subscribedGrab;
        bool grabbed;
        // Still being brought to the hand, as of the last LateUpdate of this grab; and whether the last release came during it.
        bool bringing;
        bool releasedWhileBringing;
        // Scene layout and settings before any interaction; ResetForNewRun returns to these and never overwrites them.
        Transform initialParent;
        Vector3 initialLocalPosition;
        Quaternion initialLocalRotation;
        Vector3 initialLocalScale;
        bool initialKinematic;
        RigidbodyInterpolation initialInterpolation;
        bool initialThrowOnDetach;
        InteractionLayerMask initialInteractionLayers;
        bool initialCableEnabled;

        public string PadId => padId;
        public bool IsGrabbed => grabbed;
        public bool IsAttached { get; private set; }
        public PadAttachAnchor AttachedAnchor { get; private set; }
        public Transform AttachPoint => attachPoint;
        public GameObject Visual => visual;
        public InteractorHandedness GrabbingHand { get; private set; } = InteractorHandedness.None;
        // Held and still being brought to the hand after a far grab; and whether the last release came while it was.
        public bool IsBringing => grabbed && bringing;
        public bool ReleasedWhileBringing => releasedWhileBringing;
        public event Action<AedPad> Grabbed;
        // userRelease is false for canceled selections (disable, unregister, scene unload).
        public event Action<AedPad, bool> Released;

        void Awake()
        {
            initialParent = transform.parent;
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
            initialLocalScale = transform.localScale;
            if (body != null)
            {
                initialKinematic = body.isKinematic;
                initialInterpolation = body.interpolation;
            }

            if (grab != null)
            {
                initialThrowOnDetach = grab.throwOnDetach;
                initialInteractionLayers = grab.interactionLayers;
            }

            initialCableEnabled = cable != null && cable.enabled;
        }

        void OnEnable()
        {
            if (subscribedGrab == null && grab != null)
            {
                subscribedGrab = grab;
                subscribedGrab.selectEntered.AddListener(OnSelectEntered);
                subscribedGrab.selectExited.AddListener(OnSelectExited);
            }

            // Resync with a selection that survived while only this component was disabled.
            if (!grabbed && grab != null && grab.isSelected)
                ReportGrabbed(grab.firstInteractorSelecting);
            ApplyAttachedVisuals();
        }

        void OnDisable()
        {
            // Drop the real XR selection too, so internal and XRI state cannot drift apart.
            if (grab != null && grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            ReportReleased(false);
            if (subscribedGrab != null)
            {
                subscribedGrab.selectEntered.RemoveListener(OnSelectEntered);
                subscribedGrab.selectExited.RemoveListener(OnSelectExited);
            }

            subscribedGrab = null;
        }

        public void SetGrabEnabled(bool enabled)
        {
            if (grab != null)
                grab.enabled = enabled && !IsAttached;
        }

        public void SetHighlight(bool visible)
        {
            if (highlight != null)
                highlight.SetActive(visible && !IsAttached);
        }

        void LateUpdate()
        {
            // After XRI's update of this frame (GrabBring).
            if (grabbed)
                bringing = GrabBring.IsBringing(grab);
        }

        // Snaps the adhesive face onto the anchor and fixes the pad. Only after a real release with a valid check, never after a
        // release while the pad was still being brought to the hand.
        public bool TryAttach(PadAttachAnchor anchor)
        {
            if (IsAttached || grabbed || releasedWhileBringing || anchor == null || anchor.Evaluate(this) != PadAttachCheck.Valid)
                return false;

            var target = anchor.AttachTarget;
            var localRotation = Quaternion.Inverse(transform.rotation) * attachPoint.rotation;
            var localOffset = Quaternion.Inverse(transform.rotation) * (attachPoint.position - transform.position);
            var rotation = target.rotation * Quaternion.Inverse(localRotation);
            var position = target.position - rotation * localOffset;

            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = true;
            // Interpolation would pull the transform back toward the pre-snap physics pose.
            body.interpolation = RigidbodyInterpolation.None;
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            // XRI finishes the drop in its late update; keep it from throwing or reselecting the fixed pad.
            grab.throwOnDetach = false;
            grab.interactionLayers = 0;
            transform.SetParent(anchor.transform, true);
            IsAttached = true;
            AttachedAnchor = anchor;
            // Same call, same frame: hide the flat pad, then the anchor shows the patient's conforming pad.
            SetHighlight(false);
            ApplyAttachedVisuals();
            anchor.MarkAttached(this);
            return true;
        }

        // Undoes a run: drops any selection, then restores everything TryAttach and grabbing changed.
        // The anchor clears its own occupancy; progression must ignore the canceled release this may report.
        public void ResetForNewRun()
        {
            if (grab != null && grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            ReportReleased(false);
            releasedWhileBringing = false;

            IsAttached = false;
            AttachedAnchor = null;
            transform.SetParent(initialParent, false);
            transform.localPosition = initialLocalPosition;
            transform.localRotation = initialLocalRotation;
            transform.localScale = initialLocalScale;

            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.isKinematic = initialKinematic;
                body.interpolation = initialInterpolation;
                body.position = transform.position;
                body.rotation = transform.rotation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            if (grab != null)
            {
                grab.throwOnDetach = initialThrowOnDetach;
                grab.interactionLayers = initialInteractionLayers;
            }

            if (cable != null)
                cable.enabled = initialCableEnabled;
            SetHighlight(false);
            ApplyAttachedVisuals();
        }

        void ApplyAttachedVisuals()
        {
            if (visual != null)
                visual.SetActive(!IsAttached);
            if (padCollider != null)
                padCollider.enabled = !IsAttached;
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(padId))
                missing.Add(nameof(padId));
            if (grab == null)
                missing.Add(nameof(grab));
            if (body == null)
                missing.Add(nameof(body));
            if (highlight == null)
                missing.Add(nameof(highlight));
            if (attachPoint == null)
                missing.Add(nameof(attachPoint));
            if (visual == null)
                missing.Add(nameof(visual));
            if (padCollider == null)
                missing.Add(nameof(padCollider));

            error = missing.Count > 0 ? $"AedPad '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && (visual == gameObject || !visual.transform.IsChildOf(transform)))
                error = $"AedPad '{name}': visual must be a child of the pad, not the pad root.";
            if (error == null && padCollider.gameObject != gameObject)
                error = $"AedPad '{name}': padCollider must be the grab collider on the pad root.";
            return error == null;
        }

        void OnSelectEntered(SelectEnterEventArgs args) => ReportGrabbed(args.interactorObject);

        void ReportGrabbed(IXRSelectInteractor interactor)
        {
            if (grabbed || IsAttached)
                return;

            grabbed = true;
            // Until this grab's first LateUpdate has measured it, the pad counts as still being brought.
            bringing = true;
            releasedWhileBringing = false;
            GrabbingHand = interactor != null ? interactor.handedness : InteractorHandedness.None;
            // The static cable mesh no longer matches a moved pad; rope physics is out of scope.
            if (cable != null)
                cable.enabled = false;
            Grabbed?.Invoke(this);
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (grab.isSelected)
                return;

            // Report first: a valid real release attaches synchronously and must stay kinematic.
            ReportReleased(!args.isCanceled);

            // Pads start kinematic so they keep the modeled pose; once released unattached they fall normally.
            // XRGrabInteractable restores its cached kinematic flag before this event, so this wins.
            if (!IsAttached)
                body.isKinematic = false;
        }

        void ReportReleased(bool userRelease)
        {
            if (!grabbed)
                return;

            grabbed = false;
            releasedWhileBringing = bringing;
            bringing = false;
            GrabbingHand = InteractorHandedness.None;
            Released?.Invoke(this, userRelease);
        }
    }
}
