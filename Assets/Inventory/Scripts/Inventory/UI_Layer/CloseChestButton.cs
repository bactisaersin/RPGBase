using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem
{
    [RequireComponent(typeof(Button))]
    public sealed class CloseChestButton : MonoBehaviour
    {
        [SerializeField] private InventoryWindowController windows;

        private void Awake()
        {
            if (windows == null)
            {
                Debug.LogWarning("CloseChestButton missing InventoryWindowController reference.", this);
                return;
            }

            GetComponent<Button>().onClick.AddListener(windows.CloseChest);
        }
    }
}
