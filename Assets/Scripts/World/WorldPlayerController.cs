using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NSMB.World {
    // Free-roam on the game's own movement numbers (verbatim from
    // MarioPlayerPhysicsInfo), carried by WorldMotor's cast-based capsule.
    [RequireComponent(typeof(WorldMotor))]
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

        private WorldMotor motor;
        private Vector3 horizontal;
        private float vy;
        private float lastBeat;

        private void Awake() {
            motor = GetComponent<WorldMotor>();
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

            float cap = sprint ? SprintMax : WalkMax;
            float rate = wish.sqrMagnitude < 0.001f ? ReleaseDecel
                : Vector3.Dot(horizontal, wish) < -0.01f ? SkidDecel
                : Accel;
            horizontal = Vector3.MoveTowards(horizontal, wish * cap, rate * Time.deltaTime);

            if (motor.Grounded) {
                vy = -0.5f;
                if (kb.spaceKey.wasPressedThisFrame) {
                    vy = JumpVelocity + JumpSpeedBonus * (horizontal.magnitude / WalkMax);
                    Jumped?.Invoke();
                }
            }
            float g = vy > 2.109375f ? (jumpHeld ? -7.03125f : -28.125f)
                : vy > 0f ? -28.125f
                : -38.671875f;
            vy = Mathf.Max(vy + g * Time.deltaTime, TerminalFall);

            Vector3 frameVelocity = horizontal;
            frameVelocity.y = vy;
            motor.Move(frameVelocity * Time.deltaTime);
            if (motor.Grounded && vy < 0f) {
                vy = -0.5f;
            }

            if (wish.sqrMagnitude > 0.001f) {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(wish, Vector3.up), 14f * Time.deltaTime);
            }

            if (animator) {
                animator.SetFloat("velocityMagnitude", new Vector2(motor.Velocity.x, motor.Velocity.z).magnitude);
                animator.SetFloat("velocityY", motor.Velocity.y);
                animator.SetBool("onGround", motor.Grounded);
                animator.SetBool("crouching", motor.Grounded && kb.sKey.isPressed && x == 0f);
            }

            if (Time.time - lastBeat > 2f) {
                lastBeat = Time.time;
                var sb = new StringBuilder();
                sb.Append($"[World] pos={transform.position:F2} wish=({x:F0},{z:F0}) hvel={horizontal.magnitude:F2} vy={vy:F2} ");
                sb.Append($"grounded={motor.Grounded} realvel={motor.Velocity.magnitude:F2} dt={Time.deltaTime:F4}");
                Debug.Log(sb.ToString());
            }
        }
    }
}
