using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.UI;

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
        readonly LineRenderer reticle;
        readonly Material visualMaterial;
        static readonly Color IdleColor = new Color(0.94f, 0.95f, 0.96f, 0.8f);
        static readonly Color HoverColor = new Color(0.3f, 0.78f, 1f);
        static readonly Color PressColor = new Color(1f, 0.78f, 0.35f);
        bool armed;

        public CommonMenuPointer(TrackedPoseDriver hand, bool left, Material material)
        {
            driver = hand;
            root = new GameObject(left ? "MenuRay_Left" : "MenuRay_Right");
            root.layer = CommonMenuPanel.UILayer;
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
            // Canvas sorting can cover an Overlay shader too. Keep both visuals above the menu, with an owned material.
            if (material != null)
            {
                visualMaterial = new Material(material) { name = "CommonMenuPointer (Runtime)", renderQueue = 4010 };
                if (visualMaterial.HasProperty("_BaseColor"))
                    visualMaterial.SetColor("_BaseColor", Color.white);
            }
            line = CreateLine(root, true, CommonMenuPanel.SortingOrder + 1, 0.003f);
            line.positionCount = 2;
            var cursor = new GameObject("MenuPointer");
            cursor.layer = CommonMenuPanel.UILayer;
            cursor.transform.SetParent(root.transform, false);
            reticle = CreateLine(cursor, false, CommonMenuPanel.SortingOrder + 2, 0.0018f);
            reticle.loop = true;
            reticle.positionCount = 24;
            for (var i = 0; i < reticle.positionCount; i++)
            {
                var angle = i * (Mathf.PI * 2f / reticle.positionCount);
                reticle.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.007f);
            }
            reticle.enabled = false;
            root.SetActive(true);
        }

        LineRenderer CreateLine(GameObject owner, bool worldSpace, int order, float width)
        {
            var renderer = owner.AddComponent<LineRenderer>();
            renderer.sharedMaterial = visualMaterial;
            renderer.useWorldSpace = worldSpace;
            renderer.alignment = LineAlignment.View;
            renderer.startWidth = renderer.endWidth = width;
            renderer.startColor = renderer.endColor = IdleColor;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = visualMaterial != null;
            return renderer;
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
            if (!line.enabled)
                reticle.enabled = false;
        }

        public void Draw()
        {
            if (root == null || !line.enabled)
                return;
            var start = root.transform.position;
            var hasHit = ray.TryGetCurrentUIRaycastResult(out var hit);
            var end = hasHit ? hit.worldPosition : start + root.transform.forward * 2f;
            var button = hasHit ? hit.gameObject.GetComponentInParent<Button>() : null;
            var color = button != null && button.isActiveAndEnabled && button.IsInteractable()
                ? (press.manualPerformed ? PressColor : HoverColor) : IdleColor;
            line.startColor = line.endColor = color;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            reticle.enabled = hasHit;
            if (hasHit)
            {
                var normal = hit.worldNormal.sqrMagnitude > 0.001f ? hit.worldNormal.normalized : -root.transform.forward;
                if (Vector3.Dot(normal, root.transform.forward) > 0f)
                    normal = -normal;
                reticle.transform.SetPositionAndRotation(end + normal * 0.002f, Quaternion.LookRotation(normal));
                reticle.startColor = reticle.endColor = color;
            }
        }

        public void Dispose()
        {
            press.manualPerformed = false;
            if (root != null)
            {
                root.SetActive(false);
                Object.Destroy(root);
            }
            if (visualMaterial != null)
                Object.Destroy(visualMaterial);
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
