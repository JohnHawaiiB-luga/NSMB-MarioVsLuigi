using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NSMB.World {
    // Binds the two doors of the entry screen. Buttons are found by name so the
    // scene can be assembled entirely by the editor build script.
    public class WorldEntryMenu : MonoBehaviour {

        private void Awake() {
            Bind("EnterButton", () => SceneManager.LoadScene("WorldHub"));
            // The classic game keeps its own bootstrap: Intro initialises the
            // singletons the Versus menu expects, then advances by itself.
            Bind("VersusButton", () => SceneManager.LoadScene("Intro"));
        }

        private void Bind(string name, UnityEngine.Events.UnityAction action) {
            Transform t = transform.Find(name);
            if (t && t.TryGetComponent(out Button button)) {
                button.onClick.AddListener(action);
            }
        }
    }
}
