using System.Collections;
using NSMB.UI.Loading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NSMB.World {
    // Entering the World uses the game's own loading screen — the same canvas,
    // Mario loader and loading music a Versus match gets. Their EndLoading path
    // needs a live Quantum game, so we drive the visible half ourselves.
    public static class WorldTransition {

        // Transitions run on their own persistent host so nothing can stop the
        // coroutine mid-flight and strand the loading screen on screen.
        private class Host : MonoBehaviour { }

        public static void Run(IEnumerator routine) {
            var go = GameObject.Find("WorldTransitionHost");
            if (!go) {
                go = new GameObject("WorldTransitionHost");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Host>();
            }
            go.GetComponent<Host>().StartCoroutine(routine);
        }

        public static IEnumerator ToScene(string sceneName, float minimumSeconds = 1.6f) {
            LoadingCanvas canvas = GlobalController.Instance ? GlobalController.Instance.loadingCanvas : null;
            if (canvas) {
                canvas.dontHideOnGameDestroy = true;
                canvas.Initialize(null);
            }

            float started = Time.unscaledTime;
            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f) {
                yield return null;
            }
            // Let the loader animation breathe, the way a real match does.
            while (Time.unscaledTime - started < minimumSeconds) {
                yield return null;
            }
            op.allowSceneActivation = true;
            while (!op.isDone) {
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.35f);
            if (canvas) {
                canvas.EndAnimation();
            }
        }
    }
}
