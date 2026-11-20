using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace MilTrainSimVR.B.Interaction
{
    // B copy of the XRI Starter Assets ControllerInputActionManager for the B XR rig only (B_XR_Origin variant, B input action copy):
    // one per controller, mediating its Near-Far interactor, its teleport interactor and its stick actions. Differences from the
    // sample (stick roles stay fixed in B: left stick moves, right stick turns and teleports):
    // - A grab never takes the stick. The B rig attaches far grabs near the hand (no attach offset, no stick manipulation), and
    //   whatever the selection region is, holding something never disables move, turn or teleport.
    // - Teleport aiming hides the Near-Far interactor only while it holds nothing, so a held part or item is never dropped by it.
    // - Scrollable UI under the ray: this hand's stick scrolls and this hand's locomotion is off (the other hand keeps its own).
    //   Scrolling starts only while this hand's locomotion is idle, as in the sample. Leaving the UI, locomotion returns only after
    //   this hand's stick has come back to the centre, so a stick still pushed for scrolling never turns or teleports at once.
    // Actions are enabled and disabled on the B input action copy only (never on the shared XRI asset), so no state is left for
    // the other scenes; the XR Origin's InputActionManager enables and disables the whole copy with the rig.
    [DisallowMultipleComponent]
    public sealed class BControllerInputActionManager : MonoBehaviour
    {
        [Header("Interactors")]
        [SerializeField] NearFarInteractor m_NearFarInteractor;
        [SerializeField] XRRayInteractor m_TeleportInteractor;

        [Header("Controller Actions")]
        [SerializeField] InputActionReference m_TeleportMode;
        [SerializeField] InputActionReference m_TeleportModeCancel;
        [SerializeField] InputActionReference m_Turn;
        [SerializeField] InputActionReference m_SnapTurn;
        [SerializeField] InputActionReference m_Move;
        [SerializeField] InputActionReference m_UIScroll;
        [Tooltip("This controller's thumbstick value (always enabled), read to wait for the stick centre after scrolling.")]
        [SerializeField] InputActionReference m_StickValue;

        [Header("Locomotion Settings")]
        [Tooltip("If true, continuous movement will be enabled. If false, teleport will be enabled.")]
        [SerializeField] bool m_SmoothMotionEnabled;
        [Tooltip("If true, continuous turn will be enabled. If false, snap turn will be enabled.")]
        [SerializeField] bool m_SmoothTurnEnabled;

        [Header("UI Settings")]
        [Tooltip("If true, UI scrolling will be enabled. Locomotion of this hand is off while pointing at scrollable UI.")]
        [SerializeField] bool m_UIScrollingEnabled = true;

        [Header("Mediation Events")]
        [SerializeField] UnityEvent<IXRRayProvider> m_RayInteractorChanged;

        // Stick deflection (squared) below which the stick counts as centred: 75 % of the default 0.5 press threshold, as the sample.
        const float SqrStickCentre = 0.375f * 0.375f;

        bool m_StartCalled;
        bool m_PostponedDeactivateTeleport;
        bool m_HoveringScrollableUI;
        bool m_WaitStickCentre;
        bool m_Subscribed;
        readonly HashSet<InputAction> m_LocomotionUsers = new HashSet<InputAction>();

        void OnEnable()
        {
            if (m_TeleportInteractor != null)
                m_TeleportInteractor.gameObject.SetActive(false);
            m_HoveringScrollableUI = false;
            m_WaitStickCentre = false;
            m_LocomotionUsers.Clear();
            if (m_StartCalled)
            {
                UpdateLocomotionActions();
                UpdateUIActions();
            }
            Subscribe();
        }

        void OnDisable() => Unsubscribe();

        // After the InputActionManager enabled the whole action copy in its OnEnable.
        void Start()
        {
            m_StartCalled = true;
            UpdateLocomotionActions();
            UpdateUIActions();
        }

        // Runs after the interaction manager, so a teleport interactor that was let go has completed its teleport.
        void Update()
        {
            if (m_PostponedDeactivateTeleport)
            {
                if (m_TeleportInteractor != null)
                    m_TeleportInteractor.gameObject.SetActive(false);
                m_PostponedDeactivateTeleport = false;
            }

            if (m_WaitStickCentre && !m_HoveringScrollableUI && StickCentred())
            {
                m_WaitStickCentre = false;
                UpdateLocomotionActions();
            }
        }

        void Subscribe()
        {
            if (m_Subscribed)
                return;
            if (m_NearFarInteractor != null)
            {
                m_NearFarInteractor.uiHoverEntered.AddListener(OnUIHoverEntered);
                m_NearFarInteractor.uiHoverExited.AddListener(OnUIHoverExited);
            }
            var teleportMode = ActionOf(m_TeleportMode);
            if (teleportMode != null)
            {
                teleportMode.performed += OnStartTeleport;
                teleportMode.performed += OnStartLocomotion;
                teleportMode.canceled += OnCancelTeleport;
                teleportMode.canceled += OnStopLocomotion;
            }
            var teleportCancel = ActionOf(m_TeleportModeCancel);
            if (teleportCancel != null)
                teleportCancel.performed += OnCancelTeleport;
            foreach (var reference in new[] { m_Move, m_Turn, m_SnapTurn })
            {
                var action = ActionOf(reference);
                if (action == null)
                    continue;
                action.started += OnStartLocomotion;
                action.canceled += OnStopLocomotion;
            }
            m_Subscribed = true;
        }

        void Unsubscribe()
        {
            if (!m_Subscribed)
                return;
            if (m_NearFarInteractor != null)
            {
                m_NearFarInteractor.uiHoverEntered.RemoveListener(OnUIHoverEntered);
                m_NearFarInteractor.uiHoverExited.RemoveListener(OnUIHoverExited);
            }
            var teleportMode = ActionOf(m_TeleportMode);
            if (teleportMode != null)
            {
                teleportMode.performed -= OnStartTeleport;
                teleportMode.performed -= OnStartLocomotion;
                teleportMode.canceled -= OnCancelTeleport;
                teleportMode.canceled -= OnStopLocomotion;
            }
            var teleportCancel = ActionOf(m_TeleportModeCancel);
            if (teleportCancel != null)
                teleportCancel.performed -= OnCancelTeleport;
            foreach (var reference in new[] { m_Move, m_Turn, m_SnapTurn })
            {
                var action = ActionOf(reference);
                if (action == null)
                    continue;
                action.started -= OnStartLocomotion;
                action.canceled -= OnStopLocomotion;
            }
            m_Subscribed = false;
        }

        void OnStartTeleport(InputAction.CallbackContext context)
        {
            m_PostponedDeactivateTeleport = false;
            if (m_TeleportInteractor != null)
                m_TeleportInteractor.gameObject.SetActive(true);
            // A Near-Far interactor holding a part or an item stays active, so its selection is never cancelled by teleporting.
            if (m_NearFarInteractor != null && !m_NearFarInteractor.hasSelection)
                m_NearFarInteractor.gameObject.SetActive(false);
            m_RayInteractorChanged?.Invoke(m_TeleportInteractor);
        }

        void OnCancelTeleport(InputAction.CallbackContext context)
        {
            // The teleport interactor is turned off in Update, after it had the chance to complete the teleport.
            m_PostponedDeactivateTeleport = true;
            if (m_NearFarInteractor != null)
                m_NearFarInteractor.gameObject.SetActive(true);
            m_RayInteractorChanged?.Invoke(m_NearFarInteractor);
        }

        void OnStartLocomotion(InputAction.CallbackContext context) => m_LocomotionUsers.Add(context.action);

        void OnStopLocomotion(InputAction.CallbackContext context)
        {
            m_LocomotionUsers.Remove(context.action);
            if (m_LocomotionUsers.Count == 0 && m_HoveringScrollableUI)
            {
                DisableAllLocomotionActions();
                UpdateUIActions();
            }
        }

        void OnUIHoverEntered(UIHoverEventArgs args)
        {
            m_HoveringScrollableUI = m_UIScrollingEnabled && args.deviceModel.isScrollable;
            UpdateUIActions();
            // Locomotion under way keeps going; scrolling starts once it has stopped (OnStopLocomotion).
            if (m_HoveringScrollableUI && m_LocomotionUsers.Count == 0)
            {
                m_WaitStickCentre = false;
                DisableAllLocomotionActions();
            }
        }

        void OnUIHoverExited(UIHoverEventArgs args)
        {
            var wasScrolling = m_HoveringScrollableUI;
            m_HoveringScrollableUI = false;
            UpdateUIActions();
            if (wasScrolling && m_LocomotionUsers.Count == 0 && !StickCentred())
            {
                // Still pushed for scrolling: locomotion returns once the stick is back at the centre (Update).
                m_WaitStickCentre = true;
                DisableAllLocomotionActions();
                return;
            }
            m_WaitStickCentre = false;
            UpdateLocomotionActions();
        }

        bool StickCentred()
        {
            var stick = ActionOf(m_StickValue);
            return stick == null || !stick.enabled || stick.ReadValue<Vector2>().sqrMagnitude <= SqrStickCentre;
        }

        void UpdateLocomotionActions()
        {
            SetEnabled(m_Move, m_SmoothMotionEnabled);
            SetEnabled(m_TeleportMode, !m_SmoothMotionEnabled);
            SetEnabled(m_TeleportModeCancel, !m_SmoothMotionEnabled);
            SetEnabled(m_Turn, !m_SmoothMotionEnabled && m_SmoothTurnEnabled);
            SetEnabled(m_SnapTurn, !m_SmoothMotionEnabled && !m_SmoothTurnEnabled);
        }

        void DisableAllLocomotionActions()
        {
            SetEnabled(m_TeleportMode, false);
            SetEnabled(m_TeleportModeCancel, false);
            SetEnabled(m_Move, false);
            SetEnabled(m_Turn, false);
            SetEnabled(m_SnapTurn, false);
        }

        void UpdateUIActions() => SetEnabled(m_UIScroll, m_UIScrollingEnabled && m_HoveringScrollableUI && m_LocomotionUsers.Count == 0);

        static void SetEnabled(InputActionReference reference, bool enabled)
        {
            var action = ActionOf(reference);
            if (action == null)
                return;
            if (enabled)
                action.Enable();
            else
                action.Disable();
        }

        static InputAction ActionOf(InputActionReference reference) => reference != null ? reference.action : null;
    }
}
