using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NSMB.World {
    // One shared bottom-bar dialogue box. Lines queue up; E advances, or they
    // advance themselves after a few seconds so nobody gets stuck.
    public class WorldDialogue : MonoBehaviour {

        public static WorldDialogue Instance { get; private set; }
        public static bool IsOpen => Instance && Instance.panel.activeSelf;

        public GameObject panel;
        public TMP_Text speakerText;
        public TMP_Text lineText;
        public float autoAdvanceSeconds = 5f;

        private readonly System.Collections.Generic.Queue<(string speaker, string line)> queue = new();
        private float shownAt;

        private void Awake() {
            Instance = this;
            if (panel) {
                panel.SetActive(false);
            }
        }

        public static void Say(string speaker, string[] lines) {
            if (!Instance) {
                return;
            }
            foreach (string line in lines) {
                Instance.queue.Enqueue((speaker, line));
            }
            if (!Instance.panel.activeSelf) {
                Instance.Next();
            }
        }

        private void Next() {
            if (queue.Count == 0) {
                panel.SetActive(false);
                return;
            }
            (string speaker, string line) = queue.Dequeue();
            panel.SetActive(true);
            speakerText.text = speaker;
            lineText.text = line;
            shownAt = Time.time;
        }

        private void Update() {
            if (!panel.activeSelf) {
                return;
            }
            bool advance = Time.time - shownAt > autoAdvanceSeconds;
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) {
                advance = true;
            }
            if (advance) {
                Next();
            }
        }
    }
}
