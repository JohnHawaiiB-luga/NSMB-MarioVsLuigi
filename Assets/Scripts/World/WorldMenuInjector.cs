using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NSMB.World {
    // Turns the game's own Play Game button into a fork: World Hub or Versus.
    // Both choices are clones of BtnPlay, so the chooser is their pipe-button UI
    // in their style; Versus fires the original action untouched. Also swaps the
    // menu music for the World theme.
    public class WorldMenuInjector : MonoBehaviour {

        private Button.ButtonClickedEvent versusAction;
        private RectTransform anchor;
        private GameObject newsBoard;
        private Coroutine watcher;
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
            siblings.Clear();
            chooser.Clear();
            newsBoard = null;
            versusAction = null;
            anchor = null;
            if (watcher != null) {
                StopCoroutine(watcher);
                watcher = null;
            }
            if (scene.name == "MainMenu") {
                watcher = StartCoroutine(Watch());
            } else {
                // The menu theme must not follow the player into a match or the
                // hub — both bring their own. Only the watcher is stopped here:
                // stopping every coroutine also killed the loading-screen
                // transition mid-flight, which left the loader on screen forever.
                StopMusic();
            }
        }

        private void StopMusic() {
            if (music) {
                music.Stop();
                Destroy(music.gameObject);
                music = null;
            }
        }

        private IEnumerator Watch() {
            var wait = new WaitForSeconds(0.4f);
            while (SceneManager.GetActiveScene().name == "MainMenu") {
                if (chooser.Count == 0) {
                    TryInject();
                }
                SwapMusic();
                yield return wait;
            }
            StopMusic();
        }

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

        // The menu's buttons are PipeButton prefab instances named BtnPlay,
        // BtnOptions, BtnReplays, BtnAddons, BtnAbout, BtnQuit.
        private void TryInject() {
            Button play = null;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                if (t.name != "BtnPlay") {
                    continue;
                }
                play = t.GetComponent<Button>();
                if (play) {
                    break;
                }
            }
            if (!play || !play.transform.parent) {
                return;
            }

            anchor = play.GetComponent<RectTransform>();
            siblings.Clear();
            foreach (Transform child in play.transform.parent) {
                if (child.name.StartsWith("Btn")) {
                    siblings.Add(child.gameObject);
                }
            }
            // The board belongs to the front page of the menu, so it steps
            // aside with the buttons — the way their own submenus replace it.
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                if (t.name == "NewsBoard") {
                    newsBoard = t.gameObject;
                    break;
                }
            }

            versusAction = play.onClick;
            play.onClick = new Button.ButtonClickedEvent();
            play.onClick.AddListener(ShowChooser);

            chooser.Add(Clone(play, "World Hub", new Color(0.55f, 0.18f, 0.85f), () => {
                StopMusic();
                // Their own simulation, single player, nothing to chase.
                WorldLocalGame.Launch();
            }));
            chooser.Add(Clone(play, "Versus", new Color(0.13f, 0.42f, 0.85f), () => {
                Restore();
                StopMusic();
                versusAction?.Invoke();
            }));
            chooser.Add(Clone(play, "Back", new Color(0.35f, 0.35f, 0.4f), Restore));
            Debug.Log("[World] menu fork installed on BtnPlay");
        }

        private GameObject Clone(Button source, string label, Color tint, UnityEngine.Events.UnityAction action) {
            var clone = Instantiate(source.gameObject, source.transform.parent);
            clone.name = "World_" + label.Replace(" ", "");

            // Keep their components — that is what gives the clone its cursor
            // and confirm sounds. Only the translation driver goes, since it
            // would overwrite the label on the next language event.
            foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true)) {
                if (mb && mb.GetType().Name.Contains("Translat")) {
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
            if (newsBoard) {
                newsBoard.SetActive(false);
            }
            bool laidOut = anchor && anchor.parent && anchor.parent.GetComponent<LayoutGroup>();
            float step = anchor ? anchor.rect.height + 14f : 90f;
            for (int i = 0; i < chooser.Count; i++) {
                if (!chooser[i]) {
                    continue;
                }
                chooser[i].SetActive(true);
                if (!laidOut && anchor) {
                    var rt = chooser[i].GetComponent<RectTransform>();
                    rt.anchoredPosition = anchor.anchoredPosition + new Vector2(0f, -step * i);
                }
            }
            if (chooser.Count > 0 && chooser[0] && UnityEngine.EventSystems.EventSystem.current) {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(chooser[0]);
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
            if (newsBoard) {
                newsBoard.SetActive(true);
            }
        }
    }
}
