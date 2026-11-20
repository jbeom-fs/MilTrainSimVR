using MilTrainSimVR.B.ProtectiveClothing;
using UnityEngine;

namespace MilTrainSimVR.B.TrainingField
{
    // Protective clothing sounds (see StationSfx), from ProtectiveClothingEquipment (order 50):
    // - glove / overboot wear: IsWorn(part) false -> true for GloveLeft, GloveRight, OverbootLeft, OverbootRight, each once
    //   (the last one in the frame the course completes too).
    // - jacket / trousers wear: IsWorn(part) false -> true for Jacket and Trousers, each once, at the part's wear zone on the
    //   body (the pickup is hidden where it was let go). Grabbing, far bringing, carrying, turning, a held part near its zone or
    //   let go, a worn part kept on and a reset are silent; one clip, played once without a loop. Wear radius, zones, hiding and
    //   selections are the equipment's, unchanged.
    [DefaultExecutionOrder(320)]
    [DisallowMultipleComponent]
    public sealed class ProtectiveClothingSfx : StationSfx
    {
        static readonly ProtectiveClothingPart[] WearSoundParts =
        {
            ProtectiveClothingPart.GloveLeft, ProtectiveClothingPart.GloveRight, ProtectiveClothingPart.OverbootLeft, ProtectiveClothingPart.OverbootRight,
        };
        static readonly ProtectiveClothingPart[] ClothWearParts = { ProtectiveClothingPart.Jacket, ProtectiveClothingPart.Trousers };

        [SerializeField] AudioClip gloveClip;
        [SerializeField, Range(0f, 1f)] float gloveVolume = 1f;
        [SerializeField] AudioClip overbootClip;
        [SerializeField, Range(0f, 1f)] float overbootVolume = 1f;
        [Tooltip("Jacket and trousers wear one-shot.")]
        [SerializeField] AudioClip clothWearClip;
        [SerializeField, Range(0f, 1f)] float clothWearVolume = 1f;

        ProtectiveClothingEquipment equipment;
        readonly bool[] lastWorn = new bool[WearSoundParts.Length];
        readonly bool[] lastClothWorn = new bool[ClothWearParts.Length];

        public override TrainingFieldCourseId Course => TrainingFieldCourseId.ProtectiveClothing;
        public ProtectiveClothingEquipment Equipment => equipment;

        protected override bool Resolve()
        {
            var start = station.CourseLink is ProtectiveClothingFieldCourse course ? course.TrainingStart : null;
            equipment = start != null ? start.Equipment : null;
            return equipment != null && equipment.Pieces.Count == 6;
        }

        protected override void Sample(bool first, bool live)
        {
            var play = live && !first;
            for (var i = 0; i < WearSoundParts.Length; i++)
            {
                var part = WearSoundParts[i];
                if (!Rose(equipment.IsWorn(part), ref lastWorn[i]) || !play)
                    continue;
                var glove = part == ProtectiveClothingPart.GloveLeft || part == ProtectiveClothingPart.GloveRight;
                Play(glove ? gloveClip : overbootClip, glove ? gloveVolume : overbootVolume, equipment.Piece(part).transform.position);
            }

            for (var i = 0; i < ClothWearParts.Length; i++)
            {
                var part = ClothWearParts[i];
                if (!Rose(equipment.IsWorn(part), ref lastClothWorn[i]) || !play)
                    continue;
                Play(clothWearClip, clothWearVolume, WearPoint(part));
            }
        }

        // The body's wear zone of the part (where the wear succeeded), not the hidden pickup.
        Vector3 WearPoint(ProtectiveClothingPart part)
        {
            var zone = equipment.Body != null ? equipment.Body.ZonePoint(part) : null;
            if (zone != null)
                return zone.position;
            var piece = equipment.Piece(part);
            return piece != null && piece.Worn != null ? piece.Worn.transform.position : equipment.transform.position;
        }

        // One-shots only: the base stops the one-shot source; the next sample after enable or quiet time is a baseline.
        protected override void Silence() { }
    }
}
