using System;
using MilTrainSimVR.B.Decontamination.KD1;
using UnityEngine;

namespace MilTrainSimVR.B.TrainingField
{
    // KD-1 sounds (see StationSfx), from KD1Equipment (order 50) and KD1SurfacePractice (order 55):
    // - pack open: KD1Item.IsOpen false -> true of the outer pack and of each packet, once per pack (a grab or a pull alone is
    //   not an opening; the reset closes them silently).
    // - pad wear: a pad's State entering Worn (fitted on a hand mount), once per fit.
    // - surface contact: KD1SurfacePractice.IsDone(zone) false -> true, once per zone and run (the record stays, so touching it
    //   again is silent; the last zone in the frame the course completes too). No rubbing speed, path or time is added.
    [DefaultExecutionOrder(320)]
    [DisallowMultipleComponent]
    public sealed class KD1Sfx : StationSfx
    {
        [SerializeField] AudioClip packOpenClip;
        [SerializeField, Range(0f, 1f)] float packOpenVolume = 1f;
        [SerializeField] AudioClip padWearClip;
        [SerializeField, Range(0f, 1f)] float padWearVolume = 1f;
        [SerializeField] AudioClip surfaceContactClip;
        [SerializeField, Range(0f, 1f)] float surfaceContactVolume = 1f;

        KD1Equipment equipment;
        KD1SurfacePractice practice;
        KD1Item[] packs = Array.Empty<KD1Item>();
        bool[] lastOpen = Array.Empty<bool>();
        bool[] lastWorn = Array.Empty<bool>();
        bool[] lastDone = Array.Empty<bool>();

        public override TrainingFieldCourseId Course => TrainingFieldCourseId.KD1;
        public KD1Equipment Equipment => equipment;
        public KD1SurfacePractice Practice => practice;

        protected override bool Resolve()
        {
            var course = station.CourseLink is KD1FieldCourse field ? field.TrainingCourse : null;
            equipment = course != null ? course.Equipment : null;
            practice = course != null ? course.Practice : null;
            if (equipment == null || practice == null || equipment.OuterPack == null)
                return false;
            packs = new KD1Item[equipment.Packets.Count + 1];
            packs[0] = equipment.OuterPack;
            for (var i = 0; i < equipment.Packets.Count; i++)
                packs[i + 1] = equipment.Packets[i];
            lastOpen = new bool[packs.Length];
            lastWorn = new bool[equipment.Pads.Count];
            lastDone = new bool[practice.ZoneCount];
            return Array.IndexOf(packs, null) < 0;
        }

        protected override void Sample(bool first, bool live)
        {
            var play = live && !first;
            for (var i = 0; i < packs.Length; i++)
            {
                if (Rose(packs[i].IsOpen, ref lastOpen[i]) && play)
                    Play(packOpenClip, packOpenVolume, packs[i].transform.position);
            }
            for (var i = 0; i < lastWorn.Length; i++)
            {
                var pad = equipment.Pads[i];
                if (pad != null && Rose(pad.State == KD1ItemState.Worn, ref lastWorn[i]) && play)
                    Play(padWearClip, padWearVolume, pad.transform.position);
            }
            for (var i = 0; i < lastDone.Length; i++)
            {
                if (Rose(practice.IsDone(i), ref lastDone[i]) && play)
                    Play(surfaceContactClip, surfaceContactVolume, practice.Zones[i].transform.position);
            }
        }

        protected override void Silence()
        {
        }
    }
}
