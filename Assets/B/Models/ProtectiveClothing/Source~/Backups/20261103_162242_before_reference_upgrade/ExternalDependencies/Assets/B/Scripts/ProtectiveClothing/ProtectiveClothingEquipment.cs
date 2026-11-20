using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.B.ProtectiveClothing
{
    // Protective clothing basic wearing (equipment chapter): the only owner of the six parts' wear state. Each part is worn on its
    // own, in any order; there is no training order, runner or completion screen here (next chapter). Guidance and markers only
    // read this component.
    //
    // Grab: each pickup has an XRGrabInteractable (Grip attach point, single select, either hand). This component is a select
    // filter of all six, registered from Awake to OnDestroy (so also while this component is disabled): a hand already holding
    // one part cannot take another, nothing is grabbed while resetting, after a failed reset or while this component is
    // disabled, and a worn part cannot be grabbed (its pickup is hidden as well). A selection in progress always passes, so the
    // filter never drops a part.
    //
    // Invalid setup (a missing reference, a wrong parts array, an invalid radius): the filter cannot be trusted, so Awake
    // disables every XRGrabInteractable it can reach instead (the pieces' grabs and every grab under this object); a part with
    // no grab to disable is named in the error. Nothing is grabbed or worn and the guide shows the setup error.
    //
    // Wearing (LateUpdate, order 50: after XRI's selection changes and Instantaneous grab moves of this frame, both in the
    // interaction manager's Update at -105, and after its Late phase at -105): a part is worn once when it is not worn yet, its
    // pickup is selected now, the selecting hand's tracking is valid now (read from its TrackedPoseDriver at this moment, not
    // from a cache or isSelected), input is focused, no reset runs, and its pickup Attach point is within wearRadius of its worn
    // Attach point (world distance only: no angle, face, tilt or hand direction; no release or hold time). A pickup that is not
    // held never wears, even inside the radius. Left and right are separate parts with separate targets, so a left glove at the
    // right hand never wears. Wear, in order: re-entry guard, the selection ended through XRI's cancel path, the pickup's motion
    // stopped and the pickup hidden, the aligned worn mesh shown (never moved), the part recorded as worn. No select-exit
    // callback does any of this, so a cancel never wears, releases or resets anything twice.
    //
    // A released pickup that falls more than fallLimit below this object (out of the play space) is put back on the workbench.
    //
    // Reset (development reset button; the training retry is the next chapter): new grabs and wearing locked, every held part's
    // selection cancelled, the six wear records cleared, worn meshes hidden, pickups back to their saved workbench pose,
    // visibility and rest (velocities cleared), select order, hand driver lookups and the guide target cleared; then the
    // result is checked, and the lock is released only when every part is back. A failed reset keeps input locked (IsFaulted)
    // with its reason; it is never shown as done. One reset per frame: a second press or hand in the same frame is ignored.
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
        [Tooltip("Wear distance (m): the held pickup's Attach point within this world distance of its worn Attach point wears the " +
                 "part. A VR handling value for this development scene, not a training criterion. Finite and above zero.")]
        [SerializeField] float wearRadius = 0.15f;
        [Tooltip("A released pickup this far (m) below this object is put back on the workbench.")]
        [SerializeField, Min(0.1f)] float fallLimit = 1f;

        readonly Dictionary<IXRSelectInteractor, TrackedPoseDriver> drivers = new Dictionary<IXRSelectInteractor, TrackedPoseDriver>();
        StartPose[] startPoses = Array.Empty<StartPose>();
        bool[] worn = Array.Empty<bool>();
        int[] selectOrder = Array.Empty<int>();
        int selectCounter;
        bool valid;
        bool subscribed;
        bool resetting;
        bool wearing;
        int resetFrame = -1;

        public IReadOnlyList<ProtectiveClothingPiece> Pieces => pieces;
        public float WearRadius => wearRadius;
        public bool WearRadiusValid => IsFinite(wearRadius) && wearRadius > 0f;
        public bool IsValid => valid;
        public bool IsResetting => resetting;
        // A reset failed: input stays locked until a reset succeeds.
        public bool IsFaulted { get; private set; }
        public string FaultReason { get; private set; }
        public bool InputLocked => !valid || resetting || IsFaulted || !isActiveAndEnabled;
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
            valid = TryValidate(out var error);
            if (!valid)
            {
                BlockGrabsForInvalidSetup(error);
                return;
            }
            worn = new bool[pieces.Length];
            selectOrder = new int[pieces.Length];
            startPoses = new StartPose[pieces.Length];
            for (var i = 0; i < pieces.Length; i++)
            {
                var t = pieces[i].transform;
                var body = pieces[i].Body;
                startPoses[i] = new StartPose
                {
                    parent = t.parent, localPosition = t.localPosition, localRotation = t.localRotation, active = t.gameObject.activeSelf,
                    kinematic = body.isKinematic, gravity = body.useGravity
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

        // Only the order of grabs for the guide target; never a state change of the parts.
        void OnSelectEntered(SelectEnterEventArgs args)
        {
            var index = IndexOf(args.interactableObject);
            if (index >= 0)
                selectOrder[index] = ++selectCounter;
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
            if (InputLocked || wearing || worn[index])
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
                return;
            }
            var focused = Application.isFocused;
            for (var i = 0; i < pieces.Length; i++)
            {
                if (worn[i])
                    continue;
                if (pieces[i].Grab.isSelected)
                    TryWear(i, focused);
                else if (pieces[i].transform.position.y < transform.position.y - fallLimit)
                    ReturnFallen(i);
            }
            GuidedPiece = FindGuided();
        }

        void TryWear(int index, bool focused)
        {
            var piece = pieces[index];
            if (!focused || !WearRadiusValid || HandTracking(piece) != ProtectiveClothingHandTracking.Tracked)
                return;
            if (Vector3.Distance(piece.PickupAttach.position, piece.WornAttach.position) > wearRadius)
                return;
            Wear(index);
        }

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
                Debug.Log($"[ProtectiveClothing] {piece.Part} worn ({WornCount}/{pieces.Length}).", this);
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
            Debug.Log("[ProtectiveClothing] Reset: all parts back on the workbench, nothing worn.", this);
            return true;
        }

        void RestorePickup(int index)
        {
            var piece = pieces[index];
            var t = piece.transform;
            var start = startPoses[index];
            var body = piece.Body;
            if (t.gameObject.activeSelf != start.active)
                t.gameObject.SetActive(start.active);
            StopBody(body);
            body.isKinematic = true;
            if (t.parent != start.parent)
                t.SetParent(start.parent, false);
            t.SetLocalPositionAndRotation(start.localPosition, start.localRotation);
            body.position = t.position;
            body.rotation = t.rotation;
            body.useGravity = start.gravity;
            body.isKinematic = start.kinematic;
            StopBody(body);
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
            if (!WearRadiusValid)
                problems.Add(nameof(wearRadius) + " (finite, above zero)");
            if (!IsFinite(fallLimit) || fallLimit <= 0f)
                problems.Add(nameof(fallLimit));
            error = problems.Count > 0 ? $"ProtectiveClothingEquipment '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }
    }
}
