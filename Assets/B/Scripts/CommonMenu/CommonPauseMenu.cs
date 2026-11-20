using System;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace MilTrainSimVR.B.CommonMenu
{
    // Scene-owned reusable prefab. Only this controller owns global time/audio pause; content listeners own their gameplay hold.
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public sealed class CommonPauseMenu : MonoBehaviour
    {
        enum MenuState { Closed, Open, WaitingForRelease, Leaving }

        [SerializeField] XROrigin xrOrigin;
        [SerializeField] TMP_FontAsset koreanFont;
        [SerializeField] Material panelMaterial;
        [SerializeField] Material fontOverlayMaterial;
        [SerializeField] Material rayMaterial;
        [SerializeField] CommonMenuMainRoute mainRoute;
        [SerializeField, Min(0.3f)] float distance = 1.1f;

        static CommonPauseMenu instance;
        public static bool IsPaused => instance != null && instance.state != MenuState.Closed;
        public static event Action Paused;
        public static event Action Resumed;
        public static event Action Leaving;

        readonly CommonMenuInputGate gate = new CommonMenuInputGate();
        readonly List<(MonoBehaviour owner, ICommonMenuListener listener)> listeners = new List<(MonoBehaviour, ICommonMenuListener)>();
        readonly List<InputAction> actions = new List<InputAction>();
        readonly CommonMenuPointer[] pointers = new CommonMenuPointer[2];
        readonly TrackedPoseDriver[] hands = new TrackedPoseDriver[2];
        readonly InputAction[] triggers = new InputAction[2];
        readonly InputAction[] grips = new InputAction[2];
        readonly InputAction[] sticks = new InputAction[2];
        InputAction menuKey;
        CommonMenuPanel panel;
        GameObject ownEventSystem;
        MenuState state;
        bool worldHeld;
        float savedTime;
        bool savedAudio;
        bool appPaused;
        bool HasFocus => Application.isFocused && !appPaused;
        int toggleFrame = -1;
        int neutralFrame = -1;

        void Awake()
        {
            if (koreanFont == null || panelMaterial == null || fontOverlayMaterial == null || rayMaterial == null)
            {
                Debug.LogError("[CommonMenu] Font and overlay/ray materials must be assigned on the prefab.", this);
                enabled = false;
                return;
            }
            if (mainRoute == null)
                mainRoute = GetComponent<CommonMenuMainRoute>();
            panel = new CommonMenuPanel(transform, koreanFont, panelMaterial, fontOverlayMaterial, Continue, GoMain, Quit);
        }

        void OnEnable()
        {
            if (panel == null)
                return;
            if (instance != null && instance != this && instance.state != MenuState.Leaving)
            {
                Debug.LogWarning("[CommonMenu] A menu is already active; the duplicate is disabled.", this);
                enabled = false;
                return;
            }
            instance = this;
            menuKey = MakeAction("Pause menu", InputActionType.Button, "<XRController>{LeftHand}/{MenuButton}");
            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? "LeftHand" : "RightHand";
                triggers[i] = MakeAction(side + " menu click", InputActionType.Button, "<XRController>{" + side + "}/{TriggerButton}");
                grips[i] = MakeAction(side + " release", InputActionType.Button, "<XRController>{" + side + "}/{GripButton}");
                sticks[i] = MakeAction(side + " centre", InputActionType.Value, "<XRController>{" + side + "}/{Primary2DAxis}");
            }
            menuKey.performed += OnMenuKey;
        }

        InputAction MakeAction(string name, InputActionType type, string binding)
        {
            var action = new InputAction(name, type, binding);
            actions.Add(action);
            action.Enable();
            return action;
        }

        void OnDisable()
        {
            var resume = state != MenuState.Leaving;
            state = MenuState.Closed;
            panel?.Hide();
            RestoreWorld(resume);
            if (menuKey != null)
                menuKey.performed -= OnMenuKey;
            foreach (var action in actions)
                action.Dispose();
            actions.Clear();
            menuKey = null;
            if (instance == this)
                instance = null;
        }

        void OnDestroy()
        {
            panel?.Dispose();
            if (ownEventSystem != null)
                Destroy(ownEventSystem);
        }

        void Update()
        {
            for (var i = 0; i < pointers.Length; i++)
                pointers[i]?.Update(triggers[i], state == MenuState.Open && HasFocus);
            if (state == MenuState.WaitingForRelease)
            {
                if (!ControlsReleased())
                    neutralFrame = -1;
                else if (neutralFrame < 0)
                    neutralFrame = Time.frameCount;
                else if (Time.frameCount > neutralFrame)
                {
                    // XRI and course LateUpdate must see the releases while still held before gameplay resumes.
                    state = MenuState.Closed;
                    panel.Hide();
                    RestoreWorld(true);
                }
            }
        }

        void LateUpdate()
        {
            foreach (var pointer in pointers)
                pointer?.Draw();
        }

        void OnApplicationPause(bool paused) => appPaused = paused;

        void OnMenuKey(InputAction.CallbackContext context)
        {
            if (toggleFrame == Time.frameCount || state == MenuState.Leaving || !HasFocus)
                return;
            toggleFrame = Time.frameCount;
            if (state == MenuState.Open)
                Continue();
            else if (state == MenuState.WaitingForRelease)
                ShowPanel();
            else
                Open();
        }

        public void Open()
        {
            if (state != MenuState.Closed || instance != this || !ResolveRig() || !EnsureEventSystem())
                return;
            listeners.Clear();
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (behaviour.isActiveAndEnabled && behaviour is ICommonMenuListener listener)
                    listeners.Add((behaviour, listener));
            state = MenuState.Open;
            HoldWorld();
            Notify(0);
            gate.Block(transform);
            CreatePointers();
            ShowPanel();
        }

        bool ResolveRig()
        {
            if (xrOrigin == null || !xrOrigin.gameObject.activeInHierarchy)
            {
                var candidates = FindObjectsByType<XROrigin>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                if (candidates.Length != 1)
                {
                    Debug.LogWarning("[CommonMenu] Assign the active XR Origin explicitly when more than one rig exists.", this);
                    return false;
                }
                xrOrigin = candidates[0];
            }
            if (xrOrigin.Camera == null || !CommonMenuPointer.IsTracked(xrOrigin.Camera.GetComponent<TrackedPoseDriver>()))
                return false;
            hands[0] = hands[1] = null;
            foreach (var interactor in xrOrigin.GetComponentsInChildren<XRBaseInputInteractor>(true))
            {
                var index = interactor.handedness == InteractorHandedness.Left ? 0 : interactor.handedness == InteractorHandedness.Right ? 1 : -1;
                if (index >= 0 && hands[index] == null)
                    hands[index] = interactor.GetComponentInParent<TrackedPoseDriver>();
            }
            if (hands[0] == null || hands[1] == null)
            {
                Debug.LogWarning("[CommonMenu] Both controller pose drivers are required for menu rays.", this);
                return false;
            }
            return true;
        }

        bool EnsureEventSystem()
        {
            var systems = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (systems.Length > 1)
            {
                Debug.LogError("[CommonMenu] Resolve duplicate EventSystems before using the menu.", this);
                return false;
            }
            if (systems.Length == 1)
            {
                var module = systems[0].GetComponent<XRUIInputModule>();
                if (module != null && module.isActiveAndEnabled && module.enableXRInput)
                    return true;
                Debug.LogError("[CommonMenu] The scene EventSystem needs an enabled XRUIInputModule with XR input.", systems[0]);
                return false;
            }
            ownEventSystem = new GameObject("CommonMenuEventSystem", typeof(EventSystem), typeof(XRUIInputModule));
            ownEventSystem.transform.SetParent(transform, false);
            ownEventSystem.GetComponent<XRUIInputModule>().enableXRInput = true;
            return true;
        }

        void HoldWorld()
        {
            savedTime = Time.timeScale;
            savedAudio = AudioListener.pause;
            worldHeld = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        void ShowPanel()
        {
            state = MenuState.Open;
            panel.Show(xrOrigin.Camera, distance, mainRoute != null && !mainRoute.IsAtMain);
        }

        void CreatePointers()
        {
            for (var i = 0; i < pointers.Length; i++)
                pointers[i] = new CommonMenuPointer(hands[i], i == 0, rayMaterial);
        }

        void ClearPointers()
        {
            for (var i = 0; i < pointers.Length; i++)
            {
                pointers[i]?.Dispose();
                pointers[i] = null;
            }
        }

        public void Continue()
        {
            if (state != MenuState.Open)
                return;
            panel.WaitForRelease();
            // Gameplay stays held until UI clicks/grips/sticks have been released with valid tracking.
            state = MenuState.WaitingForRelease;
            neutralFrame = -1;
        }

        bool ControlsReleased()
        {
            if (!HasFocus || xrOrigin == null || xrOrigin.Camera == null ||
                !CommonMenuPointer.IsTracked(xrOrigin.Camera.GetComponent<TrackedPoseDriver>()) || menuKey.IsPressed())
                return false;
            for (var i = 0; i < 2; i++)
                if (!CommonMenuPointer.IsTracked(hands[i]) || triggers[i].IsPressed() || grips[i].IsPressed() || sticks[i].ReadValue<Vector2>().sqrMagnitude > 0.04f)
                    return false;
            return true;
        }

        public void GoMain()
        {
            if (state != MenuState.Open || mainRoute == null || mainRoute.IsAtMain)
                return;
            if (!mainRoute.CanGoMain)
            {
                panel.Notice(TrainingSubtitles.MainUnavailable);
                return;
            }
            state = MenuState.Leaving;
            panel.Hide();
            Notify(2);
            // The default route loads synchronously: restore global state immediately before loading, with content still held.
            RestoreWorld(false);
            try
            {
                mainRoute.GoMain();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                HoldWorld();
                gate.Block(transform);
                CreatePointers();
                ShowPanel();
                panel.Notice(TrainingSubtitles.MainUnavailable);
            }
        }

        public void Quit()
        {
            if (state != MenuState.Open)
                return;
            state = MenuState.Leaving;
            panel.Hide();
            Notify(2);
            RestoreWorld(false);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void RestoreWorld(bool resume)
        {
            ClearPointers();
            if (!worldHeld)
                return;
            gate.Restore();
            if (resume)
                Notify(1);
            Time.timeScale = savedTime;
            AudioListener.pause = savedAudio;
            worldHeld = false;
            if (resume)
                listeners.Clear();
        }

        void Notify(int operation)
        {
            foreach (var value in listeners)
            {
                if (value.owner == null)
                    continue;
                try
                {
                    if (operation == 0) value.listener.OnCommonMenuPaused();
                    else if (operation == 1) value.listener.OnCommonMenuResumed();
                    else value.listener.OnCommonMenuLeaving();
                }
                catch (Exception error) { Debug.LogException(error, value.owner); }
            }
            var callbacks = operation == 0 ? Paused : operation == 1 ? Resumed : Leaving;
            if (callbacks == null)
                return;
            foreach (Action callback in callbacks.GetInvocationList())
                try { callback(); }
                catch (Exception error) { Debug.LogException(error, this); }
        }
    }
}
