using UnityEngine;

namespace NSMB.World {
    // Invisible volume that makes the companion speak when the player walks in.
    [RequireComponent(typeof(BoxCollider))]
    public class WorldDialogueTrigger : MonoBehaviour {

        public string speaker = "ERIK";
        [TextArea] public string[] lines;
        public bool once = true;

        private bool fired;

        private void OnTriggerEnter(Collider other) {
            if (fired && once) {
                return;
            }
            if (!other.GetComponent<WorldPlayerController>()) {
                return;
            }
            fired = true;
            WorldDialogue.Say(speaker, lines);
        }
    }
}
