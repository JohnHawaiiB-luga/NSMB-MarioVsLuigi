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

        [MenuItem("Tools/World/Build Scenes")]
        public static void BuildScenes() {
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

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.19f, 0.26f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 140f;
            RenderSettings.fogColor = Night;

            var lightGo = new GameObject("Moonlight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.55f;
            light.color = new Color(0.7f, 0.78f, 1f);
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            // The street: a long strip heading +Z, buildings flanking it.
            Block("Street", new Vector3(0, -0.5f, 90), new Vector3(26, 1, 220), new Color(0.09f, 0.1f, 0.13f));
            for (int i = 0; i < 11; i++) {
                float z = i * 20f;
                float h1 = 8f + (i * 7f) % 11f, h2 = 9f + (i * 5f) % 13f;
                Block("BuildingL" + i, new Vector3(-16f, h1 / 2f, z), new Vector3(6f, h1, 12f), new Color(0.07f, 0.08f, 0.11f));
                Block("BuildingR" + i, new Vector3(16f, h2 / 2f, z), new Vector3(6f, h2, 12f), new Color(0.07f, 0.08f, 0.11f));
                Color neon = (i % 4) switch { 0 => NeonRed, 1 => NeonBlue, 2 => NeonViolet, _ => NeonCyan };
                Glow("SignL" + i, new Vector3(-12.8f, 4f + (i % 3), z), new Vector3(0.3f, 2.2f, 4.5f), neon);
                Glow("SignR" + i, new Vector3(12.8f, 3.5f + ((i + 2) % 3), z + 8f), new Vector3(0.3f, 2.2f, 4.5f), neon);
            }

            // Player: greybox capsule with the controller. Models come later.
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0, 1.2f, 0);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.center = Vector3.up;
            var pctrl = player.AddComponent<WorldPlayerController>();
            // Mario is the declared placeholder body until David's model exists.
            Body(player.transform, "Assets/Models/Players/mario_big/mario_big_exported.fbx", NeonRed);

            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Night;
            camGo.transform.position = new Vector3(0, 4.5f, -7.5f);
            var follow = camGo.AddComponent<WorldCamera>();
            follow.target = player.transform;
            pctrl.cam = camGo.transform;

            // The companion.
            var npc = new GameObject("Companion");
            npc.transform.position = new Vector3(2f, 1f, -1f);
            var comp = npc.AddComponent<CompanionNPC>();
            comp.player = player.transform;
            // Luigi as the narrator's placeholder body — the nervous brother who
            // follows you around explaining things. Fitting.
            Body(npc.transform, "Assets/Models/Players/luigi_big/luigi_big.fbx", new Color(0.1f, 0.65f, 0.25f));
            FloatingLabel(npc.transform, "ERIK\n<size=55%>narrator.exe — dev build</size>", 2.6f);

            // Dialogue UI.
            BuildDialogueUi();

            // The story beats along the street.
            Trigger(new Vector3(0, 1, 6), "Oh! A visitor. Welcome to the World. I'm Erik — well, a dev-build of him. The guy you're playing? That's David. Same person. Long story, good lore.",
                    "This whole place belongs to David Erik García Arenas — engineer, Munich, dangerous amounts of free-time energy. Walk on, I'll explain him as we go.");
            Trigger(new Vector3(0, 1, 40), "See those checkmarks? At a BMW supplier he built a QA tool that runs 1,300 automated checks on 3D vehicle data before every delivery. It caught real defects. The team adopted it. He was 21.",
                    "That's the day job. The night job is where it gets weird — keep walking.");
            Trigger(new Vector3(0, 1, 80), "He reverse-engineers console games. Took a PS4 binary, translated its shaders to SPIR-V, wrote a Vulkan renderer, and fixed the hair that emulators break. There's a whole writeup. RenderDoc captures and everything.",
                    "The red door around here leads to HawaiiOS — his operating system. Yes, he built an operating system for a phone that doesn't exist. Press E at any door to go through.");
            Trigger(new Vector3(0, 1, 120), "He also sculpts. Blender, mostly EEVEE — Yakuza and Final Fantasy stuff. One day these capsules you and I are wearing become real characters he made. David vs Erik. Mark my words.",
                    "The arcade at the end of the street runs the classic Versus game this World grew out of — grab a friend, it's real multiplayer on his own server.");

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

        private static GameObject Body(Transform parent, string fbxPath, Color fallback) {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            GameObject visual;
            if (model) {
                visual = (GameObject) PrefabUtility.InstantiatePrefab(model);
                visual.transform.SetParent(parent, false);
                visual.transform.localPosition = Vector3.zero;
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
            tmp.color = new Color(0.55f, 0.95f, 0.7f, 0.85f);
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
            Frame(root.transform, c);
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0, 2, 0);
            box.size = new Vector3(4.5f, 4.5f, 4.5f);
            var portal = root.AddComponent<WorldPortal>();
            portal.sceneName = sceneName;
            portal.url = url;
            FloatingLabel(root.transform, label, 5.2f);
        }

        private static void Frame(Transform parent, Color c) {
            void Bar(string n, Vector3 lp, Vector3 ls) {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = n;
                bar.transform.SetParent(parent, false);
                bar.transform.localPosition = lp;
                bar.transform.localScale = ls;
                bar.GetComponent<Renderer>().sharedMaterial = Mat(c, true);
                Object.DestroyImmediate(bar.GetComponent<Collider>());
            }
            Bar("PostL", new Vector3(-2f, 2f, 0), new Vector3(0.4f, 4f, 0.4f));
            Bar("PostR", new Vector3(2f, 2f, 0), new Vector3(0.4f, 4f, 0.4f));
            Bar("Lintel", new Vector3(0, 4.2f, 0), new Vector3(4.4f, 0.4f, 0.4f));
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
