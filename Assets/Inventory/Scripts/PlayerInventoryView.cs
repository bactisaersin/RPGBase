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
        

        [Tooltip("Parent that contains your gear slot objects (SlotHead, SlotBody, etc.).")]
        [SerializeField] private Transform gearSlotsRoot;

        [Header("Debug")]
        [SerializeField] private bool logValidationWarnings = true;

        // Bound results (no gameplay logic yet)
        private readonly Dictionary<GearSlotId, GearSlotView> _gearSlotViewsById = new();

        public PlayerInventoryLayoutSO Layout => layout;
        public IReadOnlyDictionary<GearSlotId, GearSlotView> GearSlotViews => _gearSlotViewsById;

        private void Awake()
        {
            Bind();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep OnValidate safe and light.
            if (layout == null || gearSlotsRoot == null)
                return;


            // Optional: auto-bind while editing for faster iteration
            if (!Application.isPlaying)
                Bind();
        }
#endif

        public void Bind()
        {
            _gearSlotViewsById.Clear();

            if (layout == null || gearSlotsRoot == null)
                return;

            BindGearSlots();
            ValidateAgainstLayout();

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
