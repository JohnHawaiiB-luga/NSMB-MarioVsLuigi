using UnityEngine;

namespace NSMB.World {
    // The builder bakes the map's floor heights here on an x/z grid — physics-
    // independent ground truth, so characters stand on the world we built even
    // if PhysX queries come back empty.
    public class WorldHeightmap : MonoBehaviour {

        public static WorldHeightmap Instance { get; private set; }

        public float originX, originZ;
        public int width, depth;
        public float[] tops;

        private void Awake() {
            Instance = this;
        }

        public static bool TryGetFloor(Vector3 position, out float top) {
            top = 0f;
            var map = Instance;
            if (!map || map.tops == null || map.tops.Length == 0) {
                return false;
            }
            int ix = Mathf.FloorToInt(position.x - map.originX);
            int iz = Mathf.FloorToInt(position.z - map.originZ);
            if (ix < 0 || iz < 0 || ix >= map.width || iz >= map.depth) {
                return false;
            }
            float t = map.tops[iz * map.width + ix];
            if (float.IsNegativeInfinity(t)) {
                return false;
            }
            top = t;
            return true;
        }
    }
}
