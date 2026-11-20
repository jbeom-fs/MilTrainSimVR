using MilTrainSimVR.B.BleedingControl.Tourniquet;
using UnityEngine;

namespace MilTrainSimVR.B.TrainingField
{
    // Tourniquet sounds (see StationSfx), from the course's TourniquetOperation (order 105) of the same frame:
    // - band pull loop: BandPullProgress rising while the band end is the held part, the band is not yet prepared and the
    //   operation is not suspended (the operation already drops tremor, tracking gaps and jumps). Holding the band or moving the
    //   whole device without progress is silent.
    // - windlass turn: one short sound per turnStep degrees of WindlassTurnDegrees change while the windlass is the held part in
    //   its Turning phase, at most one per turnMinInterval (never several to catch up); a change larger than maxTurnStep in one
    //   frame (restore, reset) is not counted. Sound judgement only; the operation's turn count is unchanged.
    // - clip lock: IsWindlassSecured false -> true (WindlassSecured).
    // - velcro: IsStrapFastened false -> true (StrapFastened); covering in progress and the time record are silent.
    [DefaultExecutionOrder(320)]
    [DisallowMultipleComponent]
    public sealed class TourniquetSfx : StationSfx
    {
        [SerializeField] SfxLoop bandPullLoop = new SfxLoop();
        [SerializeField] AudioClip windlassTurnClip;
        [SerializeField, Range(0f, 1f)] float windlassTurnVolume = 1f;
        [Tooltip("Turn (deg) of the held windlass per sound.")]
        [SerializeField, Min(1f)] float turnStep = 45f;
        [Tooltip("Shortest time (s) between two turn sounds.")]
        [SerializeField, Min(0f)] float turnMinInterval = 0.3f;
        [Tooltip("A turn change larger than this in one frame (deg) is not a hand turn.")]
        [SerializeField, Min(1f)] float maxTurnStep = 60f;
        [SerializeField] AudioClip clipLockClip;
        [SerializeField, Range(0f, 1f)] float clipLockVolume = 1f;
        [SerializeField] AudioClip velcroClip;
        [SerializeField, Range(0f, 1f)] float velcroVolume = 1f;

        TourniquetOperation operation;
        float lastPull;
        bool pulling;
        float lastTurn;
        bool turning;
        float turnSum;
        float lastTurnSound = -1f;
        bool lastSecured;
        bool lastFastened;

        public override TrainingFieldCourseId Course => TrainingFieldCourseId.Tourniquet;
        public TourniquetOperation Operation => operation;
        public SfxLoop BandPullLoop => bandPullLoop;

        protected override bool Resolve()
        {
            var start = station.CourseLink is TourniquetFieldCourse course ? course.TrainingStart : null;
            operation = start != null && start.Link != null ? start.Link.Operation : null;
            return operation != null;
        }

        protected override void Sample(bool first, bool live)
        {
            var play = live && !first;
            var at = operation.transform.position;
            var active = Running && !operation.Suspended;
            var held = operation.HeldPart;

            // Band pull: rising progress of the held band end.
            var pull = operation.BandPullProgress;
            var pullAllowed = active && held == TourniquetPart.BandEnd && !operation.IsBandPrepared;
            if (pullAllowed && pulling && !first && pull > lastPull + 1e-4f)
                bandPullLoop.Mark();
            pulling = pullAllowed;
            lastPull = pull;
            bandPullLoop.Drive(pullAllowed, at);

            // Windlass turn: accumulated change of the held windlass while turning.
            var turn = operation.WindlassTurnDegrees;
            var turnAllowed = active && held == TourniquetPart.Windlass && operation.WindlassPhase == TourniquetWindlassPhase.Turning;
            if (turnAllowed && turning && !first)
            {
                var delta = Mathf.Abs(turn - lastTurn);
                if (delta <= maxTurnStep)
                    turnSum += delta;
                if (turnSum >= turnStep)
                {
                    if (lastTurnSound < 0f || Time.unscaledTime - lastTurnSound >= turnMinInterval)
                    {
                        lastTurnSound = Time.unscaledTime;
                        turnSum = 0f;
                        if (play)
                            Play(windlassTurnClip, windlassTurnVolume, at);
                    }
                    else
                    {
                        turnSum = turnStep;
                    }
                }
            }
            else
            {
                turnSum = 0f;
            }
            turning = turnAllowed;
            lastTurn = turn;

            if (Rose(operation.IsWindlassSecured, ref lastSecured) && play)
                Play(clipLockClip, clipLockVolume, at);
            if (Rose(operation.IsStrapFastened, ref lastFastened) && play)
                Play(velcroClip, velcroVolume, at);
        }

        protected override void Silence()
        {
            bandPullLoop.Silence();
            pulling = false;
            turning = false;
            turnSum = 0f;
        }
    }
}
