using UnityEngine;
using UnityEngine.InputSystem;

namespace NSMB.World {
    // Third-person walkabout for the hub. Reads the keyboard directly — on phones
    // the hosting page's touch overlay synthesizes the same key events, so one
    // input path serves both.
    [RequireComponent(typeof(CharacterController))]
    public class WorldPlayerController : MonoBehaviour {

        public float walkSpeed = 6f;
        public float sprintSpeed = 10f;
        public float jumpVelocity = 9f;
        public float gravity = -26f;
        public float turnSpeed = 14f;
        public Transform cam;

        private CharacterController controller;
        private float verticalVelocity;

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

            Vector3 forward = cam
                ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized
                : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 move = (right * x + forward * z);
            if (move.sqrMagnitude > 1f) {
                move.Normalize();
            }

            bool sprinting = kb.shiftKey.isPressed;
            Vector3 velocity = move * (sprinting ? sprintSpeed : walkSpeed);

            if (controller.isGrounded) {
                verticalVelocity = -2f;
                if (kb.spaceKey.wasPressedThisFrame) {
                    verticalVelocity = jumpVelocity;
                }
            }
            verticalVelocity += gravity * Time.deltaTime;
            velocity.y = verticalVelocity;

            controller.Move(velocity * Time.deltaTime);

            if (move.sqrMagnitude > 0.001f) {
                Quaternion target = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
            }
        }
    }
}
