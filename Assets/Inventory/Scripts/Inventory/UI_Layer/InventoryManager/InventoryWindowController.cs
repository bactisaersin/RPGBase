using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem
{
    public sealed class InventoryWindowController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject playerPanel;
        [SerializeField] private GameObject chestPanel;   // if you have a single dynamic chest panel
        [SerializeField] private GameObject vendorPanel;

        [Header("Buttons")]
        [SerializeField] private Button closePlayerButton;
        [SerializeField] private Button closeChestButton;
        [SerializeField] private Button closeVendorButton;

        [Header("Input")]
        [SerializeField] private KeyCode togglePlayerKey = KeyCode.I;

        [Header("Refs")]
        [SerializeField] private InventoryUIManager uiManager;

        private void Awake()
        {
            if (uiManager == null)
                uiManager = FindFirstObjectByType<InventoryUIManager>();

            if (closePlayerButton != null)
                closePlayerButton.onClick.AddListener(ClosePlayer);

            if (closeChestButton != null)
                closeChestButton.onClick.AddListener(CloseChest);

            if (closeVendorButton != null)
                closeVendorButton.onClick.AddListener(CloseVendor);
        }

        private void Update()
        {
            if (Input.GetKeyDown(togglePlayerKey))
                TogglePlayer();
        }

        // ---------------- Player ----------------
        public void TogglePlayer()
        {
            if (playerPanel == null) return;
            playerPanel.SetActive(!playerPanel.activeSelf);
        }

        public void OpenPlayer()
        {
            if (playerPanel == null) return;
            playerPanel.SetActive(true);
        }

        public void ClosePlayer()
        {
            if (playerPanel == null) return;
            playerPanel.SetActive(false);
        }

        // ---------------- Chest ----------------
        public void OpenChest()
        {
            if (chestPanel == null) return;
            chestPanel.SetActive(true);
        }

        public void CloseChest()
        {
            // clear state first (important)
            if (uiManager != null)
                uiManager.CloseChestUI();

            if (chestPanel == null) return;
            chestPanel.SetActive(false);
        }

        // ---------------- Vendor ----------------
        public void OpenVendor()
        {
            if (vendorPanel == null) return;
            vendorPanel.SetActive(true);
        }

        public void CloseVendor()
        {
            // clear state first (important)
            if (uiManager != null)
                uiManager.CloseVendorUI();

            if (vendorPanel == null) return;
            vendorPanel.SetActive(false);
        }
    }
}
