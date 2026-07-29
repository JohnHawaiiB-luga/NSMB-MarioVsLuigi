using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NSMB.World {
    // Turns the game's own Play Game button into a fork: World Hub or Versus.
    // Both choices are buttons cloned from theirs, so the chooser is their UI
    // in their style; Versus fires the original action untouched. Also swaps
    // the menu's music for the World theme.
    public class WorldMenuInjector : MonoBehaviour {

        private Button.ButtonClickedEvent versusAction;
        private readonly List<GameObject> siblings = new();
        private readonly List<GameObject> chooser = new();
        private AudioSource music;

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
            siblings.Clear();
            chooser.Clear();
            versusAction = null;
            if (scene.name == "MainMenu") {
                StartCoroutine(Watch());
            }
        }

        private IEnumerator Watch() {
            var wait = new WaitForSeconds(0.4f);
            for (int i = 0; i < 300; i++) {
                if (chooser.Count == 0) {
                    TryInject();
                }
                SwapMusic();
                yield return wait;
            }
        }

        // The menu's own looping player keeps its clip; ours plays instead.
        private void SwapMusic() {
            if (music && music.isPlaying) {
                return;
            }
            var clip = Resources.Load<AudioClip>("Music/world");
            if (!clip) {
                return;
            }
            foreach (var player in FindObjectsByType<NSMB.Sound.LoopingMusicPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                if (player.AudioSource) {
                    player.AudioSource.Stop();
                }
                player.enabled = false;
            }
            if (!music) {
                var go = new GameObject("WorldMenuMusic");
                music = go.AddComponent<AudioSource>();
                music.loop = true;
                music.playOnAwake = false;
                music.volume = 0.5f;
                music.spatialBlend = 0f;
            }
            music.clip = clip;
            music.Play();
        }

        private void TryInject() {
            Button play = null;
            foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                var label = b.GetComponentInChildren<TMP_Text>(true);
                if (!label) {
                    continue;
                }
                string t = label.text.ToLowerInvariant();
                if (t.Contains("play") || t.Contains("mainmenu.play")) {
                    play = b;
                    break;
                }
            }
            if (!play || !play.transform.parent) {
                return;
            }

            // Everything currently sharing the button column gets hidden while
            // the chooser is up, and restored when it closes.
            siblings.Clear();
            foreach (Transform child in play.transform.parent) {
                siblings.Add(child.gameObject);
            }

            versusAction = play.onClick;
            play.onClick = new Button.ButtonClickedEvent();
            play.onClick.AddListener(ShowChooser);

            chooser.Add(Clone(play, "World Hub", new Color(0.55f, 0.18f, 0.85f), () => {
                if (music) {
                    music.Stop();
                }
                SceneManager.LoadScene("WorldHub");
            }));
            chooser.Add(Clone(play, "Versus", new Color(0.13f, 0.42f, 0.85f), () => {
                Restore();
                versusAction?.Invoke();
            }));
            chooser.Add(Clone(play, "Back", new Color(0.35f, 0.35f, 0.4f), Restore));
        }

        private GameObject Clone(Button source, string label, Color tint, UnityEngine.Events.UnityAction action) {
            var clone = Instantiate(source.gameObject, source.transform.parent);
            clone.name = "World_" + label.Replace(" ", "");

            foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true)) {
                bool keep = mb is Button || mb is Image || mb is TMP_Text
                    || mb is LayoutElement || mb is LayoutGroup || mb is ContentSizeFitter
                    || mb is Mask || mb is RectMask2D || mb is Shadow || mb is Outline;
                if (!keep) {
                    Destroy(mb);
                }
            }
            foreach (var text in clone.GetComponentsInChildren<TMP_Text>(true)) {
                text.text = label;
            }
            var image = clone.GetComponent<Image>();
            if (image) {
                image.color = tint;
            }

            var button = clone.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);

            clone.SetActive(false);
            return clone;
        }

        private void ShowChooser() {
            foreach (var go in siblings) {
                if (go) {
                    go.SetActive(false);
                }
            }
            for (int i = 0; i < chooser.Count; i++) {
                if (!chooser[i]) {
                    continue;
                }
                chooser[i].SetActive(true);
                chooser[i].transform.SetSiblingIndex(i);
            }
            var first = chooser.Count > 0 ? chooser[0] : null;
            if (first && UnityEngine.EventSystems.EventSystem.current) {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(first);
            }
        }

        private void Restore() {
            foreach (var go in chooser) {
                if (go) {
                    go.SetActive(false);
                }
            }
            foreach (var go in siblings) {
                if (go) {
                    go.SetActive(true);
                }
            }
        }
    }
}
