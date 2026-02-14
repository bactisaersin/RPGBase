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

        [Header("Vendor Selling")]
        [SerializeField, Range(0f, 1f)] private float vendorSellMultiplier = 0.5f;


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
        //private bool _heldVendorRotationAtPickup;
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
                        if (!SpendGold(_heldVendorPrice))
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
                valid = IsValidDropOrMerge(playerGrid, cellP);
            }
            else if (_activeChestGrid != null && _activeChestGrid.TryScreenToCell(Input.mousePosition, out var cellC))
            {
                valid = IsValidDropOrMerge(_activeChestGrid, cellC);
            }
            else if (vendorWindowView != null &&
                     vendorWindowView.IsOpen &&
                     vendorWindowView.GridUI != null &&
                     vendorWindowView.GridUI.TryScreenToCell(Input.mousePosition, out var cellV))
            {
                valid = IsValidDropOrMerge(vendorWindowView.GridUI, cellV);
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

            _heldFromVendor = false;
            _heldVendor = null;
            _heldVendorTabIndex = 0;
            _heldVendorHasOrigin = false;
            _heldVendorOrigin = default;


            if (_ghostGO != null)
                _ghostGO.SetActive(false);

            ClearVendorHoldState();

        }



        // --------------------------------------------------------------------
        // Grid click handling
        // --------------------------------------------------------------------
        private bool TryClickGrid(InventoryGridUI grid)
        {
            if (grid == null || grid.Model == null)
                return false;

            if (!grid.TryScreenToCell(Input.mousePosition, out var cell))
                return false;

            bool vendorOpen = vendorWindowView != null && vendorWindowView.IsOpen;
            bool isVendorGrid = vendorOpen && vendorWindowView.GridUI == grid;

            // --------------------------------------------------------------------
            // HOLDING SOMETHING
            // --------------------------------------------------------------------
            if (_heldItem != null)
            {
                // -------------------------
                // CASE A) Holding from VENDOR (BUY FLOW)
                // -------------------------
                if (_heldFromVendor)
                {
                    // If they click vendor grid while holding a vendor item, treat as "cancel/return"
                    if (isVendorGrid)
                    {
                        ReturnHeldVendorItemToVendor();
                        return true;
                    }

                    // We only allow buying into non-vendor grids (player/chest)
                    if (_heldItem.def == null)
                        return true;

                    int unitBuyPrice = Mathf.Max(0, _heldItem.def.price);

                    // 1) MERGE into existing stack if possible
                    if (grid.Model.TryGetPlacementAt(cell, out _, out _, out var targetItem) &&
                        targetItem != null &&
                        _heldItem.def.stackable &&
                        targetItem.def == _heldItem.def)
                    {
                        int space = Mathf.Max(0, _heldItem.def.stackMax - targetItem.amount);

                        if (space > 0)
                        {
                            int move = Mathf.Min(space, _heldItem.amount);
                            int cost = move * unitBuyPrice;

                            if (!SpendGold(cost))
                            {
                                // Not enough gold: keep holding, do nothing else
                                return true;
                            }

                            targetItem.amount += move;
                            _heldItem.amount -= move;

                            grid.RedrawItems();

                            if (vendorOpen)
                                vendorWindowView.RefreshActiveTab();

                            if (_heldItem.amount <= 0)
                            {
                                ClearHeldItem();
                            }
                            else
                            {
                                // still holding remainder
                                ShowGhostForHeldItem(_heldItem, grid);
                                UpdateGhostValidityTint();
                            }

                            return true;
                        }

                        // stack full -> fall through to placement attempt (will likely fail)
                    }

                    // 2) PLACE into empty area
                    if (grid.Model.TryPlace(_heldItem, cell, out _))
                    {
                        int cost = _heldItem.amount * unitBuyPrice;

                        // Safety: if somehow gold fails, undo and return to vendor
                        if (!SpendGold(cost))
                        {
                            // Undo placement
                            grid.Model.TryPickUpAt(cell, out _, out _);
                            grid.RedrawItems();

                            ReturnHeldVendorItemToVendor();
                            return true;
                        }

                        grid.RedrawItems();

                        if (vendorOpen)
                            vendorWindowView.RefreshActiveTab();

                        ClearHeldItem();
                    }

                    return true;
                }

                // -------------------------
                // CASE B) Holding from PLAYER/CHEST and dropping onto VENDOR (SELL FLOW)
                // sell ONLY ONE unit, rest returns
                // -------------------------
                if (isVendorGrid)
                {
                    if (_heldItem.def == null)
                        return true;

                    int unitSellPrice = GetSellValuePerUnit(_heldItem.def);


                    // We will sell exactly one unit
                    bool isStack = _heldItem.def.stackable && _heldItem.amount > 1;

                    // Item to sell
                    ItemInstance sellOne = isStack
                        ? new ItemInstance(_heldItem.def, 1)
                        : _heldItem;

                    // Try merge into existing vendor stack first
                    if (grid.Model.TryGetPlacementAt(cell, out _, out _, out var targetItem) &&
                        targetItem != null &&
                        sellOne.def.stackable &&
                        targetItem.def == sellOne.def)
                    {
                        int space = Mathf.Max(0, sellOne.def.stackMax - targetItem.amount);
                        if (space > 0)
                        {
                            targetItem.amount += 1;
                            AddGold(unitSellPrice);

                            // Return remainder back to source if this was a stack
                            if (isStack)
                            {
                                _heldItem.amount -= 1;
                                _heldItem.rotated90CCW = _heldRotationAtPickup;

                                bool placedBack = false;

                                if (_heldFrom != null && _heldHasOrigin)
                                    placedBack = _heldFrom.Model.TryPlace(_heldItem, _heldOrigin, out _);

                                if (!placedBack && _heldFrom != null &&
                                    _heldFrom.Model.TryFindFirstFit(_heldItem, out var alt) &&
                                    _heldFrom.Model.TryPlace(_heldItem, alt, out _))
                                {
                                    placedBack = true;
                                }

                                if (_heldFrom != null)
                                    _heldFrom.RedrawItems();

                                // If we couldn't return remainder, keep it in hand (never delete items)
                                if (!placedBack)
                                {
                                    ShowGhostForHeldItem(_heldItem, grid);
                                    UpdateGhostValidityTint();
                                    grid.RedrawItems();
                                    if (vendorOpen) vendorWindowView.RefreshActiveTab();
                                    return true;
                                }

                                ClearHeldItem();
                            }
                            else
                            {
                                // sold whole item
                                ClearHeldItem();
                            }

                            grid.RedrawItems();
                            if (vendorOpen) vendorWindowView.RefreshActiveTab();
                            return true;
                        }
                    }

                    // Otherwise: place the sold-one at the CLICKED cell first
                    if (grid.Model.TryPlace(sellOne, cell, out _))
                    {
                        AddGold(unitSellPrice);

                        // Return remainder back to source if stack
                        if (isStack)
                        {
                            _heldItem.amount -= 1;
                            _heldItem.rotated90CCW = _heldRotationAtPickup;

                            bool placedBack = false;

                            if (_heldFrom != null && _heldHasOrigin)
                                placedBack = _heldFrom.Model.TryPlace(_heldItem, _heldOrigin, out _);

                            if (!placedBack && _heldFrom != null &&
                                _heldFrom.Model.TryFindFirstFit(_heldItem, out var alt) &&
                                _heldFrom.Model.TryPlace(_heldItem, alt, out _))
                            {
                                placedBack = true;
                            }

                            if (_heldFrom != null)
                                _heldFrom.RedrawItems();

                            if (!placedBack)
                            {
                                // keep remainder in hand
                                ShowGhostForHeldItem(_heldItem, grid);
                                UpdateGhostValidityTint();
                                grid.RedrawItems();
                                if (vendorOpen) vendorWindowView.RefreshActiveTab();
                                return true;
                            }

                            ClearHeldItem();
                        }
                        else
                        {
                            ClearHeldItem();
                        }

                        grid.RedrawItems();
                        if (vendorOpen) vendorWindowView.RefreshActiveTab();
                        return true;
                    }


                    return true;
                }

                // -------------------------
                // CASE C) Normal grid-to-grid move (player <-> chest)
                // -------------------------

                // Merge if possible
                if (grid.Model.TryGetPlacementAt(cell, out _, out _, out var target) &&
                    target != null &&
                    _heldItem.def != null &&
                    _heldItem.def.stackable &&
                    target.def == _heldItem.def)
                {
                    int space = Mathf.Max(0, _heldItem.def.stackMax - target.amount);
                    if (space > 0)
                    {
                        int move = Mathf.Min(space, _heldItem.amount);
                        target.amount += move;
                        _heldItem.amount -= move;

                        grid.RedrawItems();

                        if (_heldItem.amount <= 0)
                            ClearHeldItem();
                        else
                        {
                            ShowGhostForHeldItem(_heldItem, grid);
                            UpdateGhostValidityTint();
                        }

                        return true;
                    }
                }

                // Place into empty space
                if (grid.Model.TryPlace(_heldItem, cell, out _))
                {
                    grid.RedrawItems();
                    ClearHeldItem();
                }

                return true;
            }

            // --------------------------------------------------------------------
            // NOT HOLDING -> PICK UP
            // --------------------------------------------------------------------

            // Pick from vendor grid
            // Pick from vendor grid
            if (isVendorGrid)
            {
                // Look at what's in this cell WITHOUT removing it yet
                if (!grid.Model.TryGetPlacementAt(cell, out var origin, out _, out var targetItem) ||
                    targetItem == null || targetItem.def == null)
                    return true;

                // If it's a stack, take ONLY ONE and reduce vendor stack in-place
                if (targetItem.def.stackable && targetItem.amount > 1)
                {
                    // Take one
                    _heldItem = new ItemInstance(targetItem.def, 1);
                    _heldItem.rotated90CCW = false; // vendor items default orientation (your policy)

                    // Reduce vendor stack
                    targetItem.amount -= 1;

                    // Vendor source info for ESC return (we return 1 unit)
                    _heldFromVendor = true;
                    _heldVendor = _openVendor;
                    _heldVendorTabIndex = vendorWindowView.ActiveTabIndex;
                    _heldVendorOrigin = origin;
                    _heldVendorHasOrigin = true;

                    // Not from a normal grid
                    _heldFrom = null;
                    _heldHasOrigin = false;
                    _heldOrigin = default;
                    _heldRotationAtPickup = false;

                    grid.RedrawItems();
                    ShowGhostForHeldItem(_heldItem, grid);
                    UpdateGhostValidityTint();
                    return true;
                }

                // Otherwise (non-stack or amount==1): remove the placement as before
                if (grid.Model.TryPickUpAt(cell, out var picked, out var pickedOrigin))
                {
                    _heldItem = picked;

                    _heldFromVendor = true;
                    _heldVendor = _openVendor;
                    _heldVendorTabIndex = vendorWindowView.ActiveTabIndex;
                    _heldVendorOrigin = pickedOrigin;
                    _heldVendorHasOrigin = true;

                    _heldFrom = null;
                    _heldHasOrigin = false;
                    _heldOrigin = default;
                    _heldRotationAtPickup = false;

                    grid.RedrawItems();
                    ShowGhostForHeldItem(_heldItem, grid);
                    UpdateGhostValidityTint();
                }

                return true;
            }


            // Pick from normal grid
            if (grid.Model.TryPickUpAt(cell, out var pickedNormal, out var originNormal))
            {
                _heldItem = pickedNormal;
                _heldFrom = grid;

                _heldOrigin = originNormal;
                _heldHasOrigin = true;
                _heldRotationAtPickup = _heldItem != null && _heldItem.rotated90CCW;

                // not from vendor
                _heldFromVendor = false;
                _heldVendor = null;
                _heldVendorTabIndex = 0;
                _heldVendorHasOrigin = false;
                _heldVendorOrigin = default;

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

        public bool SpendGold(int amount)
        {
            amount = Mathf.Max(0, amount);

            if (amount == 0)
                return true;

            if (_gold < amount)
                return false;

            _gold -= amount;

            if (goldView != null)
                goldView.SetGold(_gold);

            Debug.Log($"Gold: -{amount} (Total {_gold})");
            return true;
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

        
        

        private void ClearVendorHoldState()
        {
            _heldFromVendor = false;
            _heldVendor = null;
            _heldVendorTabIndex = 0;
            _heldVendorOrigin = default;
            _heldVendorHasOrigin = false;
            //_heldVendorRotationAtPickup = false;
            _heldVendorPrice = 0;
        }

        

        private int GetSellValue(ItemInstance item)
        {
            if (item == null || item.def == null) return 0;
            float baseValue = Mathf.Max(0, item.def.price);
            int amount = Mathf.Max(1, item.amount);
            return Mathf.FloorToInt(baseValue * vendorSellMultiplier) * amount;
        }

        private int GetSellValuePerUnit(ItemDefinitionSO def)
        {
            if (def == null) return 0;
            float baseValue = Mathf.Max(0, def.price);
            return Mathf.FloorToInt(baseValue * vendorSellMultiplier);
        }

        // Returns true if remainder was returned somewhere; false means keep it in hand (or cancel)
        private bool ReturnRemainderToPickupOriginOrKeepInHand()
        {
            if (_heldItem == null)
                return false;

            if (_heldFrom != null && _heldHasOrigin)
            {
                _heldItem.rotated90CCW = _heldRotationAtPickup;

                bool placedBack = _heldFrom.Model.TryPlace(_heldItem, _heldOrigin, out _);
                if (!placedBack)
                {
                    if (_heldFrom.Model.TryFindFirstFit(_heldItem, out var alt) &&
                        _heldFrom.Model.TryPlace(_heldItem, alt, out _))
                    {
                        placedBack = true;
                    }
                }

                _heldFrom.RedrawItems();

                if (placedBack)
                {
                    ClearHeldItem();
                    return true;
                }

                // couldn't return; keep holding (don’t lose items)
                _heldItem.rotated90CCW = false;
                ShowGhostForHeldItem(_heldItem, playerGrid);
                UpdateGhostValidityTint();
                return false;
            }

            // No origin info -> keep in hand
            return false;
        }

        private bool IsValidDropOrMerge(InventoryGridUI grid, Vector2Int cell)
        {
            if (grid == null || grid.Model == null || _heldItem == null || _heldItem.def == null)
                return false;

            // 1) Merge validity: if target cell is same item stack and has room -> valid (green)
            if (grid.Model.TryGetPlacementAt(cell, out _, out _, out var target) &&
                target != null &&
                _heldItem.def.stackable &&
                target.def == _heldItem.def)
            {
                return target.amount < _heldItem.def.stackMax; // green unless full
            }

            // 2) Otherwise: normal placement validity
            return grid.Model.CanPlace(_heldItem, cell);
        }

        private void ReturnHeldVendorItemToVendor()
        {
            if (!_heldFromVendor || _heldItem == null || _heldVendor == null)
            {
                ClearHeldItem();
                return;
            }

            var model = _heldVendor.GetTabModel(_heldVendorTabIndex);
            if (model == null)
            {
                ClearHeldItem();
                return;
            }

            // Try to put back exactly where it came from first
            bool placed = false;

            if (_heldVendorHasOrigin)
                placed = model.TryPlace(_heldItem, _heldVendorOrigin, out _);

            // Fallback: anywhere in vendor grid
            if (!placed && model.TryFindFirstFit(_heldItem, out var alt))
                placed = model.TryPlace(_heldItem, alt, out _);

            // If still not placed, keep it in hand (never delete items)
            if (!placed)
                return;

            // Refresh vendor UI if it’s still open
            if (vendorWindowView != null && vendorWindowView.IsOpen)
                vendorWindowView.RefreshActiveTab(); // add this method or call BindModel again

            ClearHeldItem();
        }

        private void CancelHeldItemToSource()
        {
            if (_heldItem == null)
                return;

            // If item came from vendor, return it to vendor
            if (_heldFromVendor)
            {
                ReturnHeldVendorItemToVendor();
                return;
            }

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

            // Came from gear (or unknown): for now just clear
            ClearHeldItem();
        }

        

    }
}
