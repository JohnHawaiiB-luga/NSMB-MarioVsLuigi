using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NSMB.World {
    // Third-person free-roam using the game's own movement numbers, taken
    // verbatim from MarioPlayerPhysicsInfo: speed caps, acceleration, the jump
    // impulse with its speed bonus, and the held-jump gravity curve. The world
    // is built at native tile scale so these apply one-to-one.
    [RequireComponent(typeof(CharacterController))]
    public class WorldPlayerController : MonoBehaviour {

        public const float WalkMax = 2.8125f;
        public const float SprintMax = 5.625f;
        public const float Accel = 3.955f;
        public const float ReleaseDecel = 3.9550781196f;
        public const float SkidDecel = 10.54687536f;
        public const float JumpVelocity = 6.62109375f;
        public const float JumpSpeedBonus = 0.46875f;
        public const float TerminalFall = -5.859375f;

        public Transform cam;
        public Animator animator;
        public static System.Action Jumped;

        private CharacterController controller;
        private Vector3 horizontal;
        private float vy;
        private float lastBeat;

        private void Awake() {
            controller = GetComponent<CharacterController>();
        }

        private void Update() {
            Keyboard kb = Keyboard.current;
            if (kb == null) {
                return;
            }

            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                    - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float z = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                    - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            bool sprint = kb.shiftKey.isPressed;
            bool jumpHeld = kb.spaceKey.isPressed;

            Vector3 forward = cam ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 wish = right * x + forward * z;
            if (wish.sqrMagnitude > 1f) {
                wish.Normalize();
            }

            // Horizontal: accelerate toward the wish velocity the way the sim
            // does — walk accel normally, skid decel when reversing, release
            // decel when idle.
            float cap = sprint ? SprintMax : WalkMax;
            Vector3 target = wish * cap;
            float rate = wish.sqrMagnitude < 0.001f ? ReleaseDecel
                : Vector3.Dot(horizontal, wish) < -0.01f ? SkidDecel
                : Accel;
            horizontal = Vector3.MoveTowards(horizontal, target, rate * Time.deltaTime);

            if (controller.isGrounded) {
                vy = -0.5f;
                if (kb.spaceKey.wasPressedThisFrame) {
                    vy = JumpVelocity + JumpSpeedBonus * (horizontal.magnitude / WalkMax);
                    Jumped?.Invoke();
                }
            }

            // The game's gravity curve: floaty while rising with jump held,
            // heavier once released, heaviest falling; clamped at terminal.
            float g = vy > 2.109375f ? (jumpHeld ? -7.03125f : -28.125f)
                : vy > 0f ? -28.125f
                : -38.671875f;
            vy = Mathf.Max(vy + g * Time.deltaTime, TerminalFall);

            Vector3 before = transform.position;
            Vector3 frameVelocity = horizontal;
            frameVelocity.y = vy;
            controller.Move(frameVelocity * Time.deltaTime);
            Vector3 moved = transform.position - before;

            if (moved.sqrMagnitude > 0.000001f && wish.sqrMagnitude > 0.001f) {
                Quaternion look = Quaternion.LookRotation(new Vector3(horizontal.x, 0, horizontal.z).normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 14f * Time.deltaTime);
            } else if (wish.sqrMagnitude > 0.001f) {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(wish, Vector3.up), 14f * Time.deltaTime);
            }

            if (animator) {
                animator.SetFloat("velocityMagnitude", new Vector2(controller.velocity.x, controller.velocity.z).magnitude);
                animator.SetFloat("velocityY", controller.velocity.y);
                animator.SetBool("onGround", controller.isGrounded);
                animator.SetBool("crouching", controller.isGrounded && z < 0f && x == 0f && sprint == false && kb.sKey.isPressed);
            }

            // Forensics: movement froze on production twice with no cause found.
            // This names every condition that can freeze a CharacterController.
            if (Time.time - lastBeat > 2f) {
                lastBeat = Time.time;
                var sb = new StringBuilder();
                sb.Append($"[World] pos={transform.position:F2} moved={moved.magnitude:F4} wish=({x:F0},{z:F0}) hvel={horizontal.magnitude:F2} vy={vy:F2} ");
                sb.Append($"grounded={controller.isGrounded} ccEnabled={controller.enabled} active={gameObject.activeInHierarchy} dt={Time.deltaTime:F4} ts={Time.timeScale:F2}");
                Vector3 p = transform.position + controller.center;
                float half = Mathf.Max(0f, controller.height * 0.5f - controller.radius);
                var hits = Physics.OverlapCapsule(p + Vector3.up * half, p - Vector3.up * half, controller.radius);
                foreach (var h in hits) {
                    if (h.transform.root != transform.root) {
                        sb.Append($" overlap={h.name}");
                    }
                }
                Debug.Log(sb.ToString());
            }
        }
    }
}
