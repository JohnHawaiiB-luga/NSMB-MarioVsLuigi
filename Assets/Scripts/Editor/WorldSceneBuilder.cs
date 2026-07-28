using NSMB.World;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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
            BuildEntryScene();
            BuildHubScene();
            RegisterScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[WorldSceneBuilder] WorldEntry + WorldHub built and registered.");
        }

        // ------------------------------------------------------------------ entry

        private static void BuildEntryScene() {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Night;

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<WorldEntryMenu>();

            MakeUguiText(canvasGo.transform, "Eyebrow", "erikgaren.com presents", 26,
                new Vector2(0, 260), new Vector2(1200, 40), new Color(1, 1, 1, 0.55f));
            MakeUguiText(canvasGo.transform, "Title", "JOHN HAWAII B. LUGA'S WORLD", 72,
                new Vector2(0, 190), new Vector2(1600, 90), Color.white);
            MakeUguiText(canvasGo.transform, "Subtitle", "an explorable portfolio — dev build", 28,
                new Vector2(0, 120), new Vector2(1200, 40), new Color(1, 1, 1, 0.65f));

            MakeButton(canvasGo.transform, "EnterButton", "ENTER THE WORLD", new Vector2(0, -30), NeonRed);
            MakeButton(canvasGo.transform, "VersusButton", "VERSUS — the classic", new Vector2(0, -140), new Color(0.16f, 0.16f, 0.19f));

            MakeUguiText(canvasGo.transform, "Credit", "world under construction · versus mode is NSMB-MarioVsLuigi by ipodtouch0218 & contributors, used with permission", 18,
                new Vector2(0, -420), new Vector2(1700, 30), new Color(1, 1, 1, 0.4f));

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/WorldEntry.unity");
        }

        // -------------------------------------------------------------------- hub

        private static void BuildHubScene() {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NSMB grassland daylight, straight out of the source material.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.72f, 0.74f, 0.78f);
            RenderSettings.fog = false;

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = new Color(1f, 0.98f, 0.92f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Ground and cliffs: chunky blocks textured with the game's grassland
            // atlas — the exact tiles the Versus levels are built from.
            Material grassMat = SpriteMat(TopRowSprite("Assets/Sprites/Atlases/Terrain/grass.png"));
            Material blockMat = SpriteMat(FirstSprite("Assets/Sprites/Atlases/Terrain/animated-blocks.png", "animation_0"));

            for (int gx = -3; gx <= 3; gx++) {
                for (int gz = 0; gz < 56; gz++) {
                    TexBlock("Ground", new Vector3(gx * 4f, -2f, gz * 4f), new Vector3(4f, 4f, 4f), grassMat);
                }
            }
            for (int gz = 0; gz < 56; gz++) {
                TexBlock("CliffL", new Vector3(-16f, 0f, gz * 4f), new Vector3(4f, 8f, 4f), grassMat);
                TexBlock("CliffR", new Vector3(16f, 0f, gz * 4f), new Vector3(4f, 8f, 4f), grassMat);
            }

            // Question blocks floating at classic bonk height along the walk.
            foreach (float z in new[] { 24f, 26f, 28f, 70f, 110f, 112f, 150f, 190f }) {
                TexBlock("QBlock", new Vector3((z % 8f) - 4f, 3.4f, z), new Vector3(2f, 2f, 2f), blockMat);
            }

            // The game's own sky as the horizon, and its overworld theme in the air.
            Backdrop("SkyEnd", new Vector3(0, 24f, 240f), Quaternion.identity, new Vector2(260, 70));
            Backdrop("SkyL", new Vector3(-55f, 24f, 110f), Quaternion.Euler(0, 90, 0), new Vector2(300, 70));
            Backdrop("SkyR", new Vector3(55f, 24f, 110f), Quaternion.Euler(0, -90, 0), new Vector2(300, 70));

            var musicGo = new GameObject("Music");
            var music = musicGo.AddComponent<AudioSource>();
            music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/music/overworld.ogg");
            music.loop = true;
            music.playOnAwake = true;
            music.volume = 0.45f;
            music.spatialBlend = 0f;

            // Player: the game's Mario model at human scale, animated by the
            // game's own controller — the declared placeholder until David's model.
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0, 0.3f, 2f);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = Vector3.up * 0.9f;
            var pctrl = player.AddComponent<WorldPlayerController>();
            var marioVisual = Body(player.transform, "Assets/Models/Players/mario_big/mario_big_exported.fbx", NeonRed, 1.75f);
            pctrl.animator = WireAnimator(marioVisual, "Assets/Animations/Player/Mario/LargeMario.controller");
            Dress(marioVisual, "Assets/Materials/3d/mario/mat_mario_big.mat", "Assets/Materials/3d/mario/mat_mario_eyes.mat");

            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.72f, 1f);
            camGo.transform.position = new Vector3(0, 3.4f, -4f);
            var follow = camGo.AddComponent<WorldCamera>();
            follow.target = player.transform;
            follow.offset = new Vector3(0f, 3.2f, -5.5f);
            follow.lookHeight = 1.3f;
            pctrl.cam = camGo.transform;

            // The companion.
            var npc = new GameObject("Companion");
            npc.transform.position = new Vector3(2f, 0.3f, -1f);
            var comp = npc.AddComponent<CompanionNPC>();
            comp.player = player.transform;
            // Luigi as the narrator's placeholder body — the nervous brother who
            // follows you around explaining things. Fitting.
            var luigiVisual = Body(npc.transform, "Assets/Models/Players/luigi_big/luigi_big.fbx", new Color(0.1f, 0.65f, 0.25f), 1.85f);
            Dress(luigiVisual, "Assets/Materials/3d/luigi/mat_luigi_big.mat", "Assets/Materials/3d/luigi/mat_luigi_eyes.mat");
            FloatingLabel(npc.transform, "ERIK\n<size=55%>narrator.exe — dev build</size>", 2.4f);

            // Dialogue UI.
            BuildDialogueUi();

            // The story beats along the street — a conversation between the two of
            // them, placeholder bodies acknowledged in-fiction.
            Trigger(new Vector3(0, 1, 6),
                "ERIK (luigi.tmp)|Oh! A visitor! Welcome to the World. I'm Erik — or I will be, once the boss sculpts me a body. For now I'm, um. Borrowing Luigi.",
                "DAVID (mario.tmp)|And I'm David — same person as Erik, long story, good lore. Currently shaped like a certain plumber. Nintendo, if you're reading this: placeholders! Temporary! Don't sue, pweaseeee.",
                "ERIK (luigi.tmp)|He's serious, there's a roadmap and everything. Anyway — this street is his portfolio. Walk on, we'll explain him as we go.");
            Trigger(new Vector3(0, 1, 40),
                "ERIK (luigi.tmp)|Day job: at a BMW supplier he designed the team's pre-delivery QA tool. 1,300 automated checks on 3D vehicle data, every single release.",
                "DAVID (mario.tmp)|It caught real defects before they shipped. I was 21 when I built it. Still am, actually.");
            Trigger(new Vector3(0, 1, 80),
                "ERIK (luigi.tmp)|Night job: he reverse-engineers console games. PS4 binary, shaders translated to SPIR-V, his own Vulkan renderer. He fixed hair that emulators get wrong.",
                "DAVID (mario.tmp)|Frame 1377. The hair rendered. I told everyone. Repeatedly.",
                "ERIK (luigi.tmp)|The blue door here leads to HawaiiOS — the operating system he built for this website. Press E at any door to step through.");
            Trigger(new Vector3(0, 1, 120),
                "ERIK (luigi.tmp)|He sculpts too — Blender, EEVEE, Yakuza and Final Fantasy things. Which is how we eventually get faces that aren't... these.",
                "DAVID (mario.tmp)|The arcade at the end runs the classic Versus game this World grew out of — by ipodtouch0218 and contributors, used with permission. Bring a friend, it's real multiplayer on my own server.");

            // Doors.
            Portal("Door-HawaiiOS", new Vector3(-11f, 0, 85f), NeonBlue, "HawaiiOS\n<size=55%>his operating system — press E</size>", null, "https://erikgaren.com/os");
            Portal("Door-Portfolio", new Vector3(11f, 0, 45f), NeonCyan, "THE CV\n<size=55%>recruiter door — press E</size>", null, "https://erikgaren.com/");
            Portal("Door-Versus", new Vector3(0f, 0, 170f), NeonRed, "THE ARCADE\n<size=55%>versus — the classic, press E</size>", "Intro", null);

            // Dev-room fun facts floating around.
            DevFact(new Vector3(-6, 3.5f, 22), "// TODO: replace placeholder plumbers\n// legal says hi");
            DevFact(new Vector3(7, 4.5f, 62), "assert(checks_passed == 1300); // every release");
            DevFact(new Vector3(-7, 5f, 100), "frame 1377: the hair finally rendered.\nnobody saw. everybody was told.");
            DevFact(new Vector3(6, 3.8f, 140), "wagata, yondaime!");
            DevFact(new Vector3(-5, 3f, 55), "// he glides instead of walking.\n// not a bug: animations arrive with the real characters");

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
            tmp.fontSize = 3.2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(8, 2);
            go.AddComponent<Billboard>();
        }

        private static void DevFact(Vector3 pos, string text) {
            var go = new GameObject("DevFact");
            go.transform.position = pos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 2.4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.12f, 0.25f, 0.1f, 0.9f);
            tmp.rectTransform.sizeDelta = new Vector2(12, 3);
            go.AddComponent<Billboard>();
        }

        private static void Trigger(Vector3 pos, params string[] lines) {
            var go = new GameObject("Story-" + pos.z);
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(26, 6, 4);
            var trig = go.AddComponent<WorldDialogueTrigger>();
            trig.lines = lines;
        }

        private static void Portal(string name, Vector3 pos, Color c, string label, string sceneName, string url) {
            var root = new GameObject(name);
            root.transform.position = pos;
            Pipe(root.transform, c);
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0, 1.5f, 0);
            box.size = new Vector3(4.5f, 3.5f, 4.5f);
            var portal = root.AddComponent<WorldPortal>();
            portal.sceneName = sceneName;
            portal.url = url;
            FloatingLabel(root.transform, label, 4.6f);
        }

        // A warp pipe — the only correct shape for a door in this universe. The
        // accent colour tints the classic pipe green so destinations read apart.
        private static void Pipe(Transform parent, Color accent) {
            Color body = Color.Lerp(new Color(0.18f, 0.65f, 0.2f), accent, 0.3f);
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Shaft";
            shaft.transform.SetParent(parent, false);
            shaft.transform.localPosition = new Vector3(0, 1.1f, 0);
            shaft.transform.localScale = new Vector3(2.2f, 1.1f, 2.2f);
            shaft.GetComponent<Renderer>().sharedMaterial = Mat(body, false);
            var lip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lip.name = "Lip";
            lip.transform.SetParent(parent, false);
            lip.transform.localPosition = new Vector3(0, 2.35f, 0);
            lip.transform.localScale = new Vector3(2.7f, 0.25f, 2.7f);
            lip.GetComponent<Renderer>().sharedMaterial = Mat(body * 1.15f, false);
            var mouth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mouth.name = "Mouth";
            mouth.transform.SetParent(parent, false);
            mouth.transform.localPosition = new Vector3(0, 2.5f, 0);
            mouth.transform.localScale = new Vector3(2.2f, 0.06f, 2.2f);
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

        private static void BuildDialogueUi() {
            var canvasGo = new GameObject("DialogueCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGo.transform, false);
            var img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.72f);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0, 40);
            prt.sizeDelta = new Vector2(1250, 170);

            var speaker = MakeUguiText(panel.transform, "Speaker", "ERIK", 26, new Vector2(0, 52), new Vector2(1150, 34), new Color(1f, 0.42f, 0.32f));
            speaker.alignment = TextAlignmentOptions.Left;
            var line = MakeUguiText(panel.transform, "Line", "", 27, new Vector2(0, -20), new Vector2(1150, 96), Color.white);
            line.alignment = TextAlignmentOptions.TopLeft;

            var dlg = canvasGo.AddComponent<WorldDialogue>();
            dlg.panel = panel;
            dlg.speakerText = speaker;
            dlg.lineText = line;
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
            scenes.Insert(0, new EditorBuildSettingsScene("Assets/Scenes/WorldEntry.unity", true));
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/WorldHub.unity", true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
