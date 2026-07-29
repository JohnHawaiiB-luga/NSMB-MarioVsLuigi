using Quantum;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.World {
    // Cloned buttons are not part of their submenu's own selection handling, so
    // they were silent. This gives them the menu's real cursor and confirm
    // sounds through the game's own audio path.
    public class WorldMenuButtonSfx : MonoBehaviour,
        IPointerEnterHandler, ISelectHandler, ISubmitHandler, IPointerClickHandler {

        public void OnPointerEnter(PointerEventData eventData) {
            Play(SoundEffect.UI_Cursor);
        }

        public void OnSelect(BaseEventData eventData) {
            Play(SoundEffect.UI_Cursor);
        }

        public void OnSubmit(BaseEventData eventData) {
            Play(SoundEffect.UI_Decide);
        }

        public void OnPointerClick(PointerEventData eventData) {
            Play(SoundEffect.UI_Decide);
        }

        private static void Play(SoundEffect sound) {
            if (GlobalController.Instance) {
                GlobalController.Instance.PlaySound(sound);
            }
        }
    }
}
