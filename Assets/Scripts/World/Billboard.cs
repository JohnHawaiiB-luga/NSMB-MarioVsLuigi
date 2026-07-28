using UnityEngine;

namespace NSMB.World {
    // Keeps floating labels facing the camera.
    public class Billboard : MonoBehaviour {
        private void LateUpdate() {
            Camera cam = Camera.main;
            if (cam) {
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
            }
        }
    }
}
