using UnityEngine;

namespace InventorySystem
{
    [CreateAssetMenu(
        fileName = "ItemDefinition",
        menuName = "Inventory/Items/Item Definition",
        order = 0)]
    public sealed class ItemDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Min(0)] public int index = 0;
        public string displayName = "";
        [TextArea] public string description = "";

        [Header("Classification")]
        public ItemClass itemClass = ItemClass.Misc;
        public ItemSubclass subclass = ItemSubclass.Food;

        [Header("Economy")]
        [Min(0)] public int price = 0;

        [Header("Inventory")]
        public bool stackable = false;
        [Min(1)] public int stackMax = 1;

        [Tooltip("Grid footprint in cells (1..2 wide, 1..4 tall in your design).")]
        [Min(1)] public int width = 1;
        [Min(1)] public int height = 1;

        [Header("Visuals")]
        public Sprite icon;
        public Sprite worldSprite;

        [Tooltip("Optional: prefab/holder for world drop, etc.")]
        public GameObject worldPrefab;

        [Header("Stats (optional)")]
        [Min(0)] public int damage = 0;
        [Min(0)] public int defense = 0;
        [Min(0)] public int range = 0;

        [Header("Ammo (optional)")]
        [Tooltip("For ammo: what weapon category/name it is for (e.g. \"Bow\").")]
        public string forWeapon = "";

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep values sane.
            width = Mathf.Clamp(width, 1, 2);
            height = Mathf.Clamp(height, 1, 4);

            if (!stackable)
                stackMax = 1;
            else
                stackMax = Mathf.Max(1, stackMax);
        }



#endif
        public ItemCategory EquipCategory
        {
            get
            {
                if (itemClass == ItemClass.Weapon)
                {
                    switch (subclass)
                    {
                        case ItemSubclass.WeaponMelee: return ItemCategory.WeaponMelee;
                        case ItemSubclass.WeaponRanged: return ItemCategory.WeaponRanged;
                        default: return ItemCategory.None; // ammo etc.
                    }
                }

                if (itemClass == ItemClass.Wear)
                {
                    switch (subclass)
                    {
                        case ItemSubclass.WearHead: return ItemCategory.Head;
                        case ItemSubclass.WearBody: return ItemCategory.Body;
                        case ItemSubclass.WearHands: return ItemCategory.Hands;
                        case ItemSubclass.WearFeet: return ItemCategory.Feet;
                    }
                }

                return ItemCategory.None;
            }
        }

    }
}
