using UnityEngine;

namespace InventorySystem
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WorldItemPickup : MonoBehaviour
    {
        [SerializeField] private ItemDefinitionSO itemDef;
        [SerializeField] private int amount = 1;

        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (itemDef == null || spriteRenderer == null)
                return;

            if (itemDef.worldSprite == null)
                return;

            spriteRenderer.sprite = itemDef.worldSprite;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Lets you see the correct sprite in the editor without pressing Play.
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (itemDef == null || spriteRenderer == null || itemDef.worldSprite == null)
                return;

            spriteRenderer.sprite = itemDef.worldSprite;
        }
#endif

        private void OnMouseDown()
        {
            var mgr = FindFirstObjectByType<InventoryUIManager>();
            if (mgr == null || itemDef == null)
                return;

            bool added = mgr.TryAutoAddToPlayer(itemDef, amount);
            if (added)
                Destroy(gameObject);
        }
    }
}
