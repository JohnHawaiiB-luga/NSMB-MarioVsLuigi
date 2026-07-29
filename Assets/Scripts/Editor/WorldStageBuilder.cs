using NSMB.Tiles;
using Photon.Deterministic;
using Quantum;
using Quantum.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace NSMB.WorldEditor {
    // Builds the hub as a real Versus stage by taking one of their finished
    // levels and making it ours, rather than authoring a stage from nothing.
    //
    // The first version of this started from LevelTemplate — their deliberately
    // empty starter — and hand-wrote each field the simulation needed. Every
    // field it missed was a division by zero at runtime, found one deploy at a
    // time. A shipped level already carries all of it: painted ground, star
    // spawns, enemies, coins, backgrounds, camera bounds and music. Copy that
    // and add to it.
    public static class WorldStageBuilder {

        private const string StageName = "WorldHubStage";
        private const string ScenePath = "Assets/Scenes/Levels/" + StageName + ".unity";
        private const string AssetDir = "Assets/QuantumUser/Resources/AssetObjects/Maps/" + StageName;

        // The foundation the hub is remixed from.
        private const string SourceScene = "Assets/Scenes/Levels/DefaultGrassLevel.unity";
        private const string SourceStage = "Assets/QuantumUser/Resources/AssetObjects/Maps/Grass/DefaultGrassStageData.asset";

        // Every other shipped stage, stitched on after it so the hub is one
        // long strip of the whole game — every tileset, enemy and object in
        // play at once.
        private static readonly string[] BlendScenes = {
            "Assets/Scenes/Levels/DefaultBrickLevel.unity",
            "Assets/Scenes/Levels/DefaultCastle.unity",
            "Assets/Scenes/Levels/DefaultPipes.unity",
            "Assets/Scenes/Levels/DefaultSnow.unity",
            "Assets/Scenes/Levels/CustomBeach.unity",
            "Assets/Scenes/Levels/CustomDesert.unity",
            "Assets/Scenes/Levels/CustomGhost.unity",
            "Assets/Scenes/Levels/CustomJungle.unity",
            "Assets/Scenes/Levels/CustomSky.unity",
            "Assets/Scenes/Levels/CustomVolcano.unity",
            "Assets/Scenes/Levels/CustomBonus.unity",
        };

        // Flat ground bridging one stage to the next, so the strip is walkable
        // end to end instead of a row of islands.
        private const int Gap = 8;

        // BigStar.qtn: bitset[MaxStarSpawns] UsedStarSpawns. Their bake step
        // throws above this, and twelve stages' worth would sail past it.
        private const int MaxStarSpawns = 64;

        // Marks a scene as already stitched, so building again does not lay a
        // second copy of every stage on top of the first.
        private const string BlendMarker = "WorldBlend";

        // Ours, and not to be taken from the level we copied.
        private static readonly string[] OwnFields = {
            "m_Script", "m_Name", "Identifier", "TranslationKey",
            "GroupingTranslationKey", "StageAuthor", "MusicComposer",
            "DiscordStageImage", "SortOrder",
        };

        [MenuItem("Tools/World/Build Hub Stage")]
        public static void BuildStage() {
            Build(false);
        }

        // Throws away hub scene edits and takes the level again from scratch.
        [MenuItem("Tools/World/Rebuild Hub Stage From Level")]
        public static void RebuildStage() {
            Build(true);
        }

        private static void Build(bool fromScratch) {
            Directory.CreateDirectory(AssetDir);

            if (fromScratch && AssetDatabase.AssetPathExists(ScenePath)) {
                AssetDatabase.DeleteAsset(ScenePath);
            }
            if (!AssetDatabase.AssetPathExists(ScenePath)) {
                if (!AssetDatabase.CopyAsset(SourceScene, ScenePath)) {
                    Debug.LogError($"[WorldStageBuilder] could not copy {SourceScene}");
                    return;
                }
                Debug.Log($"[WorldStageBuilder] hub scene taken from {Path.GetFileName(SourceScene)}");
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            Blend(scene);

            var reference = AssetDatabase.LoadAssetAtPath<VersusStageData>(SourceStage);
            if (!reference) {
                Debug.LogError($"[WorldStageBuilder] could not load {SourceStage}");
                return;
            }

            string stageAssetPath = AssetDir + "/" + StageName + "Data.asset";
            var stage = AssetDatabase.LoadAssetAtPath<VersusStageData>(stageAssetPath);
            if (!stage) {
                stage = ScriptableObject.CreateInstance<VersusStageData>();
                AssetDatabase.CreateAsset(stage, stageAssetPath);
            }

            // Everything the level's own stage data says about how it plays —
            // music, spawn point and area, camera bounds, UI colour, sound
            // overrides. Copied wholesale so nothing has to be remembered.
            Inherit(reference, stage);
            Identify(stage);
            EditorUtility.SetDirty(stage);

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
                Debug.LogError("[WorldStageBuilder] the scene has no QuantumMapData");
                return;
            }
            holder.AssetRef = map;
            EditorUtility.SetDirty(holder);

            RegisterScene();

            // Saving runs their VersusStageBaker, which owns the tilemap, the
            // camera bounds and the star spawns — it reads the last of those
            // from the StarSpawn markers already standing in the level.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Backstop(reference, stage);
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

        // Stitches every other shipped stage onto the end of the base level, so
        // the hub is one continuous strip of the whole game: every tileset,
        // every enemy, every object, all live at once. A debug map made of
        // their own content rather than a mock-up of it.
        private static void Blend(Scene hub) {
            if (GameObject.Find(BlendMarker)) {
                return;
            }

            Tilemap hubMap = MainTilemap(hub);
            if (!hubMap) {
                Debug.LogError("[WorldStageBuilder] the hub scene has no tilemap with a TilemapAnimator");
                return;
            }

            var marker = new GameObject(BlendMarker);
            SceneManager.MoveGameObjectToScene(marker, hub);

            hubMap.CompressBounds();
            BoundsInt hubBounds = hubMap.cellBounds;
            int baseY = hubBounds.yMin;
            int cursor = hubBounds.xMax + Gap;
            int stars = GameObject.FindGameObjectsWithTag("StarSpawn").Length;

            // A tile from the base level's own floor, used to bridge the gaps.
            TileBase bridge = null;
            for (int x = hubBounds.xMin; x < hubBounds.xMax && bridge == null; x++) {
                bridge = hubMap.GetTile(new Vector3Int(x, baseY, 0));
            }

            int blended = 0;
            foreach (string path in BlendScenes) {
                if (!AssetDatabase.AssetPathExists(path)) {
                    Debug.LogWarning($"[WorldStageBuilder] {path} is missing, skipped");
                    continue;
                }

                Scene src = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try {
                    Tilemap srcMap = MainTilemap(src);
                    if (!srcMap) {
                        Debug.LogWarning($"[WorldStageBuilder] {Path.GetFileName(path)} has no main tilemap, skipped");
                        continue;
                    }

                    srcMap.CompressBounds();
                    BoundsInt b = srcMap.cellBounds;
                    // Bottoms line up so the whole strip shares one ground level.
                    var offset = new Vector3Int(cursor - b.xMin, baseY - b.yMin, 0);

                    foreach (var cell in b.allPositionsWithin) {
                        TileBase tile = srcMap.GetTile(cell);
                        if (!tile) {
                            continue;
                        }
                        Vector3Int target = cell + offset;
                        hubMap.SetTile(target, tile);
                        hubMap.SetTransformMatrix(target, srcMap.GetTransformMatrix(cell));
                    }

                    if (bridge) {
                        for (int x = cursor - Gap; x < cursor; x++) {
                            hubMap.SetTile(new Vector3Int(x, baseY, 0), bridge);
                        }
                    }

                    // A tile is half a world unit; their stage maths is TileOrigin / 2.
                    Adopt(src, hub, new Vector3(offset.x * 0.5f, offset.y * 0.5f, 0f), path, ref stars);

                    cursor += b.size.x + Gap;
                    blended++;
                } finally {
                    EditorSceneManager.CloseScene(src, true);
                }
            }

            hubMap.CompressBounds();
            Debug.Log($"[WorldStageBuilder] blended {blended} stages: strip is {hubMap.cellBounds.size.x}x{hubMap.cellBounds.size.y} tiles, {stars} star spawns");
        }

        // Brings a stage's live content across: anything carrying a Quantum
        // entity prototype (enemies, blocks, coins, powerups, movers) plus its
        // star spawns, up to the simulation's ceiling.
        private static void Adopt(Scene src, Scene hub, Vector3 offset, string path, ref int stars) {
            var container = new GameObject("Blend_" + Path.GetFileNameWithoutExtension(path));
            SceneManager.MoveGameObjectToScene(container, hub);

            var wanted = new HashSet<GameObject>();
            foreach (var root in src.GetRootGameObjects()) {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true)) {
                    if (mb && mb.GetType().Name.StartsWith("QPrototype")) {
                        wanted.Add(mb.gameObject);
                    }
                }
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) {
                    if (t.CompareTag("StarSpawn") && stars < MaxStarSpawns) {
                        wanted.Add(t.gameObject);
                        stars++;
                    }
                }
            }

            // A prototype nested under another would otherwise be copied twice,
            // once on its own and once inside its parent.
            foreach (var go in new List<GameObject>(wanted)) {
                for (Transform p = go.transform.parent; p; p = p.parent) {
                    if (wanted.Contains(p.gameObject)) {
                        wanted.Remove(go);
                        break;
                    }
                }
            }

            foreach (var go in wanted) {
                var clone = UnityEngine.Object.Instantiate(
                    go, go.transform.position + offset, go.transform.rotation, container.transform);
                clone.name = go.name;
            }
        }

        private static Tilemap MainTilemap(Scene scene) {
            foreach (var root in scene.GetRootGameObjects()) {
                var animator = root.GetComponentInChildren<TilemapAnimator>(true);
                if (animator && animator.TryGetComponent(out Tilemap map)) {
                    return map;
                }
            }
            return null;
        }

        // Copy the level's play settings across, leaving our identity and the
        // asset's own guid alone — sharing a guid would collide in Quantum's
        // database.
        private static void Inherit(VersusStageData from, VersusStageData to) {
            var src = new SerializedObject(from);
            var dst = new SerializedObject(to);

            var it = src.GetIterator();
            if (it.NextVisible(true)) {
                do {
                    if (Array.IndexOf(OwnFields, it.propertyPath) >= 0) {
                        continue;
                    }
                    dst.CopyFromSerializedProperty(it);
                } while (it.NextVisible(false));
            }
            dst.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Identify(VersusStageData stage) {
            stage.TranslationKey = "levels.custom.worldhub";
            stage.GroupingTranslationKey = "";
            stage.StageAuthor = "David Erik García Arenas";
            stage.MusicComposer = "Nintendo";

            // The level we inherited from pins its own camera bounds, which
            // frame one stage. The strip is a dozen of them, so let the bake
            // measure it, and give the hub two ends rather than wrapping the
            // far edge back onto the first stage.
            stage.OverrideAutomaticCameraSettings = false;
            stage.OverrideAutomaticTilemapSettings = false;
            stage.IsWrappingLevel = false;

            // The hub plays the game's own main menu theme rather than the
            // level's overworld track. world.ogg is the site menu's, not this.
            var theme = AssetDatabase.LoadAssetAtPath<LoopingMusicData>(
                "Assets/QuantumUser/Resources/AssetObjects/Music/MusicMainMenu.asset");
            if (theme) {
                stage.MainMusic = new[] { new AssetRef<LoopingMusicData>(theme.Guid) };
            } else {
                Debug.LogError("[WorldStageBuilder] MusicMainMenu is missing — keeping the level's music");
            }
        }

        // The bake should have filled these from the level. If it ever does
        // not, fall back to the level's own values rather than let the
        // simulation divide by an empty array.
        private static void Backstop(VersusStageData reference, VersusStageData stage) {
            if (stage.MainMusic == null || stage.MainMusic.Length == 0) {
                Debug.LogWarning("[WorldStageBuilder] no music after bake — taking the level's");
                stage.MainMusic = reference.MainMusic;
            }
            if (stage.BigStarSpawnpoints == null || stage.BigStarSpawnpoints.Length == 0) {
                Debug.LogWarning("[WorldStageBuilder] no star spawns after bake — taking the level's");
                stage.BigStarSpawnpoints = reference.BigStarSpawnpoints;
            }
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
            Require(stage.BigStarSpawnpoints != null && stage.BigStarSpawnpoints.Length > 0, "no star spawns: BigStarSystem divides by their count");
            Require(stage.Spawnpoint != FPVector2.Zero, "the player spawn point is at the origin — the level's was not inherited");
            Require(stage.TileDimensions.X > 0 && stage.TileDimensions.Y > 0, $"tile dimensions are {stage.TileDimensions} — the bake did not run");
            Require(stage.TileData != null && stage.TileData.Length == stage.TileDimensions.X * stage.TileDimensions.Y,
                $"tile data is {(stage.TileData == null ? 0 : stage.TileData.Length)} entries, expected {stage.TileDimensions.X * stage.TileDimensions.Y}");

            if (ok) {
                Debug.Log($"[WorldStageBuilder] stage validated: {stage.TileDimensions.X}x{stage.TileDimensions.Y} tiles, "
                    + $"{stage.MainMusic.Length} track(s), {stage.BigStarSpawnpoints.Length} star spawn(s), spawn at {stage.Spawnpoint}");
            }
            return ok;
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
