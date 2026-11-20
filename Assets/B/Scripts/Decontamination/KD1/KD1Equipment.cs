using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.B.Decontamination.KD1
{
    // Why the last trigger press did nothing (shown for a few seconds by the guide).
    // Optional extra condition of a course on top of the equipment's own rules (the equipment never references a course). Empty in the
    // equipment scene, where everything stays as it is. When one is set: AllowsInput gates new grabs, the automatic opening and
    // pull completion, fitting and removal presses (through the trigger input validity, so a press needs a valid release after the
    // course opens) and the surface practice records; AllowsSelect is also asked for each new grab (a course can make a hand that
    // still holds its select input from a button press wait for its release). A selection in progress, a held item's motion, its
    // release and the equipment reset are never gated.
    public interface IKD1CourseGate
    {
        bool AllowsInput { get; }
        bool AllowsSelect(IXRSelectInteractor interactor);
    }

    public enum KD1Notice
    {
        None = 0,
        PadNotOut = 2,
        HandHasPad = 3,
        Tracking = 4,
        Paused = 5
    }

    // KD-1 basic handling (equipment check scene; no course, runner, scoring or completion here): the only owner of the state of
    // the outer pack, its four packets and their four pads. One-hand flow (either hand): grab the outer pack (it opens) -> put it
    // down -> grab a packet in it and pull it fully out (it opens) -> put it down -> grab the pad in it and pull it fully out ->
    // trigger to fit it on that hand -> the same hand's trigger takes it off. Guidance only reads this component. The automatic
    // opening and the trigger fitting/removal are a simplified control of this virtual set, not the product's procedure.
    //
    // Opening (no trigger): the outer pack opens once in LateUpdate while it is held by a tracked hand with focus and an unlocked
    // equipment; a packet opens on the frame its pull completes (same conditions, from the held pose). A release keeps them open.
    // Opening never selects, pulls or fits anything else; a pull released before it is out goes back to its slot, still sealed.
    //
    // Trigger (the XR Origin's own Activate input, nothing new). Fitting: a new press while holding a pad that is fully out, taken
    // from XRI's activate event of the selecting interactor and applied in this LateUpdate (order 50, after XRI's update) after a
    // second check. Removal: the pad is no longer selected, so it is read from the wearing hand's own trigger source (the one
    // Near-Far interactor under that hand's controller, found from the driver), in this LateUpdate: armed, input valid, pressed now,
    // nothing selected by any interactor of that hand, and its UI ray not on the board (a UI press is never a removal). Every press
    // needs the trigger seen released by that interactor since its grab and since the last change it made (armed), so a trigger
    // already held at a grab or change does nothing until released and pressed again, holding it never repeats, and the press that
    // fitted a pad is never the one that takes it off. Each physical hand has one trigger source, so a press is used once.
    //
    // Trigger input validity (per hand, HandInputValid): unlocked equipment, application focus, the interactor active with the
    // performed action of its activate input (the action XRI reads for the press) enabled and bound to controls, and the hand's
    // TrackedPoseDriver tracked. While it is not valid that hand is disarmed, its valid-since time is dropped and its pending presses
    // are discarded (focus or pause loss applies to both hands). A release counts only once the hand has been valid for
    // RearmSettleSeconds without a break, counted from the first LateUpdate after the hand was registered (both trigger sources at
    // Awake, any other interactor at its first KD-1 grab) or after its validity returned; a new grab or a reset does not restart
    // that time (they only disarm). So a 0 read while the input was lost or just coming back is never taken as a release, and
    // validity returning never restores an earlier arming. The same validity is required when a press is collected and applied.
    //
    // Select filter (all nine grabs, from Awake to OnDestroy): a new grab needs an unlocked equipment, focus and the hand tracked;
    // it may be near or far by ray (no distance limit; KD1Item brings a free item to the hand and pulls a stored one along its
    // mouth by the hand's own motion). A stored packet needs the outer pack open;
    // a stored pad needs its packet open; an item being pulled cannot be taken over; a fitted pad is never selected. A selection
    // in progress always passes (the filter never drops an item). Hover filter (same grabs, same lifetime): a fitted pad is never a
    // hover candidate, so it never stands in front of the items a hand reaches for (its colliders are off while fitted as well).
    //
    // States (KD1Item): stored packets follow the outer pack, stored pads their packet, hidden and not selectable while that
    // container is sealed. Grabbing a stored content starts its pull along the container's Anchor_Open +Z (KD1Item held pose);
    // a release before it is out puts it back in its slot (applied in LateUpdate when the item is not held again, so a hand over
    // or XRI's own drop is not mistaken for it); once out it is free for good (no re-storing in this version) and is released onto
    // the scene (looseItems) with gravity. Opening and fitting swap only the child models and collider sets, never the item root.
    //
    // Fitting (a free pad in a hand, that hand has no pad): the pad is recorded worn first (the filter refuses it at once), its
    // selection is ended through XRI's cancel path, then it goes on that hand's KD1HandMount (Anchor_HandFit on the mount,
    // kinematic, no contacts, the fitted model). One pad per hand. Removal keeps the pad's world pose, takes it off the mount
    // under looseItems, free (packed model, mirror cleared, dynamic with gravity) and grabbable again; it is never selected or
    // fitted again by itself.
    //
    // Reset (the board's reset button): ① new input locked, ② pending presses, pulls and held-pose baselines dropped, ③ every XRI
    // selection of the nine items cancelled, ④ both mounts emptied, ⑤ outer pack sealed, packets in their slots sealed, pads in
    // their packets packed, ⑥ parents, poses, physics, colliders and selectability restored, ⑦ trigger arming, grab order and
    // the notice cleared, and EquipmentReset raised (the surface practice clears its records and contacts); then the result is checked and input is unlocked only if everything is back. The user's XR position and
    // view are never touched and the scene is never reloaded. A failed reset keeps input locked (IsFaulted, FaultReason) and is
    // never shown as done; the button stays usable to try again. One reset per frame.
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class KD1Equipment : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
    {
        public const int PacketCount = 4;
        // A trigger release counts only after the hand's input has been valid this long (s) without a break, counted from its first
        // registration or from its validity returning (not restarted by a new grab or a reset).
        const float RearmSettleSeconds = 0.25f;

        [SerializeField] KD1Item outerPack;
        [SerializeField] KD1Item[] packets = new KD1Item[PacketCount];
        [Tooltip("Pad i belongs to packet i.")]
        [SerializeField] KD1Item[] pads = new KD1Item[PacketCount];
        [SerializeField] TrackedPoseDriver leftDriver;
        [SerializeField] TrackedPoseDriver rightDriver;
        [SerializeField] KD1HandMount leftMount;
        [SerializeField] KD1HandMount rightMount;
        [Tooltip("Parent of items released after they came out of their container (not inside the kit).")]
        [SerializeField] Transform looseItems;
        [Tooltip("Engineering setting: a content is out when its whole stored outline is this far (m) past the container's mouth plane.")]
        [SerializeField] float exitMargin = 0.005f;
        [Tooltip("How long (s) the guide shows why a trigger press did nothing.")]
        [SerializeField] float noticeSeconds = 3f;
        [Tooltip("Course scene only: component implementing IKD1CourseGate (extra input condition). Empty in the equipment scene.")]
        [SerializeField] MonoBehaviour courseGate;

        readonly Dictionary<IXRInteractor, TrackedPoseDriver> drivers = new Dictionary<IXRInteractor, TrackedPoseDriver>();
        readonly Dictionary<IXRSelectInteractor, bool> armed = new Dictionary<IXRSelectInteractor, bool>();
        // Since when (unscaled s) each hand's trigger input has been valid without a break; no entry while it is not.
        readonly Dictionary<IXRSelectInteractor, float> validSince = new Dictionary<IXRSelectInteractor, float>();
        // Each hand's trigger source for removal (its Near-Far interactor) and every interactor of that hand (for "holds something").
        NearFarInteractor leftTrigger;
        NearFarInteractor rightTrigger;
        XRBaseInteractor[] leftInteractors = Array.Empty<XRBaseInteractor>();
        XRBaseInteractor[] rightInteractors = Array.Empty<XRBaseInteractor>();
        readonly List<IXRSelectInteractor> hands = new List<IXRSelectInteractor>();
        readonly HashSet<KD1Item> released = new HashSet<KD1Item>();
        readonly List<KD1Item> releasedNow = new List<KD1Item>();
        KD1Item[] items = Array.Empty<KD1Item>();
        int[] selectOrder = Array.Empty<int>();
        int selectCounter;
        Transform outerParent;
        Vector3 outerPosition;
        Quaternion outerRotation;
        readonly List<(KD1Item item, IXRSelectInteractor hand)> pending = new List<(KD1Item, IXRSelectInteractor)>();
        readonly List<(KD1Item item, IXRSelectInteractor hand)> pendingNow = new List<(KD1Item, IXRSelectInteractor)>();
        KD1Item fittedLeft;
        KD1Item fittedRight;
        KD1Notice notice;
        float noticeUntil;
        bool valid;
        bool gated;
        bool subscribed;
        bool resetting;
        bool transitioning;
        int resetFrame = -1;

        public KD1Item OuterPack => outerPack;
        public IReadOnlyList<KD1Item> Packets => packets;
        public IReadOnlyList<KD1Item> Pads => pads;
        public IReadOnlyList<KD1Item> Items => items;
        public TrackedPoseDriver LeftDriver => leftDriver;
        public TrackedPoseDriver RightDriver => rightDriver;
        public KD1HandMount LeftMount => leftMount;
        public KD1HandMount RightMount => rightMount;
        public Transform LooseItems => looseItems;
        public float ExitMargin => exitMargin;
        public bool IsValid => valid;
        public bool IsResetting => resetting;
        public bool IsFaulted { get; private set; }
        public MonoBehaviour CourseGate => courseGate;
        // The course (if any) allows new handling now. No gate assigned: always. Assigned but missing, off or not allowing: never.
        public bool CourseAllows => !gated || courseGate != null && courseGate.isActiveAndEnabled && courseGate is IKD1CourseGate gate && gate.AllowsInput;
        public string FaultReason { get; private set; }
        public bool InputLocked => !valid || resetting || IsFaulted || !isActiveAndEnabled;
        // Set by a training field while it asks whether to stop the course (its confirm dialog). Selections are kept: a held item
        // holds still (as for a hand without tracking: its hand baseline is dropped, so motion during the dialog is never applied
        // and nothing jumps after it) and a release meanwhile is applied only after the dialog (the rest state of the release,
        // never a slot return while suspended). New input is already refused by the course gate. Not saved; false by default.
        public bool Suspended { get; set; }
        // Raised on every state change and reset (views rebuild on change).
        public int Version { get; private set; }
        // The held item grabbed last (the one the guide talks about); null when nothing is held.
        public KD1Item GuidedItem { get; private set; }
        public KD1Notice Notice => Time.unscaledTime < noticeUntil ? notice : KD1Notice.None;
        // Raised by the board's reset right after every piece is restored (before the result check), never by an ordinary open, fit
        // or removal (Version changes for those too). Practice records listen to this, not to Version.
        public event Action EquipmentReset;

        public KD1Item FittedOn(KD1Hand hand) => hand == KD1Hand.Left ? fittedLeft : hand == KD1Hand.Right ? fittedRight : null;
        public KD1Item PadOf(KD1Item packet) => packet != null && packet.Kind == KD1ItemKind.Packet ? pads[packet.Number] : null;

        bool IXRSelectFilter.canProcess => this != null;
        bool IXRHoverFilter.canProcess => this != null;

        void Awake()
        {
            // Set once a gate is assigned, also when that object is later destroyed (fake null): a lost gate keeps the course locked.
            gated = !ReferenceEquals(courseGate, null);
            valid = TryValidate(out var error);
            if (!valid)
            {
                BlockGrabs(error);
                return;
            }
            items = new KD1Item[1 + 2 * PacketCount];
            items[0] = outerPack;
            for (var i = 0; i < PacketCount; i++)
            {
                items[1 + i] = packets[i];
                items[1 + PacketCount + i] = pads[i];
            }
            selectOrder = new int[items.Length];
            var outer = outerPack.transform;
            outerParent = outer.parent;
            outerPosition = outer.localPosition;
            outerRotation = outer.localRotation;
            foreach (var item in items)
            {
                item.Bind(this);
                item.Grab.selectFilters.Add(this);
                item.Grab.hoverFilters.Add(this);
                item.Grab.selectEntered.AddListener(OnSelectEntered);
                item.Grab.selectExited.AddListener(OnSelectExited);
                item.Grab.activated.AddListener(OnActivated);
            }
            subscribed = true;
            leftTrigger = TriggerSourceOf(leftDriver);
            rightTrigger = TriggerSourceOf(rightDriver);
            leftInteractors = leftDriver.GetComponentsInChildren<XRBaseInteractor>(true);
            rightInteractors = rightDriver.GetComponentsInChildren<XRBaseInteractor>(true);
            Register(leftTrigger);
            Register(rightTrigger);

            // The scene starts from the same state as a reset (outer pack where it was saved).
            RestoreAll();
            var problem = VerifyInitial();
            if (problem != null)
            {
                IsFaulted = true;
                FaultReason = problem;
                Debug.LogError("[KD1] Start state check failed: " + problem + " Input stays locked.", this);
            }
        }

        void OnDestroy()
        {
            if (!subscribed)
                return;
            foreach (var item in items)
            {
                if (item == null || item.Grab == null)
                    continue;
                item.Grab.selectFilters.Remove(this);
                item.Grab.hoverFilters.Remove(this);
                item.Grab.selectEntered.RemoveListener(OnSelectEntered);
                item.Grab.selectExited.RemoveListener(OnSelectExited);
                item.Grab.activated.RemoveListener(OnActivated);
            }
            subscribed = false;
        }

        void OnDisable()
        {
            DropTriggerInput();
            GuidedItem = null;
        }

        // Focus or pause lost: both hands lose their arming and pending presses at once (the next LateUpdate also sees it).
        void OnApplicationFocus(bool focus)
        {
            if (!focus)
                DropTriggerInput();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
                DropTriggerInput();
        }

        void DropTriggerInput()
        {
            pending.Clear();
            DisarmAll();
            validSince.Clear();
        }

        // Invalid setup: the filter cannot be trusted, so every grab under the kit and this object is switched off instead.
        void BlockGrabs(string error)
        {
            var grabs = new HashSet<XRGrabInteractable>(GetComponentsInChildren<XRGrabInteractable>(true));
            if (outerPack != null)
            {
                foreach (var grab in outerPack.GetComponentsInChildren<XRGrabInteractable>(true))
                    grabs.Add(grab);
            }
            foreach (var group in new[] { packets, pads })
            {
                if (group == null)
                    continue;
                foreach (var item in group)
                {
                    if (item != null && item.Grab != null)
                        grabs.Add(item.Grab);
                }
            }
            foreach (var grab in grabs)
                grab.enabled = false;
            Debug.LogError($"[KD1] {error} Setup invalid: grabbing is blocked ({grabs.Count} grab(s) disabled).", this);
        }

        // ---------- XRI events ----------

        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (interactor == null)
                return false;
            if (interactor.IsSelecting(interactable))
                return true;
            var item = ItemOf(interactable);
            if (item == null)
                return true;
            if (InputLocked || transitioning || !Application.isFocused || !CourseAllows || gated && !((IKD1CourseGate)courseGate).AllowsSelect(interactor))
                return false;
            if (!KD1Tracking.IsTracked(DriverOf(interactor)))
                return false;
            switch (item.State)
            {
                case KD1ItemState.Stored:
                    return item.Container.IsOpen;
                case KD1ItemState.Extracting:
                    return false;
                case KD1ItemState.Worn:
                    return false;
                default:
                    return true;
            }
        }

        // A fitted pad is not a hover candidate of any hand (see the class notes); everything else passes.
        bool IXRHoverFilter.Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
        {
            if (interactor == null)
                return true;
            if (interactor is IXRSelectInteractor selector && interactable is IXRSelectInteractable selectable && selector.IsSelecting(selectable))
                return true;
            var item = ItemOf(interactable);
            return item == null || item.State != KD1ItemState.Worn;
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            var index = IndexOf(args.interactableObject);
            if (index < 0)
                return;
            var item = items[index];
            var hand = args.interactorObject;
            Register(hand);
            armed[hand] = false;
            selectOrder[index] = ++selectCounter;
            released.Remove(item);
            item.ResetHold();
            if (resetting || transitioning)
                return;
            if (item.State == KD1ItemState.Stored)
            {
                item.SetState(KD1ItemState.Extracting);
                Present(item);
            }
            Changed();
        }

        // The rest state is applied in LateUpdate, when the item is still not held (a hand over selects it again first).
        void OnSelectExited(SelectExitEventArgs args)
        {
            var index = IndexOf(args.interactableObject);
            if (index >= 0 && !resetting)
                released.Add(items[index]);
        }

        // Taken only with the trigger armed for that hand; applied and checked again in LateUpdate.
        void OnActivated(ActivateEventArgs args)
        {
            var index = IndexOf(args.interactableObject);
            if (index < 0 || !(args.interactorObject is IXRSelectInteractor hand) || !hand.IsSelecting(items[index].Grab))
                return;
            // A pad still flying to the hand after a far grab is not fitted; the press is not used (the hand stays armed).
            if (items[index].IsBringing)
                return;
            if (!armed.TryGetValue(hand, out var ready) || !ready || !HandInputValid(hand))
                return;
            armed[hand] = false;
            pending.Add((items[index], hand));
        }

        // ---------- Frame ----------

        void LateUpdate()
        {
            if (!valid)
                return;
            RefreshTriggerValidity();
            OpenHeldOuterPack();
            ApplyPending();
            ApplyRemovalPress(KD1Hand.Left);
            ApplyRemovalPress(KD1Hand.Right);
            ApplyReleases();
            ArmReleasedTriggers();
            GuidedItem = FindGuided();
        }

        // A hand whose trigger input is not valid now loses its arming and its pending presses; a valid one keeps its start time.
        void RefreshTriggerValidity()
        {
            var now = Time.unscaledTime;
            foreach (var hand in hands)
            {
                if (HandInputValid(hand))
                {
                    if (!validSince.ContainsKey(hand))
                        validSince[hand] = now;
                    continue;
                }
                armed[hand] = false;
                validSince.Remove(hand);
                pending.RemoveAll(p => p.hand == hand);
            }
        }

        // The held, sealed outer pack opens by itself (once; the hand must be tracked with focus and the equipment unlocked).
        void OpenHeldOuterPack()
        {
            if (!outerPack.IsOpen && !InputLocked && CourseAllows && Application.isFocused && IsHolderTracked(outerPack))
                Open(outerPack);
        }

        // Removal by the wearing hand's own new trigger press while that hand holds nothing (see the class notes).
        void ApplyRemovalPress(KD1Hand hand)
        {
            var pad = FittedOn(hand);
            var source = hand == KD1Hand.Left ? leftTrigger : rightTrigger;
            if (pad == null || source == null || !armed.TryGetValue(source, out var ready) || !ready || !HandInputValid(source) ||
                !source.logicalActivateState.active)
                return;
            // The press is used here, for the removal or for nothing: never kept for a later frame.
            armed[source] = false;
            if (HoldsAnything(hand) || source.TryGetCurrentUIRaycastResult(out _))
                return;
            Remove(pad, hand);
        }

        bool HoldsAnything(KD1Hand hand)
        {
            foreach (var interactor in hand == KD1Hand.Left ? leftInteractors : rightInteractors)
            {
                if (interactor != null && interactor.hasSelection)
                    return true;
            }
            return false;
        }

        void ApplyPending()
        {
            if (pending.Count == 0)
                return;
            pendingNow.Clear();
            pendingNow.AddRange(pending);
            pending.Clear();
            foreach (var (item, hand) in pendingNow)
                ApplyPress(item, hand);
        }

        void ApplyPress(KD1Item item, IXRSelectInteractor hand)
        {
            if (InputLocked || !hand.IsSelecting(item.Grab))
                return;
            if (!Application.isFocused)
            {
                Notify(KD1Notice.Paused);
                return;
            }
            if (!HandInputValid(hand))
            {
                Notify(KD1Notice.Tracking);
                return;
            }
            var driver = DriverOf(hand);
            // Only a pad uses the trigger; the outer pack and the packets open by themselves.
            switch (item.Kind)
            {
                case KD1ItemKind.Pad:
                    var side = HandOf(driver);
                    if (item.State != KD1ItemState.Free)
                        Notify(KD1Notice.PadNotOut);
                    else if (side == KD1Hand.None)
                        return;
                    else if (FittedOn(side) != null)
                        Notify(KD1Notice.HandHasPad);
                    else
                        Fit(item, side);
                    break;
            }
        }

        void ApplyReleases()
        {
            if (released.Count == 0 || Suspended)
                return;
            releasedNow.Clear();
            releasedNow.AddRange(released);
            released.Clear();
            foreach (var item in releasedNow)
            {
                if (item.Grab.isSelected)
                    continue;
                switch (item.State)
                {
                    case KD1ItemState.Extracting:
                        // Not out yet: back in its slot, not popping out.
                        item.PlaceInSlot();
                        Present(item);
                        Changed();
                        break;
                    case KD1ItemState.Free:
                        // XRI put it back under the parent it had at the grab; a content out of its container rests in the scene.
                        if (item.IsContent && item.transform.parent != looseItems)
                            item.transform.SetParent(looseItems, true);
                        Present(item);
                        break;
                }
            }
        }

        // A hand is armed again once its trigger reads released (XRI's logical activate state of that interactor) while its input
        // is valid and has been for RearmSettleSeconds.
        void ArmReleasedTriggers()
        {
            var now = Time.unscaledTime;
            foreach (var hand in hands)
            {
                if (armed[hand] || !HandInputValid(hand) || !validSince.TryGetValue(hand, out var since) || now - since < RearmSettleSeconds)
                    continue;
                if (!((XRBaseInputInteractor)hand).logicalActivateState.active)
                    armed[hand] = true;
            }
        }

        // The hand's trigger input can be trusted now (see the class notes). Not an XRI input interactor: never.
        bool HandInputValid(IXRSelectInteractor hand) =>
            !InputLocked && CourseAllows && Application.isFocused && hand is XRBaseInputInteractor input && input != null && input.isActiveAndEnabled &&
            IsBound(input.activateInput) && KD1Tracking.IsTracked(DriverOf(hand));

        // The source XRI actually reads for the press (ReadIsPerformed / ReadWasPerformedThisFrame, and from them the logical
        // activate state) is the performed action only: it must exist, be enabled and have controls. The value action (analog
        // trigger) never stands in for it and is not required. A runtime bypass replaces that source, and manual, object or unused
        // sources cannot be checked, so none of them count.
        static bool IsBound(XRInputButtonReader reader)
        {
            if (reader == null || reader.bypass != null)
                return false;
            switch (reader.inputSourceMode)
            {
                case XRInputButtonReader.InputSourceMode.InputAction:
                    return IsBound(reader.inputActionPerformed);
                case XRInputButtonReader.InputSourceMode.InputActionReference:
                    var performed = reader.inputActionReferencePerformed;
                    return performed != null && IsBound(performed.action);
                default:
                    return false;
            }
        }

        static bool IsBound(InputAction action) => action != null && action.enabled && action.controls.Count > 0;

        KD1Item FindGuided()
        {
            KD1Item best = null;
            var order = 0;
            for (var i = 0; i < items.Length; i++)
            {
                if (!items[i].Grab.isSelected || selectOrder[i] <= order)
                    continue;
                best = items[i];
                order = selectOrder[i];
            }
            return best;
        }

        // ---------- State changes ----------

        void Open(KD1Item item)
        {
            item.SetOpen(true);
            Present(item);
            DisarmHolders(item);
            Changed();
        }

        // Called by the held pose of a content being pulled, on the frame its outline clears the mouth (tracked, focused hand).
        internal void CompleteExtraction(KD1Item item)
        {
            if (InputLocked || !CourseAllows || item.State != KD1ItemState.Extracting)
                return;
            item.SetState(KD1ItemState.Free);
            // A packet opens on the frame it is out (its pad becomes visible and can be grabbed; nothing is selected for it).
            if (item.Kind == KD1ItemKind.Packet)
                item.SetOpen(true);
            Present(item);
            DisarmHolders(item);
            Changed();
        }

        void Fit(KD1Item pad, KD1Hand hand)
        {
            if (transitioning || pad.State != KD1ItemState.Free || FittedOn(hand) != null)
                return;
            transitioning = true;
            try
            {
                // Recorded first, so the filter refuses the pad and the release step skips it during the cancel below.
                pad.SetState(KD1ItemState.Worn, hand);
                if (hand == KD1Hand.Left)
                    fittedLeft = pad;
                else
                    fittedRight = pad;
                CancelSelection(pad.Grab);
                pad.FitTo(MountOf(hand));
                Present(pad);
                Changed();
            }
            finally
            {
                transitioning = false;
            }
        }

        // Takes a fitted pad off its hand: same world pose, under looseItems, free (packed model, mirror cleared, free physics).
        void Remove(KD1Item pad, KD1Hand hand)
        {
            if (FittedOn(hand) != pad || pad.State != KD1ItemState.Worn || pad.WornOn != hand)
                return;
            if (hand == KD1Hand.Left)
                fittedLeft = null;
            else
                fittedRight = null;
            pad.transform.SetParent(looseItems, true);
            pad.SetState(KD1ItemState.Free);
            Present(pad);
            pad.SyncBody();
            Changed();
        }

        // Applies an item's state and, for a container, its stored contents' visibility and grab regions.
        void Present(KD1Item item)
        {
            item.Apply(item.Container == null || item.Container.IsOpen);
            if (item.Kind == KD1ItemKind.OuterPack)
            {
                foreach (var packet in packets)
                {
                    if (packet.State == KD1ItemState.Stored)
                        packet.Apply(item.IsOpen);
                }
            }
            else if (item.Kind == KD1ItemKind.Packet)
            {
                var pad = pads[item.Number];
                if (pad.State == KD1ItemState.Stored)
                    pad.Apply(item.IsOpen);
            }
        }

        void Changed()
        {
            Version++;
            notice = KD1Notice.None;
        }

        void Notify(KD1Notice value)
        {
            notice = value;
            noticeUntil = Time.unscaledTime + noticeSeconds;
        }

        void DisarmHolders(KD1Item item)
        {
            foreach (var hand in hands)
            {
                if (hand.IsSelecting(item.Grab))
                    armed[hand] = false;
            }
        }

        void DisarmAll()
        {
            foreach (var hand in hands)
                armed[hand] = false;
        }

        // ---------- Reset ----------

        // Board reset (see the class notes). Returns false when ignored (invalid setup, already resetting, a second call in the same
        // frame) or when the result check failed (IsFaulted and FaultReason set, input locked).
        public bool ResetEquipment()
        {
            if (!valid)
            {
                Debug.LogError("[KD1] Reset refused: the setup is invalid (see the earlier error).", this);
                return false;
            }
            if (resetting || resetFrame == Time.frameCount)
                return false;
            resetting = true;
            resetFrame = Time.frameCount;
            string problem;
            try
            {
                pending.Clear();
                released.Clear();
                foreach (var item in items)
                    item.ResetHold();
                foreach (var item in items)
                    CancelSelection(item.Grab);
                RestoreAll();
                EquipmentReset?.Invoke();
                problem = VerifyInitial();
            }
            catch (Exception e)
            {
                problem = "exception " + e.GetType().Name + ": " + e.Message;
                Debug.LogException(e, this);
            }

            IsFaulted = problem != null;
            FaultReason = problem;
            resetting = false;
            if (problem != null)
            {
                Debug.LogError("[KD1] Reset failed: " + problem + " Input stays locked.", this);
                return false;
            }
            return true;
        }

        // Steps ④-⑦ of the reset (also the start state): no selection may be active.
        void RestoreAll()
        {
            fittedLeft = null;
            fittedRight = null;
            foreach (var pad in pads)
            {
                pad.SetOpen(false);
                pad.PlaceInSlot();
            }
            foreach (var packet in packets)
            {
                packet.SetOpen(false);
                packet.PlaceInSlot();
            }
            var outer = outerPack.transform;
            outerPack.SetOpen(false);
            outerPack.SetState(KD1ItemState.Free);
            if (outer.parent != outerParent)
                outer.SetParent(outerParent, false);
            outer.SetLocalPositionAndRotation(outerPosition, outerRotation);
            outerPack.SyncBody();
            foreach (var pad in pads)
                pad.Apply(false);
            foreach (var packet in packets)
                packet.Apply(false);
            outerPack.Apply(true);
            for (var i = 0; i < selectOrder.Length; i++)
                selectOrder[i] = 0;
            selectCounter = 0;
            DisarmAll();
            drivers.Clear();
            GuidedItem = null;
            notice = KD1Notice.None;
            noticeUntil = 0f;
            Version++;
        }

        // The reset's result check (also the start state at Awake): the stored state below, and the outer pack exactly at its saved
        // start pose (0.1 mm, 0.01 deg), as the reset has just put it there in this frame.
        // The reset's result check (also the start state at Awake): the stored state below, and the outer pack exactly at its saved
        // start pose (0.1 mm, 0.01 deg), as the reset has just put it there in this frame.
        string VerifyInitial()
        {
            var problem = VerifyStoredState(out _);
            if (problem != null)
                return problem;
            var outer = outerPack.transform;
            var distance = (outer.localPosition - outerPosition).magnitude;
            var angle = Quaternion.Angle(outer.localRotation, outerRotation);
            if (distance > 1e-4f || angle > 0.01f)
                return $"[outer-pose] {Describe(outerPack)}: {distance * 1000f:F3} mm (tolerance 0.100 mm) and {angle:F3} deg (tolerance 0.010 deg) from its saved start pose";
            return null;
        }

        // Slot pose tolerances of a stored content (kinematic child of its container at identity).
        const float SlotDistanceTolerance = 1e-5f;
        const float SlotAngleTolerance = 0.01f;

        // The logical start state shared by both checks, with no pose of the free outer pack: no pad fitted or on a mount, nothing
        // selected, every item active and selectable, nothing open or fitted, the outer pack under its start parent with the sealed
        // model and colliders and its rest physics (free, gravity), every packet and pad stored at its slot (they are kinematic
        // children of their container, so physics never moves them there), hidden, with the stored physics. Returns the reason
        // (check, object with kind and number, expected and actual values) or null; key names the check and the object only (no
        // measured value), so a caller can tell a new failure from the same one.
        string VerifyStoredState(out string key)
        {
            var leftItem = leftMount.GetComponentInChildren<KD1Item>(true);
            var rightItem = rightMount.GetComponentInChildren<KD1Item>(true);
            if (fittedLeft != null || fittedRight != null || leftItem != null || rightItem != null)
            {
                key = "mount";
                return $"[mount] expected no pad fitted and none on a hand mount; fitted left {Name(fittedLeft)}, fitted right {Name(fittedRight)}, " +
                       $"under the left mount {Name(leftItem)}, under the right mount {Name(rightItem)}";
            }
            foreach (var item in items)
            {
                if (item.Grab.isSelected)
                {
                    key = "selected:" + item.name;
                    return $"[selected] {Describe(item)}: expected not selected, selected by {item.Grab.interactorsSelecting.Count} interactor(s)";
                }
                if (!item.Grab.enabled || !item.gameObject.activeInHierarchy)
                {
                    key = "selectable:" + item.name;
                    return $"[selectable] {Describe(item)}: expected grab enabled and object active; grab enabled {item.Grab.enabled}, active in hierarchy {item.gameObject.activeInHierarchy}";
                }
                if (item.IsOpen || item.WornOn != KD1Hand.None)
                {
                    key = "open-fitted:" + item.name;
                    return $"[open-fitted] {Describe(item)}: expected sealed / packed and not fitted; IsOpen {item.IsOpen}, WornOn {item.WornOn}";
                }
            }
            if (outerPack.transform.parent != outerParent)
            {
                key = "outer-parent";
                return $"[outer-parent] {Describe(outerPack)}: expected parent '{Name(outerParent)}', actual '{Name(outerPack.transform.parent)}'";
            }
            if (!outerPack.BaseModel.activeSelf || outerPack.ChangedModel.activeSelf || !outerPack.BaseColliders.activeSelf || outerPack.ChangedColliders.activeSelf)
            {
                key = "outer-display";
                return $"[outer-display] {Describe(outerPack)}: expected sealed model and colliders on, open ones off; {Sets(outerPack)}";
            }
            if (outerPack.Body.isKinematic || !outerPack.Body.useGravity)
            {
                key = "outer-physics";
                return $"[outer-physics] {Describe(outerPack)}: expected isKinematic False, useGravity True; {BodyState(outerPack)}";
            }
            foreach (var content in ContentItems())
            {
                var t = content.transform;
                if (content.State != KD1ItemState.Stored)
                {
                    key = "slot-state:" + content.name;
                    return $"[slot-state] {Describe(content)}: expected State Stored, actual {content.State}";
                }
                if (t.parent != content.HomeSlot)
                {
                    key = "slot-parent:" + content.name;
                    return $"[slot-parent] {Describe(content)}: expected parent HomeSlot '{Name(content.HomeSlot)}', actual '{Name(t.parent)}'";
                }
                var distance = t.localPosition.magnitude;
                if (distance > SlotDistanceTolerance)
                {
                    key = "slot-position:" + content.name;
                    return $"[slot-position] {Describe(content)}: local position in '{Name(content.HomeSlot)}' ({t.localPosition.x * 1000f:F4}, {t.localPosition.y * 1000f:F4}, " +
                           $"{t.localPosition.z * 1000f:F4}) mm, {distance * 1000f:F4} mm from the slot origin (tolerance {SlotDistanceTolerance * 1000f:F3} mm)";
                }
                var angle = Quaternion.Angle(t.localRotation, Quaternion.identity);
                if (angle > SlotAngleTolerance)
                {
                    key = "slot-rotation:" + content.name;
                    return $"[slot-rotation] {Describe(content)}: local rotation {angle:F4} deg from identity in '{Name(content.HomeSlot)}' (tolerance {SlotAngleTolerance:F3} deg)";
                }
                if (content.BaseModel.activeSelf || content.ChangedModel.activeSelf || content.StoredColliders.activeSelf || content.BaseColliders.activeSelf ||
                    content.ChangedColliders.activeSelf)
                {
                    key = "slot-display:" + content.name;
                    return $"[slot-display] {Describe(content)}: expected every model and collider set off inside the sealed container; {Sets(content)}";
                }
                if (!content.Body.isKinematic || content.Body.useGravity || content.Body.excludeLayers.value != ~0)
                {
                    key = "slot-physics:" + content.name;
                    return $"[slot-physics] {Describe(content)}: expected isKinematic True, useGravity False, excludeLayers -1; {BodyState(content)}";
                }
            }
            key = null;
            return null;
        }

        static string Name(UnityEngine.Object o) => o != null ? o.name : "none";

        static string Describe(KD1Item item) => $"{item.name} ({item.Kind} {item.Number + 1})";

        static string Sets(KD1Item item) =>
            $"base model {item.BaseModel.activeSelf}, changed model {item.ChangedModel.activeSelf}, base colliders {item.BaseColliders.activeSelf}, " +
            $"changed colliders {item.ChangedColliders.activeSelf}" + (item.StoredColliders != null ? $", stored colliders {item.StoredColliders.activeSelf}" : "");

        static string BodyState(KD1Item item) =>
            $"isKinematic {item.Body.isKinematic}, useGravity {item.Body.useGravity}, excludeLayers {item.Body.excludeLayers.value}";

        // A course's check right after it asked for ResetEquipment: null when everything was restored exactly (the reset's own result
        // check, outer pack pose included), else the reason.
        public string CheckRestored()
        {
            var problem = CheckUsable();
            return problem ?? VerifyInitial();
        }

        // A course's check before a new run (start screen, start press): null when a run can start now, else the reason. The same
        // logical state as after a reset (nothing selected, fitted, open or pulled out, everything stored and hidden, the rest
        // physics), but not the free outer pack's exact pose: resting with gravity it may settle on the workbench, and that is not
        // a used kit. Nothing is moved or reset here.
        public string CheckReadyToStart() => CheckReadyToStart(out _);

        // The same, with a key naming the failed check and object only (no measured value), for logging a change once.
        public string CheckReadyToStart(out string key)
        {
            var problem = CheckUsable();
            if (problem != null)
            {
                key = "usable";
                return problem;
            }
            return VerifyStoredState(out key);
        }

        string CheckUsable()
        {
            if (!valid)
                return "the equipment setup is invalid";
            if (IsFaulted)
                return "the last reset failed: " + FaultReason;
            if (resetting)
                return "a reset is running";
            return null;
        }

        IEnumerable<KD1Item> ContentItems()
        {
            foreach (var packet in packets)
                yield return packet;
            foreach (var pad in pads)
                yield return pad;
        }

        // ---------- Queries ----------

        // A hand that can use its fitted pad on a surface now: a pad fitted on it, unlocked equipment, focus, that hand tracked (its
        // mount's driver) and nothing selected by any interactor of that hand. No trigger is involved.
        public bool IsReadyForSurface(KD1Hand hand, out KD1Item pad)
        {
            pad = FittedOn(hand);
            if (pad == null || InputLocked || !CourseAllows || !Application.isFocused)
                return false;
            return IsHandTracked(hand) && !HoldsAnything(hand);
        }

        // The hand's controller is tracked now (its mount's TrackedPoseDriver; not a Transform or a selection). The one tracking
        // check for a fitted pad's hand, used by the surface readiness above and by the guide.
        public bool IsHandTracked(KD1Hand hand)
        {
            var mount = MountOf(hand);
            return mount != null && KD1Tracking.IsTracked(mount.Driver);
        }

        internal bool AllowsMotion(TrackedPoseDriver driver) => !InputLocked && !Suspended && Application.isFocused && KD1Tracking.IsTracked(driver);

        public bool IsHolderTracked(KD1Item item) =>
            item != null && item.Grab.isSelected && KD1Tracking.IsTracked(DriverOf(item.Grab.firstInteractorSelecting));

        public KD1Hand HolderHand(KD1Item item) =>
            item != null && item.Grab.isSelected ? HandOf(DriverOf(item.Grab.firstInteractorSelecting)) : KD1Hand.None;

        public KD1Hand HandOf(TrackedPoseDriver driver) =>
            driver == null ? KD1Hand.None : driver == leftDriver ? KD1Hand.Left : driver == rightDriver ? KD1Hand.Right : KD1Hand.None;

        void Register(IXRSelectInteractor hand)
        {
            if (hand == null || armed.ContainsKey(hand))
                return;
            hands.Add(hand);
            armed[hand] = false;
        }

        // The one Near-Far interactor under a controller (Starter Assets), or null when there is not exactly one.
        static NearFarInteractor TriggerSourceOf(TrackedPoseDriver driver)
        {
            if (driver == null)
                return null;
            var found = driver.GetComponentsInChildren<NearFarInteractor>(true);
            return found.Length == 1 ? found[0] : null;
        }

        KD1HandMount MountOf(KD1Hand hand) => hand == KD1Hand.Left ? leftMount : hand == KD1Hand.Right ? rightMount : null;

        internal TrackedPoseDriver DriverOf(IXRInteractor interactor)
        {
            if (interactor == null || interactor.transform == null)
                return null;
            if (!drivers.TryGetValue(interactor, out var driver) || driver == null)
            {
                driver = interactor.transform.GetComponentInParent<TrackedPoseDriver>();
                drivers[interactor] = driver;
            }
            return driver;
        }

        KD1Item ItemOf(IXRInteractable interactable)
        {
            var index = IndexOf(interactable);
            return index >= 0 ? items[index] : null;
        }

        int IndexOf(IXRInteractable interactable)
        {
            if (!valid)
                return -1;
            for (var i = 0; i < items.Length; i++)
            {
                if (ReferenceEquals(items[i].Grab, interactable))
                    return i;
            }
            return -1;
        }

        static void CancelSelection(XRGrabInteractable grab)
        {
            if (grab == null || !grab.isSelected)
                return;
            var manager = grab.interactionManager;
            if (manager != null)
                manager.CancelInteractableSelection((IXRSelectInteractable)grab);
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        // ---------- Validation ----------

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (outerPack == null || outerPack.Kind != KD1ItemKind.OuterPack)
                problems.Add("outer pack missing or not an OuterPack item");
            else if (!outerPack.TryValidate(out var outerError))
                problems.Add(outerError);
            if (packets == null || packets.Length != PacketCount || pads == null || pads.Length != PacketCount)
            {
                problems.Add($"{PacketCount} packets and {PacketCount} pads required");
            }
            else
            {
                var slots = new HashSet<Transform>();
                for (var i = 0; i < PacketCount; i++)
                {
                    var packet = packets[i];
                    var pad = pads[i];
                    if (packet == null || packet.Kind != KD1ItemKind.Packet || packet.Number != i)
                        problems.Add($"packet {i} missing or misnumbered");
                    else if (!packet.TryValidate(out var packetError))
                        problems.Add(packetError);
                    else if (packet.Container != outerPack || !slots.Add(packet.HomeSlot))
                        problems.Add($"packet {i} must have its own slot in the outer pack");
                    if (pad == null || pad.Kind != KD1ItemKind.Pad || pad.Number != i)
                        problems.Add($"pad {i} missing or misnumbered");
                    else if (!pad.TryValidate(out var padError))
                        problems.Add(padError);
                    else if (pad.Container != packet || !slots.Add(pad.HomeSlot))
                        problems.Add($"pad {i} must have its own slot in packet {i}");
                }
                var seen = new HashSet<Collider>();
                foreach (var group in new[] { new[] { outerPack }, packets, pads })
                {
                    foreach (var item in group)
                    {
                        if (item == null || item.Grab == null)
                            continue;
                        foreach (var c in item.Grab.colliders)
                        {
                            if (c != null && !seen.Add(c))
                                problems.Add($"collider '{c.name}' is listed by two grabs");
                        }
                    }
                }
            }
            if (leftDriver == null || rightDriver == null || leftDriver == rightDriver)
                problems.Add("two different controller drivers required");
            if (leftDriver != null && TriggerSourceOf(leftDriver) == null || rightDriver != null && TriggerSourceOf(rightDriver) == null)
                problems.Add("each controller needs exactly one Near-Far interactor (the hand's trigger source for removing a pad)");
            if (leftMount == null || rightMount == null || leftMount == rightMount)
                problems.Add("two hand mounts required");
            else
            {
                if (!leftMount.TryValidate(out var leftError))
                    problems.Add(leftError);
                if (!rightMount.TryValidate(out var rightError))
                    problems.Add(rightError);
                if (leftMount.Hand != KD1Hand.Left || rightMount.Hand != KD1Hand.Right || leftMount.Driver != leftDriver || rightMount.Driver != rightDriver)
                    problems.Add("mounts must be the left and right hand of the two drivers");
            }
            if (!ReferenceEquals(courseGate, null) && (courseGate == null || !(courseGate is IKD1CourseGate)))
                problems.Add(nameof(courseGate) + " missing or not an IKD1CourseGate");
            if (looseItems == null || (outerPack != null && looseItems.IsChildOf(outerPack.transform)))
                problems.Add(nameof(looseItems) + " missing or inside the kit");
            if (!IsFinite(exitMargin) || exitMargin < 0f || !IsFinite(noticeSeconds) || noticeSeconds < 0f)
                problems.Add("engineering settings out of range");
            error = problems.Count > 0 ? $"KD1Equipment '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }
    }
}
