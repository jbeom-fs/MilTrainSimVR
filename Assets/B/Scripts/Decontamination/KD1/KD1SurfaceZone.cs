using UnityEngine;

namespace MilTrainSimVR.B.Decontamination.KD1
{
    // One marked practice area on the target's outer surface (VR experience layout, not a prescribed area or count): a point on the
    // real surface with its outward normal (this object's +Z; +Y along the glove's length), placed by a ray cast
    // onto the target's mesh collider. Its contact region is a thin square slab on the surface: radius each way in the surface plane
    // and the practice's slab depth each way along the normal (KD1SurfacePractice.ContactBox). Data and display only; the practice
    // owns the record and when the markers show. Markers (display children only; they never move this object or its contact box):
    // todoMarker = translucent mint fill of the contact square, a bold outline on its edge and the area number (1, 2, 3: to tell the
    // areas apart, not an order); doneMarker = green fill and outline with a check mark instead of the number, so the state is not
    // told by colour alone.
    [DisallowMultipleComponent]
    public sealed class KD1SurfaceZone : MonoBehaviour
    {
        [Tooltip("Stable id of this practice area (unique on its target).")]
        [SerializeField] string id;
        [Tooltip("Engineering setting: half size (m) of the area in the surface plane (the contact square); the marker fill and outline cover exactly it.")]
        [SerializeField] float radius = 0.045f;
        [Tooltip("Number shown on the area (display only; any order is fine).")]
        [SerializeField] int number = 1;
        [SerializeField] GameObject todoMarker;
        [SerializeField] GameObject doneMarker;

        public string Id => id;
        public float Radius => radius;
        public int Number => number;
        public Vector3 Point => transform.position;
        public Vector3 Normal => transform.forward;
        public GameObject TodoMarker => todoMarker;
        public GameObject DoneMarker => doneMarker;

        // Display only: hidden, or the marker of the recorded state. Never changes a record.
        internal void SetMarkers(bool shown, bool done)
        {
            SetActive(todoMarker, shown && !done);
            SetActive(doneMarker, shown && done);
        }

        static void SetActive(GameObject go, bool active)
        {
            if (go.activeSelf != active)
                go.SetActive(active);
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(id))
                error = $"KD1SurfaceZone '{name}': id missing.";
            else if (float.IsNaN(radius) || radius <= 0f || radius > 0.2f)
                error = $"KD1SurfaceZone '{name}': radius out of range.";
            else if (number < 1 || number > 9)
                error = $"KD1SurfaceZone '{name}': number must be 1-9.";
            else if (todoMarker == null || doneMarker == null || todoMarker == doneMarker || !todoMarker.transform.IsChildOf(transform) ||
                     !doneMarker.transform.IsChildOf(transform))
                error = $"KD1SurfaceZone '{name}': two markers inside this zone required.";
            return error == null;
        }
    }
}
