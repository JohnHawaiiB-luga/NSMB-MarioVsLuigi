using UnityEngine;

namespace NSMB.World {
    // Player 2 on the same motor and the same numbers — follows, falls and
    // jumps a beat behind player one.
    [RequireComponent(typeof(WorldMotor))]
    public class CompanionNPC : MonoBehaviour {

        public Transform player;
        public Animator animator;
        public float followDistance = 1.6f;

        private WorldMotor motor;
        private Vector3 horizontal;
        private float vy;
        private float jumpQueued;

        private void Awake() {
            motor = GetComponent<WorldMotor>();
            WorldPlayerController.Jumped += OnPlayerJumped;
        }

        private void OnDestroy() {
            WorldPlayerController.Jumped -= OnPlayerJumped;
        }

        private void OnPlayerJumped() {
            jumpQueued = Time.time + 0.18f;
        }

        private void Update() {
            if (!player) {
                return;
            }

            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            Vector3 wish = dist > followDistance ? toPlayer.normalized : Vector3.zero;
            float cap = dist > 4f ? WorldPlayerController.SprintMax : WorldPlayerController.WalkMax;
            float rate = wish.sqrMagnitude < 0.001f ? WorldPlayerController.ReleaseDecel : WorldPlayerController.Accel;
            horizontal = Vector3.MoveTowards(horizontal, wish * cap, rate * Time.deltaTime);

            bool wantJump = jumpQueued > 0f && Time.time >= jumpQueued;
            if (motor.Grounded) {
                vy = -0.5f;
                if (wantJump) {
                    vy = WorldPlayerController.JumpVelocity;
                    jumpQueued = 0f;
                }
            }
            float g = vy > 0f ? -28.125f : -38.671875f;
            vy = Mathf.Max(vy + g * Time.deltaTime, WorldPlayerController.TerminalFall);

            Vector3 v = horizontal;
            v.y = vy;
            motor.Move(v * Time.deltaTime);
            if (motor.Grounded && vy < 0f) {
                vy = -0.5f;
            }

            if (horizontal.sqrMagnitude > 0.01f) {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(new Vector3(horizontal.x, 0, horizontal.z).normalized, Vector3.up), 10f * Time.deltaTime);
            }

            if (animator) {
                animator.SetFloat("velocityMagnitude", new Vector2(motor.Velocity.x, motor.Velocity.z).magnitude);
                animator.SetFloat("velocityY", motor.Velocity.y);
                animator.SetBool("onGround", motor.Grounded);
                animator.SetBool("crouching", false);
            }
        }
    }
}
