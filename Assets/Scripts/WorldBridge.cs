using UnityEngine;
using UnityEngine.SceneManagement;

namespace NSMB {
    // Tells the hosting web page whether the player is actually in a level, so the
    // page's touch controls only appear during gameplay — never over the menus.
    public static class WorldBridge {

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void WorldSetGameplay(int active);
#else
        private static void WorldSetGameplay(int active) { }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init() {
            SceneManager.activeSceneChanged += (_, next) => Notify(next);
            Notify(SceneManager.GetActiveScene());
        }

        private static void Notify(Scene scene) {
            bool gameplay = scene.name != "Intro" && scene.name != "MainMenu";
            WorldSetGameplay(gameplay ? 1 : 0);

            if (gameplay && scene.name == "WorldHub") {
                // Physics self-test: a runtime-made collider and a scene-baked one,
                // each under a raycast. Which one fails names the failure class.
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                probe.name = "PhysProbe";
                probe.transform.position = new Vector3(50f, -1f, 0f);
                probe.transform.localScale = new Vector3(4f, 1f, 4f);
                Physics.SyncTransforms();
                bool runtimeHit = Physics.Raycast(new Vector3(50f, 3f, 0f), Vector3.down, out _, 20f);
                bool sceneHit = Physics.Raycast(new Vector3(0f, 3f, -2f), Vector3.down, out _, 20f);
                Debug.Log($"[World] physics probe: simMode={Physics.simulationMode} runtimeHit={runtimeHit} sceneHit={sceneHit}");
                Object.Destroy(probe);
            }
        }
    }
}
