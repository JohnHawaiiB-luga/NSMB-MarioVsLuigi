using NSMB.Entities.Player;
using UnityEngine;

namespace NSMB.World {
    // A pipe that goes somewhere real. Stand on it, press down, and the game
    // runs its own quit ritual — jingle, Bowser wipe — and the page it lands on
    // is whichever part of the site this pipe belongs to.
    //
    // This is the seam between the hub and the rest of erikgaren.com: from the
    // visitor's side they walked down a pipe and came out in the portfolio,
    // rather than clicking a link that happened to sit near a game.
    public class WorldDoor : MonoBehaviour {

        public string destination = "/";
        public string label = "THE SITE";
        public float radius = 2.5f;
        public float verticalRadius = 3f;

        private Controls controls;
        private bool used;

        private void Awake() {
            controls = new Controls();
            controls.Player.Enable();
        }

        private void OnDestroy() {
            controls?.Dispose();
        }

        private void Update() {
            if (used) {
                return;
            }

            Transform mario = null;
            foreach (var m in MarioPlayerAnimator.AllMarioPlayers) {
                if (m) {
                    mario = m.transform;
                    break;
                }
            }
            if (!mario) {
                return;
            }

            bool over = Mathf.Abs(mario.position.x - transform.position.x) <= radius
                && Mathf.Abs(mario.position.y - transform.position.y) <= verticalRadius;
            if (!over || WorldDialogue.IsOpen) {
                return;
            }

            // Down, the way you enter any pipe in this game.
            if (controls.Player.Movement.ReadValue<Vector2>().y < -0.6f) {
                used = true;
                if (WorldExitFlow.Instance) {
                    WorldExitFlow.Instance.RequestQuitTo(destination);
                } else {
                    Debug.LogWarning("[World] no exit flow — door went nowhere");
                }
            }
        }
    }
}
