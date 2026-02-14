using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InventorySystem
{
    public sealed class VendorWindowView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform tabBarRoot;
        [SerializeField] private Button tabButtonTemplate;
        [SerializeField] private InventoryGridUI vendorGrid;

        [Header("Tab Visuals")]
        [SerializeField] private Color tabActiveColor = Color.white;
        [SerializeField] private Color tabInactiveColor = new Color(0.65f, 0.65f, 0.65f, 1f);


        [Header("Optional sizing")]
        [SerializeField] private ChestWindowView sizeView; // reuse your sizing script (it just resizes rects)

        private readonly List<Button> _spawnedTabs = new();
        private WorldVendor _boundVendor;
        private int _activeTabIndex;

        public bool IsOpen => _boundVendor != null;
        public InventoryGridUI GridUI => vendorGrid;
        public int ActiveTabIndex => _activeTabIndex;

        public InventoryGridUI VendorGrid => vendorGrid;

        private struct TabUI
        {
            public Button button;
            public Image buttonImage;            // TabButtonImage child
            public TextMeshProUGUI title;        // TabButtonTitle child
        }
        private readonly List<TabUI> _tabs = new();



        private void Awake()
        {
            if (panelRoot == null)
                panelRoot = gameObject;

            // Template should stay hidden
            if (tabButtonTemplate != null)
                tabButtonTemplate.gameObject.SetActive(false);
        }

        public void Open(WorldVendor vendor)
        {
            _boundVendor = vendor;
            _activeTabIndex = 0;

            if (vendorGrid != null && _boundVendor != null)
            {
                // Update grid dimensions (for TryScreenToCell etc.)
                vendorGrid.SetDimensions(_boundVendor.Columns, _boundVendor.Rows);

                // Resize the slots layer + window root (ChestWindowView)
                if (sizeView != null)
                    sizeView.ApplySize(_boundVendor.Columns, _boundVendor.Rows, vendorGrid.SlotSize);
            }

            RebuildTabs();
            ShowTab(0);
        }


        public void Close()
        {
            _boundVendor = null;
            _activeTabIndex = 0;
            ClearSpawnedTabs();

            // Optional: unbind grid so it shows empty
            if (vendorGrid != null)
                vendorGrid.BindModel(null);
        }


        private void RebuildTabs()
        {
            ClearSpawnedTabs();

            if (_boundVendor == null || tabBarRoot == null || tabButtonTemplate == null)
                return;

            int count = _boundVendor.TabCount;
            for (int i = 0; i < count; i++)
            {
                int tabIndex = i;

                var btn = Instantiate(tabButtonTemplate, tabBarRoot);
                btn.gameObject.SetActive(true);

                var img = btn.transform.Find("TabButtonImage")?.GetComponent<Image>();
                var title = btn.transform.Find("TabButtonTitle")?.GetComponent<TextMeshProUGUI>();

                if (title != null)
                    title.text = _boundVendor.GetTabName(tabIndex);

                btn.onClick.AddListener(() => ShowTab(tabIndex));

                _tabs.Add(new TabUI
                {
                    button = btn,
                    buttonImage = img,
                    title = title
                });
            }
        }

        

        private void ShowTab(int tabIndex)
        {
            if (_boundVendor == null || vendorGrid == null)
                return;

            var model = _boundVendor.GetTabModel(tabIndex);
            if (model == null)
                return;

            _activeTabIndex = tabIndex;

            // IMPORTANT: ensure UI mapping uses correct dimensions (TryScreenToCell)
            vendorGrid.SetDimensions(_boundVendor.Columns, _boundVendor.Rows);

            vendorGrid.BindModel(model);
            // (Optional) update tab visuals later (active/inactive colors)

            UpdateTabVisuals();

        }

        public void SetHiddenPlacements(HashSet<int> hidden)
        {
            if (vendorGrid != null)
                vendorGrid.SetHiddenPlacements(hidden);
        }

        

        public void RefreshActiveTab()
        {
            if (!IsOpen || vendorGrid == null || _boundVendor == null)
                return;

            vendorGrid.BindModel(_boundVendor.GetTabModel(_activeTabIndex));
            vendorGrid.RedrawItems();
        }

        private void UpdateTabVisuals()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                bool active = (i == _activeTabIndex);
                var c = active ? tabActiveColor : tabInactiveColor;

                if (_tabs[i].buttonImage != null)
                    _tabs[i].buttonImage.color = c;

                if (_tabs[i].title != null)
                    _tabs[i].title.color = c;
            }
        }


        private void ClearSpawnedTabs()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].button != null)
                    Destroy(_tabs[i].button.gameObject);
            }
            _tabs.Clear();
        }


    }
}
