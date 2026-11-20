using UnityEngine;

namespace MilTrainSimVR.B.TrainingField
{
    // Record of the seated pose given to a tourniquet or pressure bandage casualty in a field scene (on the casualty's holder).
    // The AED casualty stays lying and has none.
    // Data only, nothing runs.
    // How the pose was made: the legs bend (thighs forward, shins upright, feet where they stood) and everything above the hips
    // moves down by `drop` along the station's up, unchanged in shape; so every judge frame, marker and anchor on the upper body
    // (tourniquet target and zones, wound frame and wrap path) was moved by the same vector and still lies on the
    // same skin. The tutorial scenes and the model files are never changed.
    [DisallowMultipleComponent]
    public sealed class TrainingFieldSeatedPose : MonoBehaviour
    {
        [Tooltip("Distance (m, world) the upper body and its anchors were moved down.")]
        [SerializeField] float drop;
        [Tooltip("How the pose was applied (rig bones of the casualty).")]
        [SerializeField] string method;
        [Tooltip("Anchors moved with the upper body.")]
        [SerializeField] Transform[] movedAnchors = new Transform[0];
        [Tooltip("The chair made for the pose (no collider).")]
        [SerializeField] Transform chair;

        public float Drop => drop;
        public string Method => method;
        public Transform[] MovedAnchors => movedAnchors;
        public Transform Chair => chair;
    }
}
