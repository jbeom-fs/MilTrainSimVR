using System;
using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage
{
    // References to the parts and anchors of the imported PressureBandage_Basic model inside the B wrapper prefab. Holds no
    // behaviour: the dressing is handled by PressureDressing; the roll, bandage end and tape by PressureBandageApplication
    // (chapter 2); the strip, wrap strip and end cloth are shown by PressureBandageRibbons with runtime work meshes (hidden in
    // the saved prefab). Anchors are the model's empties (position and direction contracts in their part's local axes, not
    // colliders); they do not follow deformed work meshes. Part transforms keep the FBX axis conversion rotation as saved.
    [DisallowMultipleComponent]
    public sealed class PressureBandageKit : MonoBehaviour
    {
        [Serializable]
        public struct Parts
        {
            public Transform dressingPad;
            public Transform bandageRoll;
            public Transform bandageStrip;
            public Transform wrapStrip;
            public Transform bandageEnd;
            public Transform fixingTape;
        }

        [Serializable]
        public struct Anchors
        {
            public Transform dressingGrab;
            [Tooltip("Contact face centre; contact direction is the part's local -Z.")]
            public Transform dressingContact;
            [Tooltip("Top face centre for holding and pressing; outward is the part's local +Z.")]
            public Transform dressingPress;
            public Transform rollGrab;
            [Tooltip("Roll axis centre; the roll's local +Y is its axis.")]
            public Transform rollAxis;
            public Transform rollExit;
            public Transform stripStart;
            public Transform stripEnd;
            public Transform wrapStart;
            public Transform wrapEnd;
            public Transform endGrab;
            public Transform endFix;
            public Transform tapeGrab;
            public Transform tapeContact;
        }

        [SerializeField] Parts parts;
        [SerializeField] Anchors anchors;

        public Parts PartTransforms => parts;
        public Anchors AnchorTransforms => anchors;

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            void Need(Transform t, string label)
            {
                if (t == null)
                    missing.Add(label);
            }

            Need(parts.dressingPad, "parts.dressingPad");
            Need(parts.bandageRoll, "parts.bandageRoll");
            Need(parts.bandageStrip, "parts.bandageStrip");
            Need(parts.wrapStrip, "parts.wrapStrip");
            Need(parts.bandageEnd, "parts.bandageEnd");
            Need(parts.fixingTape, "parts.fixingTape");
            Need(anchors.dressingGrab, "anchors.dressingGrab");
            Need(anchors.dressingContact, "anchors.dressingContact");
            Need(anchors.dressingPress, "anchors.dressingPress");
            Need(anchors.rollGrab, "anchors.rollGrab");
            Need(anchors.rollAxis, "anchors.rollAxis");
            Need(anchors.rollExit, "anchors.rollExit");
            Need(anchors.stripStart, "anchors.stripStart");
            Need(anchors.stripEnd, "anchors.stripEnd");
            Need(anchors.wrapStart, "anchors.wrapStart");
            Need(anchors.wrapEnd, "anchors.wrapEnd");
            Need(anchors.endGrab, "anchors.endGrab");
            Need(anchors.endFix, "anchors.endFix");
            Need(anchors.tapeGrab, "anchors.tapeGrab");
            Need(anchors.tapeContact, "anchors.tapeContact");
            error = missing.Count > 0 ? $"PressureBandageKit '{name}' missing references: {string.Join(", ", missing)}." : null;
            return error == null;
        }
    }
}
