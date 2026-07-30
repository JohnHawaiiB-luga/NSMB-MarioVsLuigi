using NSMB.Entities.Player;
using System.Collections.Generic;
using UnityEngine;

namespace NSMB.World {
    // Luigi, walking a step behind Mario through the hub.
    //
    // He is a view-only companion rather than a second player in the
    // simulation: their game builds a PlayerElements per local player, so a
    // real second player would split the screen, which is not what a follower
    // is. He trails Mario's own path from a moment ago, which means he walks
    // where Mario walked and jumps where Mario jumped without needing to ask
    // the simulation about the ground.
    public class WorldCompanion : MonoBehaviour {

        public Animator animator;
        public float delay = 0.45f;
        public float maxLag = 9f;

        private static readonly int ParamGlowColor = Shader.PropertyToID("GlowColor");

        private readonly Queue<(float time, Vector3 position)> trail = new();
        private Transform target;
        private Vector3 lastPosition;
        private float facing = 1f;
        private bool marked;

        private void Start() {
            // Their animator paints every player who is not the camera's focus
            // with their own colour, so you can pick rivals out in Versus. Off
            // the simulation there is nobody to paint him clear again, so he
            // arrived wearing a red halo.
            var block = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) {
                renderer.GetPropertyBlock(block);
                block.SetColor(ParamGlowColor, Color.clear);
                renderer.SetPropertyBlock(block);
            }
        }

        private void Update() {
            if (!target) {
                target = FindMario();
                if (!target) {
                    return;
                }
                // The bubble anchors to whichever speaker matches the line's
                // name. Luigi carries one; without one on Mario his lines hung
                // wherever the bubble happened to be last.
                if (!marked && !target.GetComponent<WorldSpeaker>()) {
                    var speaker = target.gameObject.AddComponent<WorldSpeaker>();
                    speaker.keyword = "MARIO";
                    speaker.bubbleHeight = 1.6f;
                    marked = true;
                }
                transform.position = target.position;
                lastPosition = transform.position;
                trail.Clear();
            }

            trail.Enqueue((Time.time, target.position));

            // Too far behind to be following anything — catch up rather than
            // trudge across a stage and a half of stitched level.
            if (Vector3.Distance(transform.position, target.position) > maxLag) {
                trail.Clear();
                transform.position = target.position;
                lastPosition = transform.position;
                return;
            }

            Vector3 wanted = transform.position;
            while (trail.Count > 0 && Time.time - trail.Peek().time >= delay) {
                wanted = trail.Dequeue().position;
            }
            transform.position = wanted;

            Vector3 moved = transform.position - lastPosition;
            lastPosition = transform.position;

            float speed = Time.deltaTime > 0f ? moved.magnitude / Time.deltaTime : 0f;
            if (Mathf.Abs(moved.x) > 0.0005f) {
                facing = Mathf.Sign(moved.x);
            }
            transform.rotation = Quaternion.Euler(0f, facing > 0f ? 90f : -90f, 0f);

            if (animator) {
                // Their own animator parameters, so he moves like a player does.
                animator.SetFloat("velocityX", Mathf.Abs(moved.x) / Mathf.Max(Time.deltaTime, 0.0001f));
                animator.SetFloat("velocityY", moved.y / Mathf.Max(Time.deltaTime, 0.0001f));
                animator.SetFloat("velocityMagnitude", speed);
                animator.SetBool("onGround", Mathf.Abs(moved.y) < 0.004f);
            }
        }

        private static Transform FindMario() {
            foreach (var mario in MarioPlayerAnimator.AllMarioPlayers) {
                if (mario) {
                    return mario.transform;
                }
            }
            return null;
        }
    }
}
