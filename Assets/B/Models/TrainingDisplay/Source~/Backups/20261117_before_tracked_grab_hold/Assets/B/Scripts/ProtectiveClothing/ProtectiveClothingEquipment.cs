using System;
using System.Collections.Generic;
using MilTrainSimVR.B.Interaction;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.B.ProtectiveClothing
{
    // Optional extra condition of a course on top of the equipment's own rules (the equipment never references a course).
    // AllowsSelect is asked by the equipment's select filter for a NEW selection only (a selection in progress always passes);
    // AllowsWear by the wear judgement every frame for a held part. A course answers both from the same allowance.
    public interface IProtectiveClothingWearGate
    {
        bool AllowsSelect(IXRSelectInteractor interactor, ProtectiveClothingPart part);
        bool AllowsWear(ProtectiveClothingPart part);
    }

    // Protective clothing basic wearing on the user's own body (equipment chapter): the only owner of the six parts' wear state.
    // Each part is worn on its own, in any order; there is no training order, runner or completion screen here. Guidance,
    // markers, the body's display policy and a course only read this component.
    //
    // Wear gate (course scene only; empty in the equipment scene, where every part stays free): a component implementing
    // IProtectiveClothingWearGate. When set, a new grab needs its AllowsSelect and wearing needs its AllowsWear on top of every
    // rule below; while the gate is missing, disabled or inactive nothing is grabbed or worn (a stopped course never frees the
    // equipment). A refused held part is disarmed, never dropped.
    //
    // Grab: each pickup has an XRGrabInteractable (single select, either hand), near or far by ray. Its Attach point comes to the
    // hand with the pickup's rotation kept (dynamic attach matching the hand's attach rotation only, attach ease-in): a far grab
    // brings it smoothly, and while it is still being brought (GrabBring) the part is disarmed, so arriving inside a zone never
    // wears it (it must be taken out of the zone first, as after any grab). This component is a select
    // filter of all six, registered from Awake to OnDestroy (so also while this component is disabled): a hand already holding
    // one part cannot take another, nothing is grabbed while resetting, after a failed reset or while this component is
    // disabled, and a worn part cannot be grabbed (its pickup is hidden as well). A selection in progress always passes, so the
    // filter never drops a part; the other hand may take a held part over (single select).
    //
    // The user body must be running (ProtectiveClothingUserBody.IsRunning: valid, started, enabled) for new grabs and wearing; while
    // it is not, input is locked like a paused equipment (wear records and pickups stay as they are).
    //
    // Invalid setup (a missing reference, a wrong parts array, an invalid user body or zone): the filter cannot be trusted, so
    // Awake disables every XRGrabInteractable it can reach instead (the pieces' grabs and every grab under this object); a part
    // with no grab to disable is named in the error. Nothing is grabbed or worn and the guide shows the setup error.
    //
    // Wear target: the part's zone on the user body (ProtectiveClothingUserBody: chest front, waist front, the target hand, the
    // overboot zone above that side's instep and ankle), estimated from the HMD and controllers. Judged in LateUpdate (order 50: after XRI's
    // selection changes and Instantaneous grab moves of this frame, both in the interaction manager's Update at -105, after its
    // Late phase at -105, and after the body's solve at 40, so the pickup and the zone are both this frame's). A part is worn once
    // when all hold now: not worn yet, its pickup is selected, the selecting hand's tracking is valid (read from its
    // TrackedPoseDriver now, not from a cache or isSelected), input is focused, no reset runs, the zone follows live tracking
    // (head, and for a glove the target hand), a glove is held by the OTHER hand (left glove in the right hand, so grabbing a
    // glove never wears it on the hand that grabbed it), the part is armed, and its pickup Attach point is within the zone
    // radius (world distance only: no angle, face, tilt or hand direction; no release or hold time). Armed: a grab (also a hand
    // over) disarms the part; it arms once its Attach point has been seen beyond the zone radius by armMargin while held and
    // everything above holds, so taking a part from the workbench or from the other hand never wears it at that moment (no wait
    // time). Release, a lost holding hand / head / target hand, focus loss, the glove in the wrong hand, a paused or stopped user
    // body, a disabled equipment and a reset all disarm it again. A held, disarmed part that is still inside its zone (radius plus
    // armMargin) is reported as waiting to clear the zone, so the guide asks to take it a little away instead of "bring it". A
    // pickup that is not held never wears. Left and right are separate parts with separate zones. Wear, in order: re-entry guard, the
    // part recorded as worn, the selection ended through XRI's cancel path, the pickup's motion stopped and the pickup hidden,
    // the user body's worn mesh shown (it deforms with the body; never moved here). No select-exit callback does any of this, so
    // a cancel never wears, releases or resets anything twice.
    //
    // A released pickup that falls more than fallLimit below this object (out of the play space) is put back on the workbench.
    //
    // Reset (development reset button; the training retry is the next chapter): new grabs and wearing locked, every held part's
    // selection cancelled, the six wear records cleared, worn meshes hidden, pickups back to their saved workbench pose,
    // visibility and rest (velocities cleared), select order, arming, hand driver lookups and the guide target cleared; then the
    // result is checked, and the lock is released only when every part is back. The user's position, view and tracking origin
    // are never touched. A failed reset keeps input locked (IsFaulted) with its reason; it is never shown as done. One reset per
    // frame: a second press or hand in the same frame is ignored.
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class ProtectiveClothingEquipment : MonoBehaviour, IXRSelectFilter
    {
        struct StartPose
        {
            public Transform parent;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public bool active;
            public bool kinematic;
            public bool gravity;
        }

        [Tooltip("The six parts, one per ProtectiveClothingPart, in that order.")]
        [SerializeField] ProtectiveClothingPiece[] pieces = Array.Empty<ProtectiveClothingPiece>();
        [Tooltip("The user's body: wear zones, the worn meshes, the hand drivers.")]
        [SerializeField] ProtectiveClothingUserBody body;
        [Tooltip("A grabbed part arms once its Attach point is this far (m) beyond its zone radius; only an armed part is worn.")]
        [SerializeField] float armMargin = 0.05f;
        [Tooltip("A released pickup this far (m) below this object is put back on the workbench.")]
        [SerializeField, Min(0.1f)] float fallLimit = 1f;
        [Tooltip("Course scene only: component implementing IProtectiveClothingWearGate (extra grab and wear condition). Empty in the equipment scene.")]
        [SerializeField] MonoBehaviour wearGate;

        readonly Dictionary<IXRSelectInteractor, TrackedPoseDriver> drivers = new Dictionary<IXRSelectInteractor, TrackedPoseDriver>();
        StartPose[] startPoses = Array.Empty<StartPose>();
        bool[] worn = Array.Empty<bool>();
        int[] selectOrder = Array.Empty<int>();
        bool[] armed = Array.Empty<bool>();
        bool[] waitingToClear = Array.Empty<bool>();
        int selectCounter;
        bool valid;
        bool gated;
        bool subscribed;
        bool resetting;
        bool wearing;
        int resetFrame = -1;

        public IReadOnlyList<ProtectiveClothingPiece> Pieces => pieces;
        public ProtectiveClothingUserBody Body => body;
        public MonoBehaviour WearGate => wearGate;
        public float ArmMargin => armMargin;
        public bool IsValid => valid;
        public bool IsResetting => resetting;
        // A reset failed: input stays locked until a reset succeeds.
        public bool IsFaulted { get; private set; }
        public string FaultReason { get; private set; }
        // The user body not running (failed start, switched off) also locks: nothing would follow the user.
        public bool InputLocked => !valid || !body.IsRunning || resetting || IsFaulted || !isActiveAndEnabled;
        // Invalid setup only: grabs disabled by Awake, and pieces with no grab to disable.
        public int BlockedGrabCount { get; private set; }
        public IReadOnlyList<string> UnblockableParts { get; private set; } = Array.Empty<string>();
        // Raised on every wear and reset (views rebuild on change).
        public int Version { get; private set; }
        // The held, unworn part grabbed last (the one the guide names and marks); null when none.
        public ProtectiveClothingPiece GuidedPiece { get; private set; }

        public ProtectiveClothingPiece Piece(ProtectiveClothingPart part) => pieces[(int)part];
        public bool IsWorn(ProtectiveClothingPart part) => valid && worn[(int)part];

        public int WornCount
        {
            get
            {
                var count = 0;
                foreach (var w in worn)
                {
                    if (w)
                        count++;
                }
                return count;
            }
        }

        public bool AllWorn => valid && WornCount == pieces.Length;

        // Always processed while this object exists: a disabled equipment still refuses new grabs (see InputLocked).
        bool IXRSelectFilter.canProcess => this != null;

        void Awake()
        {
            // Set once a gate is assigned, also when that object is later destroyed (fake null): a lost gate keeps the lock.
            gated = !ReferenceEquals(wearGate, null);
            valid = TryValidate(out var error);
            if (!valid)
            {
                BlockGrabsForInvalidSetup(error);
                return;
            }
            worn = new bool[pieces.Length];
            selectOrder = new int[pieces.Length];
            armed = new bool[pieces.Length];
            waitingToClear = new bool[pieces.Length];
            startPoses = new StartPose[pieces.Length];
            for (var i = 0; i < pieces.Length; i++)
            {
                var t = pieces[i].transform;
                var pickupBody = pieces[i].Body;
                startPoses[i] = new StartPose
                {
                    parent = t.parent, localPosition = t.localPosition, localRotation = t.localRotation, active = t.gameObject.activeSelf,
                    kinematic = pickupBody.isKinematic, gravity = pickupBody.useGravity
                };
            }
            foreach (var piece in pieces)
            {
                piece.Grab.selectFilters.Add(this);
                piece.Grab.selectEntered.AddListener(OnSelectEntered);
            }
            subscribed = true;
        }

        void OnDestroy()
        {
            if (!subscribed)
                return;
            foreach (var piece in pieces)
            {
                if (piece == null || piece.Grab == null)
                    continue;
                piece.Grab.selectFilters.Remove(this);
                piece.Grab.selectEntered.RemoveListener(OnSelectEntered);
            }
            subscribed = false;
        }

        void OnDisable()
        {
            drivers.Clear();
            GuidedPiece = null;
            Disarm();
        }

        void Disarm()
        {
            for (var i = 0; i < armed.Length; i++)
            {
                armed[i] = false;
                waitingToClear[i] = false;
            }
        }

        // Every grab this setup can reach is switched off: the pieces' grabs (null entries and missing references skipped) and every
        // XRGrabInteractable under this object. A disabled interactable is unregistered by XRI and cannot be selected by any hand.
        void BlockGrabsForInvalidSetup(string error)
        {
            var grabs = new HashSet<XRGrabInteractable>();
            var unblockable = new List<string>();
            var missingEntries = 0;
            if (pieces != null)
            {
                foreach (var piece in pieces)
                {
                    if (piece == null)
                    {
                        missingEntries++;
                        continue;
                    }
                    if (piece.Grab != null)
                        grabs.Add(piece.Grab);
                    else if (piece.TryGetComponent<XRGrabInteractable>(out var own))
                        grabs.Add(own);
                    else
                        unblockable.Add(piece.name);
                }
            }
            foreach (var grab in GetComponentsInChildren<XRGrabInteractable>(true))
                grabs.Add(grab);
            foreach (var grab in grabs)
                grab.enabled = false;
            BlockedGrabCount = grabs.Count;
            if (missingEntries > 0)
                unblockable.Add($"{missingEntries} empty piece entr{(missingEntries == 1 ? "y" : "ies")}");
            UnblockableParts = unblockable;
            Debug.LogError("[ProtectiveClothing] " + error + $" Setup invalid: grabbing and wearing are blocked ({grabs.Count} grab(s) disabled)." +
                           (unblockable.Count > 0 ? " No grab found to disable for: " + string.Join(", ", unblockable) + " (check these objects)." : ""), this);
        }

        // Only the order of grabs for the guide target and the arming of the grabbed part; never a wear or visibility change.
        void OnSelectEntered(SelectEnterEventArgs args)
        {
            var index = IndexOf(args.interactableObject);
            if (index < 0)
                return;
            selectOrder[index] = ++selectCounter;
            armed[index] = false;
        }

        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (interactor == null)
                return false;
            // A selection in progress always passes: this filter never ends a grab.
            if (interactor.IsSelecting(interactable))
                return true;
            var index = IndexOf(interactable);
            if (index < 0)
                return true;
            if (InputLocked || wearing || worn[index] || !GateAllows(index, interactor))
                return false;
            // One hand, one protective clothing part.
            for (var i = 0; i < pieces.Length; i++)
            {
                if (i != index && interactor.IsSelecting(pieces[i].Grab))
                    return false;
            }
            return true;
        }

        void LateUpdate()
        {
            if (InputLocked)
            {
                GuidedPiece = null;
                Disarm();
                return;
            }
            var focused = Application.isFocused;
            for (var i = 0; i < pieces.Length; i++)
            {
                waitingToClear[i] = false;
                if (worn[i])
                    continue;
                if (pieces[i].Grab.isSelected)
                {
                    TryWear(i, focused);
                    continue;
                }
                armed[i] = false;
                if (pieces[i].transform.position.y < transform.position.y - fallLimit)
                    ReturnFallen(i);
            }
            GuidedPiece = FindGuided();
        }

        void TryWear(int index, bool focused)
        {
            var piece = pieces[index];
            var holder = DriverOf(piece.Grab.firstInteractorSelecting);
            if (!focused || !GateAllows(index, null) || ProtectiveClothingTracking.Of(holder) != ProtectiveClothingHandTracking.Tracked || IsHeldByWrongHand(piece, holder) ||
                body.ZoneTracking(piece.Part) != ProtectiveClothingHandTracking.Tracked || GrabBring.IsBringing(piece.Grab))
            {
                armed[index] = false;
                return;
            }
            var distance = Vector3.Distance(piece.PickupAttach.position, body.ZonePoint(piece.Part).position);
            var radius = body.ZoneRadius(piece.Part);
            if (!armed[index])
            {
                // Seen clear of the zone once: from the next frame on, entering the zone wears the part.
                if (distance > radius + armMargin)
                    armed[index] = true;
                else
                    waitingToClear[index] = true;
                return;
            }
            if (distance <= radius)
                Wear(index);
        }

        // The course's extra condition (see the class notes): a new selection by this interactor, or (interactor null) wearing.
        // No gate assigned: always allowed. Assigned but missing, disabled or inactive: never.
        bool GateAllows(int index, IXRSelectInteractor interactor)
        {
            if (!gated)
                return true;
            if (wearGate == null || !wearGate.isActiveAndEnabled || !(wearGate is IProtectiveClothingWearGate gate))
                return false;
            var part = (ProtectiveClothingPart)index;
            return interactor != null ? gate.AllowsSelect(interactor, part) : gate.AllowsWear(part);
        }

        // A glove must be held by the other hand: the left glove by the right controller, the right glove by the left.
        bool IsHeldByWrongHand(ProtectiveClothingPiece piece, TrackedPoseDriver holder)
        {
            var required = RequiredHolder(piece.Part);
            return required != null && holder != required;
        }

        TrackedPoseDriver RequiredHolder(ProtectiveClothingPart part) => part switch
        {
            ProtectiveClothingPart.GloveLeft => body.RightDriver,
            ProtectiveClothingPart.GloveRight => body.LeftDriver,
            _ => null
        };

        // The part is held by the hand it is meant for (a glove must be taken over by the other hand first).
        public bool IsHeldByWrongHand(ProtectiveClothingPiece piece) =>
            valid && piece != null && piece.Grab != null && piece.Grab.isSelected && IsHeldByWrongHand(piece, DriverOf(piece.Grab.firstInteractorSelecting));

        // Held, disarmed and still within its zone plus armMargin: it must be taken a little away before it can be worn. Written
        // by this frame's judgement (read by the guide after it).
        public bool IsWaitingToClearZone(ProtectiveClothingPiece piece)
        {
            var index = IndexOf(piece != null ? piece.Grab : null);
            return index >= 0 && waitingToClear[index];
        }

        // The zone of this part follows live tracking now (head; for a glove also the target hand).
        public ProtectiveClothingHandTracking ZoneTracking(ProtectiveClothingPiece piece) =>
            valid && piece != null ? body.ZoneTracking(piece.Part) : ProtectiveClothingHandTracking.Missing;

        void Wear(int index)
        {
            if (wearing || worn[index])
                return;
            wearing = true;
            try
            {
                var piece = pieces[index];
                // Recorded first, so the select filter refuses the part at once (also during the cancel below).
                worn[index] = true;
                CancelSelection(piece.Grab);
                StopBody(piece.Body);
                piece.Body.isKinematic = true;
                piece.gameObject.SetActive(false);
                piece.Worn.SetActive(true);
                selectOrder[index] = 0;
                Version++;
            }
            finally
            {
                wearing = false;
            }
        }

        void ReturnFallen(int index)
        {
            RestorePickup(index);
            Debug.LogWarning($"[ProtectiveClothing] {pieces[index].Part} fell out of the play space and was put back on the workbench.", this);
        }

        ProtectiveClothingPiece FindGuided()
        {
            ProtectiveClothingPiece best = null;
            var order = 0;
            for (var i = 0; i < pieces.Length; i++)
            {
                if (worn[i] || !pieces[i].Grab.isSelected || selectOrder[i] <= order)
                    continue;
                best = pieces[i];
                order = selectOrder[i];
            }
            return best;
        }

        // Tracking of the hand selecting this part, read now (Missing when not selected).
        public ProtectiveClothingHandTracking HandTracking(ProtectiveClothingPiece piece)
        {
            if (piece == null || piece.Grab == null || !piece.Grab.isSelected)
                return ProtectiveClothingHandTracking.Missing;
            return ProtectiveClothingTracking.Of(DriverOf(piece.Grab.firstInteractorSelecting));
        }

        // ---------- Reset ----------

        // Development reset (see the class notes). Returns false when ignored (already resetting, invalid references, a second call
        // in the same frame) or when the result check failed (then IsFaulted and FaultReason are set and input stays locked).
        public bool ResetEquipment()
        {
            if (!valid)
            {
                Debug.LogError("[ProtectiveClothing] Reset refused: references are invalid (see the earlier error).", this);
                return false;
            }
            if (resetting || resetFrame == Time.frameCount)
                return false;
            resetting = true;
            resetFrame = Time.frameCount;
            string problem;
            try
            {
                foreach (var piece in pieces)
                    CancelSelection(piece.Grab);
                for (var i = 0; i < pieces.Length; i++)
                {
                    worn[i] = false;
                    selectOrder[i] = 0;
                    armed[i] = false;
                    waitingToClear[i] = false;
                    pieces[i].Worn.SetActive(false);
                    RestorePickup(i);
                }
                selectCounter = 0;
                drivers.Clear();
                GuidedPiece = null;
                Version++;
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
                Debug.LogError("[ProtectiveClothing] Reset failed: " + problem + " Grabbing and wearing stay locked.", this);
                return false;
            }
            return true;
        }

        void RestorePickup(int index)
        {
            var piece = pieces[index];
            var t = piece.transform;
            var start = startPoses[index];
            var pickupBody = piece.Body;
            if (t.gameObject.activeSelf != start.active)
                t.gameObject.SetActive(start.active);
            StopBody(pickupBody);
            pickupBody.isKinematic = true;
            if (t.parent != start.parent)
                t.SetParent(start.parent, false);
            t.SetLocalPositionAndRotation(start.localPosition, start.localRotation);
            pickupBody.position = t.position;
            pickupBody.rotation = t.rotation;
            pickupBody.useGravity = start.gravity;
            pickupBody.isKinematic = start.kinematic;
            StopBody(pickupBody);
        }

        string VerifyInitial()
        {
            for (var i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                var t = piece.transform;
                var start = startPoses[i];
                if (piece.Grab.isSelected)
                    return piece.Part + " is still selected";
                if (worn[i] || piece.Worn.activeSelf)
                    return piece.Part + " is still worn or its worn mesh is shown";
                if (t.gameObject.activeSelf != start.active || t.parent != start.parent ||
                    (t.localPosition - start.localPosition).sqrMagnitude > 1e-8f || Quaternion.Angle(t.localRotation, start.localRotation) > 0.01f)
                    return piece.Part + " pickup is not at its workbench pose";
                if (piece.Body.isKinematic != start.kinematic || piece.Body.useGravity != start.gravity)
                    return piece.Part + " pickup rest settings not restored";
            }
            return null;
        }

        // ---------- Helpers ----------

        int IndexOf(IXRInteractable interactable)
        {
            if (!valid)
                return -1;
            for (var i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] != null && ReferenceEquals(pieces[i].Grab, interactable))
                    return i;
            }
            return -1;
        }

        TrackedPoseDriver DriverOf(IXRSelectInteractor interactor)
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

        static void CancelSelection(XRGrabInteractable grab)
        {
            if (grab == null || !grab.isSelected)
                return;
            var manager = grab.interactionManager;
            if (manager != null)
                manager.CancelInteractableSelection((IXRSelectInteractable)grab);
        }

        static void StopBody(Rigidbody body)
        {
            if (body == null || body.isKinematic)
                return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            var count = Enum.GetValues(typeof(ProtectiveClothingPart)).Length;
            if (pieces == null || pieces.Length != count)
                problems.Add($"{count} pieces required");
            else
            {
                for (var i = 0; i < pieces.Length; i++)
                {
                    if (pieces[i] == null)
                        problems.Add($"piece {(ProtectiveClothingPart)i} missing");
                    else if (pieces[i].Part != (ProtectiveClothingPart)i)
                        problems.Add($"piece {i} is {pieces[i].Part}, expected {(ProtectiveClothingPart)i}");
                    else if (!pieces[i].TryValidate(out var pieceError))
                        problems.Add(pieceError);
                }
            }
            if (body == null)
                problems.Add("user body missing");
            else if (!body.TryValidate(out var bodyError))
                problems.Add(bodyError);
            else if (pieces != null && pieces.Length == count)
            {
                for (var i = 0; i < pieces.Length; i++)
                {
                    if (pieces[i] != null && pieces[i].Worn != body.WornSkins[i].gameObject)
                        problems.Add($"piece {(ProtectiveClothingPart)i} worn mesh is not the user body's worn mesh of that part");
                }
            }
            if (!IsFinite(armMargin) || armMargin < 0f)
                problems.Add(nameof(armMargin) + " (finite, not negative)");
            if (!IsFinite(fallLimit) || fallLimit <= 0f)
                problems.Add(nameof(fallLimit));
            if (!ReferenceEquals(wearGate, null) && (wearGate == null || !(wearGate is IProtectiveClothingWearGate)))
                problems.Add(nameof(wearGate) + " missing or not an IProtectiveClothingWearGate");
            error = problems.Count > 0 ? $"ProtectiveClothingEquipment '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }
    }
}
