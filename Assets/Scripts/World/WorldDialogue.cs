using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.World {
    // Speech in the game's own dressing: its 9-sliced dialogue panel floating
    // over the speaker's head, its font, its typing sounds, and its button
    // prompt blinking when a line is ready to advance.
    public class WorldDialogue : MonoBehaviour {

        public static WorldDialogue Instance { get; private set; }
        public static bool IsOpen => Instance && Instance.bubble && Instance.bubble.activeSelf;

        public GameObject bubble;
        public TMP_Text nameText;
        public TMP_Text lineText;
        public Image prompt;
        public Image portrait;
        public Sprite marioFace, luigiFace;
        public AudioSource voice;
        public AudioClip typeClip, openClip, doneClip;
        public float charsPerSecond = 42f;
        public float autoAdvanceSeconds = 6f;

        private readonly System.Collections.Generic.Queue<(string speaker, string line)> queue = new();
        private WorldSpeaker[] speakers;
        private WorldSpeaker current;
        private Controls controls;
        private string full = "";
        private float shownAt;
        private int revealed;
        private float typeCooldown;

        private void Awake() {
            Instance = this;
            speakers = FindObjectsByType<WorldSpeaker>(FindObjectsSortMode.None);
            controls = new Controls();
            controls.Player.Enable();
            if (bubble) {
                bubble.SetActive(false);
            }
        }

        private void OnDestroy() {
            controls?.Dispose();
        }

        public static void Say(string defaultSpeaker, string[] lines) {
            if (!Instance) {
                return;
            }
            foreach (string raw in lines) {
                int split = raw.IndexOf('|');
                if (split > 0 && split < 40) {
                    Instance.queue.Enqueue((raw[..split], raw[(split + 1)..]));
                } else {
                    Instance.queue.Enqueue((defaultSpeaker, raw));
                }
            }
            if (!IsOpen) {
                Instance.Next();
            }
        }

        private void Next() {
            if (queue.Count == 0) {
                bubble.SetActive(false);
                current = null;
                return;
            }
            bool wasOpen = bubble.activeSelf;
            (string speaker, string line) = queue.Dequeue();
            bubble.SetActive(true);
            nameText.text = speaker;
            nameText.color = speaker.Contains("MARIO") ? new Color(1f, 0.5f, 0.38f)
                : speaker.Contains("LUIGI") ? new Color(0.5f, 0.95f, 0.55f)
                : Color.white;

            // Their own character art, so you can see who is talking at a glance.
            if (portrait) {
                Sprite face = speaker.Contains("LUIGI") ? luigiFace
                    : speaker.Contains("MARIO") ? marioFace
                    : null;
                portrait.sprite = face;
                portrait.enabled = face;
            }
            full = line;
            revealed = 0;
            lineText.text = "";
            shownAt = Time.time;
            current = Find(speaker);
            if (!wasOpen && voice && openClip) {
                voice.PlayOneShot(openClip, 0.6f);
            }
            Place();
        }

        private WorldSpeaker Find(string speaker) {
            WorldSpeaker match = Match(speaker);
            if (match) {
                return match;
            }
            // Mario's speaker is attached once the companion finds him, which is
            // after this list was taken — so a miss means look again, not that
            // nobody is there.
            speakers = FindObjectsByType<WorldSpeaker>(FindObjectsSortMode.None);
            return Match(speaker);
        }

        private WorldSpeaker Match(string speaker) {
            foreach (var s in speakers) {
                if (s && !string.IsNullOrEmpty(s.keyword)
                    && speaker.ToUpperInvariant().Contains(s.keyword.ToUpperInvariant())) {
                    return s;
                }
            }
            return null;
        }

        private void Place() {
            if (current) {
                bubble.transform.position = current.transform.position + Vector3.up * current.bubbleHeight;
            }
        }

        private void Update() {
            if (!IsOpen) {
                return;
            }
            Place();
            Camera cam = Camera.main;
            if (cam) {
                bubble.transform.rotation = Quaternion.LookRotation(bubble.transform.position - cam.transform.position, Vector3.up);
            }

            bool typing = revealed < full.Length;
            if (typing) {
                typeCooldown -= Time.deltaTime;
                int want = Mathf.Min(full.Length, Mathf.CeilToInt((Time.time - shownAt) * charsPerSecond));
                if (want > revealed) {
                    // One blip every few characters, like their chat does.
                    if (typeCooldown <= 0f && voice && typeClip) {
                        voice.PlayOneShot(typeClip, 0.25f);
                        typeCooldown = 0.055f;
                    }
                    revealed = want;
                    lineText.text = full[..revealed];
                }
            }

            if (prompt) {
                prompt.enabled = !typing;
                if (!typing) {
                    var c = prompt.color;
                    c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 3.4f));
                    prompt.color = c;
                }
            }

            bool pressed = controls != null && controls.Player.PowerupAction.WasPressedThisFrame();
            if (typing) {
                // First press completes the line, as their menus do.
                if (pressed) {
                    revealed = full.Length;
                    lineText.text = full;
                    if (voice && doneClip) {
                        voice.PlayOneShot(doneClip, 0.5f);
                    }
                }
                return;
            }

            if (pressed || Time.time - shownAt > autoAdvanceSeconds + full.Length / charsPerSecond) {
                Next();
            }
        }
    }
}
