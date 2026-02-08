using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem
{
    public sealed class InventoryWindowController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject playerPanel;

        [SerializeField] private GameObject chestSmallPanel;
        [SerializeField] private GameObject chestMediumPanel;
        [SerializeField] private GameObject chestLargePanel;

        [Header("Buttons")]
        [SerializeField] private Button closePlayerButton;

        [Header("Input")]
        [SerializeField] private KeyCode togglePlayerKey = KeyCode.I;

        private void Awake()
        {
            if (closePlayerButton != null)
                closePlayerButton.onClick.AddListener(ClosePlayer);
        }

        private void Update()
        {
            if (Input.GetKeyDown(togglePlayerKey))
                TogglePlayer();
        }

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

        public void OpenChestUI(ChestSize size)
        {
            CloseChest();

            switch (size)
            {
                case ChestSize.Small:
                    if (chestSmallPanel != null) chestSmallPanel.SetActive(true);
                    break;
                case ChestSize.Medium:
                    if (chestMediumPanel != null) chestMediumPanel.SetActive(true);
                    break;
                case ChestSize.Large:
                    if (chestLargePanel != null) chestLargePanel.SetActive(true);
                    break;
            }
        }

        public void CloseChest()
        {
            if (chestSmallPanel != null) chestSmallPanel.SetActive(false);
            if (chestMediumPanel != null) chestMediumPanel.SetActive(false);
            if (chestLargePanel != null) chestLargePanel.SetActive(false);
        }
    }
}
