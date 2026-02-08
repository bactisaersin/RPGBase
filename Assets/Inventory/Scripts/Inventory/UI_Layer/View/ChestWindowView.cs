using UnityEngine;

namespace InventorySystem
{
    public sealed class ChestWindowView : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Root RectTransform of the whole chest window/panel (usually this object).")]
        [SerializeField] private RectTransform windowRoot;

        [Tooltip("RectTransform that defines the grid clickable area (the object that has InventoryGridUI).")]
        [SerializeField] private RectTransform slotsLayer;

        [Header("Layout Padding (pixels)")]
        [Tooltip("Extra padding added around SlotsGrid inside the window root.")]
        [SerializeField] private Vector2 padding = new Vector2(24f, 24f);

        [Tooltip("Extra pixels added above the grid (for title bar / close button area).")]
        [SerializeField] private float extraTop = 32f;

        [Tooltip("Extra pixels added below the grid.")]
        [SerializeField] private float extraBottom = 0f;

        private void Reset()
        {
            windowRoot = (RectTransform)transform;
        }

        /// <summary>
        /// Resize the chest window to fit a cols x rows grid at a given slotSize (pixels).
        /// Call this BEFORE binding/redrawing, ideally right when opening a chest.
        /// </summary>
        public void ApplySize(int columns, int rows, int slotSize)
        {
            if (windowRoot == null)
                windowRoot = (RectTransform)transform;

            if (slotsLayer == null)
            {
                Debug.LogWarning("ChestWindowView missing slotsLayer reference.", this);
                return;
            }

            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
            slotSize = Mathf.Max(1, slotSize);

            float gridW = columns * slotSize;
            float gridH = rows * slotSize;

            // Resize the grid rect (this drives your tiled background visuals too).
            slotsLayer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, gridW);
            slotsLayer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, gridH);

            // Resize the overall window rect so it wraps the grid + padding.
            float windowW = gridW + padding.x * 2f;
            float windowH = gridH + padding.y * 2f + extraTop + extraBottom;

            windowRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, windowW);
            windowRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, windowH);
        }
    }
}
