using NSMB.World;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NSMB.WorldEditor {
    // Builds the World's hub from code so the whole thing stays reviewable,
    // repeatable and headless-buildable. Everything it places is made of the
    // game's own tiles, sounds, fonts and bodies — just arranged as a 3D
    // playground instead of a 2D stage.
    public static class WorldSceneBuilder {

        private const float Sky = 0.72f;
        private static readonly Color NeonRed = new(0.898f, 0.078f, 0f);
        private static readonly Color NeonBlue = new(0.059f, 0.424f, 0.741f);
        private static readonly Color NeonCyan = new(0f, 0.784f, 1f);

        private static TMP_FontAsset GameFont;
        private static Material GrassMat, BrickMat, BlockMat, GroundMat;

        // Baked floor grid: 120 x 120 tiles centred on the plaza.
        private const int MapWidth = 120, MapDepth = 120;
        private const float MapOriginX = -60f, MapOriginZ = -60f;
        private static float[] Bake;

        [MenuItem("Tools/World/Build Scenes")]
        public static void BuildScenes() {
            GameFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/BoldFont.asset");
            BuildHubScene();
            RegisterScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[WorldSceneBuilder] WorldHub built; the game's own menu is the front door.");
        }

        // ------------------------------------------------------------------ hub

        private static void BuildHubScene() {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Bake = new float[MapWidth * MapDepth];
            for (int i = 0; i < Bake.Length; i++) {
                Bake[i] = float.NegativeInfinity;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.74f, 0.76f, 0.8f);
            RenderSettings.fog = false;

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = new Color(1f, 0.98f, 0.93f);
            lightGo.transform.rotation = Quaternion.Euler(52f, -34f, 0f);

            GrassMat = TileMat("Assets/Sprites/Atlases/Terrain/grass.png", null, "grass");
            GroundMat = TileMat("Assets/Sprites/Atlases/Terrain/grass.png", null, "grass");
            BrickMat = TileMat("Assets/Sprites/Atlases/Terrain/3 Underground (with ice).png", null, "brick");
            BlockMat = TileMat("Assets/Sprites/Atlases/Terrain/animated-blocks.png", "animation_0", "qblock");

            // ---- the plaza: four quadrants around an open pit -----------------
            Slab("PlazaS", new Vector3(0f, -0.5f, -18f), new Vector3(40f, 1f, 20f), GrassMat);
            Slab("PlazaN", new Vector3(0f, -0.5f, 18f), new Vector3(40f, 1f, 20f), GrassMat);
            Slab("PlazaW", new Vector3(-16f, -0.5f, 0f), new Vector3(8f, 1f, 16f), GrassMat);
            Slab("PlazaE", new Vector3(16f, -0.5f, 0f), new Vector3(8f, 1f, 16f), GrassMat);

            // the pit floor, four units down, reachable and escapable
            Slab("PitFloor", new Vector3(0f, -4.5f, 0f), new Vector3(24f, 1f, 16f), GroundMat);
            for (int s = 0; s < 4; s++) {
                Slab("PitStep" + s, new Vector3(9f - s * 1.6f, -4f + s, 6.5f), new Vector3(3f, 1f, 2.4f), BrickMat);
            }

            // ---- a terrace with stairs ---------------------------------------
            for (int s = 0; s < 3; s++) {
                Slab("Step" + s, new Vector3(-9f, s * 0.9f, -9f + s * 2.4f), new Vector3(6f, 1f, 2.4f), BrickMat);
            }
            Slab("Terrace", new Vector3(-13f, 2.2f, -1f), new Vector3(12f, 1f, 12f), GrassMat);

            // ---- floating platforms, a jumpable arc --------------------------
            for (int p = 0; p < 5; p++) {
                float ang = -0.35f + p * 0.32f;
                Slab("Float" + p, new Vector3(Mathf.Sin(ang) * 13f, 1.6f + p * 0.9f, 12f + Mathf.Cos(ang) * 4f),
                    new Vector3(3.2f, 0.8f, 3.2f), BrickMat);
            }

            // ---- ? blocks at bonk height -------------------------------------
            for (int q = 0; q < 4; q++) {
                Block("QBlock" + q, new Vector3(-3f + q * 2f, 2.1f, -6f), Vector3.one, BlockMat);
            }

            // ---- brick towers to climb ---------------------------------------
            Slab("TowerA", new Vector3(11f, 0.5f, -8f), new Vector3(2f, 2f, 2f), BrickMat);
            Slab("TowerB", new Vector3(13.5f, 1.5f, -6f), new Vector3(2f, 4f, 2f), BrickMat);
            Slab("TowerC", new Vector3(16f, 2.5f, -8f), new Vector3(2f, 6f, 2f), BrickMat);

            // ---- the stage gallery: real Versus geometry as scenery -----------
            string[] stages = {
                "Assets/Scenes/LevelTemplate.unity",
                "Assets/Scenes/Levels/DefaultGrassLevel.unity",
                "Assets/Scenes/Levels/DefaultBrickLevel.unity",
                "Assets/Scenes/Levels/DefaultCastle.unity",
                "Assets/Scenes/Levels/DefaultSnow.unity",
            };
            for (int i = 0; i < stages.Length; i++) {
                ExtrudePanel(stages[i], new Vector3(-40f + i * 20f, 0f, 34f), 18, 10);
            }

            // ---- sky ----------------------------------------------------------
            Backdrop("SkyN", new Vector3(0f, 16f, 56f), Quaternion.identity, new Vector2(150f, 46f));
            Backdrop("SkyS", new Vector3(0f, 16f, -46f), Quaternion.Euler(0f, 180f, 0f), new Vector2(150f, 46f));
            Backdrop("SkyW", new Vector3(-50f, 16f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector2(150f, 46f));
            Backdrop("SkyE", new Vector3(50f, 16f, 0f), Quaternion.Euler(0f, -90f, 0f), new Vector2(150f, 46f));

            var musicGo = new GameObject("Music");
            var music = musicGo.AddComponent<AudioSource>();
            music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/music/results.ogg");
            music.loop = true;
            music.playOnAwake = true;
            music.volume = 0.42f;
            music.spatialBlend = 0f;

            // ---- the brothers --------------------------------------------------
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0.6f, -12f);
            var pcap = player.AddComponent<CapsuleCollider>();
            pcap.height = 0.95f;
            pcap.radius = 0.22f;
            pcap.center = Vector3.up * 0.5f;
            var prb = player.AddComponent<Rigidbody>();
            prb.isKinematic = true;
            prb.useGravity = false;
            player.AddComponent<WorldMotor>();
            var pctrl = player.AddComponent<WorldPlayerController>();
            var marioVisual = GameBody(player.transform, "Assets/QuantumUser/Resources/EntityPrototypes/Player/PlayerMario.prefab", 0.92f);
            pctrl.animator = marioVisual ? marioVisual.GetComponentInChildren<Animator>(true) : null;
            var pSpeaker = player.AddComponent<WorldSpeaker>();
            pSpeaker.keyword = "DAVID";
            pSpeaker.bubbleHeight = 1.35f;

            var pSfx = player.AddComponent<AudioSource>();
            pSfx.playOnAwake = false;
            pSfx.spatialBlend = 0f;
            pctrl.sfx = pSfx;
            pctrl.jumpClip = Clip("Assets/Sound/player/jump.ogg");
            pctrl.crouchClip = Clip("Assets/Sound/player/crouch.ogg");
            pctrl.skidClip = Clip("Assets/Sound/player/slide.ogg");
            pctrl.groundpoundStart = Clip("Assets/Sound/player/groundpound_start.ogg");
            pctrl.groundpoundLand = Clip("Assets/Sound/player/groundpound_landing.ogg");
            pctrl.footsteps = new[] {
                Clip("Assets/Sound/player/walk/grass_1.ogg"),
                Clip("Assets/Sound/player/walk/grass_2.ogg"),
            };

            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.72f, 1f);
            cam.farClipPlane = 400f;
            camGo.transform.position = new Vector3(0f, 2.5f, -16f);
            var follow = camGo.AddComponent<WorldCamera>();
            follow.target = player.transform;
            follow.swapClip = Clip("Assets/Sound/ui/camera_scroll.ogg");
            pctrl.cam = camGo.transform;

            var npc = new GameObject("Companion");
            npc.transform.position = new Vector3(1.4f, 0.6f, -13f);
            var ncap = npc.AddComponent<CapsuleCollider>();
            ncap.height = 1f;
            ncap.radius = 0.22f;
            ncap.center = Vector3.up * 0.52f;
            var nrb = npc.AddComponent<Rigidbody>();
            nrb.isKinematic = true;
            nrb.useGravity = false;
            npc.AddComponent<WorldMotor>();
            var comp = npc.AddComponent<CompanionNPC>();
            comp.player = player.transform;
            var luigiVisual = GameBody(npc.transform, "Assets/QuantumUser/Resources/EntityPrototypes/Player/PlayerLuigi.prefab", 0.97f);
            comp.animator = luigiVisual ? luigiVisual.GetComponentInChildren<Animator>(true) : null;
            var nSpeaker = npc.AddComponent<WorldSpeaker>();
            nSpeaker.keyword = "ERIK";
            nSpeaker.bubbleHeight = 1.5f;

            BuildBubble();
            BuildControlsCard();

            // ---- doors, as properly sized warp pipes ---------------------------
            Portal("Door-HawaiiOS", new Vector3(6f, 0f, -14f), NeonBlue, "HawaiiOS\n<size=55%>press E</size>", null, "https://erikgaren.com/os");
            Portal("Door-Portfolio", new Vector3(-6f, 0f, -14f), NeonCyan, "THE CV\n<size=55%>press E</size>", null, "https://erikgaren.com/");
            Portal("Door-Versus", new Vector3(0f, 2.7f, 22f), NeonRed, "VERSUS\n<size=55%>the classic — press E</size>", "MainMenu", null);
            Slab("VersusPad", new Vector3(0f, 2.2f, 22f), new Vector3(5f, 1f, 5f), BrickMat);

            // ---- the story, told where you meet it -----------------------------
            Trigger(new Vector3(0f, 1f, -9f), new Vector3(30f, 3f, 3f),
                "ERIK (luigi.tmp)|Oh! A visitor. Welcome to the World — the walkable half of erikgaren.com.",
                "DAVID (mario.tmp)|I'm David. Erik is also me, long story. We're wearing plumbers until the real models are sculpted. Nintendo: temporary, honest.",
                "ERIK (luigi.tmp)|Press C to swap views. Free 3D lets you wander; the side view snaps us back to 2.5D — 3D world, gameplay on one plane, the way this game has always worked.");
            Trigger(new Vector3(-13f, 3f, -1f), new Vector3(12f, 4f, 12f),
                "ERIK (luigi.tmp)|Up here is the day job: at a BMW supplier he built the team's pre-delivery QA tool — 1,300 automated checks per release candidate.",
                "DAVID (mario.tmp)|Python, Qt, a web dashboard, packaged as a Windows app. It caught real defects before they shipped.");
            Trigger(new Vector3(0f, -3.5f, 0f), new Vector3(22f, 4f, 14f),
                "DAVID (mario.tmp)|Down here is where the reverse-engineering happens: a PS4 binary rehosted natively, shaders translated to SPIR-V, a Vulkan renderer I wrote.",
                "ERIK (luigi.tmp)|He fixed hair that emulators get wrong and wrote it up. Frame 1377. He mentions it a lot.");
            Trigger(new Vector3(0f, 1f, 30f), new Vector3(40f, 4f, 6f),
                "ERIK (luigi.tmp)|Those walls behind me are real geometry from the Versus stages, lifted out of the game's own level data.",
                "DAVID (mario.tmp)|The pipe on the platform takes you to the classic mode — real multiplayer, my own server. Bring a friend.");

            // ---- baked floor ----------------------------------------------------
            var mapGo = new GameObject("Heightmap");
            var hm = mapGo.AddComponent<WorldHeightmap>();
            hm.originX = MapOriginX;
            hm.originZ = MapOriginZ;
            hm.width = MapWidth;
            hm.depth = MapDepth;
            hm.tops = Bake;

            DevFact(new Vector3(-4f, 3.4f, -6f), "// placeholder plumbers\n// legal says hi");
            DevFact(new Vector3(12f, 4.2f, -8f), "assert(checks_passed == 1300);");
            DevFact(new Vector3(0f, -2.4f, 0f), "frame 1377: the hair rendered.");

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/WorldHub.unity");
        }

        // ---------------------------------------------------------------- pieces

        // A floor/platform: one box, tiled texture, and its top baked into the map.
        private static void Slab(string name, Vector3 pos, Vector3 size, Material mat) {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            // Tile the texture by world size so blocks read as tiles, not stripes.
            var block = new MaterialPropertyBlock();
            block.SetVector("_BaseMap_ST", new Vector4(size.x, size.z, 0f, 0f));
            r.SetPropertyBlock(block);
            go.isStatic = true;
            RecordTop(pos, size);
        }

        private static void Block(string name, Vector3 pos, Vector3 size, Material mat) {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
        }

        private static void RecordTop(Vector3 pos, Vector3 size) {
            float top = pos.y + size.y / 2f;
            int x0 = Mathf.FloorToInt(pos.x - size.x / 2f - MapOriginX);
            int x1 = Mathf.CeilToInt(pos.x + size.x / 2f - MapOriginX);
            int z0 = Mathf.FloorToInt(pos.z - size.z / 2f - MapOriginZ);
            int z1 = Mathf.CeilToInt(pos.z + size.z / 2f - MapOriginZ);
            for (int z = Mathf.Max(0, z0); z < Mathf.Min(MapDepth, z1); z++) {
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(MapWidth, x1); x++) {
                    int i = z * MapWidth + x;
                    if (float.IsNegativeInfinity(Bake[i]) || top > Bake[i]) {
                        Bake[i] = top;
                    }
                }
            }
        }

        // A slice of a real Versus stage, one unit deep, standing as scenery.
        private static void ExtrudePanel(string scenePath, Vector3 origin, int sliceWidth, int sliceHeight) {
            var level = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try {
                UnityEngine.Tilemaps.Tilemap best = null;
                int bestCount = 0;
                foreach (var root in level.GetRootGameObjects()) {
                    foreach (var tm in root.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true)) {
                        tm.CompressBounds();
                        int count = tm.GetUsedTilesCount();
                        if (count > bestCount) {
                            best = tm;
                            bestCount = count;
                        }
                    }
                }
                if (!best) {
                    return;
                }
                var b = best.cellBounds;
                int x0 = b.xMin + 2;
                int x1 = Mathf.Min(b.xMax, x0 + sliceWidth);
                int y0 = b.yMin;
                int y1 = Mathf.Min(b.yMax, y0 + sliceHeight);

                var parent = new GameObject("Gallery-" + Path.GetFileNameWithoutExtension(scenePath));
                parent.transform.position = origin;
                var cache = new Dictionary<Sprite, Material>();
                for (int lx = x0; lx < x1; lx++) {
                    for (int ly = y0; ly < y1; ly++) {
                        var sprite = best.GetSprite(new Vector3Int(lx, ly, 0));
                        if (!sprite) {
                            continue;
                        }
                        if (!cache.TryGetValue(sprite, out var mat)) {
                            mat = SpriteMat(sprite);
                            cache[sprite] = mat;
                        }
                        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.name = "t";
                        cube.transform.SetParent(parent.transform, false);
                        cube.transform.localPosition = new Vector3(lx - x0 + 0.5f, ly - y0 + 0.5f, 0f);
                        cube.GetComponent<Renderer>().sharedMaterial = mat;
                        Object.DestroyImmediate(cube.GetComponent<Collider>());
                        cube.isStatic = true;
                    }
                }
            } finally {
                EditorSceneManager.CloseScene(level, true);
            }
        }

        // --------------------------------------------------------------- visuals

        // Crops one sprite out of its atlas into a standalone repeating texture,
        // so a scaled box shows tiles instead of a stretched atlas.
        private static Material TileMat(string atlasPath, string spriteName, string key) {
            string outPath = "Assets/World/Generated/tile_" + key + ".png";
            Directory.CreateDirectory("Assets/World/Generated");
            if (!File.Exists(outPath)) {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>().ToArray();
                if (sprites.Length == 0) {
                    return Mat(new Color(0.5f, 0.35f, 0.2f));
                }
                Sprite pick = spriteName != null
                    ? sprites.FirstOrDefault(s => s.name == spriteName) ?? sprites[0]
                    : sprites.OrderByDescending(s => s.rect.y).ThenBy(s => s.rect.x).First();

                // Crop from the source PNG, not sprite.texture — that one can be
                // a packed atlas whose importer is not a TextureImporter.
                var importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
                var src = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
                if (importer == null || src == null) {
                    return Mat(new Color(0.5f, 0.35f, 0.2f));
                }
                bool wasReadable = importer.isReadable;
                if (!wasReadable) {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    src = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
                }
                var rect = pick.rect;
                var pixels = src.GetPixels((int) rect.x, (int) rect.y, (int) rect.width, (int) rect.height);
                var tex = new Texture2D((int) rect.width, (int) rect.height, TextureFormat.RGBA32, false);
                tex.SetPixels(pixels);
                tex.Apply();
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                if (!wasReadable) {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
                AssetDatabase.ImportAsset(outPath);
                if (AssetImporter.GetAtPath(outPath) is TextureImporter outImporter) {
                    outImporter.wrapMode = TextureWrapMode.Repeat;
                    outImporter.filterMode = FilterMode.Point;
                    outImporter.textureCompression = TextureImporterCompression.Uncompressed;
                    outImporter.SaveAndReimport();
                }
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 0f);
            return material;
        }

        private static Material SpriteMat(Sprite sprite) {
            if (!sprite) {
                return Mat(new Color(0.55f, 0.35f, 0.16f));
            }
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", sprite.texture);
            Rect r = sprite.textureRect;
            mat.SetTextureScale("_BaseMap", new Vector2(r.width / sprite.texture.width, r.height / sprite.texture.height));
            mat.SetTextureOffset("_BaseMap", new Vector2(r.x / sprite.texture.width, r.y / sprite.texture.height));
            mat.SetFloat("_Smoothness", 0f);
            return mat;
        }

        private static Material Mat(Color c) {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Smoothness", 0f);
            return mat;
        }

        private static void Backdrop(string name, Vector3 pos, Quaternion rot, Vector2 size) {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Level Backgrounds/grass-sky.png");
            if (!tex) {
                return;
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetTexture("_BaseMap", tex);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
        }

        // --------------------------------------------------------------- bodies

        private static GameObject GameBody(Transform parent, string prefabPath, float targetHeight) {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!prefab) {
                return null;
            }
            var visual = (GameObject) PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            visual.name = "Visual";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            foreach (var mb in visual.GetComponentsInChildren<MonoBehaviour>(true)) {
                if (mb) {
                    Object.DestroyImmediate(mb, true);
                }
            }
            foreach (var col in visual.GetComponentsInChildren<Collider>(true)) {
                Object.DestroyImmediate(col, true);
            }
            foreach (var rb in visual.GetComponentsInChildren<Rigidbody>(true)) {
                Object.DestroyImmediate(rb, true);
            }

            var animator = visual.GetComponentInChildren<Animator>(true);
            if (!animator) {
                Object.DestroyImmediate(visual);
                return null;
            }
            animator.applyRootMotion = false;

            foreach (var r in visual.GetComponentsInChildren<Renderer>(true)) {
                string n = r.name.ToLowerInvariant();
                if (n.Contains("shell") || n.Contains("helmet") || n.Contains("propeller") || n.Contains("goldblock")) {
                    r.gameObject.SetActive(false);
                }
            }

            var renderers = visual.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length > 0) {
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) {
                    b.Encapsulate(r.bounds);
                }
                if (b.size.y > 0.001f) {
                    visual.transform.localScale = Vector3.one * (targetHeight / b.size.y);
                    b = renderers[0].bounds;
                    foreach (var r in renderers) {
                        b.Encapsulate(r.bounds);
                    }
                    visual.transform.localPosition = new Vector3(0f, parent.position.y - b.min.y + 0.02f, 0f);
                }
            }
            return visual;
        }

        private static AudioClip Clip(string path) {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ------------------------------------------------------------------- UI

        private static void BuildControlsCard() {
            var canvasGo = new GameObject("ControlsCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGo.transform, false);
            var img = panel.AddComponent<Image>();
            img.sprite = SpriteAsset("Assets/Sprites/UI/Menu/Elements/rounded-rect-5px.png");
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.4f;
            img.color = new Color(0.06f, 0.08f, 0.13f, 0.88f);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0f, 1f);
            prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(26f, -26f);
            prt.sizeDelta = new Vector2(452f, 232f);

            var text = MakeUguiText(panel.transform, "Controls",
                "MOVE  arrows / WASD / stick\n" +
                "JUMP  Space / Z / A — again on landing: double, triple\n" +
                "RUN  hold Shift / X\n" +
                "CROUCH  hold down · GROUND POUND  down in mid-air\n" +
                "TALK, ENTER PIPE  E / C / RB\n" +
                "SWAP VIEW  Q / V / LB — free 3D or 2.5D side-on\n" +
                "BACK TO MENU  Esc / Start\n" +
                "<size=80%>same scheme as Versus — rebind it in Options</size>",
                20f, Vector2.zero, new Vector2(420f, 206f), Color.white);
            text.alignment = TextAlignmentOptions.TopLeft;

            var title = MakeUguiText(canvasGo.transform, "Title",
                "John Hawaii B. Luga's World  ·  dev build", 20f,
                new Vector2(-26f, -30f), new Vector2(560f, 30f), new Color(1f, 1f, 1f, 0.75f));
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(1f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(1f, 1f);
            title.alignment = TextAlignmentOptions.TopRight;
        }

        // The speech bubble is a world-space canvas wearing the game's own
        // 9-sliced dialogue panel, its font and its button prompt.
        private static void BuildBubble() {
            var holder = new GameObject("Dialogue");
            var dlg = holder.AddComponent<WorldDialogue>();

            var bubble = new GameObject("Bubble");
            bubble.transform.SetParent(holder.transform, false);
            var canvas = bubble.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var crt = bubble.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(520f, 190f);
            crt.localScale = Vector3.one * 0.008f;

            var panel = new GameObject("Panel");
            panel.transform.SetParent(bubble.transform, false);
            var panelImg = panel.AddComponent<Image>();
            panelImg.sprite = SpriteAsset("Assets/Sprites/UI/Menu/Elements/rounded-rect-5px-dialogue.png");
            panelImg.type = Image.Type.Sliced;
            panelImg.pixelsPerUnitMultiplier = 0.35f;
            panelImg.color = new Color(1f, 1f, 1f, 0.97f);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            var nameTmp = MakeUguiText(panel.transform, "Name", "", 30f,
                new Vector2(18f, -16f), new Vector2(420f, 34f), Color.white);
            nameTmp.alignment = TextAlignmentOptions.TopLeft;
            var nrt = nameTmp.rectTransform;
            nrt.anchorMin = new Vector2(0f, 1f);
            nrt.anchorMax = new Vector2(0f, 1f);
            nrt.pivot = new Vector2(0f, 1f);

            var lineTmp = MakeUguiText(panel.transform, "Line", "", 25f,
                new Vector2(18f, -54f), new Vector2(470f, 118f), new Color(0.09f, 0.09f, 0.12f));
            lineTmp.alignment = TextAlignmentOptions.TopLeft;
            var lrt = lineTmp.rectTransform;
            lrt.anchorMin = new Vector2(0f, 1f);
            lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 1f);

            var promptGo = new GameObject("Prompt");
            promptGo.transform.SetParent(panel.transform, false);
            var prompt = promptGo.AddComponent<Image>();
            prompt.sprite = SpriteAsset("Assets/Sprites/UI/Menu/Elements/a-prompt.png");
            prompt.preserveAspect = true;
            var prt2 = promptGo.GetComponent<RectTransform>();
            prt2.anchorMin = new Vector2(1f, 0f);
            prt2.anchorMax = new Vector2(1f, 0f);
            prt2.pivot = new Vector2(1f, 0f);
            prt2.anchoredPosition = new Vector2(-16f, 14f);
            prt2.sizeDelta = new Vector2(42f, 42f);

            var voice = holder.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.volume = 0.75f;

            dlg.bubble = bubble;
            dlg.nameText = nameTmp;
            dlg.lineText = lineTmp;
            dlg.prompt = prompt;
            dlg.voice = voice;
            dlg.typeClip = Clip("Assets/Sound/ui/chat_keydown.wav");
            dlg.openClip = Clip("Assets/Sound/ui/windowopen.ogg");
            dlg.doneClip = Clip("Assets/Sound/ui/chat_fulltype.wav");
            bubble.SetActive(false);
        }

        private static Sprite SpriteAsset(string path) {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        }

        private static TextMeshProUGUI MakeUguiText(Transform parent, string name, string text, float size, Vector2 pos, Vector2 dims, Color c) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (GameFont) {
                tmp.font = GameFont;
            }
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = c;
            tmp.rectTransform.anchoredPosition = pos;
            tmp.rectTransform.sizeDelta = dims;
            return tmp;
        }

        // ---------------------------------------------------------------- world

        private static void FloatingLabel(Transform parent, string text, float height) {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * height;
            var tmp = go.AddComponent<TextMeshPro>();
            if (GameFont) {
                tmp.font = GameFont;
            }
            tmp.text = text;
            tmp.fontSize = 1.1f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(4.5f, 1.2f);
            go.AddComponent<Billboard>();
        }

        private static void DevFact(Vector3 pos, string text) {
            var go = new GameObject("DevFact");
            go.transform.position = pos;
            var tmp = go.AddComponent<TextMeshPro>();
            if (GameFont) {
                tmp.font = GameFont;
            }
            tmp.text = text;
            tmp.fontSize = 0.85f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.1f, 0.22f, 0.09f, 0.85f);
            tmp.rectTransform.sizeDelta = new Vector2(6.5f, 2f);
            go.AddComponent<Billboard>();
        }

        private static void Trigger(Vector3 pos, Vector3 size, params string[] lines) {
            var go = new GameObject("Story");
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var trig = go.AddComponent<WorldDialogueTrigger>();
            trig.lines = lines;
        }

        private static void Portal(string name, Vector3 pos, Color c, string label, string sceneName, string url) {
            var root = new GameObject(name);
            root.transform.position = pos;
            Pipe(root.transform, c);
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.7f, 0f);
            box.size = new Vector3(2.2f, 1.8f, 2.2f);
            var portal = root.AddComponent<WorldPortal>();
            portal.sceneName = sceneName;
            portal.url = url;
            portal.enterClip = Clip("Assets/Sound/ui/start_game.ogg");
            FloatingLabel(root.transform, label, 2.1f);
        }

        // A warp pipe at NSMB proportions: about two tiles wide, one and a bit tall.
        private static void Pipe(Transform parent, Color accent) {
            Color body = Color.Lerp(new Color(0.16f, 0.62f, 0.19f), accent, 0.28f);
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Shaft";
            shaft.transform.SetParent(parent, false);
            shaft.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            shaft.transform.localScale = new Vector3(0.9f, 0.45f, 0.9f);
            shaft.GetComponent<Renderer>().sharedMaterial = Mat(body);
            var lip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lip.name = "Lip";
            lip.transform.SetParent(parent, false);
            lip.transform.localPosition = new Vector3(0f, 0.98f, 0f);
            lip.transform.localScale = new Vector3(1.1f, 0.1f, 1.1f);
            lip.GetComponent<Renderer>().sharedMaterial = Mat(body * 1.15f);
            var mouth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mouth.name = "Mouth";
            mouth.transform.SetParent(parent, false);
            mouth.transform.localPosition = new Vector3(0f, 1.06f, 0f);
            mouth.transform.localScale = new Vector3(0.9f, 0.03f, 0.9f);
            mouth.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.05f, 0.12f, 0.06f));
            Object.DestroyImmediate(shaft.GetComponent<Collider>());
            Object.DestroyImmediate(lip.GetComponent<Collider>());
            Object.DestroyImmediate(mouth.GetComponent<Collider>());
        }

        // ------------------------------------------------------------ build list

        private static void RegisterScenes() {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path.Contains("WorldEntry") || s.path.Contains("WorldHub"));
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/WorldHub.unity", true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
