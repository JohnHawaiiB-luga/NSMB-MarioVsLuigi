using UnityEngine;

namespace NSMB.World {
    // Marks a character as a dialogue speaker; the bubble system anchors to it
    // by keyword match against the line's speaker name.
    public class WorldSpeaker : MonoBehaviour {
        public string keyword;
        public float bubbleHeight = 1.4f;
    }
}
