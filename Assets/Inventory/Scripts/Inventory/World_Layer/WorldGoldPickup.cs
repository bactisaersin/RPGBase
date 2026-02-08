using UnityEngine;
using TMPro;

namespace InventorySystem
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class WorldGoldPickup : MonoBehaviour
    {
        [Header("Amount")]
        [Min(0)] [SerializeField] private int minAmount = 1;
        [Min(0)] [SerializeField] private int maxAmount = 10;
        [SerializeField] private bool randomizeOnAwake = true;

        [Header("Label (optional)")]
        [SerializeField] private TextMeshPro amountLabel;

        [Header("Debug")]
        [SerializeField] private int amount = 1;

        public int Amount => amount;

        private void Awake()
        {
            if (randomizeOnAwake)
            {
                int min = Mathf.Min(minAmount, maxAmount);
                int max = Mathf.Max(minAmount, maxAmount);
                amount = Random.Range(min, max + 1);
            }
            else
            {
                amount = Mathf.Max(0, amount);
            }

            if (amountLabel != null)
                amountLabel.text = amount.ToString();
        }

        private void OnMouseDown()
        {
            var mgr = FindFirstObjectByType<InventoryUIManager>();
            if (mgr == null)
                return;

            mgr.AddGold(amount);
            Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (minAmount < 0) minAmount = 0;
            if (maxAmount < 0) maxAmount = 0;
        }
#endif
    }
}
