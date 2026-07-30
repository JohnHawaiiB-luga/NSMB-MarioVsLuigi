using NSMB.Tiles;
using NSMB.World;
using Photon.Deterministic;
using Quantum;
using Quantum.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

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

        // Same idea for the story layer: one Luigi is plenty.
        private const string StoryMarker = "WorldStory";

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
            BuildStory(scene);

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

                    // Bridge the seam by copying the profile of the ground
                    // immediately before it. A single row at the very bottom
                    // sat underneath the ground either side of it and read as
                    // a bottomless pit between every pair of stages.
                    if (bridge) {
                        int sample = cursor - Gap - 1;
                        int top = baseY;
                        for (int y = baseY; y < baseY + 12; y++) {
                            if (hubMap.GetTile(new Vector3Int(sample, y, 0))) {
                                top = y;
                            }
                        }
                        for (int x = cursor - Gap; x < cursor; x++) {
                            for (int y = baseY; y <= top; y++) {
                                hubMap.SetTile(new Vector3Int(x, y, 0), bridge);
                            }
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

        // Brings a stage's live content across: whole subtrees that hold Quantum
        // entity prototypes — enemies, blocks, coins, powerups, spinners, movers
        // — plus its star spawns, up to the simulation's ceiling.
        //
        // Copied as whole roots, deliberately. Lifting each prototype object out
        // on its own broke every reference that pointed sideways or upwards
        // instead of down: a spinner's SpinnerAnimator keeps a Transform for the
        // part that turns, and once its stage was closed that reference was null,
        // so the view threw on every frame. One view throwing inside
        // QuantumEntityViewUpdater's loop stops every entity after it from being
        // updated at all — the whole world freezes over a single field.
        private static void Adopt(Scene src, Scene hub, Vector3 offset, string path, ref int stars) {
            var container = new GameObject("Blend_" + Path.GetFileNameWithoutExtension(path));
            SceneManager.MoveGameObjectToScene(container, hub);

            int copied = 0;
            foreach (var root in src.GetRootGameObjects()) {
                if (Infrastructure(root)) {
                    continue;
                }

                bool carriesEntities = false;
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true)) {
                    if (mb && mb.GetType().Name.StartsWith("QPrototype")) {
                        carriesEntities = true;
                        break;
                    }
                }

                var spawns = new List<Transform>();
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) {
                    if (t.CompareTag("StarSpawn")) {
                        spawns.Add(t);
                    }
                }

                if (!carriesEntities && spawns.Count == 0) {
                    continue;
                }

                var clone = UnityEngine.Object.Instantiate(root, container.transform);
                clone.name = root.name;
                clone.transform.position += offset;
                copied++;

                // Star spawns are capped by the simulation, so trim the copy
                // rather than let the bake throw on the count.
                foreach (var t in clone.GetComponentsInChildren<Transform>(true)) {
                    if (!t || !t.CompareTag("StarSpawn")) {
                        continue;
                    }
                    if (stars < MaxStarSpawns) {
                        stars++;
                    } else {
                        UnityEngine.Object.DestroyImmediate(t.gameObject);
                    }
                }
            }

            if (copied == 0) {
                UnityEngine.Object.DestroyImmediate(container);
            }
        }

        // Roots that describe the stage itself rather than things standing in it.
        // The tilemap is copied cell by cell and the map data belongs to the hub,
        // so bringing either across would fight what is already there.
        private static bool Infrastructure(GameObject root) {
            return root.GetComponentInChildren<Tilemap>(true)
                || root.GetComponentInChildren<QuantumMapData>(true)
                || root.GetComponentInChildren<Camera>(true)
                || root.GetComponentInChildren<Canvas>(true);
        }

        // The part that makes this a portfolio rather than a playground: Luigi
        // walking a step behind you, and things to walk up to that have
        // something to say. Authored here rather than built at runtime because
        // their dialogue panel and Luigi's model are project assets, and only
        // the editor can reach those by path.
        private static void BuildStory(Scene scene) {
            if (GameObject.Find(StoryMarker)) {
                return;
            }
            var root = new GameObject(StoryMarker);
            SceneManager.MoveGameObjectToScene(root, scene);

            var stage = AssetDatabase.LoadAssetAtPath<VersusStageData>(SourceStage);
            Vector3 spawn = stage
                ? new Vector3(stage.Spawnpoint.X.AsFloat, stage.Spawnpoint.Y.AsFloat, 0f)
                : Vector3.zero;

            BuildLuigi(root.transform, spawn);
            BuildBubble(root.transform);
            BuildSigns(root.transform, spawn);
            BuildDoors(root.transform, spawn, MainTilemap(scene));
            Debug.Log("[WorldStageBuilder] story layer built: Luigi, dialogue, signs and doors");
        }

        private static void BuildLuigi(Transform parent, Vector3 spawn) {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/QuantumUser/Resources/EntityPrototypes/Player/PlayerLuigi.prefab");
            if (!prefab) {
                Debug.LogError("[WorldStageBuilder] PlayerLuigi.prefab is missing — no companion");
                return;
            }

            var luigi = (GameObject) PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(luigi, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            luigi.name = "Luigi";
            luigi.transform.position = spawn + new Vector3(-2f, 0f, 0f);

            // He is scenery, not a player. Every script comes off — all of them
            // belong to a player that the simulation drives, and any one left
            // behind hunts for an entity that was never created and throws on
            // every frame. Stripping by name missed WrappingEntityView, which
            // is exactly that: a Quantum entity view not called Quantum
            // anything. The model, its animator and its renderers are not
            // MonoBehaviours and stay.
            foreach (var mb in luigi.GetComponentsInChildren<MonoBehaviour>(true)) {
                if (mb) {
                    UnityEngine.Object.DestroyImmediate(mb, true);
                }
            }

            // MarioPlayerAnimator owns which of the powerup models is showing —
            // small, blue shell, propeller, hammer suit — and switches them as
            // the player changes state. With it stripped off, whatever the
            // prefab happened to ship active stays active, which is why he
            // turned up permanently wearing a blue shell.
            foreach (string extra in new[] {
                "blue_shell", "small_model", "Propeller", "PropellerHat",
                "HammerShell", "HammerHelmet", "HammerTuckShell", "dust",
            }) {
                foreach (var t in luigi.GetComponentsInChildren<Transform>(true)) {
                    if (t && t.name == extra) {
                        t.gameObject.SetActive(false);
                    }
                }
            }

            // Their gameplay prefabs ship without a controller because the
            // simulation assigns one; standing on his own he needs it spelled
            // out, and needs to animate whether or not he is on screen.
            var animator = luigi.GetComponentInChildren<Animator>(true);
            if (animator) {
                var controller = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/Animations/Player/Luigi/LargeLuigi.overrideController") as RuntimeAnimatorController;
                if (controller) {
                    animator.runtimeAnimatorController = controller;
                }
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            var companion = luigi.AddComponent<WorldCompanion>();
            companion.animator = animator;

            var speaker = luigi.AddComponent<WorldSpeaker>();
            speaker.keyword = "LUIGI";
            speaker.bubbleHeight = 1.6f;
        }

        private static void BuildBubble(Transform parent) {
            var canvasGo = new GameObject("DialogueBubble", typeof(Canvas));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(6f, 2.2f);
            canvasRect.localScale = Vector3.one * 0.5f;

            var panel = new GameObject("Panel", typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            var panelImage = panel.GetComponent<Image>();
            panelImage.sprite = Sprite("Assets/Sprites/UI/Menu/Elements/rounded-rect-5px-dialogue.png");
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0.06f, 0.07f, 0.12f, 0.94f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Their own character art on the left, the way a portrait box works.
            var portraitGo = new GameObject("Portrait", typeof(Image));
            portraitGo.transform.SetParent(panel.transform, false);
            var portrait = portraitGo.GetComponent<Image>();
            portrait.preserveAspect = true;
            var portraitRect = portraitGo.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.02f, 0.16f);
            portraitRect.anchorMax = new Vector2(0.20f, 0.92f);
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;

            TMP_Text name = Text(panel.transform, "Name", 0.42f, TextAlignmentOptions.TopLeft,
                new Vector2(0.22f, 0.62f), new Vector2(0.96f, 0.96f));
            TMP_Text line = Text(panel.transform, "Line", 0.34f, TextAlignmentOptions.TopLeft,
                new Vector2(0.22f, 0.08f), new Vector2(0.96f, 0.62f));

            var promptGo = new GameObject("Prompt", typeof(Image));
            promptGo.transform.SetParent(panel.transform, false);
            var prompt = promptGo.GetComponent<Image>();
            prompt.sprite = Sprite("Assets/Sprites/UI/Menu/Elements/a-prompt.png");
            prompt.preserveAspect = true;
            var promptRect = promptGo.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.88f, 0.05f);
            promptRect.anchorMax = new Vector2(0.98f, 0.3f);
            promptRect.offsetMin = Vector2.zero;
            promptRect.offsetMax = Vector2.zero;

            var voice = canvasGo.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;

            var dialogue = canvasGo.AddComponent<WorldDialogue>();
            dialogue.bubble = panel;
            dialogue.nameText = name;
            dialogue.lineText = line;
            dialogue.prompt = prompt;
            dialogue.voice = voice;
            dialogue.portrait = portrait;
            dialogue.marioFace = Face("MarioCharacter");
            dialogue.luigiFace = Face("LuigiCharacter");
            dialogue.typeClip = Clip("Assets/Sound/ui/chat_keydown.wav", "chat_keydown");
            dialogue.openClip = Clip("Assets/Sound/ui/chat_fulltype.wav", "chat_fulltype");
            dialogue.doneClip = dialogue.openClip;
        }

        private static TMP_Text Text(Transform parent, string name, float size,
            TextAlignmentOptions align, Vector2 min, Vector2 max) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Color.white;
            var rect = tmp.rectTransform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return tmp;
        }

        // Their character assets carry the art the menus use; the selection
        // sprite is the head shot.
        private static Sprite Face(string character) {
            var asset = AssetDatabase.LoadAssetAtPath<CharacterAsset>(
                $"Assets/QuantumUser/Resources/AssetObjects/Characters/{character}.asset");
            if (!asset) {
                Debug.LogWarning($"[WorldStageBuilder] {character} not found — no portrait");
                return null;
            }
            return asset.SelectionSprite ? asset.SelectionSprite : asset.ReadySprite;
        }

        private static Sprite Sprite(string path) {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!sprite) {
                Debug.LogWarning($"[WorldStageBuilder] sprite {path} not found");
            }
            return sprite;
        }

        // Their sound files move around between versions; fall back to a search
        // rather than leaving the bubble mute.
        private static AudioClip Clip(string path, string name) {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip) {
                return clip;
            }
            foreach (string guid in AssetDatabase.FindAssets($"{name} t:AudioClip")) {
                var found = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (found) {
                    return found;
                }
            }
            Debug.LogWarning($"[WorldStageBuilder] audio clip {name} not found");
            return null;
        }

        private static void BuildSigns(Transform parent, Vector3 spawn) {
            (string speaker, float x, string[] lines)[] script = {
                ("LUIGI", 8f, new[] {
                    "MARIO|Mamma mia… Luigi? Where are we?",
                    "LUIGI|I was hoping you'd know, Mario. I woke up here.",
                    "LUIGI|Look at it. Somebody took our whole world and stitched it back together wrong.",
                }),
                ("LUIGI", 52f, new[] {
                    "MARIO|The grass ends and then… bricks? Just like that?",
                    "LUIGI|Every stage we've ever raced through, all in a row. Grass, castle, snow, the beach…",
                    "LUIGI|One long strip. You can walk from one end of the game to the other.",
                }),
                ("LUIGI", 96f, new[] {
                    "MARIO|Who does something like this?!",
                    "LUIGI|An engineer. David. Munich. He takes things apart to find out what breaks them —",
                    "LUIGI|professionally, Mario. People pay him to break things and write down why.",
                    "MARIO|…And he broke US?",
                    "LUIGI|He opened us. Wanted to know how a game actually holds together underneath.",
                }),
                ("LUIGI", 150f, new[] {
                    "LUIGI|Here's the part that got me. Look down at yourself.",
                    "MARIO|What? I look fine. Handsome, even.",
                    "LUIGI|You're a 3D model, Mario. Always were. Every one of us is.",
                    "LUIGI|We were just never allowed to walk anywhere but along one flat line.",
                    "MARIO|…Mamma mia. All these years?",
                    "LUIGI|He wanted to see what happened if he moved the camera off that line. So — this.",
                }),
                ("LUIGI", 210f, new[] {
                    "MARIO|So he did all this just to poke at it?",
                    "LUIGI|To learn it. Engine, physics, the simulation that decides where we land.",
                    "LUIGI|Then he needed somewhere to keep the results, and a folder felt insulting.",
                    "LUIGI|That's the rest of him back there — erikgaren.com. Looks like a phone, runs like an OS.",
                    "LUIGI|HawaiiOS, he calls it. The pipes go there. This half just has better jumping.",
                }),
                ("LUIGI", 280f, new[] {
                    "MARIO|Let's-a-go further! I want to see the end of it!",
                    "LUIGI|It's a long walk. He left shortcuts in — the bracket keys, [ and ].",
                    "LUIGI|One press, one whole stage. F lifts the camera off the line. ` shows the wiring.",
                    "LUIGI|And N… N turns the walls off. You just fly. Don't tell Peach.",
                    "MARIO|We could have been doing that the WHOLE TIME?",
                    "LUIGI|Keep right long enough and you'll come out where you started, too. It loops.",
                }),
            };

            // Said on the way back through, so a second pass is not the same
            // recording. Indexed to match the script above.
            string[][] again = {
                new[] {
                    "LUIGI|Back already? The door's still that way, Mario.",
                    "MARIO|I know, I know. I just wanted to check it was still there.",
                },
                new[] {
                    "MARIO|The bricks are still bricks. Nothing's fixed itself.",
                    "LUIGI|It's not broken, Mario. It's just… arranged.",
                },
                new[] {
                    "MARIO|Do you think he found what he was looking for? In here?",
                    "LUIGI|He got it running, didn't he. That's usually the answer.",
                },
                new[] {
                    "MARIO|I've been walking sideways my whole life, Luigi.",
                    "LUIGI|Try the F key. Just… don't tell the others what's back there.",
                },
                new[] {
                    "LUIGI|The pipes still go to the site whenever you want out.",
                    "MARIO|Later. I like it in here.",
                },
                new[] {
                    "LUIGI|You've walked the whole thing now, haven't you.",
                    "MARIO|Twice! And I'd do it again!",
                },
            };

            for (int i = 0; i < script.Length; i++) {
                (string speaker, float x, string[] lines) = script[i];
                var go = new GameObject("Sign_" + Mathf.RoundToInt(x));
                go.transform.SetParent(parent, false);
                go.transform.position = spawn + new Vector3(x, 0f, 0f);
                var sign = go.AddComponent<WorldHubSign>();
                sign.speaker = speaker;
                sign.lines = lines;
                sign.revisitLines = i < again.Length ? again[i] : null;
            }
        }

        // Doors out of the hub, placed on the pipes the blended stages already
        // stand up. Each one runs the quit ritual and lands the player in a
        // particular part of the site, which is what stitches the two halves
        // together: you leave down a pipe rather than by closing a tab.
        private static void BuildDoors(Transform parent, Vector3 spawn, Tilemap map) {
            (string label, string url, float x, string colour)[] doors = {
                ("HAWAIIOS", "/os", 34f, "green"),
                ("PORTFOLIO", "/", 128f, "red"),
                ("PROJECTS", "/projects", 186f, "yellow"),
            };

            foreach ((string label, string url, float x, string colour) in doors) {
                float worldX = spawn.x + x;
                // A door with no pipe under it is a rumour: Luigi announces
                // something the player cannot see. Stand a real one up out of
                // their own pipe tiles, one colour per destination.
                float mouthY = Pipe(map, worldX, colour);

                var go = new GameObject("Door_" + label);
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(worldX, mouthY, 0f);

                var door = go.AddComponent<WorldDoor>();
                door.destination = url;
                door.label = label;

                DoorSign(go.transform, label);

                var signGo = new GameObject("DoorTalk_" + label);
                signGo.transform.SetParent(go.transform, false);
                var sign = signGo.AddComponent<WorldHubSign>();
                sign.speaker = "LUIGI";
                sign.radius = 5f;
                sign.repeatAfter = 40f;
                sign.lines = new[] {
                    $"LUIGI|This one's a door, Mario. Marked {label}.",
                    "LUIGI|Stand on it, press down, and you come out inside his site.",
                };
                sign.revisitLines = new[] {
                    $"LUIGI|Still marked {label}. Down whenever you like.",
                };
            }
        }

        // Paints a two-tile-wide pipe standing on whatever ground is at this
        // column and returns the world height of its mouth.
        private static float Pipe(Tilemap map, float worldX, string colour) {
            if (!map) {
                return 0f;
            }

            int left = Mathf.RoundToInt(worldX * 2f);
            BoundsInt bounds = map.cellBounds;

            // Find the ground under the column so the pipe sits on it rather
            // than hovering or sinking.
            int ground = bounds.yMin;
            for (int y = bounds.yMin; y < bounds.yMax; y++) {
                if (map.GetTile(new Vector3Int(left, y, 0)) || map.GetTile(new Vector3Int(left + 1, y, 0))) {
                    ground = y;
                }
            }

            TileBase leftTop = Load<TileBase>($"Assets/Resources/Tilemaps/Tiles/Pipes/Unbreakable/pipe_{colour}_vertical_left_top.asset");
            TileBase rightTop = Load<TileBase>($"Assets/Resources/Tilemaps/Tiles/Pipes/Unbreakable/pipe_{colour}_vertical_right_top.asset");
            TileBase leftStem = Load<TileBase>($"Assets/Resources/Tilemaps/Tiles/Pipes/Unbreakable/pipe_{colour}_vertical_left.asset");
            TileBase rightStem = Load<TileBase>($"Assets/Resources/Tilemaps/Tiles/Pipes/Unbreakable/pipe_{colour}_vertical_right.asset");
            if (!leftTop || !rightTop) {
                Debug.LogWarning($"[WorldStageBuilder] no {colour} pipe tiles — door has no pipe");
                return (ground + 1) * 0.5f;
            }

            const int height = 3;
            for (int i = 0; i < height; i++) {
                int y = ground + 1 + i;
                bool top = i == height - 1;
                map.SetTile(new Vector3Int(left, y, 0), top ? leftTop : leftStem ? leftStem : leftTop);
                map.SetTile(new Vector3Int(left + 1, y, 0), top ? rightTop : rightStem ? rightStem : rightTop);
            }

            // Standing on the mouth, in world units — tiles are half a unit.
            return (ground + 1 + height) * 0.5f;
        }

        // A board over the pipe saying where it goes, in the game's own panel art.
        private static void DoorSign(Transform parent, string label) {
            var canvasGo = new GameObject("Sign", typeof(Canvas));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localPosition = new Vector3(0.5f, 2.2f, 0f);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(4f, 1f);
            rect.localScale = Vector3.one * 0.5f;

            var panel = new GameObject("Panel", typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            var image = panel.GetComponent<Image>();
            image.sprite = Sprite("Assets/Sprites/UI/Menu/Elements/rounded-rect-5px.png");
            image.type = Image.Type.Sliced;
            image.color = new Color(0.05f, 0.06f, 0.1f, 0.9f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            TMP_Text text = Text(panel.transform, "Label", 0.5f, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one);
            text.text = label + "\n<size=70%>↓ press down</size>";
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
            // measure it.
            stage.OverrideAutomaticCameraSettings = false;
            stage.OverrideAutomaticTilemapSettings = false;

            // Wrapping, but over 573 units instead of a Versus arena: walk right
            // long enough and the far end comes back around to the start, so the
            // hub reads as one lap rather than a corridor with two dead ends.
            stage.IsWrappingLevel = true;

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
