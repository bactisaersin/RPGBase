using System;
using UnityEngine;

namespace InventorySystem
{
    [Serializable]
    public sealed class ItemInstance
    {
        public ItemDefinitionSO def;
        public int amount;

        // False = default orientation, True = rotated 90° CCW (width/height swapped)
        public bool rotated90CCW;

        public int Width => rotated90CCW ? def.height : def.width;
        public int Height => rotated90CCW ? def.width : def.height;

        public ItemInstance(ItemDefinitionSO def, int amount = 1)
        {
            this.def = def;
            this.amount = Mathf.Max(1, amount);
            rotated90CCW = false;
        }

        public void RotateCCW()
        {
            rotated90CCW = !rotated90CCW;
        }
    }
}
