using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem
{
    public sealed class InventoryWindowController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject playerPanel;
        [SerializeField] private GameObject chestPanel;

        [Header("Buttons")]
        [SerializeField] private Button closePlayerButton;
        [SerializeField] private Button closeChestButton;

        [Header("Input")]
        [SerializeField] private KeyCode togglePlayerKey = KeyCode.I;

        private InventoryUIManager _mgr;

        private void Awake()
        {
            _mgr = FindFirstObjectByType<InventoryUIManager>();

            if (closePlayerButton != null)
                closePlayerButton.onClick.AddListener(ClosePlayer);

            // IMPORTANT: closing chest should notify the manager too,
            // so it clears _activeChestGrid / open chest reference.
            if (closeChestButton != null)
                closeChestButton.onClick.AddListener(CloseChest);
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
            if (chestPanel != null)
                chestPanel.SetActive(false);

            // Let manager clear its active chest references.
            if (_mgr != null)
                _mgr.CloseChestUI();
        }
    }
}
