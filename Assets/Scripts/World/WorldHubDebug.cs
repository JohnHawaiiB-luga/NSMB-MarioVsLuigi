using NSMB.Cameras;
using NSMB.Entities.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NSMB.World {
    // The hub's debug panel, in the spirit of the Dragon Engine e_dip_* tables:
    // numbered rows in categories, read-outs on the left, switches on the right.
    //
    // A reduced port of the standalone hub's inspector. The rows that tuned
    // movement are gone on purpose: in here the player is a Quantum entity and
    // those numbers live in the simulation's own assets, where a view-side
    // script has no business writing them mid-match.
    public class WorldHubDebug : MonoBehaviour {

        private const string HostName = "WorldHubDebug";

        private static bool shown;
        private static float fps;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init() {
            if (GameObject.Find(HostName)) {
                return;
            }
            var host = new GameObject(HostName);
            DontDestroyOnLoad(host);
            host.AddComponent<WorldHubDebug>();
        }

        private void Update() {
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.1f);

            // The host outlives scene changes, so without this the panel — and
            // its keys — follow you back out to the main menu.
            if (!WorldLocalGame.Running) {
                shown = false;
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) {
                return;
            }
            if (keyboard.tabKey.wasPressedThisFrame) {
                shown = !shown;
            }
            if (keyboard.cKey.wasPressedThisFrame) {
                ToggleCamera();
            }
        }

        // Their own camera already knows how to be a free camera, and how to
        // tween back to the player afterwards — so the switch between free
        // looking and the side-on view is theirs, not a second camera of mine.
        public static void ToggleCamera() {
            var camera = FindFirstObjectByType<CameraAnimator>();
            if (!camera) {
                return;
            }
            camera.Mode = camera.Mode == CameraAnimator.CameraMode.FollowPlayer
                ? CameraAnimator.CameraMode.Freecam
                : CameraAnimator.CameraMode.FollowPlayer;
        }

        private void OnGUI() {
            // Nothing at all until it is asked for. An always-on hint sat over
            // the game's own HUD, and an OnGUI rect eats touches wherever it
            // is drawn — a debug panel has no business taking the controls.
            if (!shown || !WorldLocalGame.Running) {
                return;
            }

            // OnGUI works in raw pixels, so on a 4K screen the panel comes out
            // a quarter of the size it was drawn at and unreadable.
            Matrix4x4 restore = GUI.matrix;
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);

            var camera = FindFirstObjectByType<CameraAnimator>();
            Transform mario = null;
            foreach (var m in MarioPlayerAnimator.AllMarioPlayers) {
                if (m) {
                    mario = m.transform;
                    break;
                }
            }

            // Right-hand side, clear of their coin and star counters and of the
            // page's thumb controls along the bottom. In scaled space, so the
            // screen width has to come back through the same scale.
            float x = (Screen.width / scale) - 338;
            GUI.Box(new Rect(x, 8, 330, 190), "JOHN HAWAII B. LUGA'S WORLD — DEBUG");
            int row = 0;
            void Line(string label, string value) {
                GUI.Label(new Rect(x + 10, 32 + row * 18, 310, 18), $"{row + 1:00}  {label,-14} {value}");
                row++;
            }

            Line("fps", fps.ToString("0"));
            Line("time scale", Time.timeScale.ToString("0.00"));
            Line("camera", camera ? camera.Mode.ToString() : "none");
            Line("position", mario ? mario.position.ToString("0.0") : "no player");
            Line("players", MarioPlayerAnimator.AllMarioPlayers.Count.ToString());
            Line("dialogue", WorldDialogue.IsOpen ? "open" : "idle");
            Line("session", WorldLocalGame.Running ? "world hub" : "versus");

            if (GUI.Button(new Rect(x + 10, 32 + row * 18 + 6, 150, 22), "camera (C)")) {
                ToggleCamera();
            }
            if (GUI.Button(new Rect(x + 168, 32 + row * 18 + 6, 150, 22),
                Time.timeScale > 0.9f ? "slow motion" : "normal speed")) {
                Time.timeScale = Time.timeScale > 0.9f ? 0.35f : 1f;
            }

            GUI.matrix = restore;
        }
    }
}
