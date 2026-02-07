using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


namespace InventorySystem
{
    public sealed class InventoryGridUI : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int columns = 10;
        [SerializeField] private int rows = 4;
        [SerializeField] private int slotSize = 32;

        [Header("UI References")]
        [SerializeField] private RectTransform gridRect;
        [SerializeField] private RectTransform itemsLayer;
        [SerializeField] private GameObject itemIconPrefab;

        public int Columns => columns;
        public int Rows => rows;
        public int SlotSize => slotSize;

        public InventoryGridModel Model { get; private set; }

        // Track spawned icon objects per placement (key: origin+item identity is enough for now)
        private readonly List<GameObject> _spawnedIcons = new();

        private void Awake()
        {
            if (gridRect == null)
                gridRect = (RectTransform)transform;

            // Only auto-create if nothing else will bind a model.
            if (Model == null)
                Model = new InventoryGridModel(columns, rows);

            RedrawItems();
        }


        public bool TryScreenToCell(Vector2 screenPos, out Vector2Int cell)
        {
            cell = default;

            if (gridRect == null)
                return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, screenPos, null, out var local))
                return false;

            // Assumes pivot is centered (0.5,0.5) like your hover script did.
            float w = gridRect.rect.width;
            float h = gridRect.rect.height;

            float x = local.x + w * 0.5f;
            float y = local.y + h * 0.5f;

            if (x < 0 || y < 0 || x >= w || y >= h)
                return false;

            int col = Mathf.FloorToInt(x / slotSize);
            int row = Mathf.FloorToInt((h - y) / slotSize);

            int cols = Model != null ? Model.Columns : columns;
            int rws = Model != null ? Model.Rows : rows;

            if (col < 0 || col >= cols || row < 0 || row >= rws)
                return false;


            cell = new Vector2Int(col, row);
            return true;
        }

        public void RedrawItems()
        {
            // Clear old icons
            for (int i = 0; i < _spawnedIcons.Count; i++)
            {
                if (_spawnedIcons[i] != null)
                    Destroy(_spawnedIcons[i]);
            }
            _spawnedIcons.Clear();

            if (itemsLayer == null || itemIconPrefab == null || Model == null)
                return;

            foreach (var p in Model.EnumeratePlacements())
            {
                SpawnIcon(p.origin, p.size, p.item);
            }
        }

        private void SpawnIcon(Vector2Int origin, Vector2Int size, ItemInstance item)
        {
            var go = Instantiate(itemIconPrefab, itemsLayer);
            _spawnedIcons.Add(go);

            var rt = (RectTransform)go.transform;

            // Size in pixels
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x * slotSize);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y * slotSize);

            // Position: top-left origin
            // We assume itemsLayer aligns with gridRect and uses centered pivot like the grid.
            float gridW = gridRect.rect.width;
            float gridH = gridRect.rect.height;

            float x = origin.x * slotSize;
            float y = origin.y * slotSize;

            // Convert top-left to anchored (center-pivot) space:
            float anchoredX = -gridW * 0.5f + x + (rt.rect.width * 0.5f);
            float anchoredY = gridH * 0.5f - y - (rt.rect.height * 0.5f);

            rt.anchoredPosition = new Vector2(anchoredX, anchoredY);
            // Rotate only the child Image, not the container.
            var iconImg = go.transform.Find("itemIcon")?.GetComponent<Image>();
            if (iconImg != null)
            {
                var iconRT = (RectTransform)iconImg.transform;

                // Make it center-anchored so we can control size precisely.
                iconRT.anchorMin = iconRT.anchorMax = new Vector2(0.5f, 0.5f);
                iconRT.pivot = new Vector2(0.5f, 0.5f);
                iconRT.anchoredPosition = Vector2.zero;

                float containerW = rt.rect.width;
                float containerH = rt.rect.height;

                if (item.rotated90CCW)
                {
                    // Swap the icon size, then rotate it.
                    iconRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, containerH);
                    iconRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, containerW);
                    iconRT.localEulerAngles = new Vector3(0f, 0f, 90f);
                }
                else
                {
                    iconRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, containerW);
                    iconRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, containerH);
                    iconRT.localEulerAngles = Vector3.zero;
                }
            }



            // Set icon sprite if present
            var img = go.GetComponentInChildren<Image>(true);
            if (img != null)
                img.sprite = item.def.icon;

            // Stack count (optional child named "countText" with UnityEngine.UI.Text)
            // Stack count (optional child named "countText" with TextMeshProUGUI)
            var countText = go.transform.Find("countText")?.GetComponent<TextMeshProUGUI>();
            if (countText != null)
            {
                bool show = item.def.stackable && item.amount > 1;
                countText.gameObject.SetActive(show);
                if (show)
                    countText.text = item.amount.ToString();
            }


        }

        public void BindModel(InventoryGridModel model)
        {
            Model = model;
            RedrawItems();
        }

    }
}
