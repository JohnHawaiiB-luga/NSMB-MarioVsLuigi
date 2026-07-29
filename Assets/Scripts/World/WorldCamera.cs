using UnityEngine;

namespace NSMB.World {
    // Two cameras in one: the 3D chase view, and the game's own side-on 2D view.
    // C slides between them. Movement is camera-relative, so the controls stay
    // honest in both: in side view, left/right walks the screen like the real game.
    public class WorldCamera : MonoBehaviour {

        public Transform target;
        public Vector3 offset = new(0f, 1.9f, -3.3f);
        public Vector3 sideOffset = new(-7.5f, 1.6f, 0f);
        public float smoothTime = 0.18f;
        public float lookHeight = 0.65f;
        public float blendSpeed = 2.2f;

        // 0 = free 3D chase, 1 = the game's own 2.5D side-on view. Past the
        // halfway point the controller also locks movement to the plane, which
        // is what makes NSMB 2.5D rather than 3D: 3D world, 2D gameplay.
        public float SideBlend => blend;
        public bool PlaneLocked => blend > 0.5f;

        private float blend;
        private float targetBlend;
        private Vector3 velocity;

        private Controls controls;

        private void Awake() {
            controls = new Controls();
            controls.Player.Enable();
        }

        private void OnDestroy() {
            controls?.Dispose();
        }

        private void Update() {
            // Reserve Item (Q / V / LB) has nothing to hold in free roam, so it
            // swaps the view — their binding, their gamepad button, no new keys.
            if (controls != null && controls.Player.ReserveItem.WasPressedThisFrame()) {
                targetBlend = targetBlend > 0.5f ? 0f : 1f;
            }
            blend = Mathf.MoveTowards(blend, targetBlend, blendSpeed * Time.deltaTime);
        }

        private void LateUpdate() {
            if (!target) {
                return;
            }
            Vector3 wanted = target.position + Vector3.Lerp(offset, sideOffset, Smooth(blend));
            transform.position = Vector3.SmoothDamp(transform.position, wanted, ref velocity, smoothTime);
            transform.LookAt(target.position + Vector3.up * lookHeight);
        }

        private static float Smooth(float t) {
            return t * t * (3f - 2f * t);
        }
    }
}
