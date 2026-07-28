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
            bool gameplay = scene.name != "Intro" && scene.name != "MainMenu" && scene.name != "WorldEntry";
            WorldSetGameplay(gameplay ? 1 : 0);
        }
    }
}
