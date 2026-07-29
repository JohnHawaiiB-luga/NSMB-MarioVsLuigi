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
        // Tracked from the load event: their menu can arrive additively, so the
        // active scene is not a reliable way to ask "are we in the menu".
        private string currentScene = "";
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
            // One watcher for the lifetime of the game. Scene names proved
            // useless here: their menu loads a stage in the background as its
            // own scene, so "which scene are we in" keeps changing while the
            // menu is very much still on screen. Presence of BtnPlay is the
            // only honest signal.
            StartCoroutine(Watch());
        }

        private void Forget() {
            siblings.Clear();
            chooser.Clear();
            newsBoard = null;
            versusAction = null;
            anchor = null;
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
            // The theme starts immediately, so the game's own track never gets
            // a moment on the title screen before ours takes over.
            SwapMusic();
            while (true) {
                bool inMenu = FindPlayButton();
                if (inMenu) {
                    if (chooser.Count == 0) {
                        TryInject();
                    }
                    SwapMusic();
                } else if (chooser.Count > 0) {
                    // The menu is gone: drop the theme and forget the buttons,
                    // so the next visit rebuilds cleanly.
                    StopMusic();
                    Forget();
                }
                yield return wait;
            }
        }

        private Button FindPlayButton() {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                if (t.name == "BtnPlay") {
                    return t.GetComponent<Button>();
                }
            }
            return null;
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
            Button play = FindPlayButton();
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
                // Hand the original event back to the button and fire it there:
                // invoking a detached UnityEvent did nothing, so Versus was dead.
                StartCoroutine(FireVersus(play));
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
            if (!clone.GetComponent<WorldMenuButtonSfx>()) {
                clone.AddComponent<WorldMenuButtonSfx>();
            }

            clone.SetActive(false);
            return clone;
        }

        // The board stays put; its posts step aside for a note explaining the
        // two modes, and come back when the fork closes.
        private void ShowModeBlurb(bool show) {
            if (!newsBoard) {
                return;
            }
            foreach (Transform child in newsBoard.transform) {
                if (child.name != "WorldModeBlurb") {
                    child.gameObject.SetActive(!show);
                }
            }

            var existing = newsBoard.transform.Find("WorldModeBlurb");
            if (!show) {
                if (existing) {
                    existing.gameObject.SetActive(false);
                }
                return;
            }

            if (!existing) {
                var go = new GameObject("WorldModeBlurb");
                go.transform.SetParent(newsBoard.transform, false);
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 25f;
                tmp.alignment = TextAlignmentOptions.TopLeft;
                tmp.color = Color.white;
                tmp.text =
                    "<size=130%>WORLD HUB</size>\n" +
                    "The walkable half of erikgaren.com — my own stage running this game's " +
                    "engine. No timer, no lives, nothing to chase: wander, and take a pipe " +
                    "to the site or to HawaiiOS.\n\n" +
                    "<size=130%>VERSUS</size>\n" +
                    "The classic mode, untouched — real online multiplayer on my own server. " +
                    "Create a room or join one by ID, two to ten players. Bring a friend.\n\n" +
                    "<size=130%>BACK</size>\n" +
                    "Return to the news board.";
                var rt = tmp.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(36f, 36f);
                rt.offsetMax = new Vector2(-36f, -80f);
                existing = go.transform;
            }
            existing.gameObject.SetActive(true);
        }

        private void ShowChooser() {
            foreach (var go in siblings) {
                if (go) {
                    go.SetActive(false);
                }
            }
            ShowModeBlurb(true);
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

        private IEnumerator FireVersus(Button play) {
            if (!play || versusAction == null) {
                yield break;
            }
            var mine = play.onClick;
            play.onClick = versusAction;
            play.onClick.Invoke();
            yield return null;
            if (play) {
                play.onClick = mine;
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
            ShowModeBlurb(false);
        }
    }
}
