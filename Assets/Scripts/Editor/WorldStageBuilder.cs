using Photon.Deterministic;
using Quantum;
using Quantum.Editor;
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

            // Copy a shipped stage's data rather than creating a blank one: it
            // carries authored settings the simulation relies on (spawn point,
            // camera bounds, tile dimensions). A blank asset left those at zero,
            // which is what killed the run right after the countdown.
            string stageAssetPath = AssetDir + "/" + StageName + "Data.asset";
            var stage = AssetDatabase.LoadAssetAtPath<VersusStageData>(stageAssetPath);
            if (!stage) {
                const string reference = "Assets/QuantumUser/Resources/AssetObjects/Maps/Grass/DefaultGrassStageData.asset";
                if (!AssetDatabase.CopyAsset(reference, stageAssetPath)) {
                    Debug.LogError("[WorldStageBuilder] could not copy the reference stage data");
                    return;
                }
                AssetDatabase.ImportAsset(stageAssetPath);
                stage = AssetDatabase.LoadAssetAtPath<VersusStageData>(stageAssetPath);
                stage.TranslationKey = "levels.custom.worldhub";
                EditorUtility.SetDirty(stage);
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

            // Saving the scene runs VersusStageBaker, which owns the tilemap
            // and camera fields. Everything below is what it does not touch, so
            // it has to be written after the bake and on every run.
            Configure(stage);
            EditorUtility.SetDirty(stage);

            // Quantum keeps its own asset database keyed by guid. New assets
            // must carry its label and the database must be rebuilt, or the
            // runtime reports "Unable to find asset [guid] (Quantum.Map)".
            AssetDatabase.SetLabels(stage, new[] { QuantumUnityDBUtilities.AssetLabel });
            AssetDatabase.SetLabels(map, new[] { QuantumUnityDBUtilities.AssetLabel });
            AssetDatabase.SaveAssets();
            QuantumUnityDBUtilities.RefreshGlobalDB(true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!Validate(stage)) {
                return;
            }
            Debug.Log($"[WorldStageBuilder] hub stage ready. Map guid: {map.Guid.Value}");
        }

        // Their simulation reads these without guarding, so an empty list or a
        // zero here is a division by zero the moment gameplay starts. The hub
        // died on exactly that twice: no star spawn points, then no music.
        private static void Configure(VersusStageData stage) {
            stage.TranslationKey = "levels.custom.worldhub";
            stage.StageAuthor = "David Erik García Arenas";
            stage.MusicComposer = "Nintendo";

            // The hub runs the game's own main menu theme; world.ogg stays on
            // the site's menu, where it belongs.
            stage.MainMusic = new[] { Music("MusicMainMenu") };
            stage.InvincibleMusic = Music("MusicStarman");
            stage.MegaMushroomMusic = Music("MusicMegaMushroom");

            // The plaza floor is two tile rows at y -12..-11, so its surface is
            // world y -5. Spawn just above it, centred.
            stage.Spawnpoint = new FPVector2(0, FP.FromFloat_UNSAFE(-3.5f));
            stage.SpawnpointArea = new FPVector2(FP.FromFloat_UNSAFE(1.4f), FP.FromFloat_UNSAFE(0.8f));

            var spots = new FPVector2[4];
            for (int i = 0; i < spots.Length; i++) {
                spots[i] = new FPVector2(FP.FromFloat_UNSAFE(-12f + i * 8f), FP.FromFloat_UNSAFE(-4f));
            }
            stage.BigStarSpawnpoints = spots;
        }

        private static AssetRef<LoopingMusicData> Music(string name) {
            var asset = Load<LoopingMusicData>(
                "Assets/QuantumUser/Resources/AssetObjects/Music/" + name + ".asset");
            if (!asset) {
                Debug.LogError($"[WorldStageBuilder] music asset {name} is missing");
                return default;
            }
            return new AssetRef<LoopingMusicData>(asset.Guid);
        }

        // A bad stage used to surface as "remainder by zero" in the browser,
        // half an hour and a deploy later. Catch it here instead.
        private static bool Validate(VersusStageData stage) {
            bool ok = true;
            void Require(bool condition, string what) {
                if (!condition) {
                    Debug.LogError("[WorldStageBuilder] " + what);
                    ok = false;
                }
            }

            Require(stage.MainMusic != null && stage.MainMusic.Length > 0, "no main music: VersusStageData.GetCurrentMusic divides by MainMusic.Length");
            Require(stage.MainMusic == null || Array.TrueForAll(stage.MainMusic, m => m.Id.IsValid), "a main music entry is an invalid asset reference");
            Require(stage.BigStarSpawnpoints != null && stage.BigStarSpawnpoints.Length > 0, "no star spawn points: BigStarSystem divides by their count");
            Require(stage.TileDimensions.X > 0 && stage.TileDimensions.Y > 0, $"tile dimensions are {stage.TileDimensions} — the bake did not run");
            Require(stage.TileData != null && stage.TileData.Length == stage.TileDimensions.X * stage.TileDimensions.Y,
                $"tile data is {(stage.TileData == null ? 0 : stage.TileData.Length)} entries, expected {stage.TileDimensions.X * stage.TileDimensions.Y}");

            if (ok) {
                Debug.Log($"[WorldStageBuilder] stage validated: {stage.TileDimensions.X}x{stage.TileDimensions.Y} tiles, "
                    + $"{stage.MainMusic.Length} track(s), {stage.BigStarSpawnpoints.Length} star spawn(s), spawn at {stage.Spawnpoint}");
            }
            return ok;
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
