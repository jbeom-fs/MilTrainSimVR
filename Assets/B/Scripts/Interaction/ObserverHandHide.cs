using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.B.Interaction
{
    // Keeps the rig's base VR hands (the C_ControllerHand_L/R renderers under B_XR_Origin) out of the scene's observer views
    // (protective clothing mirror, K1 mirror, K1 rear view): the mannequin / avatar and its worn equipment are what those views
    // show. The HMD and every other camera keep the hands' current state.
    //
    // The single owner of this per-camera hiding in a scene: one active instance lists every observer camera (a second one stays
    // off with an error, so two never save and restore the same renderers over each other). Rendering only: no object, hand or
    // controller is switched off, so tracking, finger poses, interactors, grabs and UI keep running.
    //
    // Per observer camera: right before it renders (beginCameraRendering) each hand's forceRenderingOff is saved and set; right
    // after it (endCameraRendering) the saved value is put back. The saved value is whatever the hand's owners decided before
    // rendering (ProtectiveClothingUserBody hides a hand while its glove is drawn, in LateUpdate and before render, never during
    // a camera), so a hidden gloved hand stays hidden and a shown hand comes back for the HMD; nothing is forced to false and no
    // glove or wear state is touched. The result does not depend on the camera order. A hide left open (no end callback before
    // another camera or the next frame) is closed first with the saved values; disabling or unloading closes it too.
    //
    // References only (no ProtectiveClothing or K1 type), so it can be used in any B scene: observer cameras and the hand
    // renderers are linked per scene. The HMD camera (tag MainCamera) and any camera under the hands' XR Origin are refused.
    [DisallowMultipleComponent]
    public sealed class ObserverHandHide : MonoBehaviour
    {
        [Tooltip("Observer cameras of this scene (mirrors, rear view). Never the HMD camera.")]
        [SerializeField] Camera[] observerCameras = Array.Empty<Camera>();
        [Tooltip("Base VR hand renderers of the rig (C_ControllerHand_L/R under B_XR_Origin) hidden from the observer cameras only.")]
        [SerializeField] Renderer[] baseHands = Array.Empty<Renderer>();

        static ObserverHandHide owner;
        bool[] saved = Array.Empty<bool>();
        bool hiding;
        bool subscribed;

        public IReadOnlyList<Camera> ObserverCameras => observerCameras;
        public IReadOnlyList<Renderer> BaseHands => baseHands;

        // Enter Play Mode without a domain reload keeps statics.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOwner() => owner = null;

        void OnEnable()
        {
            if (subscribed)
                return;
            if (owner != null && owner != this)
            {
                Debug.LogError($"[ObserverHandHide] '{name}': '{owner.name}' already hides the base hands in observer views; this one stays off.", this);
                return;
            }
            if (!TryValidate(out var error))
            {
                Debug.LogError("[ObserverHandHide] " + error + " Base hands stay visible in observer views.", this);
                return;
            }
            saved = new bool[baseHands.Length];
            hiding = false;
            owner = this;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
            subscribed = true;
        }

        void OnDisable()
        {
            if (!subscribed)
                return;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            subscribed = false;
            Restore();
            if (owner == this)
                owner = null;
        }

        // Before the hands' owners decide this frame's state (their LateUpdate and before-render run after this point).
        void LateUpdate() => Restore();

        void OnBeginCamera(ScriptableRenderContext _, Camera renderingCamera)
        {
            var observer = IsObserver(renderingCamera);
            // Already hidden for an observer whose end never came: keep the saved values for this observer, or close it first.
            if (hiding)
            {
                if (observer)
                    return;
                Restore();
            }
            if (!observer)
                return;
            for (var i = 0; i < baseHands.Length; i++)
            {
                var hand = baseHands[i];
                if (hand == null)
                    continue;
                saved[i] = hand.forceRenderingOff;
                hand.forceRenderingOff = true;
            }
            hiding = true;
        }

        void OnEndCamera(ScriptableRenderContext _, Camera renderingCamera)
        {
            if (hiding && IsObserver(renderingCamera))
                Restore();
        }

        void Restore()
        {
            if (!hiding)
                return;
            for (var i = 0; i < baseHands.Length; i++)
            {
                if (baseHands[i] != null)
                    baseHands[i].forceRenderingOff = saved[i];
            }
            hiding = false;
        }

        bool IsObserver(Camera camera)
        {
            if (camera == null)
                return false;
            foreach (var observer in observerCameras)
            {
                if (observer == camera)
                    return true;
            }
            return false;
        }

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (observerCameras == null || observerCameras.Length == 0 || Array.IndexOf(observerCameras, null) >= 0)
                problems.Add("observer cameras missing");
            if (baseHands == null || baseHands.Length == 0 || Array.IndexOf(baseHands, null) >= 0)
                problems.Add("base hand renderers missing");
            if (problems.Count == 0)
            {
                if (new HashSet<Camera>(observerCameras).Count != observerCameras.Length || new HashSet<Renderer>(baseHands).Count != baseHands.Length)
                    problems.Add("a camera or hand listed twice");
                foreach (var camera in observerCameras)
                {
                    if (camera.CompareTag("MainCamera"))
                        problems.Add($"camera '{camera.name}' is the HMD camera (MainCamera)");
                    foreach (var hand in baseHands)
                    {
                        var rig = hand.GetComponentInParent<XROrigin>(true);
                        if (rig != null && camera.transform.IsChildOf(rig.transform))
                        {
                            problems.Add($"camera '{camera.name}' is on the hands' rig");
                            break;
                        }
                    }
                }
            }
            error = problems.Count > 0 ? $"ObserverHandHide '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }
    }
}
