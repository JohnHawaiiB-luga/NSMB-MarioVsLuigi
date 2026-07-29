using UnityEngine;

namespace NSMB.World {
    // Manual capsule movement: sweep, move, slide. Replaces CharacterController,
    // whose native actor refused to activate in the WebGL build ("Move called on
    // inactive controller", 330 times and counting). Casts and transforms have
    // no activation state to get wrong.
    [RequireComponent(typeof(CapsuleCollider))]
    public class WorldMotor : MonoBehaviour {

        private const float Skin = 0.03f;

        private CapsuleCollider capsule;

        public bool Grounded { get; private set; }
        public Vector3 Velocity { get; private set; }

        private void Awake() {
            capsule = GetComponent<CapsuleCollider>();
        }

        private void Points(out Vector3 bottom, out Vector3 top) {
            Vector3 c = transform.position + capsule.center;
            float half = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
            bottom = c - Vector3.up * half;
            top = c + Vector3.up * half;
        }

        public void Move(Vector3 displacement) {
            Vector3 start = transform.position;

            Slide(new Vector3(displacement.x, 0f, displacement.z));

            float dy = displacement.y;
            if (Mathf.Abs(dy) > 0.00001f) {
                Points(out var b, out var t);
                Vector3 dir = dy > 0f ? Vector3.up : Vector3.down;
                float dist = Mathf.Abs(dy);
                if (Physics.CapsuleCast(b, t, capsule.radius, dir, out RaycastHit hit, dist + Skin, ~0, QueryTriggerInteraction.Ignore)) {
                    transform.position += dir * Mathf.Max(0f, hit.distance - Skin);
                    Grounded = dy < 0f;
                } else {
                    transform.position += dir * dist;
                    Grounded = false;
                }
            } else {
                Points(out var b, out var t);
                Grounded = Physics.CapsuleCast(b, t, capsule.radius, Vector3.down, out _, Skin * 3f, ~0, QueryTriggerInteraction.Ignore);
            }

            Velocity = (transform.position - start) / Mathf.Max(Time.deltaTime, 0.0001f);
        }

        private void Slide(Vector3 horizontal) {
            for (int i = 0; i < 2 && horizontal.sqrMagnitude > 0.0000001f; i++) {
                Points(out var b, out var t);
                float dist = horizontal.magnitude;
                Vector3 dir = horizontal / dist;
                if (Physics.CapsuleCast(b, t, capsule.radius, dir, out RaycastHit hit, dist + Skin, ~0, QueryTriggerInteraction.Ignore)) {
                    float travel = Mathf.Max(0f, hit.distance - Skin);
                    transform.position += dir * travel;
                    horizontal = Vector3.ProjectOnPlane(dir * (dist - travel), hit.normal);
                    horizontal.y = 0f;
                } else {
                    transform.position += dir * dist;
                    break;
                }
            }
        }
    }
}
