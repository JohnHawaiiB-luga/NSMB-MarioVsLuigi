using System.Collections;
using NSMB.UI;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.World {
    // Leaving the game is a ritual, not a jump cut: the quit jingle, then the
    // engine's own Bowser-shaped wipe, and only then does the browser go back
    // to the site. Triggered from the game's Quit button or from the page's
    // corner exit link, which calls RequestQuit through SendMessage.
    public class WorldExitFlow : MonoBehaviour {

        public const string ObjectName = "WorldExitBridge";

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void WorldExitSite(string url);
#else
        private static void WorldExitSite(string url) {
            Debug.Log("[World] exit to site: " + (string.IsNullOrEmpty(url) ? "/" : url));
        }
#endif

        public static WorldExitFlow Instance { get; private set; }

        private bool leaving;
        private string destination;
        private AudioSource audioSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init() {
            var host = new GameObject(ObjectName);
            DontDestroyOnLoad(host);
            host.AddComponent<WorldExitFlow>();
        }

        private void Awake() {
            Instance = this;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => {
                if (scene.name == "MainMenu") {
                    StartCoroutine(HookQuitButton());
                }
            };
        }

        // Called by the page (SendMessage) and by the game's own Quit button.
        public void RequestQuit() {
            RequestQuitTo("");
        }

        // The same ritual, ending somewhere specific — a pipe marked PROJECTS
        // leaves the player standing in that part of the site rather than at
        // the front door.
        public void RequestQuitTo(string url) {
            if (!leaving) {
                destination = url;
                StartCoroutine(Leave());
            }
        }

        private IEnumerator Leave() {
            leaving = true;

            var clip = Resources.Load<AudioClip>("Sound/quit");
            if (clip) {
                audioSource.PlayOneShot(clip, 0.85f);
            }

            bool faded = false;
            var fader = GlobalController.Instance ? GlobalController.Instance.fader : null;
            if (fader) {
                // Respawn style: the Bowser silhouette closes over the screen.
                fader.Fade(AnimatedFader.FadeStyle.Respawn, AnimatedFader.FadeStyle.Cut, () => faded = true);
            } else {
                faded = true;
            }

            // Leave when both the wipe and the jingle are done — whichever
            // takes longer — with a hard ceiling so the exit never hangs.
            float started = Time.unscaledTime;
            float jingle = clip ? Mathf.Min(clip.length, 2.2f) : 0.5f;
            while (Time.unscaledTime - started < 3f) {
                bool jingleDone = Time.unscaledTime - started >= jingle;
                if (faded && jingleDone) {
                    break;
                }
                yield return null;
            }

            WorldExitSite(destination ?? "");
        }

        // Their Quit button means "leave the game" — on the web that means the
        // site, not Application.Quit, which does nothing in a browser.
        private IEnumerator HookQuitButton() {
            var wait = new WaitForSeconds(0.4f);
            for (int i = 0; i < 200; i++) {
                foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                    if (t.name != "BtnQuit") {
                        continue;
                    }
                    var button = t.GetComponent<Button>();
                    if (button && button.onClick.GetPersistentEventCount() >= 0 && !t.GetComponent<QuitMarker>()) {
                        t.gameObject.AddComponent<QuitMarker>();
                        button.onClick = new Button.ButtonClickedEvent();
                        button.onClick.AddListener(RequestQuit);
                        yield break;
                    }
                }
                yield return wait;
            }
        }

        private class QuitMarker : MonoBehaviour { }
    }
}
