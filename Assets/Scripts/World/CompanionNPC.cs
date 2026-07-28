using UnityEngine;

namespace NSMB.World {
    // The narrator. Follows the player at a respectful distance and bobs a little
    // so the greybox capsule still feels alive.
    public class CompanionNPC : MonoBehaviour {

        public Transform player;
        public float followDistance = 3f;
        public float speed = 8f;

        private Vector3 basePosition;

        private void Update() {
            if (!player) {
                return;
            }
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.magnitude > followDistance) {
                Vector3 goal = player.position - toPlayer.normalized * followDistance;
                goal.y = player.position.y;
                transform.position = Vector3.Lerp(transform.position, goal, speed * Time.deltaTime);
            }
            if (toPlayer.sqrMagnitude > 0.01f) {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(toPlayer.normalized, Vector3.up), 6f * Time.deltaTime);
            }
            transform.position += Vector3.up * (Mathf.Sin(Time.time * 2.2f) * 0.004f);
        }
    }
}
