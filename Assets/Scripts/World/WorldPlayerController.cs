using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace NSMB.World {
    // Free-roam that plays like the game: the sim's own movement values, its
    // signature moves (jump chain, skid, ground pound, crouch) and its own
    // animator parameters and sounds — just with nothing to chase.
    [RequireComponent(typeof(WorldMotor))]
    public class WorldPlayerController : MonoBehaviour {

        // MarioPlayerPhysicsInfo, verbatim — and live-tunable from the inspector,
        // which is the whole point of having one.
        public const float WalkMaxDefault = 2.8125f;
        public const float SprintMaxDefault = 5.625f;
        public const float JumpVelocityDefault = 6.62109375f;
        public const float GravityScaleDefault = 1f;

        public static float WalkMax = WalkMaxDefault;
        public static float SprintMax = SprintMaxDefault;
        public static float JumpVelocity = JumpVelocityDefault;
        public static float GravityScale = GravityScaleDefault;

        public const float Accel = 3.955f;
        public const float ReleaseDecel = 3.9550781196f;
        public const float SkidDecel = 10.54687536f;
        public const float SkidMinVelocity = 4.6875f;
        public const float JumpSpeedBonus = 0.46875f;
        public const float JumpTripleBonus = 0.5f;
        public const float TerminalFall = -5.859375f;
        public const float GroundpoundVelocity = -9f;

        // What the inspector reads back.
        public static WorldPlayerController Active { get; private set; }
        public Vector3 DebugVelocity => motor ? motor.Velocity : Vector3.zero;
        public bool DebugGrounded => motor && motor.Grounded;
        public int DebugJumpStage => jumpStage;

        public Transform cam;
        public Animator animator;
        public AudioSource sfx;
        public AudioClip jumpClip, crouchClip, skidClip, groundpoundStart, groundpoundLand;
        public AudioClip[] footsteps;

        public static System.Action Jumped;

        // The game's own input actions: same bindings, same gamepad support and
        // the same remapping its Options menu already offers.
        private Controls controls;
        private WorldMotor motor;
        private Vector3 horizontal;
        private float vy;
        private float lastBeat;

        // The jump chain: land and jump again quickly for double, then triple.
        private int jumpStage;
        private float lastLandTime = -10f;
        private bool groundpounding;
        private bool wasGrounded = true;
        private float stepTimer;
        private bool crouched;

        private void Awake() {
            Active = this;
            motor = GetComponent<WorldMotor>();
            controls = new Controls();
            controls.Player.Enable();
            controls.UI.Enable();
        }

        private void OnDestroy() {
            controls?.Dispose();
        }

        private void Update() {
            if (controls == null) {
                return;
            }
            if (controls.UI.Pause.WasPressedThisFrame()) {
                SceneManager.LoadScene("MainMenu");
                return;
            }

            Vector2 move = controls.Player.Movement.ReadValue<Vector2>();
            float x = move.x;
            float z = move.y;
            bool down = move.y < -0.4f;
            bool sprint = controls.Player.Sprint.IsPressed();
            bool jumpHeld = controls.Player.Jump.IsPressed();
            bool jumpPressed = controls.Player.Jump.WasPressedThisFrame();

            Vector3 forward = cam ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 wish = right * x + forward * z;

            // In side view the game is 2.5D: the world stays 3D, the gameplay
            // collapses onto one axis, exactly like the DS original.
            var rig = cam ? cam.GetComponent<WorldCamera>() : null;
            if (rig && rig.PlaneLocked) {
                wish.x = 0f;
            }
            if (wish.sqrMagnitude > 1f) {
                wish.Normalize();
            }

            // Dev-room flight: gravity off, jump rises, crouch sinks.
            if (WorldDebugInspector.FlyMode) {
                Vector3 flyVel = wish * (sprint ? SprintMax * 2f : SprintMax) * WorldDebugInspector.SpeedScale;
                flyVel.y = (jumpHeld ? 5f : 0f) - (down ? 5f : 0f);
                motor.Move(flyVel * Time.deltaTime);
                if (wish.sqrMagnitude > 0.001f) {
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(wish, Vector3.up), 14f * Time.deltaTime);
                }
                if (animator) {
                    animator.SetBool("onGround", false);
                    animator.SetFloat("velocityY", flyVel.y);
                }
                return;
            }

            // Ground pound: down in mid-air freezes you and drives you into the
            // floor, exactly like the game's signature move.
            if (!motor.Grounded && down && !groundpounding) {
                groundpounding = true;
                horizontal = Vector3.zero;
                vy = 0f;
                Play(groundpoundStart);
            }

            bool skidding = false;
            if (groundpounding) {
                vy = GroundpoundVelocity;
                if (motor.Grounded) {
                    groundpounding = false;
                    Play(groundpoundLand);
                }
            } else {
                crouched = motor.Grounded && down && wish.sqrMagnitude < 0.01f;
                float cap = (sprint ? SprintMax : WalkMax) * WorldDebugInspector.SpeedScale;
                if (crouched) {
                    cap = 0f;
                }

                // Skid: input opposing real motion above the sim's threshold.
                skidding = motor.Grounded
                    && horizontal.magnitude > SkidMinVelocity
                    && wish.sqrMagnitude > 0.01f
                    && Vector3.Dot(horizontal.normalized, wish) < -0.3f;

                float rate = skidding ? SkidDecel
                    : wish.sqrMagnitude < 0.001f || crouched ? ReleaseDecel
                    : Accel;
                horizontal = Vector3.MoveTowards(horizontal, wish * cap, rate * Time.deltaTime);

                if (motor.Grounded) {
                    vy = -0.5f;
                    if (jumpPressed && !crouched) {
                        // Chain only if the last landing was recent.
                        jumpStage = Time.time - lastLandTime < 0.28f ? Mathf.Min(jumpStage + 1, 2) : 0;
                        float bonus = JumpSpeedBonus * (horizontal.magnitude / WalkMax);
                        vy = JumpVelocity + bonus + (jumpStage == 2 ? JumpTripleBonus : 0f);
                        Play(jumpClip, jumpStage == 0 ? 1f : jumpStage == 1 ? 1.08f : 1.16f);
                        Jumped?.Invoke();
                    }
                }

                float g = (vy > 2.109375f ? (jumpHeld ? -7.03125f : -28.125f)
                    : vy > 0f ? -28.125f
                    : -38.671875f) * GravityScale;
                vy = Mathf.Max(vy + g * Time.deltaTime, TerminalFall * Mathf.Max(1f, GravityScale));
            }

            Vector3 frameVelocity = horizontal;
            frameVelocity.y = vy;
            motor.Move(frameVelocity * Time.deltaTime);
            if (motor.Grounded && vy < 0f) {
                vy = -0.5f;
            }

            // Landing: remember when, so the next jump can chain.
            if (motor.Grounded && !wasGrounded) {
                lastLandTime = Time.time;
            } else if (!motor.Grounded && wasGrounded && vy <= 0f) {
                jumpStage = 0;
            }
            wasGrounded = motor.Grounded;

            // Footsteps, paced by speed.
            if (motor.Grounded && horizontal.magnitude > 0.6f && footsteps != null && footsteps.Length > 0) {
                stepTimer -= Time.deltaTime * horizontal.magnitude;
                if (stepTimer <= 0f) {
                    stepTimer = 1.6f;
                    Play(footsteps[Random.Range(0, footsteps.Length)], Random.Range(0.94f, 1.06f), 0.35f);
                }
            }

            if (wish.sqrMagnitude > 0.001f && !crouched && !groundpounding) {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(wish, Vector3.up), 14f * Time.deltaTime);
            }

            if (animator) {
                Vector3 v = motor.Velocity;
                animator.SetFloat("velocityX", new Vector2(v.x, v.z).magnitude);
                animator.SetFloat("velocityY", v.y);
                animator.SetFloat("velocityMagnitude", new Vector2(v.x, v.z).magnitude);
                animator.SetBool("onGround", motor.Grounded);
                animator.SetBool("crouching", crouched);
                animator.SetBool("groundpound", groundpounding);
                animator.SetBool("skidding", skidding);
                animator.SetBool("doublejump", jumpStage == 1 && !motor.Grounded);
                animator.SetBool("triplejump", jumpStage == 2 && !motor.Grounded);
                animator.SetBool("dead", false);
                animator.SetBool("knockback", false);
                animator.SetBool("holding", false);
                animator.SetBool("invincible", false);
                animator.SetBool("mega", false);
                animator.SetBool("pipe", false);
            }

            if (Time.time - lastBeat > 4f) {
                lastBeat = Time.time;
                Debug.Log($"[World] pos={transform.position:F1} grounded={motor.Grounded} speed={horizontal.magnitude:F2} stage={jumpStage}");
            }
        }

        private void Play(AudioClip clip, float pitch = 1f, float volume = 0.7f) {
            if (!sfx || !clip) {
                return;
            }
            sfx.pitch = pitch;
            sfx.PlayOneShot(clip, volume);
        }
    }
}
