using NSMB.UI.MainMenu;
using NSMB.Utilities.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NSMB.UI.Elements {
    public class PipeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {

        //---Serialized Variables
        [SerializeField] private RectTransform rect;
        [SerializeField] private Button button;
        [SerializeField] private Clickable clickable;
        [SerializeField] private Image image;
        [SerializeField] private TMP_Text label;

        [SerializeField] private Color selectedColor = Color.white, deselectedColor = Color.gray;
        [SerializeField] private Vector2 sizeDecreasePixels = new Vector2(50f, 0);

        //---Private Variables
        private Color disabledColor;
        private Vector2 size;
        private bool hover;
        private bool wasSelected;
        private bool freshlyEnabled;

        public void OnValidate() {
            this.SetIfNull(ref rect);
            this.SetIfNull(ref button);
            this.SetIfNull(ref clickable);
            this.SetIfNull(ref image, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref label, UnityExtensions.GetComponentType.Children);

            disabledColor = new Color(deselectedColor.r * 0.5f, deselectedColor.g * 0.5f, deselectedColor.b * 0.5f);
        }

        public void OnDisable() {
            hover = false;
        }

        public void OnEnable() {
            // Opening a menu selects its default button, and that selection is
            // not the cursor moving — it should not click.
            freshlyEnabled = true;
        }

        private bool Interactable => (!button || button.IsInteractable())
            && (!clickable || clickable.Interactable);

        private void PlayCursorSound() {
            if (!Interactable) {
                return;
            }
            if (MainMenuCanvas.Instance) {
                MainMenuCanvas.Instance.PlayCursorSound();
            } else if (GlobalController.Instance) {
                GlobalController.Instance.PlaySound(SoundEffect.UI_Cursor);
            }
        }

        public void Start() {
            size = rect.sizeDelta;
        }

        public void Update() {
            if ((button && !button.IsInteractable())
                || (clickable && !clickable.Interactable)) {
                rect.sizeDelta = size - sizeDecreasePixels;
                image.color = disabledColor;
                label.color = Color.gray;
                return;
            }
            // The cursor landing on a button is the sound the menu was missing:
            // moving through it with the keyboard or pad was silent.
            bool selected = EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject;
            if (selected && !wasSelected && !freshlyEnabled) {
                PlayCursorSound();
            }
            wasSelected = selected;
            freshlyEnabled = false;

            if (hover || selected) {
                rect.sizeDelta = size;
                image.color = selectedColor;
                label.color = Color.yellow;
            } else {
                rect.sizeDelta = size - sizeDecreasePixels;
                image.color = deselectedColor;
                label.color = Color.white;
            }
        }

        public void OnPointerEnter(PointerEventData eventData) {
            if (!hover) {
                PlayCursorSound();
            }
            hover = true;
        }

        public void OnPointerExit(PointerEventData eventData) {
            hover = false;
        }
    }
}
