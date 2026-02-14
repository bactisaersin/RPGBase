using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InventorySystem
{
    public sealed class GearSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private GearSlotId slotId;

        [Header("Runtime Equipped UI")]
        [SerializeField] private RectTransform equippedRoot;   // the empty child container
        [SerializeField] private GameObject itemIconViewPrefab; // ItemIconView prefab (root + child "itemIcon")

        private InventoryUIManager _mgr;

        // Spawned runtime UI
        private RectTransform _iconContainerRT;  // ItemIconView root RectTransform
        private RectTransform _iconImageRT;      // child "itemIcon" RectTransform
        private Image _iconImage;                // child "itemIcon" Image

        public GearSlotId SlotId => slotId;

        private void Awake()
        {
            _mgr = FindFirstObjectByType<InventoryUIManager>();

            // Optional auto-find if you named it "EquippedRoot"
            if (equippedRoot == null)
                equippedRoot = transform.Find("EquippedRoot") as RectTransform;
        }

        public void OnPointerEnter(PointerEventData eventData) => _mgr?.NotifyGearHover(this);
        public void OnPointerExit(PointerEventData eventData) => _mgr?.NotifyGearExit(this);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            _mgr?.OnGearSlotClicked(this);
        }

        public void SetEquipped(ItemInstance item, int slotPixelSize)
        {
            if (item == null || item.def == null)
                return;

            EnsureRuntimeIcon();

            if (_iconImage == null || _iconContainerRT == null || _iconImageRT == null)
                return;

            _iconImage.sprite = item.def.icon;
            _iconImage.enabled = _iconImage.sprite != null;

            // Size container to item footprint (weapons can be smaller than slot)
            float w = item.Width * slotPixelSize;
            float h = item.Height * slotPixelSize;

            _iconContainerRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
            _iconContainerRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);

            // Center inside the gear slot
            _iconContainerRT.anchorMin = _iconContainerRT.anchorMax = new Vector2(0.5f, 0.5f);
            _iconContainerRT.pivot = new Vector2(0.5f, 0.5f);
            _iconContainerRT.anchoredPosition = Vector2.zero;
            _iconContainerRT.localEulerAngles = Vector3.zero;

            // Rotate only the inner image if you ever allow it; for now keep it default
            _iconImageRT.anchorMin = _iconImageRT.anchorMax = new Vector2(0.5f, 0.5f);
            _iconImageRT.pivot = new Vector2(0.5f, 0.5f);
            _iconImageRT.anchoredPosition = Vector2.zero;
            _iconImageRT.localEulerAngles = Vector3.zero;

            // Make inner image fill container
            _iconImageRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
            _iconImageRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);

            _iconContainerRT.gameObject.SetActive(true);
        }

        public void ClearEquipped()
        {
            if (_iconContainerRT != null)
                _iconContainerRT.gameObject.SetActive(false);

            if (_iconImage != null)
            {
                _iconImage.sprite = null;
                _iconImage.enabled = false;
            }
        }

        private void EnsureRuntimeIcon()
        {
            if (_iconContainerRT != null)
                return;

            if (equippedRoot == null)
            {
                Debug.LogWarning($"GearSlotView '{name}' missing equippedRoot reference.", this);
                return;
            }

            if (itemIconViewPrefab == null)
            {
                Debug.LogWarning($"GearSlotView '{name}' missing itemIconViewPrefab reference.", this);
                return;
            }

            var go = Instantiate(itemIconViewPrefab, equippedRoot);
            _iconContainerRT = go.GetComponent<RectTransform>();

            // Find child "itemIcon"
            var child = go.transform.Find("itemIcon");
            if (child != null)
            {
                _iconImageRT = (RectTransform)child;
                _iconImage = child.GetComponent<Image>();
                if (_iconImage != null)
                    _iconImage.raycastTarget = false; // don't block slot hover/click
            }

            // Start hidden
            _iconContainerRT.gameObject.SetActive(false);
        }
    }
}
