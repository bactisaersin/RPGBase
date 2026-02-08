using System;
using UnityEngine;

namespace InventorySystem
{
    // Gear slot identifiers (match your UI: SlotHead, SlotBody, etc.)
    public enum GearSlotId
    {
        Head,
        Body,
        Hands,
        Feet,
        WeaponL,
        WeaponR,
        Waist,
        RingL,
        RingR
    }

    // Item categories used for acceptance rules.
    public enum ItemCategory
    {
        None,
        Head,
        Body,
        Hands,
        Feet,
        Weapon,
        Waist,
        Ring
    }

    public enum GearSlotSizePolicy
    {
        // Slot accepts only items that match the slot footprint exactly.
        Exact,

        // Slot accepts items up to the slot footprint (item can be smaller).
        UpTo
    }

    [Serializable]
    public struct GearSlotDefinition
    {
        public GearSlotId slotId;
        public ItemCategory acceptsCategory;

        [Min(1)] public int slotWidth;
        [Min(1)] public int slotHeight;

        public GearSlotSizePolicy sizePolicy;
    }

    [CreateAssetMenu(
        fileName = "PlayerInventoryLayout",
        menuName = "Inventory/Layout/Player Inventory Layout",
        order = 0)]
    public sealed class PlayerInventoryLayoutSO : ScriptableObject
    {
        [Header("Main Grid (cells)")]
        [Min(1)] public int mainGridWidth = 10;
        [Min(1)] public int mainGridHeight = 4;

        [Header("Gear Slots")]
        public GearSlotDefinition[] gearSlots = Array.Empty<GearSlotDefinition>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Basic sanity so you catch mistakes early.
            mainGridWidth = Mathf.Max(1, mainGridWidth);
            mainGridHeight = Mathf.Max(1, mainGridHeight);

            if (gearSlots == null)
                gearSlots = Array.Empty<GearSlotDefinition>();

            for (int i = 0; i < gearSlots.Length; i++)
            {
                var def = gearSlots[i];

                def.slotWidth = Mathf.Max(1, def.slotWidth);
                def.slotHeight = Mathf.Max(1, def.slotHeight);

                gearSlots[i] = def;
            }
        }
#endif
    }
}
