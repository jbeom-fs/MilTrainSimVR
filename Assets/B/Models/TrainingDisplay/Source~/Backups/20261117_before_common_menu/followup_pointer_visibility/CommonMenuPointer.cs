using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.B.CommonMenu
{
    // Separate UI-only ray: a held rifle or two held pieces never prevent pressing the three menu buttons.
    internal sealed class CommonMenuPointer
    {
        readonly GameObject root;
        readonly TrackedPoseDriver driver;
        readonly XRRayInteractor ray;
        readonly XRInputButtonReader press;
        readonly LineRenderer line;
        bool armed;

        public CommonMenuPointer(TrackedPoseDriver hand, bool left, Material material)
        {
            driver = hand;
            root = new GameObject(left ? "MenuRay_Left" : "MenuRay_Right");
            root.SetActive(false);
            root.transform.SetParent(hand.transform, false);
            ray = root.AddComponent<XRRayInteractor>();
            ray.handedness = left ? InteractorHandedness.Left : InteractorHandedness.Right;
            ray.interactionLayers = new InteractionLayerMask();
            // XRI passes this mask to TrackedDeviceGraphicRaycaster too: zero would reject every menu Graphic.
            ray.raycastMask = 1 << CommonMenuPanel.UILayer;
            ray.maxRaycastDistance = 5f;
            ray.lineType = XRRayInteractor.LineType.StraightLine;
            ray.enableUIInteraction = true;
            ray.blockUIOnInteractableSelection = false;
            ray.selectInput = new XRInputButtonReader(inputSourceMode: XRInputButtonReader.InputSourceMode.Unused);
            ray.activateInput = new XRInputButtonReader(inputSourceMode: XRInputButtonReader.InputSourceMode.Unused);
            press = new XRInputButtonReader(inputSourceMode: XRInputButtonReader.InputSourceMode.ManualValue);
            ray.uiPressInput = press;
            line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = 2;
            line.startWidth = line.endWidth = 0.002f;
            line.startColor = line.endColor = Color.white;
            line.useWorldSpace = true;
            line.enabled = material != null;
            root.SetActive(true);
        }

        public void Update(InputAction trigger, bool shown)
        {
            var tracked = IsTracked(driver);
            var down = trigger != null && trigger.IsPressed();
            if (!tracked || !shown)
                armed = false;
            else if (!down)
                armed = true;
            press.manualPerformed = shown && tracked && armed && down;
            press.manualValue = press.manualPerformed ? 1f : 0f;
            ray.enableUIInteraction = shown && tracked;
            line.enabled = shown && tracked && line.sharedMaterial != null;
        }

        public void Draw()
        {
            if (root == null || !line.enabled)
                return;
            var start = root.transform.position;
            var end = ray.TryGetCurrentUIRaycastResult(out var hit) ? hit.worldPosition : start + root.transform.forward * 2f;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        public void Dispose()
        {
            if (root == null)
                return;
            press.manualPerformed = false;
            root.SetActive(false);
            Object.Destroy(root);
        }

        internal static bool IsTracked(TrackedPoseDriver pose)
        {
            if (pose == null || !pose.isActiveAndEnabled)
                return false;
            var action = pose.trackingStateInput.action;
            const InputTrackingState both = InputTrackingState.Position | InputTrackingState.Rotation;
            if (action == null || !action.enabled || action.controls.Count == 0 ||
                ((InputTrackingState)action.ReadValue<int>() & both) != both)
                return false;
            var position = pose.positionInput.action;
            var rotation = pose.rotationInput.action;
            return position != null && position.enabled && position.controls.Count > 0 &&
                   rotation != null && rotation.enabled && rotation.controls.Count > 0;
        }
    }
}
