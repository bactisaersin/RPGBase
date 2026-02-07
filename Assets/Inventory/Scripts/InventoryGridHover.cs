using UnityEngine;

public class InventoryGridHover : MonoBehaviour
{
    [Header("Grid Settings")]
    public int columns = 10;
    public int rows = 4;
    public int slotSize = 32;

    private RectTransform rectTransform;

    private int _lastRow = -1;
    private int _lastCol = -1;
    private bool _wasInside = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (rectTransform == null)
            return;

        // Convert screen mouse position to local position in RectTransform
        bool isInside = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            Input.mousePosition,
            null, // Screen Space - Overlay
            out Vector2 localMousePos
        );

        if (!isInside)
        {
            // Left the rect: reset so next entry logs again.
            if (_wasInside)
            {
                _wasInside = false;
                _lastRow = -1;
                _lastCol = -1;
            }
            return;
        }

        _wasInside = true;

        // Convert local position to grid coordinates
        if (TryGetSlotFromLocalPosition(localMousePos, out int row, out int col))
        {
            if (row != _lastRow || col != _lastCol)
            {
                _lastRow = row;
                _lastCol = col;

                int index = row * columns + col;
                Debug.Log($"Hovering slot -> Row: {row}, Col: {col}, Index: {index}");
            }
        }
        else
        {
            // Inside rect but not inside a valid cell (shouldn't happen with your bounds,
            // but safe reset so next valid cell logs).
            _lastRow = -1;
            _lastCol = -1;
        }
    }

    bool TryGetSlotFromLocalPosition(Vector2 localPos, out int row, out int col)
    {
        row = -1;
        col = -1;

        float width = rectTransform.rect.width;
        float height = rectTransform.rect.height;

        // Convert from centered pivot space to bottom-left origin
        float x = localPos.x + width * 0.5f;
        float y = localPos.y + height * 0.5f;

        // Check bounds
        if (x < 0 || y < 0 || x >= width || y >= height)
            return false;

        col = Mathf.FloorToInt(x / slotSize);
        row = Mathf.FloorToInt((height - y) / slotSize); // invert Y for top-down rows

        // Safety clamp
        if (col < 0 || col >= columns || row < 0 || row >= rows)
            return false;

        return true;
    }
}
