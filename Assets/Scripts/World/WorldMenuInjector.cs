using NSMB.UI.MainMenu;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.World {
    // Adds World Hub to the main menu as one more entry in their own button
    // list, and swaps the menu theme for ours.
    //
    // This used to hide their whole menu behind a chooser of cloned buttons,
    // which broke everything the menu gives you for free: the arrow keys had
    // nowhere to go (their buttons navigate by explicit up/down links, and
    // clones inherited links to hidden objects), cancelling left the menu
    // half-dismantled behind the title screen, and the news board vanished with
    // the buttons it sits beside. Being a button among their buttons costs none
    // of that: Play Game stays exactly as it was, so Versus, Back and cancel
    // are all still theirs.
    public class WorldMenuInjector : MonoBehaviour {

        private const string ButtonName = "BtnWorldHub";

        private Button worldButton;
        private GameObject blurb;
        private GameObject newsBoard;
        private AudioSource music;
        private readonly List<NSMB.Sound.LoopingMusicPlayer> silenced = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init() {
            var host = new GameObject("WorldMenuInjector");
            DontDestroyOnLoad(host);
            host.AddComponent<WorldMenuInjector>();
        }

        private void Awake() {
            StartCoroutine(Watch());
        }

        // Presence of the Play button is the only honest "are we in the menu"
        // signal: their menu loads a stage in the background as its own scene,
        // so scene identity keeps changing while the menu is still on screen.
        private IEnumerator Watch() {
            var wait = new WaitForSeconds(0.25f);
            while (true) {
                Button play = FindPlayButton();
                if (play) {
                    if (!worldButton) {
                        Inject(play);
                    }
                    SilenceTheirs();
                    PlayWorldTheme();
                    UpdateBlurb();
                } else if (worldButton) {
                    StopMusic();
                    worldButton = null;
                    blurb = null;
                    newsBoard = null;
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

        private void Inject(Button play) {
            if (!play.transform.parent) {
                return;
            }

            // The menu comes back with its own objects when it reopens, so a
            // button built earlier is still there to be reused.
            var existing = play.transform.parent.Find(ButtonName);
            if (existing) {
                worldButton = existing.GetComponent<Button>();
                FindBoard();
                return;
            }

            var clone = Instantiate(play.gameObject, play.transform.parent);
            clone.name = ButtonName;
            clone.transform.SetSiblingIndex(play.transform.GetSiblingIndex());

            // The translation driver would overwrite our label on the next
            // language event; everything else of theirs is what makes the
            // button look and sound like one of the menu's own.
            foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true)) {
                if (mb && mb.GetType().Name.Contains("Translat")) {
                    Destroy(mb);
                }
            }
            foreach (var text in clone.GetComponentsInChildren<TMP_Text>(true)) {
                text.text = "World Hub";
            }

            worldButton = clone.GetComponent<Button>();
            worldButton.onClick = new Button.ButtonClickedEvent();
            worldButton.onClick.AddListener(() => {
                StopMusic();
                WorldLocalGame.Launch();
            });

            Link(play);
            FindBoard();
            Debug.Log("[World] World Hub added to the menu above BtnPlay");
        }

        // Their buttons navigate by explicit up/down links rather than by
        // position, so a new one has to be spliced into the chain by hand or
        // the arrow keys walk straight past it.
        private void Link(Button play) {
            Selectable above = play.navigation.selectOnUp;

            var mine = worldButton.navigation;
            mine.mode = Navigation.Mode.Explicit;
            mine.selectOnUp = above;
            mine.selectOnDown = play;
            worldButton.navigation = mine;

            var theirs = play.navigation;
            theirs.selectOnUp = worldButton;
            play.navigation = theirs;

            if (above is Button aboveButton) {
                var chain = aboveButton.navigation;
                chain.selectOnDown = worldButton;
                aboveButton.navigation = chain;
            }
        }

        private void FindBoard() {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                if (t.name == "NewsBoard") {
                    newsBoard = t.gameObject;
                    return;
                }
            }
        }

        // The board keeps its place; its posts step aside for a word about the
        // hub while the button is highlighted, and come back afterwards.
        private void UpdateBlurb() {
            if (!newsBoard || !worldButton) {
                return;
            }

            bool show = UnityEngine.EventSystems.EventSystem.current
                && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == worldButton.gameObject;

            if (show && !blurb) {
                var go = new GameObject("WorldModeBlurb");
                go.transform.SetParent(newsBoard.transform, false);
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 25f;
                tmp.alignment = TextAlignmentOptions.TopLeft;
                tmp.color = Color.white;
                tmp.text =
                    "<size=130%>WORLD HUB</size>\n" +
                    "The walkable half of erikgaren.com — my own stage running this game's " +
                    "engine. No timer, no lives, nothing to chase: wander, read the signs, " +
                    "and take a pipe back to the site or to HawaiiOS.\n\n" +
                    "<size=130%>PLAY GAME</size>\n" +
                    "The classic mode, untouched — real online multiplayer on my own server. " +
                    "Create a room or join one by ID, two to ten players. Bring a friend.";
                var rt = tmp.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(36f, 36f);
                rt.offsetMax = new Vector2(-36f, -80f);
                blurb = go;
            }

            if (blurb) {
                blurb.SetActive(show);
            }
            foreach (Transform child in newsBoard.transform) {
                if (child.gameObject != blurb) {
                    child.gameObject.SetActive(!show);
                }
            }
        }

        // Their player keeps starting the menu theme after ours begins, so this
        // has to run every tick rather than once when the theme is swapped.
        private void SilenceTheirs() {
            foreach (var player in FindObjectsByType<NSMB.Sound.LoopingMusicPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                if (player.AudioSource && player.AudioSource.isPlaying) {
                    player.AudioSource.Stop();
                }
                if (player.enabled) {
                    player.enabled = false;
                }
                if (!silenced.Contains(player)) {
                    silenced.Add(player);
                }
            }
        }

        private void PlayWorldTheme() {
            if (music && music.isPlaying) {
                return;
            }
            var clip = Resources.Load<AudioClip>("Music/world");
            if (!clip) {
                Debug.LogWarning("[World] Resources/Music/world is missing — the menu keeps their theme");
                return;
            }
            if (!music) {
                var go = new GameObject("WorldMenuMusic");
                DontDestroyOnLoad(go);
                music = go.AddComponent<AudioSource>();
                music.loop = true;
                music.playOnAwake = false;
                music.volume = 0.5f;
                music.spatialBlend = 0f;
            }
            music.clip = clip;
            music.Play();
        }

        private void StopMusic() {
            if (music) {
                music.Stop();
                Destroy(music.gameObject);
                music = null;
            }
            // Hand their player back: while it is disabled it cannot honour a
            // track's authored loop points, so gameplay music would restart at
            // the end of the file instead of at its loop.
            foreach (var player in silenced) {
                if (player) {
                    player.enabled = true;
                }
            }
            silenced.Clear();
        }
    }
}
