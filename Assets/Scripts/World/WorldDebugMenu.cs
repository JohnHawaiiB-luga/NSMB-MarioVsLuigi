using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NSMB.World {
    // A dev room in menu form. Opened with the game's Scoreboard button (Tab),
    // which has nothing to show in free roam. Navigated with the movement keys
    // and confirmed with the powerup button, so it works on a pad too.
    public class WorldDebugMenu : MonoBehaviour {

        public GameObject panel;
        public TMP_Text body;
        public WorldPlayerController player;
        public WorldMotor motor;
        public WorldCamera rig;
        public GameObject graphyPrefab;
        public AudioSource ui;
        public AudioClip moveClip, selectClip;

        private Controls controls;
        private GameObject graphy;
        private int index;
        private float repeat;
        private bool open;

        private readonly (string label, string hint)[] items = {
            ("Stats overlay", "FPS, memory and audio graphs"),
            ("Fly mode", "gravity off, free vertical movement"),
            ("Speed", "walk and run multiplier"),
            ("Slow motion", "time scale"),
            ("Teleport: spawn", "back to the start"),
            ("Teleport: terrace", "the QA-tool balcony"),
            ("Teleport: the pit", "where the reverse-engineering lives"),
            ("Teleport: gallery", "the Versus stage walls"),
            ("Teleport: versus pipe", "the pad with the pipe out"),
            ("Controls card", "show or hide the corner help"),
        };

        public static bool FlyMode { get; private set; }
        public static float SpeedScale { get; private set; } = 1f;
        public GameObject controlsCard;

        private void Awake() {
            controls = new Controls();
            controls.Player.Enable();
            controls.UI.Enable();
            if (panel) {
                panel.SetActive(false);
            }
        }

        private void OnDestroy() {
            controls?.Dispose();
            FlyMode = false;
            SpeedScale = 1f;
            Time.timeScale = 1f;
        }

        private void Update() {
            if (controls == null) {
                return;
            }
            if (controls.UI.Scoreboard.WasPressedThisFrame()) {
                open = !open;
                if (panel) {
                    panel.SetActive(open);
                }
                Click(selectClip);
                Render();
            }
            if (!open) {
                return;
            }

            Vector2 nav = controls.Player.Movement.ReadValue<Vector2>();
            repeat -= Time.unscaledDeltaTime;
            if (Mathf.Abs(nav.y) > 0.5f && repeat <= 0f) {
                index = (index - (int) Mathf.Sign(nav.y) + items.Length) % items.Length;
                repeat = 0.18f;
                Click(moveClip);
                Render();
            } else if (Mathf.Abs(nav.y) <= 0.5f) {
                repeat = 0f;
            }

            if (controls.Player.PowerupAction.WasPressedThisFrame() || controls.UI.Submit.WasPressedThisFrame()) {
                Activate(index);
                Click(selectClip);
                Render();
            }
        }

        private void Activate(int i) {
            switch (i) {
            case 0:
                if (!graphy && graphyPrefab) {
                    graphy = Instantiate(graphyPrefab);
                } else if (graphy) {
                    graphy.SetActive(!graphy.activeSelf);
                }
                break;
            case 1:
                FlyMode = !FlyMode;
                break;
            case 2:
                SpeedScale = SpeedScale >= 3f ? 1f : SpeedScale + 1f;
                break;
            case 3:
                Time.timeScale = Time.timeScale > 0.9f ? 0.35f : 1f;
                break;
            case 4: Teleport(new Vector3(0f, 0.6f, -12f)); break;
            case 5: Teleport(new Vector3(-13f, 3.2f, -1f)); break;
            case 6: Teleport(new Vector3(0f, -3.4f, 0f)); break;
            case 7: Teleport(new Vector3(0f, 0.6f, 28f)); break;
            case 8: Teleport(new Vector3(0f, 3.2f, 22f)); break;
            case 9:
                if (controlsCard) {
                    controlsCard.SetActive(!controlsCard.activeSelf);
                }
                break;
            }
        }

        private void Teleport(Vector3 where) {
            if (player) {
                player.transform.position = where;
            }
        }

        private void Render() {
            if (!body) {
                return;
            }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<size=110%>DEV ROOM</size>   <size=75%>Tab closes · move to pick · E confirms</size>\n");
            for (int i = 0; i < items.Length; i++) {
                string state = i switch {
                    0 => graphy && graphy.activeSelf ? "on" : "off",
                    1 => FlyMode ? "on" : "off",
                    2 => "x" + SpeedScale.ToString("0"),
                    3 => Time.timeScale < 0.9f ? "on" : "off",
                    9 => controlsCard && controlsCard.activeSelf ? "shown" : "hidden",
                    _ => "go",
                };
                sb.Append(i == index ? "<color=#ffd34d>> " : "  ");
                sb.Append(items[i].label);
                sb.Append("   <size=80%>[").Append(state).Append("]</size>");
                if (i == index) {
                    sb.Append("   <size=75%><alpha=#99>").Append(items[i].hint).Append("</size>");
                    sb.Append("</color>");
                }
                sb.AppendLine();
            }
            body.text = sb.ToString();
        }

        private void Click(AudioClip clip) {
            if (ui && clip) {
                ui.PlayOneShot(clip, 0.6f);
            }
        }
    }
}
