using MilTrainSimVR.B.ProtectiveClothing;
using UnityEngine;

namespace MilTrainSimVR.B.TrainingField
{
    // Protective clothing sounds (see StationSfx), from ProtectiveClothingEquipment (order 50):
    // - glove / overboot wear: IsWorn(part) false -> true for GloveLeft, GloveRight, OverbootLeft, OverbootRight, each once
    //   (the last one in the frame the course completes too).
    // - cloth move loop: only the jacket and trousers pickups (the large garments; gloves and overboots have their own wear
    //   sound and stay out of the loop), while one is selected, not worn and moves in the XR Origin's frame beyond a tremor dead
    //   band. Holding still, a worn part following the body, the wear snap, a reset, a teleport or a tracking return jump are
    //   silent. Wear radius, zones and hiding are the equipment's, unchanged.
    [DefaultExecutionOrder(320)]
    [DisallowMultipleComponent]
    public sealed class ProtectiveClothingSfx : StationSfx
    {
        static readonly ProtectiveClothingPart[] WearSoundParts =
        {
            ProtectiveClothingPart.GloveLeft, ProtectiveClothingPart.GloveRight, ProtectiveClothingPart.OverbootLeft, ProtectiveClothingPart.OverbootRight,
        };
        static readonly ProtectiveClothingPart[] ClothLoopParts = { ProtectiveClothingPart.Jacket, ProtectiveClothingPart.Trousers };

        [SerializeField] AudioClip gloveClip;
        [SerializeField, Range(0f, 1f)] float gloveVolume = 1f;
        [SerializeField] AudioClip overbootClip;
        [SerializeField, Range(0f, 1f)] float overbootVolume = 1f;
        [SerializeField] SfxLoop clothLoop = new SfxLoop();
        [Tooltip("Held garment motion in the XR Origin's frame (m).")]
        [SerializeField] SfxMotion clothMotion = new SfxMotion();

        ProtectiveClothingEquipment equipment;
        readonly bool[] lastWorn = new bool[WearSoundParts.Length];
        ProtectiveClothingPiece lastHeld;

        public override TrainingFieldCourseId Course => TrainingFieldCourseId.ProtectiveClothing;
        public ProtectiveClothingEquipment Equipment => equipment;
        public SfxLoop ClothLoop => clothLoop;

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

            ProtectiveClothingPiece held = null;
            if (Running && !equipment.IsResetting && !equipment.IsFaulted)
            {
                foreach (var part in ClothLoopParts)
                {
                    var piece = equipment.Piece(part);
                    if (piece != null && piece.Grab != null && piece.Grab.isSelected && !equipment.IsWorn(part))
                    {
                        held = piece;
                        break;
                    }
                }
            }
            if (held == null || first || held != lastHeld)
                clothMotion.Clear();
            lastHeld = held;
            if (held != null)
            {
                var origin = station.Owner != null && station.Owner.Origin != null ? station.Owner.Origin.transform : null;
                var point = held.transform.position;
                if (clothMotion.Moved(origin != null ? origin.InverseTransformPoint(point) : point))
                    clothLoop.Mark();
            }
            clothLoop.Drive(held != null, held != null ? held.transform.position : equipment.transform.position);
        }

        protected override void Silence()
        {
            clothLoop.Silence();
            clothMotion.Clear();
            lastHeld = null;
        }
    }
}
