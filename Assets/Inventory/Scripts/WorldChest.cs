using UnityEngine;

namespace InventorySystem
{
    public enum ChestSize
    {
        Small,
        Medium,
        Large
    }

    [RequireComponent(typeof(Collider2D))]
    public sealed class WorldChest : MonoBehaviour
    {
        [Header("Chest Type")]
        [SerializeField] private ChestSize size = ChestSize.Small;

        public ChestSize Size => size;
        public InventoryGridModel Model { get; private set; }

        private void Awake()
        {
            // Pick model size based on chest type.
            // Make these match your UI variants exactly.
            (int cols, int rows) = size switch
            {
                ChestSize.Small => (4, 4),
                ChestSize.Medium => (6, 4),
                ChestSize.Large => (8, 5),
                _ => (4, 4)
            };

            Model = new InventoryGridModel(cols, rows);
        }

        private void OnMouseDown()
        {
            var mgr = FindFirstObjectByType<InventoryUIManager>();
            if (mgr != null)
                mgr.OpenChest(this);
        }
    }
}
