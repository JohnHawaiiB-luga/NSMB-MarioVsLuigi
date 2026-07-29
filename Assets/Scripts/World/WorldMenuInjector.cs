using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NSMB.World {
    // Grafts one button — WORLD — into the game's real main menu by cloning
    // its own Play button, so it inherits the exact style, sounds and layout
    // language. No scene surgery: upstream's menu stays untouched on disk.
    public class WorldMenuInjector : MonoBehaviour {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init() {
            var host = new GameObject("WorldMenuInjector");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<WorldMenuInjector>();
        }

        private void Awake() {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            if (scene.name == "MainMenu") {
                StartCoroutine(Inject());
            }
        }

        private IEnumerator Inject() {
            // Give the menu a moment to build itself and apply translations.
            yield return null;
            yield return null;

            if (GameObject.Find("WorldButton")) {
                yield break;
            }

            Button play = null;
            foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                var label = b.GetComponentInChildren<TMP_Text>();
                if (label && label.text.ToLowerInvariant().Contains("play")) {
                    play = b;
                    break;
                }
            }
            if (!play) {
                yield break;
            }

            var clone = Instantiate(play.gameObject, play.transform.parent);
            clone.name = "WorldButton";
            clone.transform.SetSiblingIndex(play.transform.GetSiblingIndex());

            // Strip upstream logic from the clone but keep every visual part.
            foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true)) {
                bool keep = mb is Button || mb is Image || mb is TMP_Text
                    || mb is LayoutElement || mb is LayoutGroup || mb is ContentSizeFitter || mb is Mask || mb is RectMask2D;
                if (!keep) {
                    Destroy(mb);
                }
            }

            var text = clone.GetComponentInChildren<TMP_Text>();
            if (text) {
                text.text = "World";
            }
            var image = clone.GetComponent<Image>();
            if (image) {
                image.color = new Color(0.5f, 0.16f, 0.78f);
            }

            // Without a layout group the clone lands exactly on the original —
            // lift it one slot above, making World the headline mode.
            if (!play.transform.parent.GetComponent<LayoutGroup>()) {
                var prt = play.GetComponent<RectTransform>();
                var crt = clone.GetComponent<RectTransform>();
                crt.anchoredPosition = prt.anchoredPosition + new Vector2(0f, prt.rect.height + 16f);
            }

            var button = clone.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => SceneManager.LoadScene("WorldHub"));
        }
    }
}
