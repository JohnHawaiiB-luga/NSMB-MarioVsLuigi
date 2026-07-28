using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace NSMB.World {
    // A doorway in the world: stand inside, press E (the powerup button on
    // phones), and it either loads a scene or sends the browser somewhere.
    [RequireComponent(typeof(BoxCollider))]
    public class WorldPortal : MonoBehaviour {

        public string sceneName;
        public string url;

        private bool playerInside;

        private void OnTriggerEnter(Collider other) {
            if (other.GetComponent<WorldPlayerController>()) {
                playerInside = true;
            }
        }

        private void OnTriggerExit(Collider other) {
            if (other.GetComponent<WorldPlayerController>()) {
                playerInside = false;
            }
        }

        private void Update() {
            if (!playerInside || WorldDialogue.IsOpen) {
                return;
            }
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.eKey.wasPressedThisFrame) {
                return;
            }
            if (!string.IsNullOrEmpty(sceneName)) {
                SceneManager.LoadScene(sceneName);
            } else if (!string.IsNullOrEmpty(url)) {
                Application.OpenURL(url);
            }
        }
    }
}
