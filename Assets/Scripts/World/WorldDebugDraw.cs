using UnityEngine;

namespace NSMB.World {
    // The debug-draw layer: immediate-mode lines rendered after the scene, the
    // way the engine oracle's debug_draw_3d pass works. Fed by the inspector's
    // visualizer toggles.
    public class WorldDebugDraw : MonoBehaviour {

        private Material lines;

        private void Awake() {
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader) {
                lines = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                lines.SetInt("_SrcBlend", (int) UnityEngine.Rendering.BlendMode.SrcAlpha);
                lines.SetInt("_DstBlend", (int) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                lines.SetInt("_Cull", (int) UnityEngine.Rendering.CullMode.Off);
                lines.SetInt("_ZWrite", 0);
            }
        }

        private void OnRenderObject() {
            if (!lines) {
                return;
            }
            bool any = WorldDebugInspector.DrawHeightmap || WorldDebugInspector.DrawTriggers || WorldDebugInspector.DrawVelocity;
            if (!any) {
                return;
            }

            lines.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);

            if (WorldDebugInspector.DrawHeightmap) {
                DrawHeightmapGrid();
            }
            if (WorldDebugInspector.DrawTriggers) {
                DrawTriggerVolumes();
            }
            if (WorldDebugInspector.DrawVelocity) {
                DrawVelocityVector();
            }

            GL.End();
            GL.PopMatrix();
        }

        // The baked floor, sampled around the player so the grid stays cheap.
        private void DrawHeightmapGrid() {
            var map = WorldHeightmap.Instance;
            var player = WorldPlayerController.Active;
            if (!map || !player) {
                return;
            }
            GL.Color(new Color(0.2f, 1f, 0.5f, 0.55f));
            Vector3 p = player.transform.position;
            const int R = 14;
            for (int dz = -R; dz <= R; dz++) {
                for (int dx = -R; dx <= R; dx++) {
                    Vector3 a = new(Mathf.Floor(p.x) + dx, 0f, Mathf.Floor(p.z) + dz);
                    if (!WorldHeightmap.TryGetFloor(a, out float top)) {
                        continue;
                    }
                    Vector3 c = new(a.x, top + 0.02f, a.z);
                    GL.Vertex(c);
                    GL.Vertex(c + Vector3.right);
                    GL.Vertex(c);
                    GL.Vertex(c + Vector3.forward);
                }
            }
        }

        private void DrawTriggerVolumes() {
            GL.Color(new Color(1f, 0.85f, 0.2f, 0.7f));
            foreach (var box in FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)) {
                if (!box.isTrigger) {
                    continue;
                }
                Bounds b = new(box.transform.TransformPoint(box.center), box.size);
                Wire(b);
            }
        }

        private void DrawVelocityVector() {
            var player = WorldPlayerController.Active;
            if (!player) {
                return;
            }
            Vector3 from = player.transform.position + Vector3.up * 0.5f;
            GL.Color(new Color(1f, 0.3f, 0.3f, 0.9f));
            GL.Vertex(from);
            GL.Vertex(from + player.DebugVelocity * 0.35f);
        }

        private static void Wire(Bounds b) {
            Vector3 c = b.center, e = b.extents;
            Vector3[] v = {
                c + new Vector3(-e.x, -e.y, -e.z), c + new Vector3(e.x, -e.y, -e.z),
                c + new Vector3(e.x, -e.y, e.z), c + new Vector3(-e.x, -e.y, e.z),
                c + new Vector3(-e.x, e.y, -e.z), c + new Vector3(e.x, e.y, -e.z),
                c + new Vector3(e.x, e.y, e.z), c + new Vector3(-e.x, e.y, e.z),
            };
            int[] edges = { 0,1, 1,2, 2,3, 3,0, 4,5, 5,6, 6,7, 7,4, 0,4, 1,5, 2,6, 3,7 };
            for (int i = 0; i < edges.Length; i += 2) {
                GL.Vertex(v[edges[i]]);
                GL.Vertex(v[edges[i + 1]]);
            }
        }
    }
}
