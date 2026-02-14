using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class WorldVendor : MonoBehaviour
    {
        [Header("Vendor Catalog")]
        [SerializeField] private VendorCatalogSO catalog;

        [Header("Grid Size (same for all tabs)")]
        [Min(1)] [SerializeField] private int columns = 10;
        [Min(1)] [SerializeField] private int rows = 10;

        [Header("Populate Mode (simple)")]
        [Tooltip("If true: for each tab, iterate items in order; if weight>0, place exactly one; stop when an item cannot fit.")]
        [SerializeField] private bool populateOnAwake = true;

        private InventoryGridModel[] _tabModels;

        public VendorCatalogSO Catalog => catalog;
        public int Columns => columns;
        public int Rows => rows;

        public int TabCount => _tabModels != null ? _tabModels.Length : 0;

        public InventoryGridModel GetTabModel(int tabIndex)
        {
            if (_tabModels == null || tabIndex < 0 || tabIndex >= _tabModels.Length)
                return null;
            return _tabModels[tabIndex];
        }

        public string GetTabName(int tabIndex)
        {
            if (catalog == null || !catalog.TryGetTab(tabIndex, out var tab) || tab == null)
                return $"Tab {tabIndex}";
            return string.IsNullOrWhiteSpace(tab.tabName) ? $"Tab {tabIndex}" : tab.tabName;
        }

        private void Awake()
        {
            BuildModels();

            if (populateOnAwake)
                PopulateModels_Simple();
        }

        private void BuildModels()
        {
            int tabCount = (catalog != null) ? catalog.TabCount : 0;
            if (tabCount <= 0)
            {
                _tabModels = new InventoryGridModel[0];
                return;
            }

            _tabModels = new InventoryGridModel[tabCount];
            for (int i = 0; i < tabCount; i++)
                _tabModels[i] = new InventoryGridModel(columns, rows);
        }

        private void PopulateModels_Simple()
        {
            if (catalog == null || _tabModels == null)
                return;

            for (int tabIndex = 0; tabIndex < _tabModels.Length; tabIndex++)
            {
                var model = _tabModels[tabIndex];
                if (model == null)
                    continue;

                if (!catalog.TryGetTab(tabIndex, out var tab) || tab == null || tab.items == null)
                    continue;

                // Simple rule:
                // - iterate in order
                // - weight > 0 => place exactly one
                // - stop at first item that cannot fit
                for (int i = 0; i < tab.items.Count; i++)
                {
                    var entry = tab.items[i];
                    if (entry.weight <= 0 || entry.item == null)
                        continue;

                    var inst = new ItemInstance(entry.item, 1);
                    inst.rotated90CCW = false;

                    if (!model.TryFindFirstFit(inst, out var origin))
                        break; // stop placing further items for this tab

                    if (!model.TryPlace(inst, origin, out _))
                        break; // also stop if placement fails unexpectedly
                }
            }
        }

        private void OnMouseDown()
        {
            var mgr = FindFirstObjectByType<InventoryUIManager>();
            if (mgr != null)
                mgr.OpenVendor(this);
        }


#if UNITY_EDITOR
        private void OnValidate()
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
        }
#endif

        // Next step will call InventoryUIManager.OpenVendor(this) on click.
        // We’ll add OnMouseDown after the Vendor UI exists.
    }
}
