using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem
{
    [CreateAssetMenu(
        fileName = "VendorCatalog",
        menuName = "Inventory/Vendor/Vendor Catalog",
        order = 0)]
    public sealed class VendorCatalogSO : ScriptableObject
    {
        [Serializable]
        public struct WeightedItem
        {
            public ItemDefinitionSO item;

            [Tooltip("0 = never include. >0 = enabled (later: higher means higher chance).")]
            [Min(0)] public int weight;
        }

        [Serializable]
        public sealed class Tab
        {
            public string tabName = "Tab";
            public List<WeightedItem> items = new List<WeightedItem>(); 
        }

        [Header("Tabs (extendable)")]
        [Tooltip("Each tab will correspond to one vendor page/grid.")]
        public List<Tab> tabs = new List<Tab>
        {
            new Tab { tabName = "Weapons" },
            new Tab { tabName = "Wear" },
            new Tab { tabName = "Misc" }
        };

        /// <summary>
        /// Safe accessor: returns false if index out of range or tab missing.
        /// </summary>
        public bool TryGetTab(int tabIndex, out Tab tab)
        {
            tab = null;

            if (tabs == null)
                return false;

            if (tabIndex < 0 || tabIndex >= tabs.Count)
                return false;

            tab = tabs[tabIndex];
            return tab != null;
        }

        public int TabCount => tabs != null ? tabs.Count : 0;
    }
}
