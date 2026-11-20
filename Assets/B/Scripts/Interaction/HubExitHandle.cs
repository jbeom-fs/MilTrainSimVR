using System.Collections;
using MilTrainSimVR.A.Common;
using MilTrainSimVR.B.CommonMenu;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace MilTrainSimVR.B.Interaction
{
    // The training field's way out: a near grab of the real door knob (the Round_knob of sage_glass_door, room side) fades the
    // screen and goes back to the hub with A's CourseRoute.ReturnToHub (CourseRoute.Last stays the course the hub entered, so the
    // hub puts the user back at that course's portal). Lives on a small B object under the scene's Door_Range with its own
    // BoxCollider around the knob and an XRSimpleInteractable; the door leaf, the doorway and walking up to the door do nothing.
    //
    // Near only, decided on the select frame. The B rig has one NearFarInteractor per hand that does both near and far selects,
    // and the far attach settings of the rig make selectionRegion and the attach offset no proof of where a select came from. So
    // the proof is the hand's own near caster at the moment of the select: its sphere (castRadius at the palm cast origin,
    // limited to palmReach) must overlap the knob collider now. As a hover filter the same check keeps the far ray from ever
    // targeting the knob (XRI only makes hoverable interactables valid targets), so nothing far is ever selected that could later
    // turn near. As a select filter it also requires, on that frame:
    //   - the select input pressed on this very frame (logicalSelectState.wasPerformedThisFrame; no manual interaction), so a press
    //     held from far away while walking or reaching in never selects; letting go and pressing again at the knob does;
    //   - the hand's position and rotation tracked (HandTracking), the application focused and not paused, the common menu closed
    //     (its own input gate also refuses every select while open), and the hub scene loadable;
    //   - no exit already under way (the first accepted select of a frame wins; any later or second-hand select is refused).
    // A selection already held always passes (XRI re-checks the filters while selecting), so the filter never ends one itself.
    //
    // One exit per scene visit (instance state: the next visit's object starts fresh, nothing static is kept). It uses ScreenFader
    // directly instead of A's DoorTransition, whose static busy flag would stay set if the scene left during its fade. Hover and
    // touch only show nothing; no prompt or UI is added.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class HubExitHandle : MonoBehaviour, IXRHoverFilter, IXRSelectFilter
    {
        [Tooltip("Box around the door knob (on this object). Not a trigger: the rig's near casters ignore triggers.")]
        [SerializeField] Collider knobCollider;
        [Tooltip("Scene screen fader (SceneSystems/ScreenFader). Empty: the first one in the scene.")]
        [SerializeField] ScreenFader fader;
        [Tooltip("Largest distance (m) from the hand's near cast origin (palm) to the knob box; the near caster's own radius is used when smaller.")]
        [SerializeField, Min(0.01f)] float palmReach = 0.1f;
        [SerializeField, Min(0f)] float fadeOutSeconds = 0.4f;

        XRSimpleInteractable interactable;
        bool valid;
        bool leaving;
        // ReturnToHub was called: the scene is about to unload; the black screen stays until the hub is drawn.
        bool loadRequested;
        bool appPaused;

        public Collider KnobCollider => knobCollider;
        public bool IsLeaving => leaving;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            valid = TryValidate(out var error);
            if (!valid)
                Debug.LogError("[HubExitHandle] " + error + " The door handle cannot take the user to the hub.", this);
            if (fader == null)
                fader = FindFirstObjectByType<ScreenFader>();
            if (fader == null)
                Debug.LogWarning("[HubExitHandle] No ScreenFader in the scene: the hub loads without a fade.", this);
            interactable.hoverFilters.Add(this);
            interactable.selectFilters.Add(this);
            interactable.selectEntered.AddListener(OnSelectEntered);
        }

        void OnDestroy()
        {
            if (interactable == null)
                return;
            interactable.hoverFilters.Remove(this);
            interactable.selectFilters.Remove(this);
            interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        void OnDisable()
        {
            // An exit stopped during its fade (this object switched off) must not leave the screen black or the handle dead.
            if (!leaving || loadRequested)
                return;
            StopAllCoroutines();
            leaving = false;
            if (fader != null)
                fader.SetAlpha(0f);
        }

        void OnApplicationPause(bool paused) => appPaused = paused;

        // Always processed: with this component off or invalid the knob refuses everything instead of becoming a plain ray target.
        public bool canProcess => true;

        public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable target) =>
            valid && isActiveAndEnabled && !leaving && IsNearKnob(interactor);

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable target)
        {
            if (interactor != null && interactor.IsSelecting(target))
                return true;
            if (!valid || !isActiveAndEnabled || leaving || CommonPauseMenu.IsPaused || !Application.isFocused || appPaused)
                return false;
            if (!(interactor is XRBaseInputInteractor input) || input.isPerformingManualInteraction ||
                !input.logicalSelectState.wasPerformedThisFrame)
                return false;
            if (!HandTracking.IsPoseTracked(HandTracking.DriverOf(interactor)) || !IsNearKnob(interactor))
                return false;
            return CourseRoute.CanLoad(CourseRoute.HubScenePath);
        }

        // The hand's near caster overlaps the knob box now: palm cast origin within its sphere radius (capped by palmReach).
        bool IsNearKnob(IXRInteractor interactor)
        {
            if (!(interactor is NearFarInteractor nearFar) || !nearFar.isActiveAndEnabled || !nearFar.enableNearCasting)
                return false;
            if (!(nearFar.nearInteractionCaster is SphereInteractionCaster caster) || !caster.isActiveAndEnabled)
                return false;
            var origin = caster.effectiveCastOrigin;
            if (origin == null || !knobCollider.enabled || !knobCollider.gameObject.activeInHierarchy)
                return false;
            if ((caster.physicsLayerMask.value & (1 << knobCollider.gameObject.layer)) == 0)
                return false;
            var radius = Mathf.Min(caster.castRadius * caster.transform.lossyScale.x, palmReach);
            if (!(radius > 0f))
                return false;
            var palm = origin.position;
            return (knobCollider.ClosestPoint(palm) - palm).sqrMagnitude <= radius * radius;
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (leaving || !valid)
                return;
            leaving = true;
            Debug.Log($"[HubExitHandle] {name}: door knob grabbed → hub");
            StartCoroutine(Leave());
        }

        IEnumerator Leave()
        {
            if (fader != null)
                yield return fader.FadeTo(1f, fadeOutSeconds);
            // CourseRoute keeps Last (the course entered from the hub); the hub lights the screen again.
            loadRequested = true;
            CourseRoute.ReturnToHub();
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (knobCollider == null)
                error = "Knob collider missing.";
            else if (knobCollider.isTrigger)
                error = $"Knob collider '{knobCollider.name}' is a trigger; the near casters ignore triggers.";
            else if (knobCollider.GetComponentInParent<XRSimpleInteractable>(true) != GetComponent<XRSimpleInteractable>())
                error = $"Knob collider '{knobCollider.name}' does not belong to this handle's interactable.";
            return error == null;
        }
    }
}
