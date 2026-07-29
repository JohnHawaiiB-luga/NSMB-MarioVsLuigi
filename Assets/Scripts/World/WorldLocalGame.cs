using NSMB.Networking;
using Photon.Deterministic;
using Quantum;
using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace NSMB.World {
    // The hub as the real game: a local Quantum session running their own
    // simulation, their physics, their animators, their HUD and their loading
    // and ready screens — one player, no objective, nothing to chase.
    //
    // Everything here mirrors NetworkHandler.StartQuantum, swapping the
    // multiplayer session for a local one and driving the lobby commands the
    // host would normally send.
    public class WorldLocalGame : MonoBehaviour {

        public const string ObjectName = "WorldLocalGame";
        public static bool Running { get; private set; }

        public static void Launch() {
            var host = GameObject.Find(ObjectName);
            if (!host) {
                host = new GameObject(ObjectName);
                DontDestroyOnLoad(host);
                host.AddComponent<WorldLocalGame>();
            }
            var runner = host.GetComponent<WorldLocalGame>();
            runner.StartCoroutine(runner.Run());
        }

        private IEnumerator Run() {
            if (Running) {
                yield break;
            }
            Running = true;

            Task<bool> start = StartSession();
            while (!start.IsCompleted) {
                yield return null;
            }
            if (!start.Result) {
                Running = false;
                Debug.LogWarning("[World] local session failed to start — falling back to the standalone hub");
                WorldTransition.Run(WorldTransition.ToScene("WorldHub"));
                yield break;
            }

            QuantumGame game = NetworkHandler.Runner.Game;

            // Wait for the player to exist in the simulation before the lobby
            // commands land — the host flag is on player data.
            float deadline = Time.unscaledTime + 8f;
            while (Time.unscaledTime < deadline) {
                var frame = game.Frames.Predicted;
                if (frame != null && game.GetLocalPlayers().Count > 0) {
                    break;
                }
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.4f);

            // Free roam: no timer, no lives, and a star count nobody reaches by
            // accident. The stage stays whatever the room defaults to until the
            // hub's own map is authored.
            game.SendCommand(new CommandChangeRules {
                EnabledChanges = CommandChangeRules.Rules.StarsToWin
                    | CommandChangeRules.Rules.Lives
                    | CommandChangeRules.Rules.TimerMinutes
                    | CommandChangeRules.Rules.TeamsEnabled,
                StarsToWin = 99,
                Lives = 0,
                TimerMinutes = 0,
                TeamsEnabled = false,
            });

            yield return new WaitForSecondsRealtime(0.5f);
            game.SendCommand(new CommandToggleReady());
            yield return new WaitForSecondsRealtime(0.3f);
            game.SendCommand(new CommandToggleCountdown());
        }

        private static async Task<bool> StartSession() {
            var arguments = new SessionRunner.Arguments {
                GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                ClientId = "world-local",
                RuntimeConfig = new RuntimeConfig {
                    SimulationConfig = GlobalController.Instance.config,
                    Map = null,
                    IsRealGame = true,
                },
                SessionConfig = QuantumDeterministicSessionConfigAsset.DefaultConfig,
                GameMode = DeterministicGameMode.Local,
                RunnerId = "WORLDHUB",
                // Must match their multiplayer session: their gamemode code
                // walks Constants.MaxPlayers and indexes player-sized data, so
                // a smaller session reads out of bounds the moment play starts.
                PlayerCount = Constants.MaxPlayers,
                DeltaTimeType = SimulationUpdateTime.EngineDeltaTime,
            };

            try {
                NetworkHandler.Runner = await QuantumRunner.StartGameAsync(arguments);
                NetworkHandler.Runner.Game.AddPlayer(new RuntimePlayer {
                    PlayerNickname = Settings.Instance.generalNickname ?? "David",
                    UserId = "world-local",
                    UseColoredNickname = Settings.Instance.generalUseNicknameColor,
                    Character = Settings.Instance.generalCharacter,
                    Palette = Settings.Instance.generalPalette,
                });
                return true;
            } catch (Exception e) {
                Debug.LogWarning("[World] local session error: " + e.Message);
                return false;
            }
        }
    }
}
