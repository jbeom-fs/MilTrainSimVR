using System;
using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.B.K1GasMask
{
    // Carrier flap pose from one openness value (0 closed .. 1 fully open), interpolated between fixed samples.
    // Samples are parent-relative local poses already retargeted onto the imported rig;
    // nothing is read from source files at runtime. The only writer of the flap bone transforms; BagRoot is never driven.
    public sealed class K1CarrierFlapPose : MonoBehaviour
    {
        [Serializable]
        public sealed class Sample
        {
            public float openness;
            public Vector3[] localPositions = Array.Empty<Vector3>();
            public Quaternion[] localRotations = Array.Empty<Quaternion>();
        }

        [Tooltip("Render rig bones FlapBase, FlapMid, FlapTip, parent first.")]
        [SerializeField] Transform[] bones = Array.Empty<Transform>();
        [Tooltip("Ascending openness from 0 to 1, from the source opening poses.")]
        [SerializeField] Sample[] samples = Array.Empty<Sample>();

        public float Openness { get; private set; }
        public IReadOnlyList<Transform> Bones => bones;
        public IReadOnlyList<Sample> Samples => samples;

        // Each bone hinges about one axis, so slerp between neighbouring samples keeps the source's linear bend per segment.
        public void Apply(float openness)
        {
            Openness = Mathf.Clamp01(openness);
            var i = 1;
            while (i < samples.Length - 1 && samples[i].openness < Openness)
                i++;

            var a = samples[i - 1];
            var b = samples[i];
            var t = Mathf.InverseLerp(a.openness, b.openness, Openness);
            for (var k = 0; k < bones.Length; k++)
            {
                bones[k].localPosition = Vector3.Lerp(a.localPositions[k], b.localPositions[k], t);
                bones[k].localRotation = Quaternion.Slerp(a.localRotations[k], b.localRotations[k], t);
            }
        }

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (bones == null || bones.Length == 0 || Array.IndexOf(bones, null) >= 0)
                problems.Add("bones missing");
            if (samples == null || samples.Length < 2)
            {
                problems.Add("fewer than 2 samples");
            }
            else
            {
                if (samples[0].openness != 0f || samples[samples.Length - 1].openness != 1f)
                    problems.Add("samples must start at 0 and end at 1");
                for (var i = 0; i < samples.Length; i++)
                {
                    var s = samples[i];
                    if (i > 0 && s.openness <= samples[i - 1].openness)
                        problems.Add($"sample {i} not ascending");
                    if (bones != null && (s.localPositions == null || s.localPositions.Length != bones.Length
                                          || s.localRotations == null || s.localRotations.Length != bones.Length))
                        problems.Add($"sample {i} bone count mismatch");
                }
            }

            error = problems.Count > 0 ? $"K1CarrierFlapPose '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }
    }
}
