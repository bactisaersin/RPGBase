using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem
{
    public sealed class InventoryUIManager : MonoBehaviour
    {
        [Header("Views / Data")]
        [SerializeField] private PlayerInventoryView playerInventoryView;

        [Header("Player Grid")]
        [SerializeField] private InventoryGridUI playerGrid;

        [Header("Chest UI")]
        [SerializeField] private GameObject chestPanelRoot;   // InventoryChest root panel
        [SerializeField] private ChestWindowView chestWindowView;
        [SerializeField] private InventoryGridUI chestGrid;   // InventoryGridUI on SlotsGrid

        [Header("Vendor UI")]
        [SerializeField] private InventoryWindowController windows;
        [SerializeField] private VendorWindowView vendorWindowView;

        [Header("Cursor Ghost")]
        [SerializeField] private RectTransform cursorLayer;     // UI layer under Canvas (stretched full screen)
        [SerializeField] private GameObject ghostIconPrefab;    // Prefab with child "itemIcon" (Image)

        [Header("Input")]
        [SerializeField] private KeyCode rotateKey = KeyCode.R;

        [Header("Cursor Offset (screen pixels)")]
        [SerializeField] private Vector2 cursorOffset = new Vector2(-16f, 16f);

        [Header("Currency")]
        [SerializeField] private PlayerGoldView goldView;

        [SerializeField] private int startingGold = 0;

        
        private WorldVendor _openVendor;



        private int _gold;
        public int Gold => _gold;


        // Windows controller (cached)
        //private InventoryWindowController _windows;

        // Active chest session
        private WorldChest _openChest;
        private InventoryGridUI _activeChestGrid;

        // Held item state
        private ItemInstance _heldItem;
        private InventoryGridUI _heldFrom; // null if held item came from gear
        private Vector2Int _heldOrigin;
        private bool _heldHasOrigin;
        private bool _heldRotationAtPickup;

        // Vendor-held state (when the ghost came from vendor)
        private bool _heldFromVendor;
        private WorldVendor _heldVendor;
        private int _heldVendorTabIndex;
        private Vector2Int _heldVendorOrigin;
        private bool _heldVendorHasOrigin;
        private bool _heldVendorRotationAtPickup;
        private int _heldVendorPrice;


        // Ghost UI
        private GameObject _ghostGO;
        private RectTransform _ghostRT;
        private Image _ghostIconImage;
        private RectTransform _ghostIconRT;

        // Gear hover + data
        private GearSlotView _hoveredGear;
        private readonly Dictionary<GearSlotId, GearSlotDefinition> _gearDefs = new();
        private readonly Dictionary<GearSlotId, ItemInstance> _equipped = new();

        private InventoryGridModel _playerModel;


        private void Awake()
        {
            CacheGearDefinitions();
            windows = FindFirstObjectByType<InventoryWindowController>();

            // Create player model even if UI is closed
            _playerModel = new InventoryGridModel(10, 4);

            // Bind it immediately (works even if the panel is inactive)
            if (playerGrid != null)
                playerGrid.BindModel(_playerModel);

            _gold = Mathf.Max(0, startingGold);
            if (goldView != null)
                goldView.SetGold(_gold);

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
                if (vendorWindowView != null && vendorWindowView.IsOpen)
                {
                    if (TryClickGrid(vendorWindowView.GridUI)) return;
                }
                // Gear clicks are handled by GearSlotView (UI events).
                // Grid clicks are handled here.
                if (TryClickGrid(playerGrid)) return;
                if (TryClickGrid(_activeChestGrid)) return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
                CancelHeldItemToSource();
        }

        // --------------------------------------------------------------------
        // Chest open/close
        // --------------------------------------------------------------------
        public void OpenChest(WorldChest chest)
        {
            if (chest == null)
                return;

            // Optional: close previous chest UI (contents are in the model, so they stay)
            _openChest = chest;

            if (chestPanelRoot != null)
                chestPanelRoot.SetActive(true);

            if (chestGrid == null)
            {
                Debug.LogWarning("InventoryUIManager: chestGrid is not assigned.", this);
                return;
            }

            // Resize chest window to this chest's size
            if (chestWindowView != null)
                chestWindowView.ApplySize(chest.Columns, chest.Rows, chestGrid.SlotSize);

            // Bind model (this sets Columns/Rows via model, not inspector)
            chestGrid.BindModel(chest.Model);
            chestGrid.RedrawItems();

            _activeChestGrid = chestGrid;
        }


        public void CloseChestUI()
        {
            _openChest = null;
            _activeChestGrid = null;

            if (chestPanelRoot != null)
                chestPanelRoot.SetActive(false);
        }


        // --------------------------------------------------------------------
        // Called by GearSlotView
        // --------------------------------------------------------------------
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

            // ------------------------------------------------------------
            // HOLDING -> equip attempt
            // ------------------------------------------------------------
            if (_heldItem != null)
            {
                // Vendor-held rule (simple for now): vendor items must be single items to equip
                if (_heldFromVendor && _heldItem.def != null && _heldItem.def.stackable && _heldItem.amount > 1)
                {
                    Debug.Log("Cannot equip a vendor stack yet. Place into grid first (or we can implement per-unit buying).");
                    return;
                }

                // Swap behavior if occupied
                _equipped.TryGetValue(slotId, out var oldItem);

                // Decide what we'll equip (whole item or 1 from stack)
                ItemInstance itemToEquip = _heldItem;

                // ------------------------------------------------------------
                // Stack-from-grid equip behavior (your existing logic)
                // ------------------------------------------------------------
                if (!_heldFromVendor &&
                    _heldItem.def != null &&
                    _heldItem.def.stackable &&
                    _heldItem.amount > 1)
                {
                    // Validate equip using a temporary 1-stack instance
                    itemToEquip = new ItemInstance(_heldItem.def, 1);
                    itemToEquip.rotated90CCW = false;

                    if (!CanEquipTo(slotId, itemToEquip))
                        return;

                    // Take one from stack
                    _heldItem.amount -= 1;

                    // Return remainder to where it came from (same rotation it had in the grid)
                    if (_heldFrom != null && _heldHasOrigin)
                    {
                        _heldItem.rotated90CCW = _heldRotationAtPickup;

                        bool placedBack = _heldFrom.Model.TryPlace(_heldItem, _heldOrigin, out _);

                        if (!placedBack)
                        {
                            // fallback: place somewhere else in same grid
                            if (_heldFrom.Model.TryFindFirstFit(_heldItem, out var alt) &&
                                _heldFrom.Model.TryPlace(_heldItem, alt, out _))
                            {
                                placedBack = true;
                            }
                        }

                        _heldFrom.RedrawItems();

                        if (!placedBack)
                        {
                            // Don't lose items: keep remainder in hand
                            _heldItem.rotated90CCW = false;
                            ShowGhostForHeldItem(_heldItem, playerGrid);
                            UpdateGhostValidityTint();
                            return;
                        }

                        // Remainder returned successfully -> clear hand
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
                    // Normal equip (single item)
                    _heldItem.rotated90CCW = false;

                    if (!CanEquipTo(slotId, _heldItem))
                        return;

                    // If held item came from vendor, charge now (only after it's valid)
                    if (_heldFromVendor)
                    {
                        if (!TrySpendGold(_heldVendorPrice))
                        {
                            ReturnHeldVendorItemToVendor();
                            ClearHeldItem();
                            return;
                        }
                    }

                    itemToEquip = _heldItem;
                }

                // Actually equip
                _equipped[slotId] = itemToEquip;

                int slotPx = playerGrid != null ? playerGrid.SlotSize : 32;
                slotView.SetEquipped(itemToEquip, slotPx);

                // If slot was occupied, put old item in hand
                if (oldItem != null)
                {
                    _heldItem = oldItem;
                    _heldFrom = null;
                    _heldHasOrigin = false;
                    _heldOrigin = default;
                    _heldRotationAtPickup = false;

                    // old item is NOT vendor-held
                    ClearVendorHoldState();

                    ShowGhostForHeldItem(_heldItem, playerGrid);
                    UpdateGhostValidityTint();
                }
                else
                {
                    // If we still had the original held item (non-stack path), clear it now
                    if (_heldItem != null)
                        ClearHeldItem();
                }

                return;
            }

            // ------------------------------------------------------------
            // EMPTY HAND -> pick up from gear
            // ------------------------------------------------------------
            if (_equipped.TryGetValue(slotId, out var equippedItem) && equippedItem != null)
            {
                _equipped.Remove(slotId);
                slotView.ClearEquipped();

                _heldItem = equippedItem;
                _heldFrom = null;
                _heldHasOrigin = false;
                _heldOrigin = default;
                _heldRotationAtPickup = false;

                // coming from gear is not vendor-held
                ClearVendorHoldState();

                ShowGhostForHeldItem(_heldItem, playerGrid);
                UpdateGhostValidityTint();
            }
        }


        // --------------------------------------------------------------------
        // Equip rules
        // --------------------------------------------------------------------
        private void CacheGearDefinitions()
        {
            _gearDefs.Clear();

            if (playerInventoryView == null || playerInventoryView.Layout == null)
                return;

            var defs = playerInventoryView.Layout.gearSlots;
            for (int i = 0; i < defs.Length; i++)
                _gearDefs[defs[i].slotId] = defs[i];
        }

        private bool CanEquipTo(GearSlotId slotId, ItemInstance item)
        {
            if (item == null || item.def == null)
                return false;

            if (!_gearDefs.TryGetValue(slotId, out var def))
                return false;

            if (item.def.EquipCategory != def.acceptsCategory)
                return false;

            if (def.sizePolicy == GearSlotSizePolicy.UpTo)
                return item.Width <= def.slotWidth && item.Height <= def.slotHeight;

            return true; // Exact policy: category is enough in your design
        }

        // --------------------------------------------------------------------
        // Ghost
        // --------------------------------------------------------------------
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

            if (_hoveredGear != null)
            {
                // Preview equip in default orientation
                bool prevRot = _heldItem.rotated90CCW;
                _heldItem.rotated90CCW = false;
                valid = CanEquipTo(_hoveredGear.SlotId, _heldItem);
                _heldItem.rotated90CCW = prevRot;
            }
            else if (playerGrid != null && playerGrid.TryScreenToCell(Input.mousePosition, out var cellP))
            {
                valid = playerGrid.Model.CanPlace(_heldItem, cellP);
            }
            else if (_activeChestGrid != null && _activeChestGrid.TryScreenToCell(Input.mousePosition, out var cellC))
            {
                valid = _activeChestGrid.Model.CanPlace(_heldItem, cellC);
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

                _ghostIconImage = _ghostGO.transform.Find("itemIcon")?.GetComponent<Image>();
                _ghostIconRT = _ghostIconImage != null ? (RectTransform)_ghostIconImage.transform : null;

                if (_ghostIconImage != null)
                    _ghostIconImage.raycastTarget = false;

                if (_ghostRT != null)
                    _ghostRT.localEulerAngles = Vector3.zero;
            }

            if (_ghostGO != null)
                _ghostGO.SetActive(true);

            if (_ghostIconImage != null && item != null && item.def != null)
                _ghostIconImage.sprite = item.def.icon;


            RefreshGhostForHeldItem(grid);
        }

        private void RefreshGhostForHeldItem(InventoryGridUI grid)
        {
            if (_heldItem == null || _ghostRT == null || _ghostIconRT == null || grid == null)
                return;

            _ghostRT.localEulerAngles = Vector3.zero;

            float containerW = _heldItem.Width * grid.SlotSize;
            float containerH = _heldItem.Height * grid.SlotSize;

            _ghostRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, containerW);
            _ghostRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, containerH);

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

            _heldHasOrigin = false;
            _heldOrigin = default;
            _heldRotationAtPickup = false;

            if (_ghostGO != null)
                _ghostGO.SetActive(false);

            ClearVendorHoldState();

        }

        private void CancelHeldItemToSource()
        {
            if (_heldFromVendor)
            {
                ReturnHeldVendorItemToVendor();
                ClearHeldItem();
                return;
            }

            if (_heldItem == null)
                return;

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

            // Came from gear (or unknown): drop policy later. For now just clear.
            ClearHeldItem();
        }

        // --------------------------------------------------------------------
        // Grid click handling
        // --------------------------------------------------------------------
        private bool TryClickGrid(InventoryGridUI grid)
        {
            if (grid == null)
                return false;

            if (grid.Model == null)
                return false;

            if (!grid.TryScreenToCell(Input.mousePosition, out var cell))
                return false;

            // ----------------------------------------------------------------
            // HOLDING -> merge/place
            // ----------------------------------------------------------------
            if (_heldItem != null)
            {
                // Vendor-held items: only allow placing into PLAYER grid (buy-on-place)
                if (_heldFromVendor)
                {
                    // Ignore clicks on non-player grids (vendor grid, chest grid, etc.)
                    if (grid != playerGrid)
                        return true;

                    // Must be placeable first
                    if (!grid.Model.CanPlace(_heldItem, cell))
                        return true;

                    // Must be affordable
                    if (!TrySpendGold(_heldVendorPrice))
                    {
                        ReturnHeldVendorItemToVendor();
                        ClearHeldItem();
                        return true;
                    }

                    // Paid: finalize by placing into player grid
                    if (grid.Model.TryPlace(_heldItem, cell, out _))
                    {
                        grid.RedrawItems();
                        ClearHeldItem();
                    }
                    else
                    {
                        // If place somehow fails after CanPlace, refund is not handled for now.
                        // Safer behavior: return item to vendor and refund gold if you add refunds later.
                        Debug.LogWarning("Unexpected: paid but failed to place item. Returning to vendor.");
                        ReturnHeldVendorItemToVendor();
                        ClearHeldItem();
                    }

                    return true;
                }

                // Normal held item (not from vendor): merge stacks if clicking same stackable item
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
                            ClearHeldItem();

                        return true;
                    }
                }

                // Normal placement into any grid
                if (grid.Model.TryPlace(_heldItem, cell, out _))
                {
                    grid.RedrawItems();
                    ClearHeldItem();
                }

                return true;
            }

            // ----------------------------------------------------------------
            // NOT HOLDING -> pick up
            // ----------------------------------------------------------------
            if (grid.Model.TryPickUpAt(cell, out var picked, out var origin))
            {
                _heldItem = picked;
                _heldFrom = grid;

                _heldOrigin = origin;
                _heldHasOrigin = true;
                _heldRotationAtPickup = _heldItem != null && _heldItem.rotated90CCW;

                // If pickup came from vendor grid, mark vendor-held state
                if (vendorWindowView != null &&
                    vendorWindowView.IsOpen &&
                    vendorWindowView.GridUI == grid)
                {
                    _heldFromVendor = true;
                    _heldVendor = _openVendor;
                    _heldVendorTabIndex = vendorWindowView.ActiveTabIndex;

                    _heldVendorOrigin = origin;
                    _heldVendorHasOrigin = true;
                    _heldVendorRotationAtPickup = _heldRotationAtPickup;

                    _heldVendorPrice = (_heldItem != null && _heldItem.def != null)
                        ? Mathf.Max(0, _heldItem.def.price)
                        : 0;
                }

                grid.RedrawItems();
                ShowGhostForHeldItem(_heldItem, grid);
                UpdateGhostValidityTint();
            }

            return true;
        }

        // --------------------------------------------------------------------
        // World pickup entry point
        // --------------------------------------------------------------------
        public bool TryAutoAddToPlayer(ItemDefinitionSO def, int amount = 1)
        {
            if (_playerModel == null || def == null)
                return false;

            bool ok = _playerModel.TryAddItem(def, amount, out int added);

            // redraw only if UI exists (it can be inactive, still fine to call)
            if (playerGrid != null)
                playerGrid.RedrawItems();

            if (!ok || added < amount)
            {
                Debug.Log($"Inventory full. Could only add {added}/{amount} of '{def.displayName}'.");
                return false;
            }

            return true;
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
                return;

            _gold += amount;

            if (goldView != null)
                goldView.SetGold(_gold);

            Debug.Log($"Gold: +{amount} (Total {_gold})");
        }

        public void OpenVendor(WorldVendor vendor)
        {
            if (vendor == null || vendorWindowView == null)
                return;

            // Optional policy: close chest when opening vendor
            CloseChestUI();

            _openVendor = vendor;

            // Show panel (visibility lives in WindowController)
            if (windows != null)
                windows.OpenVendor();

            // Bind + build tabs + resize content
            vendorWindowView.Open(vendor);
        }

        public void CloseVendorUI()
        {
            _openVendor = null;

            // Clear held item policy: optional (choose one)
            // A) Keep holding item even if vendor closes
            // B) Cancel held item back to source
            // For now, do nothing.

            if (vendorWindowView != null)
                vendorWindowView.Close();

            if (_heldFromVendor)
            {
                ReturnHeldVendorItemToVendor();
                ClearHeldItem();
            }

        }

        public bool CanAfford(int cost)
        {
            if (cost <= 0) return true;
            return _gold >= cost;
        }

        public bool TrySpendGold(int cost)
        {
            if (cost <= 0) return true;
            if (_gold < cost) return false;

            _gold -= cost;

            if (goldView != null)
                goldView.SetGold(_gold);

            Debug.Log($"Gold: -{cost} (Total {_gold})");
            return true;
        }

        private void ClearVendorHoldState()
        {
            _heldFromVendor = false;
            _heldVendor = null;
            _heldVendorTabIndex = 0;
            _heldVendorOrigin = default;
            _heldVendorHasOrigin = false;
            _heldVendorRotationAtPickup = false;
            _heldVendorPrice = 0;
        }

        private void ReturnHeldVendorItemToVendor()
        {
            if (!_heldFromVendor || _heldItem == null || _heldVendor == null)
                return;

            var model = _heldVendor.GetTabModel(_heldVendorTabIndex);
            if (model == null)
            {
                Debug.LogWarning("Vendor model missing when returning item.");
                return;
            }

            // Restore rotation that it had in vendor
            _heldItem.rotated90CCW = _heldVendorRotationAtPickup;

            bool placed = false;

            if (_heldVendorHasOrigin)
                placed = model.TryPlace(_heldItem, _heldVendorOrigin, out _);

            if (!placed && model.TryFindFirstFit(_heldItem, out var alt))
                placed = model.TryPlace(_heldItem, alt, out _);

            if (!placed)
                Debug.LogWarning("Could not return vendor item back to vendor grid (grid full?).");

            // Refresh vendor UI if open
            if (vendorWindowView != null)
                vendorWindowView.RefreshActiveTab();
        }


    }
}
