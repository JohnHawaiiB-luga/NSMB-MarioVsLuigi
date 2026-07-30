using NSMB.Cameras;
using NSMB.Entities.Player;
using Photon.Deterministic;
using Quantum;
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

        // 1146 tiles is 573 world units across twelve stages — near enough one
        // stage per press.
        private const float StageStride = 48f;

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
            // Their own bindings take C (powerup), V (reserve item), Tab
            // (scoreboard), Shift/X (sprint), Z/Space (jump) and Escape (pause).
            // Backquote is the console key by convention and F is free.
            // Backquote sits somewhere else on a German layout, so P opens the
            // panel too. Logged because "the key did nothing" and "the key was
            // never seen" look identical from the outside.
            if (keyboard.backquoteKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame) {
                shown = !shown;
                Debug.Log($"[World] debug panel {(shown ? "open" : "closed")}");
            }
            if (keyboard.fKey.wasPressedThisFrame) {
                ToggleCamera();
            }
            // The strip is 573 units end to end; walking it is a hike, so the
            // brackets step a stage at a time.
            if (keyboard.rightBracketKey.wasPressedThisFrame) {
                Warp(StageStride);
            }
            if (keyboard.leftBracketKey.wasPressedThisFrame) {
                Warp(-StageStride);
            }
            if (keyboard.nKey.wasPressedThisFrame) {
                Send(CommandMvLDebugCmd.DebugCommand.ToggleNoclip, FPVector2.Zero);
            }
        }

        // The hub's runner is started by WorldLocalGame with its own id, so
        // QuantumRunner.DefaultGame is not necessarily it — and a null-conditional
        // on that static swallowed every command silently. NetworkHandler.Runner
        // is the reference the hub actually started.
        private static QuantumGame Game =>
            NSMB.Networking.NetworkHandler.Runner != null
                ? NSMB.Networking.NetworkHandler.Runner.Game
                : QuantumRunner.DefaultGame;

        private static void Send(CommandMvLDebugCmd.DebugCommand id, FPVector2 position) {
            QuantumGame game = Game;
            if (game == null) {
                Debug.LogWarning($"[World] {id}: no running game to send to");
                return;
            }
            game.SendCommand(new CommandMvLDebugCmd {
                CommandId = id,
                Position = position,
            });
            Debug.Log($"[World] sent {id}");
        }

        // Through the simulation, as a command: a transform moved from the view
        // side is fought by physics and gone by the next tick.
        private static void Warp(float dx) {
            Transform mario = LocalMario();
            if (!mario) {
                return;
            }

            // A little height so you drop onto whatever is there rather than
            // waking up inside it.
            Send(CommandMvLDebugCmd.DebugCommand.Warp, new FPVector2(
                FP.FromFloat_UNSAFE(mario.position.x + dx),
                FP.FromFloat_UNSAFE(mario.position.y + 3f)));
        }

        // Reads the state straight off the simulation rather than keeping a copy
        // here, so the panel cannot disagree with what is actually true.
        private static unsafe bool Noclipping() {
            QuantumGame game = Game;
            Frame frame = game?.Frames.Predicted;
            if (frame == null) {
                return false;
            }
            foreach ((var entity, var mario) in frame.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                if (frame.Unsafe.TryGetPointer(entity, out PhysicsObject* physics)) {
                    return physics->IsFrozen && physics->DisableCollision;
                }
            }
            return false;
        }

        private static Transform LocalMario() {
            foreach (var m in MarioPlayerAnimator.AllMarioPlayers) {
                if (m) {
                    return m.transform;
                }
            }
            return null;
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
            GUI.Box(new Rect(x, 8, 330, 250), "JOHN HAWAII B. LUGA'S WORLD — DEBUG");
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
            Line("noclip", Noclipping() ? "ON — flying" : "off");

            if (GUI.Button(new Rect(x + 10, 32 + row * 18 + 6, 150, 22), "camera (F)")) {
                ToggleCamera();
            }
            if (GUI.Button(new Rect(x + 168, 32 + row * 18 + 6, 150, 22),
                Time.timeScale > 0.9f ? "slow motion" : "normal speed")) {
                Time.timeScale = Time.timeScale > 0.9f ? 0.35f : 1f;
            }
            if (GUI.Button(new Rect(x + 10, 32 + row * 18 + 32, 150, 22), "warp back  [")) {
                Warp(-StageStride);
            }
            if (GUI.Button(new Rect(x + 168, 32 + row * 18 + 32, 150, 22), "warp on  ]")) {
                Warp(StageStride);
            }
            if (GUI.Button(new Rect(x + 10, 32 + row * 18 + 58, 308, 22), "noclip / fly  (N)")) {
                Send(CommandMvLDebugCmd.DebugCommand.ToggleNoclip, FPVector2.Zero);
            }

            GUI.matrix = restore;
        }
    }
}
