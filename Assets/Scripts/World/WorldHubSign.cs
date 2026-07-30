using NSMB.Entities.Player;
using UnityEngine;

namespace NSMB.World {
    // A place in the hub with something to say. Walking near it puts words in
    // Luigi's mouth; walking away and coming back says them again, but not
    // until it has had a moment to reset.
    public class WorldHubSign : MonoBehaviour {

        public string speaker = "LUIGI";
        [TextArea(2, 6)] public string[] lines;

        // Said on the way back through. Walking past a sign a second time and
        // hearing the same introduction makes the world feel like a recording.
        [TextArea(2, 6)] public string[] revisitLines;
        public float radius = 5f;
        public float verticalRadius = 7f;
        public float repeatAfter = 25f;

        private float spokenAt = -999f;
        private bool inside;
        private int visits;

        private void Update() {
            if (lines == null || lines.Length == 0) {
                return;
            }

            Transform mario = FindMario();
            if (!mario) {
                return;
            }

            // Generous vertically: the strip climbs and drops between stages,
            // and a sign should not go quiet because the ground did.
            float dx = Mathf.Abs(mario.position.x - transform.position.x);
            float dy = Mathf.Abs(mario.position.y - transform.position.y);
            bool near = dx <= radius && dy <= verticalRadius;

            if (near && !inside && Time.time - spokenAt > repeatAfter && !WorldDialogue.IsOpen) {
                bool returning = visits > 0 && revisitLines != null && revisitLines.Length > 0;
                WorldDialogue.Say(speaker, returning ? revisitLines : lines);
                spokenAt = Time.time;
                visits++;
            }
            inside = near;
        }

        private static Transform FindMario() {
            foreach (var mario in MarioPlayerAnimator.AllMarioPlayers) {
                if (mario) {
                    return mario.transform;
                }
            }
            return null;
        }

        private void OnDrawGizmosSelected() {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
