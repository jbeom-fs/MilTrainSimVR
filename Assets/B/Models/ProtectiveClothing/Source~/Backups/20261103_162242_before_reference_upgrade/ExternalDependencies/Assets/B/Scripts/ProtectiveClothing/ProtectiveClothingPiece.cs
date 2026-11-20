using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MilTrainSimVR.B.ProtectiveClothing
{
    // The six protective clothing parts. Left and right are the mannequin's own left and right.
    public enum ProtectiveClothingPart
    {
        Jacket = 0,
        Trousers = 1,
        GloveLeft = 2,
        GloveRight = 3,
        OverbootLeft = 4,
        OverbootRight = 5
    }

    // One protective clothing part: its pickup (the grabbable workbench prop on this object) and its worn mesh (inside the shared
    // worn model, aligned with the mannequin). Data only; ProtectiveClothingEquipment owns every state change (wear, hide, show,
    // reset). The worn mesh keeps its imported place: it is shown or hidden, never moved.
    //
    // Points (FBX Empties, named by the model contract in Assets/B/Models/ProtectiveClothing/Source~/README_Model.md):
    // - Grip_<Name>_Pickup: the grab attach point of the pickup.
    // - Attach_<Name>_Pickup: the pickup's point measured for wearing.
    // - Attach_<Name>_Worn: the body target of the part (a child of <Name>_Worn, in the mannequin's frame).
    // Wearing is judged by the world distance between the two Attach points only (no angle, face or hand direction).
    [DisallowMultipleComponent]
    public sealed class ProtectiveClothingPiece : MonoBehaviour
    {
        [SerializeField] ProtectiveClothingPart part;
        [SerializeField] XRGrabInteractable grab;
        [SerializeField] Rigidbody body;
        [Tooltip("The pickup's one simple grab collider (on this object).")]
        [SerializeField] Collider grabCollider;
        [SerializeField] Transform grip;
        [SerializeField] Transform pickupAttach;
        [Tooltip("<Name>_Worn mesh object inside the worn model instance under the mannequin root.")]
        [SerializeField] GameObject worn;
        [SerializeField] Transform wornAttach;

        public ProtectiveClothingPart Part => part;
        public XRGrabInteractable Grab => grab;
        public Rigidbody Body => body;
        public Collider GrabCollider => grabCollider;
        public Transform Grip => grip;
        public Transform PickupAttach => pickupAttach;
        public GameObject Worn => worn;
        public Transform WornAttach => wornAttach;

        // Model contract name of a part (FBX object names).
        public static string ModelName(ProtectiveClothingPart part) => part switch
        {
            ProtectiveClothingPart.Jacket => "Jacket",
            ProtectiveClothingPart.Trousers => "Trousers",
            ProtectiveClothingPart.GloveLeft => "Glove_L",
            ProtectiveClothingPart.GloveRight => "Glove_R",
            ProtectiveClothingPart.OverbootLeft => "Overboot_L",
            _ => "Overboot_R"
        };

        public static string GripName(ProtectiveClothingPart part) => $"Grip_{ModelName(part)}_Pickup";
        public static string PickupAttachName(ProtectiveClothingPart part) => $"Attach_{ModelName(part)}_Pickup";
        public static string PickupMeshName(ProtectiveClothingPart part) => $"{ModelName(part)}_Pickup";
        public static string WornName(ProtectiveClothingPart part) => $"{ModelName(part)}_Worn";
        public static string WornAttachName(ProtectiveClothingPart part) => $"Attach_{ModelName(part)}_Worn";

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (grab == null || body == null || grabCollider == null || grip == null || pickupAttach == null || worn == null || wornAttach == null)
                problems.Add("a reference is missing");
            else
            {
                if (grab.transform != transform || body.transform != transform || grabCollider.transform != transform)
                    problems.Add("grab, body and collider must be on the pickup object");
                if (!grab.colliders.Contains(grabCollider))
                    problems.Add("the grab does not use the collider");
                if (grab.attachTransform != grip)
                    problems.Add("the grab attach point is not the Grip");
                if (!grip.IsChildOf(transform) || !pickupAttach.IsChildOf(transform))
                    problems.Add("Grip and pickup Attach must be inside the pickup");
                if (!wornAttach.IsChildOf(worn.transform))
                    problems.Add("worn Attach must be inside the worn mesh");
                if (grip.name != GripName(part) || pickupAttach.name != PickupAttachName(part) || worn.name != WornName(part) ||
                    wornAttach.name != WornAttachName(part))
                    problems.Add($"names do not match the {ModelName(part)} model contract");
                if (worn.transform.IsChildOf(transform))
                    problems.Add("the worn mesh must not be inside the pickup");
            }
            error = problems.Count > 0 ? $"ProtectiveClothingPiece '{name}' ({part}): {string.Join(", ", problems)}." : null;
            return error == null;
        }
    }
}
