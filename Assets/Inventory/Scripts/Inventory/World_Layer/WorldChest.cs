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

        [Header("Config")]
        [SerializeField] private ChestSizeConfigSO sizeConfig;

        public ChestSize Size => size;
        public InventoryGridModel Model { get; private set; }

        private void Awake()
        {
            if (sizeConfig == null)
            {
                Debug.LogError($"WorldChest '{name}' missing ChestSizeConfigSO reference.", this);
                // Fallback to something safe
                Model = new InventoryGridModel(4, 4);
                return;
            }

            sizeConfig.TryGet(size, out int cols, out int rows);
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
