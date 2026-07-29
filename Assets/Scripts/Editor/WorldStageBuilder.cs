using Quantum;
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace NSMB.WorldEditor {
    // Authors the hub as a real Versus stage, following the same recipe their
    // own Tools/MvLO/Create New Map window uses: copy the level template, make
    // a VersusStageData + Map pair, point the scene's QuantumMapData at it, and
    // register the scene. Saving the scene bakes the Quantum map automatically.
    //
    // Without this the hub had no stage of its own, so a local session simply
    // ran their default Versus map — which is exactly what it looked like.
    public static class WorldStageBuilder {

        private const string StageName = "WorldHubStage";
        private const string ScenePath = "Assets/Scenes/Levels/" + StageName + ".unity";
        private const string AssetDir = "Assets/QuantumUser/Resources/AssetObjects/Maps/" + StageName;

        [MenuItem("Tools/World/Build Hub Stage")]
        public static void BuildStage() {
            Directory.CreateDirectory(AssetDir);

            if (!AssetDatabase.AssetPathExists(ScenePath)) {
                if (!AssetDatabase.CopyAsset("Assets/Scenes/LevelTemplate.unity", ScenePath)) {
                    Debug.LogError("[WorldStageBuilder] could not copy the level template");
                    return;
                }
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath);

            string stageAssetPath = AssetDir + "/" + StageName + "Data.asset";
            var stage = AssetDatabase.LoadAssetAtPath<VersusStageData>(stageAssetPath);
            if (!stage) {
                stage = ScriptableObject.CreateInstance<VersusStageData>();
                stage.name = StageName;
                stage.TranslationKey = "levels.custom.worldhub";
                AssetDatabase.CreateAsset(stage, stageAssetPath);
            }

            string mapAssetPath = AssetDir + "/" + StageName + "Map.asset";
            var map = AssetDatabase.LoadAssetAtPath<Map>(mapAssetPath);
            if (!map) {
                map = ScriptableObject.CreateInstance<Map>();
                AssetDatabase.CreateAsset(map, mapAssetPath);
            }
            map.Scene = StageName;
            map.ScenePath = ScenePath;
            map.UserAsset = stage;
            EditorUtility.SetDirty(map);

            var holder = UnityEngine.Object.FindFirstObjectByType<QuantumMapData>();
            if (!holder) {
                Debug.LogError("[WorldStageBuilder] the template has no QuantumMapData");
                return;
            }
            holder.AssetRef = map;
            EditorUtility.SetDirty(holder);

            PaintHub();
            RegisterScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[WorldStageBuilder] hub stage ready. Map guid: {map.Guid.Value}");
        }

        // A wide, safe plaza with a few things to jump on — their tiles, their
        // grid, so the simulation treats it exactly like any Versus stage.
        private static void PaintHub() {
            var ground = FindTilemap("Tilemap_Ground") ?? FindTilemap("Tilemap");
            if (!ground) {
                Debug.LogError("[WorldStageBuilder] no ground tilemap in the template");
                return;
            }

            var grass = Load<TileBase>("Assets/Resources/Tilemaps/Tiles/Grass/GrassGround.asset");
            var semisolid = Load<TileBase>("Assets/Resources/Tilemaps/Tiles/Grass/BrownSemisolid.asset");
            var wood = Load<TileBase>("Assets/Resources/Tilemaps/Tiles/Grass/WoodBlock.asset");
            if (!grass) {
                Debug.LogError("[WorldStageBuilder] grass tile missing");
                return;
            }

            ground.ClearAllTiles();

            // Floor across the whole stage width, two rows thick.
            for (int x = -32; x <= 31; x++) {
                for (int y = -12; y <= -11; y++) {
                    ground.SetTile(new Vector3Int(x, y, 0), grass);
                }
            }

            // Steps up on the left, a plateau on the right.
            for (int s = 0; s < 4; s++) {
                for (int x = -26 + s * 3; x < -23 + s * 3; x++) {
                    ground.SetTile(new Vector3Int(x, -10 + s, 0), grass);
                }
            }
            for (int x = 14; x <= 26; x++) {
                ground.SetTile(new Vector3Int(x, -7, 0), grass);
            }

            // Floating semisolid platforms to hop between.
            if (semisolid) {
                foreach (int[] p in new[] { new[] { -8, -7 }, new[] { -2, -5 }, new[] { 4, -3 }, new[] { 10, -5 } }) {
                    for (int x = p[0]; x < p[0] + 4; x++) {
                        ground.SetTile(new Vector3Int(x, p[1], 0), semisolid);
                    }
                }
            }

            // A couple of blocks to bonk.
            if (wood) {
                foreach (int x in new[] { -14, -12, 0, 2, 18 }) {
                    ground.SetTile(new Vector3Int(x, -8, 0), wood);
                }
            }

            ground.CompressBounds();
            EditorUtility.SetDirty(ground);
        }

        private static Tilemap FindTilemap(string name) {
            foreach (var tm in UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                if (tm.name == name) {
                    return tm;
                }
            }
            return null;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void RegisterScene() {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes) {
                if (s.path == ScenePath) {
                    return;
                }
            }
            Array.Resize(ref scenes, scenes.Length + 1);
            scenes[^1] = new EditorBuildSettingsScene {
                path = ScenePath,
                guid = AssetDatabase.GUIDFromAssetPath(ScenePath),
                enabled = true,
            };
            EditorBuildSettings.scenes = scenes;
        }
    }
}
