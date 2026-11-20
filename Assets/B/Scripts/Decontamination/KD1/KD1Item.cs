using System;
using System.Collections.Generic;
using MilTrainSimVR.B.Interaction;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
using UnityEngine.XR.Interaction.Toolkit.Utilities;

namespace MilTrainSimVR.B.Decontamination.KD1
{
    public enum KD1ItemKind
    {
        OuterPack = 0,
        Packet = 1,
        Pad = 2
    }

    public enum KD1ItemState
    {
        // In its home slot inside the container (packet: the outer pack; pad: its packet), following the container.
        Stored = 0,
        // Held and being pulled out along the container's mouth, still in the container's frame.
        Extracting = 1,
        // Out of its container (the outer pack is always free): held, or resting with gravity.
        Free = 2,
        // Pad only: fitted on a hand mount.
        Worn = 3
    }

    // One KD-1 piece (the outer pack, a packet or a pad) on its stable interaction root. This object keeps the XRGrabInteractable,
    // the Rigidbody and this component in every state; the state models and collider sets are children that are switched, never
    // this root, so a held root is never removed or disabled by a change of state.
    //
    // Children:
    // - baseModel: the sealed (outer pack, packet) or packed (pad) FBX instance; changedModel: the open or the fitted instance.
    //   One of them is shown, or none while a content is stored in a sealed container. Both at this object's identity, so a
    //   change keeps the root and every stored content where they are. The open models' separated seal strips (photographed
    //   beside the body) are switched off in the prefab and not used.
    // - storedColliders (packet, pad): the mouth-side part of the content, the only grab region while stored or being pulled,
    //   and only while its container is open; baseColliders: the whole sealed or packed body; changedColliders: the open body
    //   (a back-end grab box plus a physics-only body box, so the mouth side is left to the content's own grab region) or the
    //   fitted pad's working sheet (kept in the model setup; switched off while the pad is fitted). Only the colliders listed in
    //   the grab interactable select it.
    // - slot (packet, pad): this content's home pose, a child of its container; a stored content is its child at identity.
    //
    // Physics: free and not held, dynamic with gravity; stored, being pulled or worn, kinematic without gravity and excluded from
    // every contact (Rigidbody.excludeLayers), so a content never pushes its container or the hand. XRI owns kinematic and gravity
    // while this is held; the equipment applies the rest physics after a release.
    //
    // Held pose (the only grab transformer of this grab, Instantaneous): free, a FreeHold of the grab (the item keeps its rotation
    // and turns only with the hand; a far grab, or the same grab at the moment the content comes out, brings it to the hand
    // smoothly, a near one holds it where it is; a hand over, tracking return and the end of a hold re-take the reference from the
    // current pose, so nothing jumps); being pulled, the slot pose moved along the container mouth (Anchor_Open +Z) by the hand
    // point's motion in the container's frame, so it follows a container held in the other hand. The hand point is a RelativeHand
    // (starts at the item, moves only by the controller's motion in the tracking space, carried with the rig while the container
    // is held), so near and far pulls give the same amount and locomotion never pulls; while the container itself is being
    // brought to the other hand the pull takes a new baseline. A hand that is not tracked or not focused (or a locked equipment)
    // holds it still and drops its baseline: motion during the loss and the jump when tracking returns are never used. The content
    // is out once every corner of its stored outline is exitMargin past the mouth plane; the equipment then makes it free.
    //
    // KD1Equipment owns every state change; this component only applies the state it is given.
    [DisallowMultipleComponent]
    public sealed class KD1Item : XRBaseGrabTransformer
    {
        // Extraction may run this far (m) past the exit distance before it stops.
        const float ExtractionOvershoot = 0.03f;

        [SerializeField] KD1ItemKind kind;
        [Tooltip("Packet and pad number 0..3 (a pad has the number of its packet); 0 for the outer pack.")]
        [SerializeField] int number;
        [SerializeField] XRGrabInteractable grab;
        [SerializeField] Rigidbody body;
        [Tooltip("Sealed (outer pack, packet) or packed (pad) model instance.")]
        [SerializeField] GameObject baseModel;
        [Tooltip("Open (outer pack, packet) or fitted (pad) model instance.")]
        [SerializeField] GameObject changedModel;
        [Tooltip("Colliders of the sealed or packed body when free.")]
        [SerializeField] GameObject baseColliders;
        [Tooltip("Colliders of the open body (grab box + physics body) or of the fitted pad.")]
        [SerializeField] GameObject changedColliders;
        [Tooltip("Packet, pad: mouth-side grab region while stored or being pulled. Empty for the outer pack.")]
        [SerializeField] GameObject storedColliders;
        [Tooltip("Packet: the outer pack. Pad: its packet. Empty for the outer pack.")]
        [SerializeField] KD1Item container;
        [Tooltip("Packet, pad: home slot inside the container.")]
        [SerializeField] Transform slot;
        [Tooltip("Outer pack, packet: Anchor_Open of the open model (mouth point, +Z out of the mouth).")]
        [SerializeField] Transform mouth;
        [Tooltip("Pad: Anchor_HandFit of the fitted model.")]
        [SerializeField] Transform handFit;
        [Tooltip("Pad: Anchor_Contact of the fitted model (+Z out of the working face).")]
        [SerializeField] Transform contact;
        [Tooltip("Packet, pad: bounds of the stored (sealed / packed) model in this object's space, for the exit check.")]
        [SerializeField] Bounds storedOutline;

        KD1Equipment equipment;
        Vector3 slotPosition;
        Quaternion slotRotation = Quaternion.identity;
        float exitDistance;
        float maxExtraction;

        IXRSelectInteractor controlHand;
        TrackedPoseDriver controlDriver;
        bool hasBaseline;
        Vector3 baselineHand;
        float baselineExtraction;
        readonly RelativeHand pullPoint = new RelativeHand();
        readonly FreeHold free = new FreeHold();
        // The current grab has not set up its free hold yet: it starts with a bring (if far) instead of a plain re-take.
        bool freshGrab;

        public KD1ItemKind Kind => kind;
        public int Number => number;
        public XRGrabInteractable Grab => grab;
        public Rigidbody Body => body;
        public GameObject BaseModel => baseModel;
        public GameObject ChangedModel => changedModel;
        public GameObject BaseColliders => baseColliders;
        public GameObject ChangedColliders => changedColliders;
        public GameObject StoredColliders => storedColliders;
        public KD1Item Container => container;
        public Transform HomeSlot => slot;
        public Transform Mouth => mouth;
        public Transform HandFit => handFit;
        public Transform Contact => contact;
        public Bounds StoredOutline => storedOutline;

        public KD1ItemState State { get; private set; }
        // Outer pack, packet: opened. Never closed again except by the reset.
        public bool IsOpen { get; private set; }
        public KD1Hand WornOn { get; private set; }
        // Extraction along the container mouth in metres (0 = in the slot).
        public float Extraction { get; private set; }
        public float ExitDistance => exitDistance;

        public bool IsContent => kind != KD1ItemKind.OuterPack;
        // A free item still being brought to the hand after a far grab (nothing is fitted with it meanwhile).
        public bool IsBringing => grab != null && grab.isSelected && State == KD1ItemState.Free && free.IsBringing;

        // ---------- Called by KD1Equipment ----------

        internal void Bind(KD1Equipment owner)
        {
            equipment = owner;
            if (!IsContent)
                return;
            // The slot pose and the mouth plane in the container's frame; the content's stored rotation is the slot's.
            var c = container.transform;
            slotPosition = c.InverseTransformPoint(slot.position);
            slotRotation = Quaternion.Inverse(c.rotation) * slot.rotation;
            var mouthPoint = c.InverseTransformPoint(container.mouth.position);
            var axis = c.InverseTransformDirection(container.mouth.forward).normalized;
            var lowest = float.PositiveInfinity;
            var b = storedOutline;
            for (var i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                lowest = Mathf.Min(lowest, Vector3.Dot(slotPosition + slotRotation * corner - mouthPoint, axis));
            }
            exitDistance = Mathf.Max(0f, owner.ExitMargin - lowest);
            maxExtraction = exitDistance + ExtractionOvershoot;
        }

        internal void SetState(KD1ItemState state, KD1Hand wornOn = KD1Hand.None)
        {
            State = state;
            WornOn = state == KD1ItemState.Worn ? wornOn : KD1Hand.None;
            if (state != KD1ItemState.Extracting)
                Extraction = 0f;
        }

        internal void SetOpen(bool open) => IsOpen = open;

        // Clears the held-pose baselines (new grab, hand over, reset): the next held frame starts from the current pose.
        internal void ResetHold()
        {
            controlHand = null;
            controlDriver = null;
            hasBaseline = false;
            baselineHand = default;
            baselineExtraction = 0f;
            freshGrab = false;
            pullPoint.End();
            free.End();
        }

        // Shows the model and colliders of the current state and, when not held, sets the rest physics of the state. containerOpen:
        // the container is open (true for the outer pack). Never activates or deactivates this root or the grab.
        internal void Apply(bool containerOpen)
        {
            var hidden = State == KD1ItemState.Stored && !containerOpen;
            var changed = kind == KD1ItemKind.Pad ? State == KD1ItemState.Worn : IsOpen;
            var stored = State == KD1ItemState.Stored || State == KD1ItemState.Extracting;
            SetActive(baseModel, !hidden && !changed);
            SetActive(changedModel, !hidden && changed);
            if (kind == KD1ItemKind.Pad && State != KD1ItemState.Worn)
                changedModel.transform.localScale = Vector3.one;
            SetActive(storedColliders, stored && containerOpen);
            SetActive(baseColliders, !stored && !changed);
            // A fitted pad has no active collider: it is never grabbed (the wearing hand's trigger takes it off) and never
            // stands in front of what a hand reaches for.
            SetActive(changedColliders, !stored && changed && State != KD1ItemState.Worn);

            var free = State == KD1ItemState.Free;
            body.excludeLayers = free ? 0 : ~0;
            body.interpolation = free ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            if (grab.isSelected)
                return;
            StopBody();
            body.useGravity = free;
            body.isKinematic = !free;
            StopBody();
        }

        // Puts a content back in its slot (identity under it), stored. Its physics comes with Apply. The Transform alone is set: a
        // stored content is a kinematic child that must follow its container's Transform. Writing Rigidbody.position / rotation here
        // would be applied to the Transform after the next physics step, putting the content back at this moment's world pose while
        // the container (free, with gravity) settles, so it would end up off its slot (the kinematic body takes the Transform's pose
        // at the next simulation by itself).
        internal void PlaceInSlot()
        {
            transform.SetParent(slot, false);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            SetState(KD1ItemState.Stored);
        }

        // Fits a pad on a hand mount: the fitted model (mirrored if the mount says so) with its Anchor_HandFit on the mount point.
        // The pad must already be released by XRI.
        internal void FitTo(KD1HandMount mount)
        {
            changedModel.transform.localScale = mount.MirrorPad ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            // HandFit in this object's space, after the mirror (read from its matrix: a mirrored parent flips only its X axis).
            var fit = transform.worldToLocalMatrix * handFit.localToWorldMatrix;
            var fitPosition = (Vector3)fit.GetColumn(3);
            var fitRotation = Quaternion.LookRotation(fit.MultiplyVector(Vector3.forward), fit.MultiplyVector(Vector3.up));
            var inverse = Quaternion.Inverse(fitRotation);
            // Transform only, as in PlaceInSlot: the kinematic pad must follow the moving mount, not a pose written for the next step.
            transform.SetParent(mount.transform, false);
            transform.SetLocalPositionAndRotation(-(inverse * fitPosition), inverse);
        }

        // For a body that physics moves (the free outer pack, a removed pad): its physics pose now equals its Transform.
        internal void SyncBody()
        {
            body.position = transform.position;
            body.rotation = transform.rotation;
        }

        void StopBody()
        {
            if (body.isKinematic)
                return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        // ---------- Held pose (grab transformer) ----------

        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
        {
            localScale = transform.localScale;
            var hand = grabInteractable.firstInteractorSelecting;
            if (hand != controlHand)
            {
                // New grab or another hand took over: continue from the current pose with this hand.
                controlHand = hand;
                controlDriver = equipment != null ? equipment.DriverOf(hand) : null;
                hasBaseline = false;
                freshGrab = true;
                pullPoint.Begin(hand != null ? hand.transform : null, transform.position);
            }

            var extracting = State == KD1ItemState.Extracting && IsContent;
            if (equipment == null || !equipment.AllowsMotion(controlDriver))
            {
                hasBaseline = false;
                pullPoint.Step(false);
                targetPose = extracting ? ExtractionPose(Extraction) : new Pose(transform.position, transform.rotation);
                return;
            }

            var handPose = new Pose(controlDriver.transform.position, controlDriver.transform.rotation);
            if (!extracting)
            {
                if (!hasBaseline)
                    BeginFree(handPose, new Pose(transform.position, transform.rotation));
                targetPose = free.Evaluate(handPose);
                return;
            }

            // Hand point motion along the mouth, measured in the container's frame (the container may be held and moved by the
            // other hand; then the point rides with the rig as the real hand would).
            pullPoint.CarriedByRig = container.Grab.isSelected;
            pullPoint.Step(true);
            var c = container.transform;
            var handInContainer = c.InverseTransformPoint(pullPoint.Position);
            // A container flying to the other hand moves without any pull: the pull starts again from there.
            if (!hasBaseline || container.IsBringing)
            {
                baselineHand = handInContainer;
                baselineExtraction = Extraction;
                hasBaseline = true;
            }
            var axis = c.InverseTransformDirection(container.mouth.forward).normalized;
            Extraction = Mathf.Clamp(baselineExtraction + Vector3.Dot(handInContainer - baselineHand, axis), 0f, maxExtraction);
            targetPose = ExtractionPose(Extraction);
            if (Extraction < exitDistance)
                return;

            // Whole outline out of the mouth: free from this exact pose with the same hand (a far grab is brought to the hand from here).
            equipment.CompleteExtraction(this);
            if (State != KD1ItemState.Free)
                return;
            pullPoint.End();
            BeginFree(handPose, targetPose);
        }

        Pose ExtractionPose(float extraction)
        {
            var c = container.transform;
            return new Pose(c.TransformPoint(slotPosition) + container.mouth.forward * extraction, c.rotation * slotRotation);
        }

        // A fresh grab (or the moment the grab's content comes out) starts the free hold with a bring of the item point nearest to
        // the hand; anything else (hand allowed again after a loss or a hold) re-takes the reference from the current pose.
        void BeginFree(Pose hand, Pose item)
        {
            if (freshGrab)
            {
                var grip = XRInteractableUtility.TryGetClosestPointOnCollider(grab, hand.position, out var nearest) ? nearest.point : item.position;
                free.Begin(hand, item, grip);
                freshGrab = false;
            }
            else
            {
                free.Rebase(hand, item);
            }
            hasBaseline = true;
        }

        // ---------- Validation ----------

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (grab == null || body == null || grab.transform != transform || body.transform != transform)
                problems.Add("grab and body must be on this object");
            if (baseModel == null || changedModel == null || baseModel == changedModel || !IsChild(baseModel) || !IsChild(changedModel))
                problems.Add("two different state models inside this object are required");
            if (baseColliders == null || changedColliders == null || !IsChild(baseColliders) || !IsChild(changedColliders))
                problems.Add("base and changed collider sets inside this object are required");
            if (IsContent)
            {
                if (storedColliders == null || !IsChild(storedColliders))
                    problems.Add("stored collider set missing");
                if (container == null || slot == null || !slot.IsChildOf(container.transform) || slot.IsChildOf(transform))
                    problems.Add("container and a home slot inside the container are required");
                else if (container.mouth == null)
                    problems.Add("the container has no mouth anchor");
                if (storedOutline.size.x <= 0f || storedOutline.size.y <= 0f || storedOutline.size.z <= 0f)
                    problems.Add(nameof(storedOutline) + " empty");
            }
            else if (container != null || slot != null || storedColliders != null)
            {
                problems.Add("the outer pack has no container, slot or stored colliders");
            }
            if (kind != KD1ItemKind.Pad && (mouth == null || !mouth.IsChildOf(changedModel != null ? changedModel.transform : transform)))
                problems.Add("mouth must be the open model's Anchor_Open");
            if (kind == KD1ItemKind.Pad && (handFit == null || contact == null || changedModel == null ||
                                            !handFit.IsChildOf(changedModel.transform) || !contact.IsChildOf(changedModel.transform)))
                problems.Add("HandFit and Contact must be the fitted model's anchors");
            if (kind == KD1ItemKind.OuterPack ? number != 0 : number < 0 || number >= KD1Equipment.PacketCount)
                problems.Add("number out of range");
            if (grab != null)
            {
                if (grab.startingSingleGrabTransformers.Count != 1 || grab.startingSingleGrabTransformers[0] != this || grab.addDefaultGrabTransformers ||
                    grab.startingMultipleGrabTransformers.Count != 0)
                    problems.Add("this component must be the grab's only transformer (no default transformers)");
                if (grab.movementType != XRBaseInteractable.MovementType.Instantaneous || grab.selectMode != InteractableSelectMode.Single || grab.throwOnDetach)
                    problems.Add("grab must be Instantaneous, single select, no throw");
                var sets = new[] { baseColliders, changedColliders, storedColliders };
                var own = new HashSet<Collider>();
                foreach (var set in sets)
                {
                    if (set == null)
                        continue;
                    var listed = 0;
                    foreach (var c in set.GetComponentsInChildren<Collider>(true))
                    {
                        own.Add(c);
                        if (grab.colliders.Contains(c))
                            listed++;
                    }
                    if (listed == 0)
                        problems.Add($"collider set '{set.name}' has no grab collider");
                }
                foreach (var c in grab.colliders)
                {
                    if (c == null || !own.Contains(c))
                        problems.Add("grab lists a collider outside this item's collider sets");
                }
            }
            error = problems.Count > 0 ? $"KD1Item '{name}' ({kind} {number}): {string.Join(", ", problems)}." : null;
            return error == null;
        }

        bool IsChild(GameObject go) => go != null && go.transform != transform && go.transform.IsChildOf(transform);
    }
}
