using UnityEngine;

namespace NSMB.World {
    // Player 2. A real entity on the same physics as the player — walks, falls
    // and jumps like anyone else, it just takes its orders from "stay near
    // player one" instead of a keyboard.
    [RequireComponent(typeof(CharacterController))]
    public class CompanionNPC : MonoBehaviour {

        public Transform player;
        public Animator animator;
        public float followDistance = 1.6f;

        private CharacterController controller;
        private Vector3 horizontal;
        private float vy;
        private float jumpQueued;

        private void Awake() {
            controller = GetComponent<CharacterController>();
            WorldPlayerController.Jumped += OnPlayerJumped;
        }

        private void OnDestroy() {
            WorldPlayerController.Jumped -= OnPlayerJumped;
        }

        private void OnPlayerJumped() {
            // A beat behind, like a good player two.
            jumpQueued = Time.time + 0.18f;
        }

        private void Update() {
            if (!player) {
                return;
            }

            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            Vector3 wish = Vector3.zero;
            if (dist > followDistance) {
                wish = toPlayer.normalized;
            }

            float cap = dist > 4f ? WorldPlayerController.SprintMax : WorldPlayerController.WalkMax;
            float rate = wish.sqrMagnitude < 0.001f ? WorldPlayerController.ReleaseDecel : WorldPlayerController.Accel;
            horizontal = Vector3.MoveTowards(horizontal, wish * cap, rate * Time.deltaTime);

            bool wantJump = jumpQueued > 0f && Time.time >= jumpQueued;
            if (controller.isGrounded) {
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
            controller.Move(v * Time.deltaTime);

            if (horizontal.sqrMagnitude > 0.01f) {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(new Vector3(horizontal.x, 0, horizontal.z).normalized, Vector3.up), 10f * Time.deltaTime);
            }

            if (animator) {
                animator.SetFloat("velocityMagnitude", new Vector2(controller.velocity.x, controller.velocity.z).magnitude);
                animator.SetFloat("velocityY", controller.velocity.y);
                animator.SetBool("onGround", controller.isGrounded);
                animator.SetBool("crouching", false);
            }
        }
    }
}
