using TMPro;
using UnityEngine;

namespace InventorySystem
{
    public sealed class PlayerGoldView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI amountText;

        public void SetGold(int amount)
        {
            if (amountText == null)
                return;

            amountText.text = amount.ToString();
        }
    }
}
