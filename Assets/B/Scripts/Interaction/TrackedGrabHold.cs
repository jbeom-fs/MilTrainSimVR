using System.Collections.Generic;
using MilTrainSimVR.B.CommonMenu;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace MilTrainSimVR.B.Interaction
{
    // Single-grab pose of a B free item moved by XRGrabInteractable (AED pads, the tourniquet body before it is attached, the
    // pressure bandage dressing, roll, end and tape, the six protective clothing pickups), installed by the item's owner in Awake.
    // It is the item's only single grab transformer: the default XRGeneralGrabTransformer is not added and XRI's own attach
    // ease-in is taken over (set to 0 while installed; its configured seconds are this bring's seconds), so one writer computes the
    // target pose in every phase XRI writes it (Dynamic and OnBeforeRender, Instantaneous) and XRI only applies it.
    //
    // Normal grab (near or far): the item keeps its rotation and turns only with the hand; the item's dynamic attach point (XRI's
    // closest collider point, or the pickup's Attach point) comes to the hand's attach point over the bring seconds (FreeHold, no
    // bring when it is already within GrabBring.ArrivedDistance). The first valid sample of a grab starts it from the current pose.
    // Hand pose invalid (HandTracking: position and rotation tracked, actions bound), selection kept: the item keeps its last valid
    // pose in the XR Origin's tracking space (so locomotion carries it as it carries the frozen hand) and the bring does not advance;
    // nothing of the hand during the loss is ever applied.
    // Recovery: the first valid sample only re-takes the relation between the current item pose and the recovered hand pose
    // (FreeHold.Rebase): position, rotation and any course check stay as they are on that sample (IsHoldingStill is true for that
    // frame); from the next sample only new hand motion counts, and a bring under way continues from the stopped pose for its
    // remaining part. A finished bring never starts again.
    // Selection life: tracking loss never drops, reselects or re-grabs anything. A new grab (also a swap to the other hand, which XRI
    // makes a drop and a grab in single select) starts fresh; XRI's normal release and cancel end it as before; nothing of an
    // earlier hand or grab is reused. Uninstall (owner OnDestroy) restores the grab's own settings.
    public sealed class TrackedGrabHold : IXRGrabTransformer
    {
        static readonly List<IXRGrabTransformer> s_Transformers = new List<IXRGrabTransformer>();

        readonly XRGrabInteractable grab;
        readonly FreeHold free = new FreeHold();
        readonly float bringSeconds;
        readonly bool savedAddDefault;
        readonly float savedEase;
        bool installed;

        IXRSelectInteractor hand;
        TrackedPoseDriver driver;
        Transform space;
        // The current grab has no valid hand sample yet; the first one starts the hold (with its bring).
        bool pendingStart;
        // The hand pose is not valid now: the item is held at heldInSpace.
        bool lost;
        Pose heldInSpace;
        int recoveredFrame = -1;

        TrackedGrabHold(XRGrabInteractable grabInteractable)
        {
            grab = grabInteractable;
            savedAddDefault = grab.addDefaultGrabTransformers;
            savedEase = grab.attachEaseInTime;
            bringSeconds = Mathf.Max(savedEase, 0.0001f);
        }

        public static TrackedGrabHold Install(XRGrabInteractable grabInteractable)
        {
            if (grabInteractable == null)
                return null;
            var existing = Of(grabInteractable);
            if (existing != null)
                return existing;
            var hold = new TrackedGrabHold(grabInteractable);
            grabInteractable.addDefaultGrabTransformers = false;
            grabInteractable.attachEaseInTime = 0f;
            grabInteractable.AddSingleGrabTransformer(hold);
            hold.installed = true;
            return hold;
        }

        public void Uninstall()
        {
            if (!installed)
                return;
            installed = false;
            Clear();
            if (grab == null)
                return;
            grab.RemoveSingleGrabTransformer(this);
            grab.addDefaultGrabTransformers = savedAddDefault;
            grab.attachEaseInTime = savedEase;
        }

        // The hold installed on this grab, if any.
        public static TrackedGrabHold Of(XRGrabInteractable grabInteractable)
        {
            if (grabInteractable == null)
                return null;
            s_Transformers.Clear();
            grabInteractable.GetSingleGrabTransformers(s_Transformers);
            TrackedGrabHold found = null;
            foreach (var t in s_Transformers)
            {
                if (t is TrackedGrabHold hold)
                {
                    found = hold;
                    break;
                }
            }
            s_Transformers.Clear();
            return found;
        }

        // The bring of this grab is under way (its progress is below 1). Stops advancing while the hand is not tracked.
        public bool IsBringing => installed && grab != null && grab.isSelected && !pendingStart && free.IsBringing;

        // Held but the item is not following the hand this frame: no valid hand sample yet for this grab, the hand pose is not
        // valid, or this frame took the recovery sample. Nothing is completed with the item meanwhile.
        public bool IsHoldingStill => installed && grab != null && grab.isSelected && (pendingStart || lost || recoveredFrame == Time.frameCount);

        // Item pose the hold gives for this hand attach pose now (no bring advance), for a release pose (also inside the selectExited
        // of the release, when XRI has already removed the interactor); false while it does not follow the hand (no valid sample of
        // this grab yet, or the hand pose was not valid at the last update).
        public bool TryPoseFor(Pose handAttach, out Pose pose)
        {
            pose = default;
            if (!installed || grab == null || hand == null || pendingStart || lost || !free.IsActive)
                return false;
            pose = free.Peek(handAttach);
            return true;
        }

        // ---------- IXRGrabTransformer ----------

        public bool canProcess => installed;

        public void OnLink(XRGrabInteractable grabInteractable)
        {
        }

        public void OnUnlink(XRGrabInteractable grabInteractable) => Clear();

        // A new selection (0 -> 1): nothing of an earlier grab or hand is kept.
        public void OnGrab(XRGrabInteractable grabInteractable) => Clear();

        // Single select: a hand swap is a drop and a new grab; Process also starts fresh for any new holder.
        public void OnGrabCountChanged(XRGrabInteractable grabInteractable, Pose targetPose, Vector3 localScale)
        {
        }

        public void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
        {
            var t = grab.transform;
            localScale = t.localScale;
            var current = new Pose(t.position, t.rotation);
            var interactor = grab.firstInteractorSelecting;
            if (interactor == null)
            {
                targetPose = current;
                return;
            }
            if (interactor != hand)
            {
                Clear();
                hand = interactor;
                driver = HandTracking.DriverOf(interactor);
                space = RelativeHand.TrackingSpaceOf(interactor.transform);
                pendingStart = true;
            }

            var attach = interactor.GetAttachTransform(grab);
            if (attach == null || !HandTracking.IsPoseTracked(driver) || CommonPauseMenu.IsPaused)
            {
                if (!lost)
                {
                    lost = true;
                    heldInSpace = ToSpace(current);
                }
                targetPose = FromSpace(heldInSpace);
                return;
            }

            var handPose = new Pose(attach.position, attach.rotation);
            if (pendingStart)
            {
                var grip = grab.GetAttachTransform(interactor);
                free.Begin(handPose, current, grip != null ? grip.position : handPose.position, bringSeconds, GrabBring.ArrivedDistance);
                pendingStart = false;
                lost = false;
                targetPose = current;
                return;
            }
            if (lost)
            {
                // Recovery sample: only the relation is re-taken; the item stays where it is.
                free.Rebase(handPose, current);
                lost = false;
                recoveredFrame = Time.frameCount;
                targetPose = current;
                return;
            }

            var pose = free.Evaluate(handPose);
            targetPose = new Pose(grab.trackPosition ? pose.position : current.position, grab.trackRotation ? pose.rotation : current.rotation);
        }

        void Clear()
        {
            hand = null;
            driver = null;
            space = null;
            pendingStart = false;
            lost = false;
            recoveredFrame = -1;
            free.End();
        }

        Pose ToSpace(Pose world)
        {
            if (space == null)
                return world;
            var inverse = Quaternion.Inverse(space.rotation);
            return new Pose(inverse * (world.position - space.position), inverse * world.rotation);
        }

        Pose FromSpace(Pose local) =>
            space == null ? local : new Pose(space.position + space.rotation * local.position, space.rotation * local.rotation);
    }
}
