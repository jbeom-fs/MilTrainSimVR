using UnityEngine;
using UnityEngine.Events;

namespace MilTrainSimVR.B.Training
{
    public sealed class TrainingRunner : MonoBehaviour
    {
        [SerializeField] TrainingDefinition definition;
        [SerializeField] SceneBindings bindings;
        [SerializeField] bool startOnStart = true;
        [SerializeField] UnityEvent<string> stepChanged = new UnityEvent<string>();
        [SerializeField] UnityEvent completed = new UnityEvent();
        TrainingContext context;
        int runId;

        public TrainingStatus Status => context?.Status ?? TrainingStatus.Idle;
        public string CurrentStepId => context?.CurrentStepId;
        public TrainingStepToken CurrentToken => context?.Token ?? default;
        // Held while a training field asks the trainee whether to stop (B training field confirm dialog): the run stays Running,
        // no step is judged and no time is counted. Courses also read it as an input lock. Cleared by every start and stop.
        public bool Held { get; set; }

        void Start()
        {
            if (startOnStart)
                StartTraining();
        }

        [ContextMenu("Start Training")]
        public void StartTraining()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || Status == TrainingStatus.Running)
                return;

            Held = false;
            Unsubscribe();
            if (definition == null || bindings == null)
            {
                Debug.LogError("[Training] Definition and SceneBindings are required.", this);
                return;
            }

            if (!definition.TryValidate(out var error) || !bindings.TryInitialize(out error))
            {
                Debug.LogError($"[Training] {error}", this);
                return;
            }

            foreach (var step in definition.Steps)
            {
                foreach (var condition in step.Conditions)
                {
                    if (!bindings.ContainsTarget(condition.TargetId) ||
                        (condition.Kind != TrainingConditionKind.Action && !bindings.HasState(condition.TargetId, condition.SignalId)))
                    {
                        Debug.LogError($"[Training] Step '{step.StepId}': missing target '{condition.TargetId}' or state '{condition.SignalId}'.", this);
                        return;
                    }
                }
            }

            context = new TrainingContext(definition.Steps, bindings.TryGetState);
            context.Begin(++runId, Time.frameCount);
            foreach (var target in bindings.Targets)
                target.ActionReported += OnActionReported;
            NotifyStepChanged();
        }

        // Delayed producers must retain the token captured when their work began.
        public bool ReportAction(string targetId, string signalId, TrainingStepToken token)
        {
            return isActiveAndEnabled && !Held && context != null && bindings != null && bindings.isActiveAndEnabled &&
                context.ReportAction(targetId, signalId, token, Time.frameCount);
        }

        void OnActionReported(TrainingTarget target, string signalId)
        {
            ReportAction(target.TargetId, signalId, CurrentToken);
        }

        void LateUpdate()
        {
            if (context == null || Status != TrainingStatus.Running || Held)
                return;

            if (bindings == null || !bindings.isActiveAndEnabled)
            {
                StopTraining();
                return;
            }

            if (!context.Tick(Time.deltaTime, Time.frameCount))
                return;

            if (Status == TrainingStatus.Completed)
            {
                Unsubscribe();
                completed.Invoke();
            }
            else
            {
                NotifyStepChanged();
            }
        }

        void NotifyStepChanged()
        {
            stepChanged.Invoke(CurrentStepId);
        }

        [ContextMenu("Stop Training")]
        public void StopTraining()
        {
            Held = false;
            Unsubscribe();
            context?.Stop();
        }

        void Unsubscribe()
        {
            if (bindings == null)
                return;

            foreach (var target in bindings.Targets)
            {
                if (target != null)
                    target.ActionReported -= OnActionReported;
            }
        }

        void OnDisable() => StopTraining();
    }
}
