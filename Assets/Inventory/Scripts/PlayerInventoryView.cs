using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem
{
    /// <summary>
    /// Attach this to the ROOT of your PlayerInventory UI prefab.
    /// No interaction / no items yet:
    /// - Reads PlayerInventoryLayoutSO
    /// - Binds the prebuilt main grid slot Images
    /// - Binds the gear slot UI objects (SlotHead, SlotBody, etc.) via GearSlotView
    /// - Runs simple validation checks
    /// </summary>
    public sealed class PlayerInventoryView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private PlayerInventoryLayoutSO layout;

        [Header("UI References")]
        [Tooltip("Parent that contains exactly width*height slot objects (your 40 slot Images).")]
        [SerializeField] private Transform mainGridSlotsRoot;

        [Tooltip("Parent that contains your gear slot objects (SlotHead, SlotBody, etc.).")]
        [SerializeField] private Transform gearSlotsRoot;

        [Header("Debug")]
        [SerializeField] private bool logValidationWarnings = true;

        // Bound results (no gameplay logic yet)
        private Image[] _mainGridSlots = Array.Empty<Image>();
        private readonly Dictionary<GearSlotId, GearSlotView> _gearSlotViewsById = new();

        public PlayerInventoryLayoutSO Layout => layout;
        public IReadOnlyList<Image> MainGridSlots => _mainGridSlots;
        public IReadOnlyDictionary<GearSlotId, GearSlotView> GearSlotViews => _gearSlotViewsById;

        private void Awake()
        {
            Bind();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep OnValidate safe and light.
            if (layout == null || mainGridSlotsRoot == null || gearSlotsRoot == null)
                return;

            // Optional: auto-bind while editing for faster iteration
            if (!Application.isPlaying)
                Bind();
        }
#endif

        public void Bind()
        {
            _gearSlotViewsById.Clear();

            if (layout == null || mainGridSlotsRoot == null || gearSlotsRoot == null)
            {
                _mainGridSlots = Array.Empty<Image>();
                return;
            }

            BindMainGridSlots();
            BindGearSlots();
            ValidateAgainstLayout();
        }

        private void BindMainGridSlots()
        {
            int childCount = mainGridSlotsRoot.childCount;
            _mainGridSlots = new Image[childCount];

            for (int i = 0; i < childCount; i++)
            {
                var child = mainGridSlotsRoot.GetChild(i);

                // Slot object should have an Image (your slot background)
                if (!child.TryGetComponent<Image>(out var img))
                {
                    img = child.GetComponentInChildren<Image>(true);
                }

                _mainGridSlots[i] = img;
            }
        }

        private void BindGearSlots()
        {
            // Your gear slot UI objects should have GearSlotView on them.
            // (Add GearSlotView to SlotHead, SlotBody, SlotWeaponL, etc.)
            var gearViews = gearSlotsRoot.GetComponentsInChildren<GearSlotView>(true);

            foreach (var view in gearViews)
            {
                if (view == null)
                    continue;

                var id = view.SlotId;

                if (_gearSlotViewsById.ContainsKey(id))
                {
                    if (logValidationWarnings)
                        Debug.LogWarning($"Duplicate GearSlotView for id '{id}' under '{gearSlotsRoot.name}'.", view);
                    continue;
                }

                _gearSlotViewsById.Add(id, view);
            }
        }

        private void ValidateAgainstLayout()
        {
            // Main grid slot count
            int expected = layout.mainGridWidth * layout.mainGridHeight;
            if (_mainGridSlots.Length != expected && logValidationWarnings)
            {
                Debug.LogWarning(
                    $"Main grid slot count mismatch on '{name}'. " +
                    $"Layout expects {layout.mainGridWidth}x{layout.mainGridHeight} = {expected}, " +
                    $"but '{mainGridSlotsRoot.name}' has {_mainGridSlots.Length} children.",
                    this);
            }

            // Gear slots required by layout
            for (int i = 0; i < layout.gearSlots.Length; i++)
            {
                var def = layout.gearSlots[i];

                if (!_gearSlotViewsById.ContainsKey(def.slotId) && logValidationWarnings)
                {
                    Debug.LogWarning(
                        $"Missing GearSlotView for '{def.slotId}' on '{name}'. " +
                        $"Add GearSlotView to the corresponding UI object (e.g. Slot{def.slotId}).",
                        this);
                }
            }

            // Optional: warn if UI has slots not defined in layout
            foreach (var kvp in _gearSlotViewsById)
            {
                bool existsInLayout = false;
                for (int i = 0; i < layout.gearSlots.Length; i++)
                {
                    if (layout.gearSlots[i].slotId == kvp.Key)
                    {
                        existsInLayout = true;
                        break;
                    }
                }

                if (!existsInLayout && logValidationWarnings)
                {
                    Debug.LogWarning(
                        $"GearSlotView '{kvp.Key}' exists in UI but not in layout asset '{layout.name}'.",
                        kvp.Value);
                }
            }
        }
    }

    /// <summary>
    /// Add this to each gear slot UI object (SlotHead, SlotBody, SlotWeaponL, etc.).
    /// This is how the PlayerInventoryView discovers and identifies your manually placed gear slots.
    /// </summary>
    
}
