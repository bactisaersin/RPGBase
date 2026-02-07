using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem
{
    public sealed class InventoryUIManager : MonoBehaviour
    {
        [Header("Views / Data")]
        [SerializeField] private PlayerInventoryView playerInventoryView;

        [Header("Grids")]
        [SerializeField] private InventoryGridUI playerGrid;
        [SerializeField] private InventoryGridUI chestGrid;

        [Header("Cursor Ghost")]
        [SerializeField] private RectTransform cursorLayer;     // UI layer under Canvas (stretched full screen)
        [SerializeField] private GameObject ghostIconPrefab;    // Prefab with child "itemIcon" (Image)

        [Header("Input")]
        [SerializeField] private KeyCode rotateKey = KeyCode.R;

        [Header("Cursor Offset (screen pixels)")]
        [SerializeField] private Vector2 cursorOffset = new Vector2(-16f, 16f);

        private ItemInstance _heldItem;
        private InventoryGridUI _heldFrom; // null if held item came from gear

        // Ghost UI
        private GameObject _ghostGO;
        private RectTransform _ghostRT;
        private Image _ghostIconImage;
        private RectTransform _ghostIconRT;

        // Gear hover + data
        private GearSlotView _hoveredGear;
        private readonly Dictionary<GearSlotId, GearSlotDefinition> _gearDefs = new();
        private readonly Dictionary<GearSlotId, ItemInstance> _equipped = new();

        private Vector2Int _heldOrigin;
        private bool _heldRotationAtPickup;
        private bool _heldHasOrigin;

        private void Awake()
        {
            CacheGearDefinitions();
        }

        private void CacheGearDefinitions()
        {
            _gearDefs.Clear();

            if (playerInventoryView == null || playerInventoryView.Layout == null)
                return;

            var defs = playerInventoryView.Layout.gearSlots;
            for (int i = 0; i < defs.Length; i++)
                _gearDefs[defs[i].slotId] = defs[i];
        }

        private void Update()
        {
            UpdateGhostPosition();

            if (_heldItem != null)
                UpdateGhostValidityTint();

            if (_heldItem != null && Input.GetKeyDown(rotateKey))
            {
                _heldItem.RotateCCW();
                RefreshGhostForHeldItem(playerGrid);
            }

            if (Input.GetMouseButtonDown(0))
            {
                // Gear clicks are handled by GearSlotView (UI events).
                // Grid clicks are handled here.
                if (TryClickGrid(playerGrid)) return;
                if (TryClickGrid(chestGrid)) return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
                CancelHeldItemToSource();
        }

        // ---------- Called by GearSlotView ----------
        public void NotifyGearHover(GearSlotView view)
        {
            _hoveredGear = view;
            if (_heldItem != null)
                UpdateGhostValidityTint();
        }

        public void NotifyGearExit(GearSlotView view)
        {
            if (_hoveredGear == view)
                _hoveredGear = null;

            if (_heldItem != null)
                UpdateGhostValidityTint();
        }

        public void OnGearSlotClicked(GearSlotView slotView)
        {
            if (slotView == null)
                return;

            var slotId = slotView.SlotId;

            // Holding something -> try equip
            if (_heldItem != null)
            {
                // Requirement: equip should use default orientation
                _heldItem.rotated90CCW = false;

                if (!CanEquipTo(slotId, _heldItem))
                    return;

                // Swap behavior if occupied
                _equipped.TryGetValue(slotId, out var oldItem);

                // Equip uses default orientation
                bool originalRot = _heldItem.rotated90CCW;
                _heldItem.rotated90CCW = false;

                ItemInstance itemToEquip = _heldItem;

                // If it's a stack, equip one and return the rest to the pickup origin
                if (_heldItem.def != null && _heldItem.def.stackable && _heldItem.amount > 1)
                {
                    // Equip exactly 1
                    itemToEquip = new ItemInstance(_heldItem.def, 1);
                    itemToEquip.rotated90CCW = false; // always default in gear

                    // Keep the remainder
                    _heldItem.amount -= 1;

                    // Restore original rotation before placing remainder back
                    _heldItem.rotated90CCW = _heldRotationAtPickup;

                    // Return remainder back where it came from (must be a grid pickup)
                    if (_heldFrom != null && _heldHasOrigin)
                    {
                        // This SHOULD always fit because we removed it from there.
                        // But if something else now occupies that area, TryPlace can fail.
                        bool placedBack = _heldFrom.Model.TryPlace(_heldItem, _heldOrigin, out _);

                        if (!placedBack)
                        {
                            // Fallback: try find another fit in the same grid
                            if (_heldFrom.Model.TryFindFirstFit(_heldItem, out var alt) &&
                                _heldFrom.Model.TryPlace(_heldItem, alt, out _))
                            {
                                placedBack = true;
                            }
                        }

                        _heldFrom.RedrawItems();

                        if (!placedBack)
                        {
                            // IMPORTANT: Don't lose items. Keep remainder in hand.
                            // Also re-apply equip preview default orientation for the ghost.
                            _heldItem.rotated90CCW = false;
                            ShowGhostForHeldItem(_heldItem, playerGrid);
                            UpdateGhostValidityTint();
                            return; // stop equip attempt
                        }

                        // Remainder successfully returned -> clear hand
                        ClearHeldItem();
                    }
                    else
                    {
                        // No origin info: keep remainder in hand (optional policy)
                        _heldItem.rotated90CCW = false;
                    }
                    
                }
                else
                {
                    // Non-stack or amount==1: rotation resets on equip as you already do
                    //_heldItem.rotated90CCW = false;
                }




                _equipped[slotId] = itemToEquip;

                // Draw equipped item on child layer (not on slot background)
                int slotPx = playerGrid != null ? playerGrid.SlotSize : 32;
                slotView.SetEquipped(itemToEquip, slotPx);

                if (oldItem == null)
                {
                    ClearHeldItem();
                }
                else
                {
                    // Put old item into hand
                    _heldItem = oldItem;
                    _heldFrom = null; // now coming from gear
                    ShowGhostForHeldItem(_heldItem, playerGrid);
                    UpdateGhostValidityTint();
                }

                return;
            }

            // Empty hand -> pick up from gear if exists
            if (_equipped.TryGetValue(slotId, out var equippedItem) && equippedItem != null)
            {
                _equipped.Remove(slotId);

                // Clear equipped visuals
                slotView.ClearEquipped();

                _heldItem = equippedItem;
                _heldFrom = null; // came from gear
                ShowGhostForHeldItem(_heldItem, playerGrid);
                UpdateGhostValidityTint();
            }
        }

        // ---------- Equip rules ----------
        private bool CanEquipTo(GearSlotId slotId, ItemInstance item)
        {
            if (item == null || item.def == null)
                return false;

            if (!_gearDefs.TryGetValue(slotId, out var def))
                return false;

            // Category check
            if (item.def.EquipCategory != def.acceptsCategory)
                return false;

            // Size policy
            if (def.sizePolicy == GearSlotSizePolicy.UpTo)
                return item.Width <= def.slotWidth && item.Height <= def.slotHeight;

            // Exact policy: you said head/body/etc are consistent so we can accept without size check.
            return true;
        }

        // ---------- Ghost ----------
        private void UpdateGhostPosition()
        {
            if (_heldItem == null || _ghostRT == null)
                return;

            _ghostRT.position = (Vector2)Input.mousePosition + cursorOffset;
        }

        private void UpdateGhostValidityTint()
        {
            if (_heldItem == null || _ghostIconImage == null)
                return;

            bool valid = false;

            // Gear takes priority if hovered
            if (_hoveredGear != null)
            {
                bool prevRot = _heldItem.rotated90CCW;
                _heldItem.rotated90CCW = false; // preview equip in default orientation
                valid = CanEquipTo(_hoveredGear.SlotId, _heldItem);
                _heldItem.rotated90CCW = prevRot;
            }
            else if (playerGrid != null && playerGrid.TryScreenToCell(Input.mousePosition, out var cellP))
            {
                valid = playerGrid.Model.CanPlace(_heldItem, cellP);
            }
            else if (chestGrid != null && chestGrid.TryScreenToCell(Input.mousePosition, out var cellC))
            {
                valid = chestGrid.Model.CanPlace(_heldItem, cellC);
            }

            _ghostIconImage.color = valid ? Color.green : Color.red;
        }

        private void ShowGhostForHeldItem(ItemInstance item, InventoryGridUI grid)
        {
            if (cursorLayer == null || ghostIconPrefab == null)
            {
                Debug.LogWarning("Cursor ghost not configured (cursorLayer or ghostIconPrefab missing).", this);
                return;
            }

            if (_ghostGO == null)
            {
                _ghostGO = Instantiate(ghostIconPrefab, cursorLayer);
                _ghostRT = _ghostGO.GetComponent<RectTransform>();

                // Must have child named "itemIcon" with Image
                _ghostIconImage = _ghostGO.transform.Find("itemIcon")?.GetComponent<Image>();
                _ghostIconRT = _ghostIconImage != null ? (RectTransform)_ghostIconImage.transform : null;

                // Ensure ghost never blocks clicks
                if (_ghostIconImage != null)
                    _ghostIconImage.raycastTarget = false;

                // Container never rotates
                if (_ghostRT != null)
                    _ghostRT.localEulerAngles = Vector3.zero;
            }

            if (_ghostGO != null)
                _ghostGO.SetActive(true);

            if (_ghostIconImage != null)
                _ghostIconImage.sprite = item.def.icon;

            RefreshGhostForHeldItem(grid);
        }

        private void RefreshGhostForHeldItem(InventoryGridUI grid)
        {
            if (_heldItem == null || _ghostRT == null || _ghostIconRT == null || grid == null)
                return;

            // Container should never rotate
            _ghostRT.localEulerAngles = Vector3.zero;

            float containerW = _heldItem.Width * grid.SlotSize;
            float containerH = _heldItem.Height * grid.SlotSize;

            // Container size = footprint (axis-aligned)
            _ghostRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, containerW);
            _ghostRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, containerH);

            // Rotate only the child icon, swap its rect size when rotated
            _ghostIconRT.anchorMin = _ghostIconRT.anchorMax = new Vector2(0.5f, 0.5f);
            _ghostIconRT.pivot = new Vector2(0.5f, 0.5f);
            _ghostIconRT.anchoredPosition = Vector2.zero;

            if (_heldItem.rotated90CCW)
            {
                _ghostIconRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, containerH);
                _ghostIconRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, containerW);
                _ghostIconRT.localEulerAngles = new Vector3(0f, 0f, 90f);
            }
            else
            {
                _ghostIconRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, containerW);
                _ghostIconRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, containerH);
                _ghostIconRT.localEulerAngles = Vector3.zero;
            }
        }

        private void ClearHeldItem()
        {
            _heldItem = null;
            _heldFrom = null;

            if (_ghostGO != null)
                _ghostGO.SetActive(false);

            _heldHasOrigin = false;
            _heldOrigin = default;
            _heldRotationAtPickup = false;
        }

        private void CancelHeldItemToSource()
        {
            if (_heldItem == null)
                return;

            // If it came from a grid, return it there
            if (_heldFrom != null)
            {
                if (_heldFrom.Model.TryFindFirstFit(_heldItem, out var o) &&
                    _heldFrom.Model.TryPlace(_heldItem, o, out _))
                {
                    _heldFrom.RedrawItems();
                }

                ClearHeldItem();
                return;
            }

            // If it came from gear, just clear for now (later you can return it to the slot or drop it)
            ClearHeldItem();
        }

        // ---------- Grid click handling ----------
        private bool TryClickGrid(InventoryGridUI grid)
        {
            if (grid == null)
                return false;

            if (!grid.TryScreenToCell(Input.mousePosition, out var cell))
                return false;

            // Holding -> try place
            if (_heldItem != null)
            {
                // If clicking an existing stack of the same item, merge into it
                if (grid.Model.TryGetPlacementAt(cell, out _, out _, out var targetItem) &&
                    targetItem != null &&
                    _heldItem.def != null &&
                    _heldItem.def.stackable &&
                    targetItem.def == _heldItem.def)
                {
                    int space = Mathf.Max(0, _heldItem.def.stackMax - targetItem.amount);
                    if (space > 0)
                    {
                        int move = Mathf.Min(space, _heldItem.amount);
                        targetItem.amount += move;
                        _heldItem.amount -= move;

                        grid.RedrawItems();

                        if (_heldItem.amount <= 0)
                            ClearHeldItem(); // fully merged

                        return true;
                    }
                }


                if (grid.Model.TryPlace(_heldItem, cell, out _))
                {
                    grid.RedrawItems();
                    ClearHeldItem();
                }
                return true;
            }

            // Not holding -> pick up
            // Not holding -> pick up
            if (grid.Model.TryPickUpAt(cell, out var picked, out var origin))
            {
                _heldItem = picked;
                _heldFrom = grid;

                _heldOrigin = origin;
                _heldHasOrigin = true;

                _heldRotationAtPickup = _heldItem.rotated90CCW;

                grid.RedrawItems();
                ShowGhostForHeldItem(_heldItem, grid);
                UpdateGhostValidityTint();
            }


            return true;
        }

        // ---------- World pickup entry point ----------
        public bool TryAutoAddToPlayer(ItemDefinitionSO def, int amount = 1)
        {
            if (playerGrid == null || playerGrid.Model == null || def == null)
                return false;

            bool ok = playerGrid.Model.TryAddItem(def, amount, out int added);

            playerGrid.RedrawItems();

            if (!ok || added < amount)
            {
                Debug.Log($"Inventory full. Could only add {added}/{amount} of '{def.displayName}'.");
                return false;
            }

            return true;
        }

    }
}
