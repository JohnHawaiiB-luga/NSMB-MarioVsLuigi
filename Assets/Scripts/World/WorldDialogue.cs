using TMPro;
using UnityEngine;

namespace NSMB.World {
    // Speech bubbles over the speakers' heads, name on top, advanced with E or
    // by themselves. Lines carry their speaker as "NAME|text"; the bubble hops
    // to whichever WorldSpeaker matches the name.
    public class WorldDialogue : MonoBehaviour {

        public static WorldDialogue Instance { get; private set; }
        public static bool IsOpen => Instance && Instance.bubble && Instance.bubble.activeSelf;

        public GameObject bubble;
        public TMP_Text nameText;
        public TMP_Text lineText;
        public Transform panel;
        public AudioSource voice;
        public float autoAdvanceSeconds = 5f;

        private readonly System.Collections.Generic.Queue<(string speaker, string line)> queue = new();
        private WorldSpeaker[] speakers;
        private WorldSpeaker current;
        private float shownAt;
        private Controls controls;

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
            (string speaker, string line) = queue.Dequeue();
            bubble.SetActive(true);
            nameText.text = speaker;
            nameText.color = speaker.Contains("DAVID") ? new Color(1f, 0.45f, 0.35f)
                : speaker.Contains("ERIK") ? new Color(0.5f, 0.95f, 0.55f)
                : Color.white;
            lineText.text = line;
            shownAt = Time.time;
            current = Find(speaker);
            if (voice && voice.clip) {
                voice.PlayOneShot(voice.clip);
            }
            Place();
        }

        private WorldSpeaker Find(string speaker) {
            foreach (var s in speakers) {
                if (!string.IsNullOrEmpty(s.keyword) && speaker.ToUpperInvariant().Contains(s.keyword.ToUpperInvariant())) {
                    return s;
                }
            }
            return null;
        }

        private void Place() {
            if (!current) {
                return;
            }
            bubble.transform.position = current.transform.position + Vector3.up * current.bubbleHeight;
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
            bool advance = Time.time - shownAt > autoAdvanceSeconds;
            if (controls != null && controls.Player.PowerupAction.WasPressedThisFrame()) {
                advance = true;
            }
            if (advance) {
                Next();
            }
        }
    }
}
