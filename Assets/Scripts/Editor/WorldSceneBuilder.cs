using NSMB.World;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace NSMB.WorldEditor {
    // Assembles the World's scenes from code so the whole restructure stays
    // reviewable, repeatable and headless-buildable. Run via Tools menu or
    // -executeMethod NSMB.WorldEditor.WorldSceneBuilder.BuildScenes.
    public static class WorldSceneBuilder {

        private static readonly Color Night = new(0.043f, 0.063f, 0.09f);
        private static readonly Color NeonRed = new(0.898f, 0.078f, 0f);
        private static readonly Color NeonBlue = new(0.059f, 0.424f, 0.741f);
        private static readonly Color NeonViolet = new(0.415f, 0f, 1f);
        private static readonly Color NeonCyan = new(0f, 0.784f, 1f);

        // The game's own UI font, so every piece of World text speaks MvL.
        private static TMP_FontAsset GameFont;

        [MenuItem("Tools/World/Build Scenes")]
        public static void BuildScenes() {
            GameFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/BoldFont.asset");
            BuildHubScene();
            RegisterScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[WorldSceneBuilder] WorldHub built and registered; the game's own menu is the front door.");
        }

        // -------------------------------------------------------------------- hub

        private static void BuildHubScene() {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NSMB daylight, straight out of the source material.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.72f, 0.74f, 0.78f);
            RenderSettings.fog = false;

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = new Color(1f, 0.98f, 0.92f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // ------------------------------------------------------------------
            // The test map: slices of the game's actual Versus stages, extruded
            // from their tilemaps into 3D corridors laid end to end. Native tile
            // scale — one tile is one unit — so the sim's movement numbers apply.
            // ------------------------------------------------------------------
            Material grassMat = SpriteMat(TopRowSprite("Assets/Sprites/Atlases/Terrain/grass.png"));
            Material blockMat = SpriteMat(FirstSprite("Assets/Sprites/Atlases/Terrain/animated-blocks.png", "animation_0"));

            float zCursor = 0f;
            var sectionStarts = new List<float>();
            BakeOrigin = -8f;
            Bake = new float[900];
            for (int i = 0; i < Bake.Length; i++) {
                Bake[i] = -7.5f;
            }
            string[] stages = {
                "Assets/Scenes/Levels/DefaultGrassLevel.unity",
                "Assets/Scenes/Levels/DefaultBrickLevel.unity",
                "Assets/Scenes/Levels/DefaultCastle.unity",
                "Assets/Scenes/Levels/DefaultPipes.unity",
                "Assets/Scenes/Levels/DefaultSnow.unity",
            };
            foreach (string stagePath in stages) {
                sectionStarts.Add(zCursor);
                zCursor = ExtrudeSection(stagePath, zCursor, 34) + 2f;
                // Connector floor between stages, and a safety pit floor below.
                for (int i = 0; i < 6; i++) {
                    TexBlock("Connector", new Vector3(0, -0.5f, zCursor + i), new Vector3(Lane, 1f, 1f), grassMat);
                }
                zCursor += 6f;
            }
            float worldEnd = zCursor + 8f;
            TexBlock("SpawnPad", new Vector3(0, -0.5f, -2f), new Vector3(Lane, 1f, 6f), grassMat);
            var pit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pit.name = "CatchFloor";
            pit.transform.position = new Vector3(0, -8f, worldEnd / 2f);
            pit.transform.localScale = new Vector3(Lane * 3f, 1f, worldEnd + 40f);
            pit.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.1f, 0.08f, 0.1f), false);
            pit.isStatic = true;

            // A few floating ?-blocks of our own between stages.
            foreach (float z in new[] { sectionStarts[0] + 10f, sectionStarts[1] + 8f, sectionStarts[3] + 12f }) {
                TexBlock("QBlock", new Vector3(0f, 2.6f, z), Vector3.one, blockMat);
            }

            // The game's own sky wraps the horizon; results.ogg loops per the
            // director's pick.
            Backdrop("SkyEnd", new Vector3(0, 18f, worldEnd + 30f), Quaternion.identity, new Vector2(220, 60));
            Backdrop("SkyL", new Vector3(-30f, 18f, worldEnd / 2f), Quaternion.Euler(0, 90, 0), new Vector2(worldEnd + 80, 60));
            Backdrop("SkyR", new Vector3(30f, 18f, worldEnd / 2f), Quaternion.Euler(0, -90, 0), new Vector2(worldEnd + 80, 60));

            var musicGo = new GameObject("Music");
            var music = musicGo.AddComponent<AudioSource>();
            music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/music/results.ogg");
            music.loop = true;
            music.playOnAwake = true;
            music.volume = 0.45f;
            music.spatialBlend = 0f;

            // Player: Mario at native scale (the sim's large hitbox is 0.82
            // tiles), on the sim's own movement numbers.
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0, 0.4f, -2f);
            var pcap = player.AddComponent<CapsuleCollider>();
            pcap.height = 0.95f;
            pcap.radius = 0.22f;
            pcap.center = Vector3.up * 0.5f;
            var prb = player.AddComponent<Rigidbody>();
            prb.isKinematic = true;
            prb.useGravity = false;
            player.AddComponent<WorldMotor>();
            var pctrl = player.AddComponent<WorldPlayerController>();
            // The game's own gameplay entity prefab — its animator, avatar and
            // materials already wired the way the Versus mode uses them.
            var marioVisual = GameBody(player.transform, "Assets/QuantumUser/Resources/EntityPrototypes/Player/PlayerMario.prefab", 0.92f);
            pctrl.animator = marioVisual ? marioVisual.GetComponentInChildren<Animator>(true) : null;
            if (!pctrl.animator) {
                marioVisual = Body(player.transform, "Assets/Models/Players/mario_big/mario_big_exported.fbx", NeonRed, 0.92f);
                pctrl.animator = WireAnimator(marioVisual, "Assets/Animations/Player/Mario/LargeMario.controller");
                Dress(marioVisual, "Assets/Materials/3d/mario/mat_mario_big.mat", "Assets/Materials/3d/mario/mat_mario_eyes.mat");
            }
            var pSpeaker = player.AddComponent<WorldSpeaker>();
            pSpeaker.keyword = "DAVID";
            pSpeaker.bubbleHeight = 1.35f;

            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.72f, 1f);
            camGo.transform.position = new Vector3(0, 1.9f, -5f);
            var follow = camGo.AddComponent<WorldCamera>();
            follow.target = player.transform;
            follow.offset = new Vector3(0f, 1.9f, -3.3f);
            follow.lookHeight = 0.65f;
            pctrl.cam = camGo.transform;

            // Player two: Luigi on the same physics, a real entity that follows.
            var npc = new GameObject("Companion");
            npc.transform.position = new Vector3(1.2f, 0.4f, -3f);
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
            if (!comp.animator) {
                luigiVisual = Body(npc.transform, "Assets/Models/Players/luigi_big/luigi_big.fbx", new Color(0.1f, 0.65f, 0.25f), 0.97f);
                comp.animator = WireAnimator(luigiVisual, "Assets/Animations/Player/Luigi/LargeLuigi.overrideController");
                Dress(luigiVisual, "Assets/Materials/3d/luigi/mat_luigi_big.mat", "Assets/Materials/3d/luigi/mat_luigi_eyes.mat");
            }
            FloatingLabel(npc.transform, "ERIK\n<size=55%>narrator.exe — dev build</size>", 1.75f);
            var nSpeaker = npc.AddComponent<WorldSpeaker>();
            nSpeaker.keyword = "ERIK";
            nSpeaker.bubbleHeight = 1.5f;

            // Speech bubbles with the game's font and its chat SFX.
            BuildBubble();

            // Story beats at each stage section — a conversation between the two
            // of them, placeholder bodies acknowledged in-fiction.
            Trigger(new Vector3(0, 1, sectionStarts[0] + 3f),
                "ERIK (luigi.tmp)|Oh! A visitor! Welcome to the World. I'm Erik — or I will be, once the boss sculpts me a body. For now I'm, um. Borrowing Luigi.",
                "DAVID (mario.tmp)|And I'm David — same person as Erik, long story, good lore. Currently shaped like a certain plumber. Nintendo, if you're reading this: placeholders! Temporary! Don't sue, pweaseeee.",
                "ERIK (luigi.tmp)|He's serious, there's a roadmap and everything. This test map is stitched from the Versus stages — walk on, we'll explain him as we go.");
            Trigger(new Vector3(0, 1, sectionStarts[1] + 3f),
                "ERIK (luigi.tmp)|Day job: at a BMW supplier he designed the team's pre-delivery QA tool. 1,300 automated checks on 3D vehicle data, every single release.",
                "DAVID (mario.tmp)|It caught real defects before they shipped. I was 21 when I built it. Still am, actually.");
            Trigger(new Vector3(0, 1, sectionStarts[2] + 3f),
                "ERIK (luigi.tmp)|Night job: he reverse-engineers console games. PS4 binary, shaders translated to SPIR-V, his own Vulkan renderer. He fixed hair that emulators get wrong.",
                "DAVID (mario.tmp)|Frame 1377. The hair rendered. I told everyone. Repeatedly.",
                "ERIK (luigi.tmp)|The green pipes are doors — stand on top of the story and press E. One leads to HawaiiOS, the operating system he built for this website.");
            Trigger(new Vector3(0, 1, sectionStarts[3] + 3f),
                "ERIK (luigi.tmp)|He sculpts too — Blender, EEVEE, Yakuza and Final Fantasy things. Which is how we eventually get faces that aren't... these.",
                "DAVID (mario.tmp)|The pipe at the end runs the classic Versus game this World grew out of — by ipodtouch0218 and contributors, used with permission. Bring a friend, it's real multiplayer on my own server.");

            // Doors: warp pipes on the connectors between stages.
            Portal("Door-HawaiiOS", new Vector3(2.4f, 0, sectionStarts[1] - 5f), NeonBlue, "HawaiiOS\n<size=55%>his operating system — press E</size>", null, "https://erikgaren.com/os");
            Portal("Door-Portfolio", new Vector3(-2.4f, 0, sectionStarts[2] - 5f), NeonCyan, "THE CV\n<size=55%>recruiter door — press E</size>", null, "https://erikgaren.com/");
            Portal("Door-Versus", new Vector3(0f, 0, worldEnd - 3f), NeonRed, "THE ARCADE\n<size=55%>versus — the classic, press E</size>", "MainMenu", null);

            // Physics-independent ground truth for the motor's fallback.
            var mapGo = new GameObject("Heightmap");
            var hm = mapGo.AddComponent<WorldHeightmap>();
            hm.zOrigin = BakeOrigin;
            hm.floorTops = Bake.Take(Mathf.CeilToInt(worldEnd - BakeOrigin) + 16).ToArray();

            // Dev-room fun facts floating around.
            DevFact(new Vector3(-2.4f, 2.2f, sectionStarts[0] + 12f), "// TODO: replace placeholder plumbers\n// legal says hi");
            DevFact(new Vector3(2.6f, 2.6f, sectionStarts[1] + 12f), "assert(checks_passed == 1300); // every release");
            DevFact(new Vector3(-2.6f, 2.8f, sectionStarts[2] + 12f), "frame 1377: the hair finally rendered.\nnobody saw. everybody was told.");
            DevFact(new Vector3(2.4f, 2.2f, sectionStarts[4] + 12f), "wagata, yondaime!");

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/WorldHub.unity");
        }

        // ---------------------------------------------------------------- helpers

        // Instantiates a model normalised to a target height with its feet at the
        // parent's origin — import scale on these FBXes is wildly off (the first
        // build had the camera standing inside Mario's boot).
        private static GameObject Body(Transform parent, string fbxPath, Color fallback, float targetHeight) {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            GameObject visual;
            if (model) {
                visual = (GameObject) PrefabUtility.InstantiatePrefab(model);
                visual.transform.SetParent(parent, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                var renderers = visual.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0) {
                    Bounds b = renderers[0].bounds;
                    foreach (var r in renderers) {
                        b.Encapsulate(r.bounds);
                    }
                    if (b.size.y > 0.001f) {
                        float k = targetHeight / b.size.y;
                        visual.transform.localScale = Vector3.one * k;
                        // Recompute after scaling, then drop the feet onto the origin.
                        b = renderers[0].bounds;
                        foreach (var r in renderers) {
                            b.Encapsulate(r.bounds);
                        }
                        float lift = parent.position.y - b.min.y + 0.02f;
                        visual.transform.localPosition = new Vector3(0f, lift, 0f);
                    }
                    // The FBX ships unmaterialed (the game assigns these in its
                    // prefabs) and must never bring colliders of its own.
                    foreach (var col in visual.GetComponentsInChildren<Collider>()) {
                        Object.DestroyImmediate(col);
                    }
                }
            } else {
                visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.transform.SetParent(parent, false);
                visual.transform.localPosition = Vector3.up;
                visual.GetComponent<Renderer>().sharedMaterial = Mat(fallback, false);
            }
            visual.name = "Visual";
            return visual;
        }

        // The game's material set for a body: eyes renderers get the eye material,
        // everything else wears the body material.
        private static void Dress(GameObject visual, string bodyMatPath, string eyesMatPath) {
            var body = AssetDatabase.LoadAssetAtPath<Material>(bodyMatPath);
            var eyes = AssetDatabase.LoadAssetAtPath<Material>(eyesMatPath);
            if (!body) {
                return;
            }
            foreach (var r in visual.GetComponentsInChildren<Renderer>()) {
                bool isEyes = eyes && r.name.ToLowerInvariant().Contains("eye");
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) {
                    mats[i] = isEyes ? eyes : body;
                }
                r.sharedMaterials = mats;
            }
        }

        // Instantiates the game's own player entity prefab and strips everything
        // that needs a running Quantum simulation, keeping the rig: Animator,
        // avatar, meshes and materials exactly as the Versus mode renders them.
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

            // Quantum views, the gameplay animator driver, colliders, physics.
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
            if (!animator.runtimeAnimatorController) {
                animator.runtimeAnimatorController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Player/Mario/LargeMario.controller");
            }

            // The gameplay prefab hides power-up props (shells, helmets) until
            // the sim enables them; without the sim they would float in place.
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

        private static Animator WireAnimator(GameObject visual, string controllerPath) {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (!controller) {
                return null;
            }
            var animator = visual.GetComponentInChildren<Animator>();
            if (!animator) {
                animator = visual.AddComponent<Animator>();
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            return animator;
        }

        private static Material Mat(Color c, bool emissive) {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = c };
            mat.SetColor("_BaseColor", c);
            if (emissive) {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 3.2f);
            }
            return mat;
        }

        private static GameObject Block(string name, Vector3 pos, Vector3 size, Color c) {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Mat(c, false);
            return go;
        }

        private static GameObject Glow(string name, Vector3 pos, Vector3 size, Color c) {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Mat(c, true);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void FloatingLabel(Transform parent, string text, float height) {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * height;
            var tmp = go.AddComponent<TextMeshPro>();
            if (GameFont) {
                tmp.font = GameFont;
            }
            tmp.text = text;
            tmp.fontSize = 1.15f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(4.5f, 1.2f);
            go.AddComponent<Billboard>();
        }

        private static void DevFact(Vector3 pos, string text) {
            var go = new GameObject("DevFact");
            go.transform.position = pos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 1f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.12f, 0.25f, 0.1f, 0.9f);
            tmp.rectTransform.sizeDelta = new Vector2(6.5f, 2f);
            go.AddComponent<Billboard>();
        }

        private static void Trigger(Vector3 pos, params string[] lines) {
            var go = new GameObject("Story-" + pos.z);
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(Lane, 3f, 1.5f);
            var trig = go.AddComponent<WorldDialogueTrigger>();
            trig.lines = lines;
        }

        private static void Portal(string name, Vector3 pos, Color c, string label, string sceneName, string url) {
            var root = new GameObject(name);
            root.transform.position = pos;
            Pipe(root.transform, c);
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0, 0.9f, 0);
            box.size = new Vector3(2.4f, 2f, 2.4f);
            var portal = root.AddComponent<WorldPortal>();
            portal.sceneName = sceneName;
            portal.url = url;
            FloatingLabel(root.transform, label, 2.5f);
        }

        // A warp pipe — the only correct shape for a door in this universe. The
        // accent colour tints the classic pipe green so destinations read apart.
        private static void Pipe(Transform parent, Color accent) {
            Color body = Color.Lerp(new Color(0.18f, 0.65f, 0.2f), accent, 0.3f);
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Shaft";
            shaft.transform.SetParent(parent, false);
            shaft.transform.localPosition = new Vector3(0, 0.6f, 0);
            shaft.transform.localScale = new Vector3(1.25f, 0.6f, 1.25f);
            shaft.GetComponent<Renderer>().sharedMaterial = Mat(body, false);
            var lip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lip.name = "Lip";
            lip.transform.SetParent(parent, false);
            lip.transform.localPosition = new Vector3(0, 1.28f, 0);
            lip.transform.localScale = new Vector3(1.5f, 0.14f, 1.5f);
            lip.GetComponent<Renderer>().sharedMaterial = Mat(body * 1.15f, false);
            var mouth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mouth.name = "Mouth";
            mouth.transform.SetParent(parent, false);
            mouth.transform.localPosition = new Vector3(0, 1.37f, 0);
            mouth.transform.localScale = new Vector3(1.25f, 0.035f, 1.25f);
            mouth.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.05f, 0.12f, 0.06f), false);
            Object.DestroyImmediate(shaft.GetComponent<Collider>());
            Object.DestroyImmediate(lip.GetComponent<Collider>());
            Object.DestroyImmediate(mouth.GetComponent<Collider>());
        }

        private static Sprite TopRowSprite(string atlasPath) {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>().ToArray();
            if (sprites.Length == 0) {
                return null;
            }
            float topY = sprites.Max(s => s.rect.y);
            return sprites.Where(s => Mathf.Approximately(s.rect.y, topY)).OrderBy(s => s.rect.x).First();
        }

        private static Sprite FirstSprite(string atlasPath, string name) {
            return AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>()
                .FirstOrDefault(s => s.name == name);
        }

        private static Material SpriteMat(Sprite sprite) {
            if (!sprite) {
                return Mat(new Color(0.55f, 0.35f, 0.16f), false);
            }
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", sprite.texture);
            Rect r = sprite.textureRect;
            mat.SetTextureScale("_BaseMap", new Vector2(r.width / sprite.texture.width, r.height / sprite.texture.height));
            mat.SetTextureOffset("_BaseMap", new Vector2(r.x / sprite.texture.width, r.y / sprite.texture.height));
            mat.SetFloat("_Smoothness", 0f);
            return mat;
        }

        private static void TexBlock(string name, Vector3 pos, Vector3 size, Material mat) {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
            for (float z = pos.z - size.z / 2f; z < pos.z + size.z / 2f; z += 1f) {
                RecordFloor(z + 0.5f, pos.y + size.y / 2f);
            }
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

        private const float Lane = 8f;
        private static float[] Bake;
        private static float BakeOrigin;

        // Floor-like tops (ankle to head height) enter the baked heightmap.
        private static void RecordFloor(float z, float top) {
            if (Bake == null || top < -1f || top > 2.5f) {
                return;
            }
            int i = Mathf.FloorToInt(z - BakeOrigin);
            if (i >= 0 && i < Bake.Length) {
                Bake[i] = Mathf.Max(Bake[i], top);
            }
        }

        // Opens a level scene additively, finds its most-used tilemap (the solid
        // layer), and extrudes a horizontal slice of it into corridor geometry:
        // each tile becomes a lane-wide box, so the stage's side profile turns
        // into walkable 3D floors, steps, platforms and pits. Returns the z
        // where the slice ends.
        private static float ExtrudeSection(string scenePath, float zStart, int sliceWidth) {
            var level = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try {
                Tilemap best = null;
                int bestCount = 0;
                foreach (var root in level.GetRootGameObjects()) {
                    foreach (var tm in root.GetComponentsInChildren<Tilemap>(true)) {
                        tm.CompressBounds();
                        int count = tm.GetUsedTilesCount();
                        if (count > bestCount) {
                            best = tm;
                            bestCount = count;
                        }
                    }
                }
                if (!best) {
                    return zStart + sliceWidth;
                }

                var b = best.cellBounds;
                int x0 = b.xMin + 2;
                int x1 = Mathf.Min(b.xMax, x0 + sliceWidth);
                int yTop = Mathf.Min(b.yMax, b.yMin + 14);

                int floorY = int.MaxValue;
                for (int lx = x0; lx < x1; lx++) {
                    for (int ly = b.yMin; ly < yTop; ly++) {
                        if (best.GetSprite(new Vector3Int(lx, ly, 0))) {
                            floorY = Mathf.Min(floorY, ly);
                        }
                    }
                }
                if (floorY == int.MaxValue) {
                    return zStart + sliceWidth;
                }

                var parent = new GameObject("Stage-" + System.IO.Path.GetFileNameWithoutExtension(scenePath));
                var cache = new Dictionary<Sprite, Material>();
                for (int lx = x0; lx < x1; lx++) {
                    for (int ly = floorY; ly < yTop; ly++) {
                        Sprite sprite = best.GetSprite(new Vector3Int(lx, ly, 0));
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
                        // The slice's lowest tile row tops out at world y = 0.
                        cube.transform.position = new Vector3(0f, ly - floorY - 0.5f, zStart + (lx - x0) + 0.5f);
                        cube.transform.localScale = new Vector3(Lane, 1f, 1f);
                        cube.GetComponent<Renderer>().sharedMaterial = mat;
                        cube.isStatic = true;
                        RecordFloor(cube.transform.position.z, cube.transform.position.y + 0.5f);
                    }
                }
                return zStart + (x1 - x0);
            } finally {
                EditorSceneManager.CloseScene(level, true);
            }
        }

        // The over-head speech bubble: dark backing quad, name and line in the
        // game's font, and the game's chat SFX as the voice blip.
        private static void BuildBubble() {
            var holder = new GameObject("Dialogue");
            var dlg = holder.AddComponent<WorldDialogue>();

            var bubble = new GameObject("Bubble");
            bubble.transform.SetParent(holder.transform, false);

            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(back.GetComponent<Collider>());
            back.name = "Back";
            back.transform.SetParent(bubble.transform, false);
            back.transform.localScale = new Vector3(3.6f, 1.2f, 1f);
            var backMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            backMat.SetColor("_BaseColor", new Color(0.02f, 0.04f, 0.08f, 0.88f));
            backMat.SetFloat("_Surface", 1f);
            backMat.SetOverrideTag("RenderType", "Transparent");
            backMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            backMat.SetInt("_SrcBlend", (int) UnityEngine.Rendering.BlendMode.SrcAlpha);
            backMat.SetInt("_DstBlend", (int) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            backMat.SetInt("_ZWrite", 0);
            backMat.renderQueue = 3000;
            back.GetComponent<Renderer>().sharedMaterial = backMat;

            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(bubble.transform, false);
            nameGo.transform.localPosition = new Vector3(0f, 0.68f, -0.01f);
            var nameTmp = nameGo.AddComponent<TextMeshPro>();
            if (GameFont) {
                nameTmp.font = GameFont;
            }
            nameTmp.fontSize = 1.5f;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.rectTransform.sizeDelta = new Vector2(3.4f, 0.4f);

            var lineGo = new GameObject("Line");
            lineGo.transform.SetParent(bubble.transform, false);
            lineGo.transform.localPosition = new Vector3(0f, 0.02f, -0.01f);
            var lineTmp = lineGo.AddComponent<TextMeshPro>();
            if (GameFont) {
                lineTmp.font = GameFont;
            }
            lineTmp.fontSize = 1.1f;
            lineTmp.alignment = TextAlignmentOptions.Top;
            lineTmp.rectTransform.sizeDelta = new Vector2(3.35f, 1.05f);

            var voice = holder.AddComponent<AudioSource>();
            voice.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/ui/chat_fulltype.wav");
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.volume = 0.65f;

            dlg.bubble = bubble;
            dlg.nameText = nameTmp;
            dlg.lineText = lineTmp;
            dlg.voice = voice;
            bubble.SetActive(false);
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
            var rt = tmp.rectTransform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = dims;
            return tmp;
        }

        private static void MakeButton(Transform parent, string name, string label, Vector2 pos, Color c) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = c;
            go.AddComponent<Button>();
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(460, 84);
            var text = MakeUguiText(go.transform, "Label", label, 30, Vector2.zero, new Vector2(440, 60), Color.white);
            text.fontStyle = FontStyles.Bold;
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
