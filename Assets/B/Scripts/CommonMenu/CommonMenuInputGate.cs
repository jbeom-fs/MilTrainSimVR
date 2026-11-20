using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace MilTrainSimVR.B.CommonMenu
{
    // Runtime changes belong to this pause only. References and enabled flags are restored, never shared action bindings.
    internal sealed class CommonMenuInputGate : IXRSelectFilter
    {
        readonly List<XRBaseInteractable> targets = new List<XRBaseInteractable>();
        readonly List<LocomotionProvider> locomotion = new List<LocomotionProvider>();
        readonly List<(XRBaseInputInteractor owner, XRInputButtonReader reader)> activation = new List<(XRBaseInputInteractor, XRInputButtonReader)>();
        readonly List<(Behaviour owner, bool enabled)> ui = new List<(Behaviour, bool)>();
        readonly List<(CanvasGroup group, bool interactable, bool rays, bool created)> canvases = new List<(CanvasGroup, bool, bool, bool)>();
        bool blocked;

        public bool canProcess => blocked;

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) =>
            !(interactable is BaseTeleportationInteractable) && interactor != null && interactor.IsSelecting(interactable);

        public void Block(Transform menu)
        {
            if (blocked)
                return;
            blocked = true;
            foreach (var target in Object.FindObjectsByType<XRBaseInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (target.transform.IsChildOf(menu))
                    continue;
                // A normal select exit can teleport. Cancel explicitly while opening the menu instead.
                if (target is BaseTeleportationInteractable && target.isSelected && target.interactionManager != null)
                    target.interactionManager.CancelInteractableSelection((IXRSelectInteractable)target);
                target.selectFilters.Add(this);
                targets.Add(target);
            }
            foreach (var provider in Object.FindObjectsByType<LocomotionProvider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!provider.enabled)
                    continue;
                locomotion.Add(provider);
                provider.enabled = false;
            }
            foreach (var interactor in Object.FindObjectsByType<XRBaseInputInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (interactor.transform.IsChildOf(menu))
                    continue;
                activation.Add((interactor, interactor.activateInput));
                interactor.activateInput = new XRInputButtonReader(inputSourceMode: XRInputButtonReader.InputSourceMode.Unused);
            }
            foreach (var near in Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                ui.Add((near, near.enableUIInteraction));
                near.enableUIInteraction = false;
            }
            foreach (var ray in Object.FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                ui.Add((ray, ray.enableUIInteraction));
                ray.enableUIInteraction = false;
            }
            foreach (var poke in Object.FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                ui.Add((poke, poke.enableUIInteraction));
                poke.enableUIInteraction = false;
            }
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.transform.IsChildOf(menu))
                    continue;
                var group = canvas.GetComponent<CanvasGroup>();
                var created = group == null;
                if (created)
                    group = canvas.gameObject.AddComponent<CanvasGroup>();
                canvases.Add((group, group.interactable, group.blocksRaycasts, created));
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        public void Restore()
        {
            if (!blocked)
                return;
            blocked = false;
            foreach (var target in targets)
                if (target != null)
                    target.selectFilters.Remove(this);
            foreach (var value in activation)
                if (value.owner != null)
                    value.owner.activateInput = value.reader;
            foreach (var value in ui)
            {
                if (value.owner is NearFarInteractor near && near != null)
                    near.enableUIInteraction = value.enabled;
                else if (value.owner is XRRayInteractor ray && ray != null)
                    ray.enableUIInteraction = value.enabled;
                else if (value.owner is XRPokeInteractor poke && poke != null)
                    poke.enableUIInteraction = value.enabled;
            }
            foreach (var value in canvases)
            {
                if (value.group == null)
                    continue;
                value.group.interactable = value.interactable;
                value.group.blocksRaycasts = value.rays;
                if (value.created)
                    Object.Destroy(value.group);
            }
            foreach (var provider in locomotion)
                if (provider != null)
                    provider.enabled = true;
            targets.Clear();
            activation.Clear();
            ui.Clear();
            canvases.Clear();
            locomotion.Clear();
        }
    }
}
