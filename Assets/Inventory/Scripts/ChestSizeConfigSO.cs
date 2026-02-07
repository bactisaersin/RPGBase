using UnityEngine;

namespace InventorySystem
{
    [CreateAssetMenu(fileName = "ChestSizeConfig", menuName = "Inventory/Chest Size Config")]
    public sealed class ChestSizeConfigSO : ScriptableObject
    {
        [System.Serializable]
        public struct SizeDef
        {
            public ChestSize size;
            [Min(1)] public int columns;
            [Min(1)] public int rows;
        }

        public SizeDef small = new SizeDef { size = ChestSize.Small, columns = 4, rows = 4 };
        public SizeDef medium = new SizeDef { size = ChestSize.Medium, columns = 6, rows = 4 };
        public SizeDef large = new SizeDef { size = ChestSize.Large, columns = 8, rows = 5 };

        public bool TryGet(ChestSize s, out int cols, out int rows)
        {
            SizeDef def = s switch
            {
                ChestSize.Small => small,
                ChestSize.Medium => medium,
                ChestSize.Large => large,
                _ => small
            };

            cols = Mathf.Max(1, def.columns);
            rows = Mathf.Max(1, def.rows);
            return true;
        }
    }
}
