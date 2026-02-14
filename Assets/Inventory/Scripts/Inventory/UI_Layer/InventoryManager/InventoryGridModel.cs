using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem
{
    public sealed class InventoryGridModel
    {
        public readonly int Columns;
        public readonly int Rows;

        private int _nextPlacementId = 1;

        private readonly CellData[,] _cells;
        private readonly Dictionary<int, Placement> _placements = new();

        private struct CellData
        {
            public int placementId;     // 0 = empty
            public Vector2Int origin;   // top-left origin for the placement
        }

        private sealed class Placement
        {
            public int id;
            public ItemInstance item;
            public Vector2Int origin;
            public Vector2Int size;
        }

        public InventoryGridModel(int columns, int rows)
        {
            Columns = Mathf.Max(1, columns);
            Rows = Mathf.Max(1, rows);
            _cells = new CellData[Columns, Rows];
        }

        // ---------------- NEW: placement queries ----------------
        public bool TryGetPlacementIdAt(Vector2Int cell, out int placementId)
        {
            placementId = 0;
            if (!IsInBounds(cell))
                return false;

            placementId = _cells[cell.x, cell.y].placementId;
            return placementId != 0;
        }

        public bool TryGetPlacementById(int placementId, out Vector2Int origin, out Vector2Int size, out ItemInstance item)
        {
            origin = default;
            size = default;
            item = null;

            if (placementId == 0)
                return false;

            if (!_placements.TryGetValue(placementId, out var p))
                return false;

            origin = p.origin;
            size = p.size;
            item = p.item;
            return true;
        }

        // ---------------- NEW: remove by ID (for vendor commit) ----------------
        public bool TryRemovePlacement(int placementId, out ItemInstance removedItem)
        {
            removedItem = null;

            if (placementId == 0)
                return false;

            if (!_placements.TryGetValue(placementId, out var p))
                return false;

            ClearCells(placementId, p.origin, p.size);
            _placements.Remove(placementId);

            removedItem = p.item;
            return true;
        }

        // ---------------- EXISTING: stacks add ----------------
        public bool TryAddItem(ItemDefinitionSO def, int amount, out int added)
        {
            added = 0;
            if (def == null || amount <= 0)
                return false;

            int remaining = amount;

            // 1) Fill existing stacks first (if stackable)
            if (def.stackable)
            {
                foreach (var kvp in _placements)
                {
                    var p = kvp.Value;
                    var inst = p.item;

                    if (inst == null || inst.def != def)
                        continue;

                    int space = Mathf.Max(0, def.stackMax - inst.amount);
                    if (space <= 0)
                        continue;

                    int take = Mathf.Min(space, remaining);
                    inst.amount += take;
                    remaining -= take;
                    added += take;

                    if (remaining <= 0)
                        return true;
                }
            }

            // 2) Place new stack instances for the remainder
            while (remaining > 0)
            {
                int stackAmount = def.stackable ? Mathf.Min(def.stackMax, remaining) : remaining;

                var newInst = new ItemInstance(def, stackAmount);
                newInst.rotated90CCW = false;

                if (!TryFindFirstFit(newInst, out var origin))
                    return added > 0;

                if (!TryPlace(newInst, origin, out _))
                    return added > 0;

                remaining -= stackAmount;
                added += stackAmount;
            }

            return true;
        }

        public bool TryFindFirstFit(ItemInstance item, out Vector2Int origin)
        {
            origin = default;
            var size = new Vector2Int(item.Width, item.Height);

            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Columns; x++)
                {
                    var o = new Vector2Int(x, y);
                    if (CanPlaceAt(size, o))
                    {
                        origin = o;
                        return true;
                    }
                }

            return false;
        }

        public bool TryPlace(ItemInstance item, Vector2Int origin, out int placementId)
        {
            placementId = 0;
            var size = new Vector2Int(item.Width, item.Height);

            if (!CanPlaceAt(size, origin))
                return false;

            int id = _nextPlacementId++;
            var p = new Placement
            {
                id = id,
                item = item,
                origin = origin,
                size = size
            };
            _placements.Add(id, p);

            FillCells(id, origin, size);
            placementId = id;
            return true;
        }

        public bool TryPickUpAt(Vector2Int cell, out ItemInstance picked, out Vector2Int origin)
        {
            picked = null;
            origin = default;

            if (!IsInBounds(cell))
                return false;

            int id = _cells[cell.x, cell.y].placementId;
            if (id == 0)
                return false;

            origin = _cells[cell.x, cell.y].origin;

            if (!_placements.TryGetValue(id, out var placement))
                return false;

            ClearCells(id, placement.origin, placement.size);
            _placements.Remove(id);

            picked = placement.item;
            return true;
        }

        public bool TryGetPlacementAt(Vector2Int cell, out Vector2Int origin, out Vector2Int size, out ItemInstance item)
        {
            origin = default;
            size = default;
            item = null;

            if (!IsInBounds(cell))
                return false;

            int id = _cells[cell.x, cell.y].placementId;
            if (id == 0)
                return false;

            if (!_placements.TryGetValue(id, out var p))
                return false;

            origin = p.origin;
            size = p.size;
            item = p.item;
            return true;
        }

        // ---------------- NEW: enumerate WITH ID (for hiding vendor items) ----------------
        public IEnumerable<(int placementId, Vector2Int origin, Vector2Int size, ItemInstance item)> EnumeratePlacementsWithId()
        {
            foreach (var kvp in _placements)
            {
                var p = kvp.Value;
                yield return (p.id, p.origin, p.size, p.item);
            }
        }

        public bool CanPlace(ItemInstance item, Vector2Int origin)
        {
            return CanPlaceAt(new Vector2Int(item.Width, item.Height), origin);
        }

        private bool CanPlaceAt(Vector2Int size, Vector2Int origin)
        {
            if (origin.x < 0 || origin.y < 0) return false;
            if (origin.x + size.x > Columns) return false;
            if (origin.y + size.y > Rows) return false;

            for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                {
                    if (_cells[origin.x + x, origin.y + y].placementId != 0)
                        return false;
                }

            return true;
        }

        private bool IsInBounds(Vector2Int c)
        {
            return c.x >= 0 && c.y >= 0 && c.x < Columns && c.y < Rows;
        }

        private void FillCells(int id, Vector2Int origin, Vector2Int size)
        {
            for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                {
                    _cells[origin.x + x, origin.y + y].placementId = id;
                    _cells[origin.x + x, origin.y + y].origin = origin;
                }
        }

        private void ClearCells(int id, Vector2Int origin, Vector2Int size)
        {
            for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                {
                    int cx = origin.x + x;
                    int cy = origin.y + y;

                    if (_cells[cx, cy].placementId == id)
                    {
                        _cells[cx, cy].placementId = 0;
                        _cells[cx, cy].origin = default;
                    }
                }
        }
    }
}
