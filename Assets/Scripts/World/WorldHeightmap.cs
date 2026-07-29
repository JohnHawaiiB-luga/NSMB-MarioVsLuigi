using UnityEngine;

namespace NSMB.World {
    // The builder bakes the corridor's floor heights in here — one sample per
    // z unit. Physics-independent ground truth: if the cast-based motor gets
    // nothing from PhysX, characters still stand on the world we built.
    public class WorldHeightmap : MonoBehaviour {

        public static WorldHeightmap Instance { get; private set; }

        public float zOrigin;
        public float[] floorTops;

        private void Awake() {
            Instance = this;
        }

        public static bool TryGetFloor(Vector3 position, out float top) {
            top = 0f;
            var map = Instance;
            if (!map || map.floorTops == null || map.floorTops.Length == 0) {
                return false;
            }
            int i = Mathf.FloorToInt(position.z - map.zOrigin);
            if (i < 0 || i >= map.floorTops.Length) {
                return false;
            }
            top = map.floorTops[i];
            return true;
        }
    }
}
