using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InventorySystem
{
    public sealed class WeaponSlotToggleView : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject meleeRoot;
        [SerializeField] private GameObject rangedRoot;

        [Header("Left Buttons")]
        [SerializeField] private Button leftI;
        [SerializeField] private Button leftII;

        [Header("Right Buttons")]
        [SerializeField] private Button rightI;
        [SerializeField] private Button rightII;

        [Header("Colors")]
        [SerializeField] private Color activeColor = Color.white;
        [SerializeField] private Color inactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        private bool _showMelee = true;

        private void Awake()
        {
            if (leftI != null) leftI.onClick.AddListener(ShowMelee);
            if (leftII != null) leftII.onClick.AddListener(ShowRanged);

            if (rightI != null) rightI.onClick.AddListener(ShowMelee);
            if (rightII != null) rightII.onClick.AddListener(ShowRanged);

            // Default
            ShowMelee();
        }

        public void ShowMelee()
        {
            _showMelee = true;

            if (meleeRoot != null) meleeRoot.SetActive(true);
            if (rangedRoot != null) rangedRoot.SetActive(false);

            RefreshButtonColors();
        }

        public void ShowRanged()
        {
            _showMelee = false;

            if (meleeRoot != null) meleeRoot.SetActive(false);
            if (rangedRoot != null) rangedRoot.SetActive(true);

            RefreshButtonColors();
        }

        private void RefreshButtonColors()
        {
            // I = melee active
            SetButtonVisual(leftI, _showMelee);
            SetButtonVisual(rightI, _showMelee);

            // II = ranged active
            SetButtonVisual(leftII, !_showMelee);
            SetButtonVisual(rightII, !_showMelee);
        }

        private void SetButtonVisual(Button btn, bool active)
        {
            if (btn == null)
                return;

            Color c = active ? activeColor : inactiveColor;

            // Tint the button graphic (Image)
            if (btn.targetGraphic != null)
                btn.targetGraphic.color = c;

            // Tint the TMP label (either on the button or in children)
            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
                tmp.color = c;
        }
    }
}
