using UnityEngine;
using UnityEngine.InputSystem.XR;

namespace MilTrainSimVR.B.Decontamination.KD1
{
    public enum KD1Hand
    {
        None = 0,
        Left = 1,
        Right = 2
    }

    // The fitting point of one hand: where a fitted pad's Anchor_HandFit (wrist centre; +Y toward the fingers, +Z the back of the
    // hand) goes. Follows that hand's controller pose (its TrackedPoseDriver, the Starter Assets aim/device pose) with this
    // hand's own offset, in LateUpdate and before render, so a fitted pad (a child of this object) moves with the controller in
    // the same frame. It is not under the XR Origin: the rig and its prefab are never changed, and nothing here hides or moves
    // the controller or its model.
    //
    // The controller pose is not the wrist: the offset estimates the wrist centre of a hand holding the controller from that
    // pose, separately for each hand (the pad model is a static right-hand fit, so the left mount mirrors the fitted model by
    // default). The offsets are development estimates; adjust them per hand on this object
    // (fitPosition, fitRotation, mirrorPad) while checking in the headset. A pose of a lost controller is its last pose; the
    // equipment never fits or removes a pad while that hand is not tracked.
    [DisallowMultipleComponent]
    public sealed class KD1HandMount : MonoBehaviour
    {
        [SerializeField] KD1Hand hand;
        [Tooltip("This hand's controller TrackedPoseDriver ('Left Controller' / 'Right Controller' of the XR Origin).")]
        [SerializeField] TrackedPoseDriver driver;
        [Tooltip("Wrist centre (pad Anchor_HandFit) in the controller pose's space, metres. Development estimate per hand.")]
        [SerializeField] Vector3 fitPosition;
        [Tooltip("Anchor_HandFit rotation in the controller pose's space (Euler degrees): its +Y toward the fingers, +Z the back of the hand.")]
        [SerializeField] Vector3 fitRotation;
        [Tooltip("Show the fitted pad model mirrored across its centre plane (the model is a right-hand fit).")]
        [SerializeField] bool mirrorPad;

        public KD1Hand Hand => hand;
        public TrackedPoseDriver Driver => driver;
        public bool MirrorPad => mirrorPad;
        public Vector3 FitPosition => fitPosition;
        public Vector3 FitRotation => fitRotation;

        void OnEnable()
        {
            Application.onBeforeRender += Follow;
            Follow();
        }

        void OnDisable() => Application.onBeforeRender -= Follow;

        void LateUpdate() => Follow();

        void Follow()
        {
            if (driver == null)
                return;
            var controller = driver.transform;
            transform.SetPositionAndRotation(controller.TransformPoint(fitPosition), controller.rotation * Quaternion.Euler(fitRotation));
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (hand == KD1Hand.None || driver == null)
                error = $"KD1HandMount '{name}': hand or driver missing.";
            else if (!IsFinite(fitPosition) || !IsFinite(fitRotation))
                error = $"KD1HandMount '{name}': fit offset is not finite.";
            else if ((transform.lossyScale - Vector3.one).sqrMagnitude > 1e-6f)
                error = $"KD1HandMount '{name}': world scale must be 1.";
            return error == null;
        }

        static bool IsFinite(Vector3 v) => !float.IsNaN(v.x + v.y + v.z) && !float.IsInfinity(v.x + v.y + v.z);
    }
}
