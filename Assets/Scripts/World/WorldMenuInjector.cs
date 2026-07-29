using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NSMB.World {
    // Grafts one button — World — into the game's real main menu by cloning its
    // own Play button, so it inherits the exact style, sounds and layout
    // language. The menu builds itself behind the title screen and its buttons
    // start inactive, so this keeps watching until they exist.
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
            StopAllCoroutines();
            if (scene.name == "MainMenu") {
                StartCoroutine(Watch());
            }
        }

        private IEnumerator Watch() {
            var wait = new WaitForSeconds(0.4f);
            for (int i = 0; i < 300; i++) {
                if (!GameObject.Find("WorldButton") && TryInject()) {
                    Debug.Log("[World] menu button injected");
                }
                yield return wait;
            }
        }

        private bool TryInject() {
            Button play = null;
            foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                var label = b.GetComponentInChildren<TMP_Text>(true);
                if (!label) {
                    continue;
                }
                string t = label.text.ToLowerInvariant();
                if (t.Contains("play") || t.Contains("ui.mainmenu.play")) {
                    play = b;
                    break;
                }
            }
            if (!play || !play.transform.parent) {
                return false;
            }

            var clone = Instantiate(play.gameObject, play.transform.parent);
            clone.name = "WorldButton";
            clone.transform.SetSiblingIndex(play.transform.GetSiblingIndex());

            // Strip upstream logic from the clone, keep every visual part.
            foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true)) {
                bool keep = mb is Button || mb is Image || mb is TMP_Text
                    || mb is LayoutElement || mb is LayoutGroup || mb is ContentSizeFitter
                    || mb is Mask || mb is RectMask2D || mb is Shadow || mb is Outline;
                if (!keep) {
                    Destroy(mb);
                }
            }

            foreach (var text in clone.GetComponentsInChildren<TMP_Text>(true)) {
                text.text = "World";
            }
            var image = clone.GetComponent<Image>();
            if (image) {
                image.color = new Color(0.55f, 0.18f, 0.85f);
            }

            // Without a layout group the clone lands exactly on the original —
            // lift it one slot so World reads as the headline mode.
            if (!play.transform.parent.GetComponent<LayoutGroup>()) {
                var prt = play.GetComponent<RectTransform>();
                var crt = clone.GetComponent<RectTransform>();
                crt.anchoredPosition = prt.anchoredPosition + new Vector2(0f, prt.rect.height + 14f);
            }

            var button = clone.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => SceneManager.LoadScene("WorldHub"));
            clone.SetActive(true);
            return true;
        }
    }
}
