using UnityEngine;

namespace InventorySystem
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class WorldChest : MonoBehaviour
    {
        [Header("Chest Size (grid cells)")]
        [Min(1)] [SerializeField] private int columns = 4;
        [Min(1)] [SerializeField] private int rows = 4;

        public int Columns => columns;
        public int Rows => rows;

        public InventoryGridModel Model { get; private set; }

        private void Awake()
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);

            Model = new InventoryGridModel(columns, rows);
        }

        private void OnMouseDown()
        {
            var mgr = FindFirstObjectByType<InventoryUIManager>();
            if (mgr != null)
                mgr.OpenChest(this);
        }
    }
}
