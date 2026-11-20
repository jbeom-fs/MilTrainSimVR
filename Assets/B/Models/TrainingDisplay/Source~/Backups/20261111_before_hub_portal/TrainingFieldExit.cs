using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.B.TrainingField
{
    // Exit board of a training field (at its ExitAnchor): one button to the other field scene. The coordinator decides whether the
    // user may leave (no station in use, that scene in the build's scene list) and loads it; this board only shows why not. The new
    // field starts fresh: its own XR Origin at its EntryAnchor, every station at its start screen.
    [DefaultExecutionOrder(310)]
    [DisallowMultipleComponent]
    public sealed class TrainingFieldExit : MonoBehaviour
    {
        [SerializeField] TrainingFieldCoordinator coordinator;
        [SerializeField] TMP_FontAsset koreanFont;
        [SerializeField] TMP_Text infoText;
        [SerializeField] Button exitButton;
        [SerializeField] TMP_Text exitButtonText;

        bool valid;
        bool leaving;
        string shownInfo;

        public TrainingFieldCoordinator Coordinator => coordinator;

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError($"[TrainingField] {error}", this);
                return;
            }
            Apply(exitButtonText, coordinator.Field == TrainingFieldKind.Cbrn ? TrainingSubtitles.ExitToFirstAidButton : TrainingSubtitles.ExitToCbrnButton);
        }

        void OnEnable()
        {
            if (valid)
                exitButton.onClick.AddListener(OnExit);
        }

        void OnDisable()
        {
            if (valid)
                exitButton.onClick.RemoveListener(OnExit);
        }

        void LateUpdate()
        {
            if (!valid || leaving)
                return;
            var ready = coordinator.CanLeave(out var reason);
            var info = ready ? TrainingSubtitles.ExitReady : reason;
            if (info != shownInfo)
            {
                shownInfo = info;
                Apply(infoText, info);
            }
            if (exitButton.interactable != ready)
                exitButton.interactable = ready;
        }

        void OnExit()
        {
            if (leaving || !coordinator.CanLeave(out _))
                return;
            leaving = true;
            exitButton.interactable = false;
            Apply(infoText, TrainingSubtitles.ExitLoading);
            if (!coordinator.TryLeave())
                leaving = false;
        }

        void Apply(TMP_Text text, string value)
        {
            text.font = koreanFont;
            text.text = value;
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (coordinator == null)
                missing.Add(nameof(coordinator));
            if (koreanFont == null)
                missing.Add(nameof(koreanFont));
            if (infoText == null || exitButton == null || exitButtonText == null)
                missing.Add("info text or exit button");
            error = missing.Count > 0 ? $"TrainingFieldExit '{name}' missing references: {string.Join(", ", missing)}." : null;
            return error == null;
        }
    }
}
