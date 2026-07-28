using UnityEngine;

namespace NSMB.World {
    // Trailing chase camera: sits behind and above the player, eases into place.
    public class WorldCamera : MonoBehaviour {

        public Transform target;
        public Vector3 offset = new(0f, 4.5f, -7.5f);
        public float smoothTime = 0.25f;
        public float lookHeight = 1.6f;

        private Vector3 velocity;

        private void LateUpdate() {
            if (!target) {
                return;
            }
            Vector3 desired = target.position + target.TransformDirection(Vector3.forward) * 0f + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            transform.LookAt(target.position + Vector3.up * lookHeight);
        }
    }
}
