using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace NSMB.World {
    // A debug inspector in the spirit of the Dragon Engine's e_dip_* table:
    // every knob is a registered property with an id, a category and a live
    // value, rather than a hand-drawn menu. Categories on the left, properties
    // on the right, kill switches and visualizers throughout.
    //
    // Opened with the game's Scoreboard button; navigated with its movement and
    // confirmed with its powerup button, so a pad drives it too.
    public class WorldDebugInspector : MonoBehaviour {

        public enum Kind { Toggle, Cycle, Action, Readout }

        private class Prop {
            public int Id;
            public string Category;
            public string Name;
            public Kind Kind;
            public Func<string> Value;
            public Action Activate;
        }

        public GameObject panel;
        public TMP_Text left, right, footer;
        public GameObject graphyPrefab;
        public GameObject controlsCard, bubbleRoot;
        public WorldPlayerController player;
        public WorldCamera rig;
        public Light sun;
        public AudioSource ui;
        public AudioClip moveClip, selectClip, openClip;

        // Kill switches other systems read.
        public static bool DrawHeightmap, DrawTriggers, DrawVelocity;
        public static bool HideGallery, HideSky, HidePlatforms, HideCompanion;

        private readonly List<Prop> props = new();
        private readonly List<string> categories = new();
        private Controls controls;
        private GameObject graphy;
        private int catIndex, propIndex;
        private float repeat;
        private bool open;
        private float fps;

        private void Awake() {
            controls = new Controls();
            controls.Player.Enable();
            controls.UI.Enable();
            Register();
            if (panel) {
                panel.SetActive(false);
            }
        }

        private void OnDestroy() {
            controls?.Dispose();
            WorldPlayerController.WalkMax = WorldPlayerController.WalkMaxDefault;
            WorldPlayerController.SprintMax = WorldPlayerController.SprintMaxDefault;
            WorldPlayerController.JumpVelocity = WorldPlayerController.JumpVelocityDefault;
            WorldPlayerController.GravityScale = WorldPlayerController.GravityScaleDefault;
            Time.timeScale = 1f;
        }

        private void Add(string category, string name, Kind kind, Func<string> value, Action activate = null) {
            props.Add(new Prop { Id = props.Count + 1, Category = category, Name = name, Kind = kind, Value = value, Activate = activate });
            if (!categories.Contains(category)) {
                categories.Add(category);
            }
        }

        private static string OnOff(bool b) => b ? "<color=#8fe08f>on</color>" : "<color=#888>off</color>";

        private void Register() {
            // ---- system -------------------------------------------------------
            Add("system", "stats overlay", Kind.Toggle, () => OnOff(graphy && graphy.activeSelf), () => {
                if (!graphy && graphyPrefab) {
                    graphy = Instantiate(graphyPrefab);
                } else if (graphy) {
                    graphy.SetActive(!graphy.activeSelf);
                }
            });
            Add("system", "time scale", Kind.Cycle, () => Time.timeScale.ToString("0.00"), () => {
                Time.timeScale = Time.timeScale > 0.9f ? 0.35f : Time.timeScale > 0.3f ? 0.1f : 1f;
            });
            Add("system", "fly mode", Kind.Toggle, () => OnOff(FlyMode), () => FlyMode = !FlyMode);
            Add("system", "speed scale", Kind.Cycle, () => "x" + SpeedScale.ToString("0"), () => {
                SpeedScale = SpeedScale >= 4f ? 1f : SpeedScale + 1f;
            });
            Add("system", "reload hub", Kind.Action, () => "go", () =>
                UnityEngine.SceneManagement.SceneManager.LoadScene("WorldHub"));

            // ---- render kill switches, the light_off_* idea ---------------------
            Add("render", "gallery off", Kind.Toggle, () => OnOff(HideGallery), () => {
                HideGallery = !HideGallery;
                SetActiveByPrefix("Gallery-", !HideGallery);
            });
            Add("render", "sky off", Kind.Toggle, () => OnOff(HideSky), () => {
                HideSky = !HideSky;
                SetActiveByPrefix("Sky", !HideSky);
            });
            Add("render", "platforms off", Kind.Toggle, () => OnOff(HidePlatforms), () => {
                HidePlatforms = !HidePlatforms;
                SetActiveByPrefix("Float", !HidePlatforms);
            });
            Add("render", "directional light off", Kind.Toggle, () => OnOff(sun && !sun.enabled), () => {
                if (sun) {
                    sun.enabled = !sun.enabled;
                }
            });
            Add("render", "ambient off", Kind.Toggle, () => OnOff(RenderSettings.ambientLight.maxColorComponent < 0.05f), () => {
                RenderSettings.ambientLight = RenderSettings.ambientLight.maxColorComponent < 0.05f
                    ? new Color(0.74f, 0.76f, 0.8f) : Color.black;
            });
            Add("render", "wireframe-ish (shadows off)", Kind.Toggle, () => OnOff(sun && sun.shadows == LightShadows.None), () => {
                if (sun) {
                    sun.shadows = sun.shadows == LightShadows.None ? LightShadows.Soft : LightShadows.None;
                }
            });

            // ---- visualizers, the debug_draw layer ------------------------------
            Add("debug draw", "heightmap grid", Kind.Toggle, () => OnOff(DrawHeightmap), () => DrawHeightmap = !DrawHeightmap);
            Add("debug draw", "trigger volumes", Kind.Toggle, () => OnOff(DrawTriggers), () => DrawTriggers = !DrawTriggers);
            Add("debug draw", "velocity vector", Kind.Toggle, () => OnOff(DrawVelocity), () => DrawVelocity = !DrawVelocity);

            // ---- physics, live-tunable -----------------------------------------
            Add("physics", "walk cap", Kind.Cycle, () => WorldPlayerController.WalkMax.ToString("0.00"), () => {
                WorldPlayerController.WalkMax = WorldPlayerController.WalkMax > 5f
                    ? WorldPlayerController.WalkMaxDefault : WorldPlayerController.WalkMax * 1.5f;
            });
            Add("physics", "sprint cap", Kind.Cycle, () => WorldPlayerController.SprintMax.ToString("0.00"), () => {
                WorldPlayerController.SprintMax = WorldPlayerController.SprintMax > 12f
                    ? WorldPlayerController.SprintMaxDefault : WorldPlayerController.SprintMax * 1.5f;
            });
            Add("physics", "jump velocity", Kind.Cycle, () => WorldPlayerController.JumpVelocity.ToString("0.00"), () => {
                WorldPlayerController.JumpVelocity = WorldPlayerController.JumpVelocity > 12f
                    ? WorldPlayerController.JumpVelocityDefault : WorldPlayerController.JumpVelocity * 1.35f;
            });
            Add("physics", "gravity scale", Kind.Cycle, () => WorldPlayerController.GravityScale.ToString("0.00"), () => {
                WorldPlayerController.GravityScale = WorldPlayerController.GravityScale > 1.9f ? 0.35f
                    : WorldPlayerController.GravityScale < 0.5f ? 1f : 2f;
            });
            Add("physics", "restore sim values", Kind.Action, () => "go", () => {
                WorldPlayerController.WalkMax = WorldPlayerController.WalkMaxDefault;
                WorldPlayerController.SprintMax = WorldPlayerController.SprintMaxDefault;
                WorldPlayerController.JumpVelocity = WorldPlayerController.JumpVelocityDefault;
                WorldPlayerController.GravityScale = WorldPlayerController.GravityScaleDefault;
            });

            // ---- camera ---------------------------------------------------------
            Add("camera", "view", Kind.Toggle, () => rig && rig.PlaneLocked ? "2.5D side" : "free 3D", () => {
                if (rig) {
                    rig.ToggleView();
                }
            });
            Add("camera", "distance", Kind.Cycle, () => rig ? rig.offset.z.ToString("0.0") : "-", () => {
                if (rig) {
                    rig.offset.z = rig.offset.z < -6f ? -3.3f : rig.offset.z - 1.5f;
                }
            });
            Add("camera", "height", Kind.Cycle, () => rig ? rig.offset.y.ToString("0.0") : "-", () => {
                if (rig) {
                    rig.offset.y = rig.offset.y > 5f ? 1.9f : rig.offset.y + 1f;
                }
            });

            // ---- characters ------------------------------------------------------
            Add("character", "companion off", Kind.Toggle, () => OnOff(HideCompanion), () => {
                HideCompanion = !HideCompanion;
                var comp = FindFirstObjectByType<CompanionNPC>();
                if (comp) {
                    comp.gameObject.SetActive(!HideCompanion);
                }
            });
            Add("character", "teleport: spawn", Kind.Action, () => "go", () => Teleport(new Vector3(0f, 0.6f, -12f)));
            Add("character", "teleport: terrace", Kind.Action, () => "go", () => Teleport(new Vector3(-13f, 3.2f, -1f)));
            Add("character", "teleport: pit", Kind.Action, () => "go", () => Teleport(new Vector3(0f, -3.4f, 0f)));
            Add("character", "teleport: gallery", Kind.Action, () => "go", () => Teleport(new Vector3(0f, 0.6f, 28f)));
            Add("character", "teleport: versus pipe", Kind.Action, () => "go", () => Teleport(new Vector3(0f, 3.2f, 22f)));

            // ---- ui ---------------------------------------------------------------
            Add("ui", "controls card", Kind.Toggle, () => OnOff(controlsCard && controlsCard.activeSelf), () => {
                if (controlsCard) {
                    controlsCard.SetActive(!controlsCard.activeSelf);
                }
            });

            // ---- counters, the cdebug_counter idea ---------------------------------
            Add("counters", "fps", Kind.Readout, () => fps.ToString("0"));
            Add("counters", "position", Kind.Readout, () => player ? player.transform.position.ToString("F1") : "-");
            Add("counters", "velocity", Kind.Readout, () => player ? player.DebugVelocity.magnitude.ToString("F2") : "-");
            Add("counters", "grounded", Kind.Readout, () => player ? OnOff(player.DebugGrounded) : "-");
            Add("counters", "jump stage", Kind.Readout, () => player ? player.DebugJumpStage.ToString() : "-");
            Add("counters", "heightmap here", Kind.Readout, () => {
                if (player && WorldHeightmap.TryGetFloor(player.transform.position, out float top)) {
                    return top.ToString("F2");
                }
                return "none";
            });
            Add("counters", "objects", Kind.Readout, () => FindObjectsByType<Transform>(FindObjectsSortMode.None).Length.ToString());
        }

        public static bool FlyMode { get; private set; }
        public static float SpeedScale { get; private set; } = 1f;

        private void Teleport(Vector3 where) {
            if (player) {
                player.transform.position = where;
            }
        }

        private static void SetActiveByPrefix(string prefix, bool active) {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                if (t.name.StartsWith(prefix) && t.parent == null) {
                    t.gameObject.SetActive(active);
                }
            }
        }

        private void Update() {
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.1f);
            if (controls == null) {
                return;
            }

            if (controls.UI.Scoreboard.WasPressedThisFrame()) {
                open = !open;
                if (panel) {
                    panel.SetActive(open);
                }
                Click(openClip);
            }
            if (!open) {
                return;
            }

            Vector2 nav = controls.Player.Movement.ReadValue<Vector2>();
            repeat -= Time.unscaledDeltaTime;
            if (repeat <= 0f && (Mathf.Abs(nav.y) > 0.5f || Mathf.Abs(nav.x) > 0.5f)) {
                if (Mathf.Abs(nav.x) > 0.5f) {
                    catIndex = (catIndex + (int) Mathf.Sign(nav.x) + categories.Count) % categories.Count;
                    propIndex = 0;
                } else {
                    var list = InCategory();
                    propIndex = (propIndex - (int) Mathf.Sign(nav.y) + list.Count) % list.Count;
                }
                repeat = 0.16f;
                Click(moveClip);
            } else if (Mathf.Abs(nav.y) <= 0.5f && Mathf.Abs(nav.x) <= 0.5f) {
                repeat = 0f;
            }

            if (controls.Player.PowerupAction.WasPressedThisFrame() || controls.UI.Submit.WasPressedThisFrame()) {
                var list = InCategory();
                if (propIndex < list.Count && list[propIndex].Activate != null) {
                    list[propIndex].Activate();
                    Click(selectClip);
                }
            }
            Render();
        }

        private List<Prop> InCategory() {
            var list = new List<Prop>();
            if (categories.Count == 0) {
                return list;
            }
            string cat = categories[catIndex];
            foreach (var p in props) {
                if (p.Category == cat) {
                    list.Add(p);
                }
            }
            return list;
        }

        private void Render() {
            if (!left || !right) {
                return;
            }
            var lb = new StringBuilder();
            lb.AppendLine("<size=115%>INSPECTOR</size>\n");
            for (int i = 0; i < categories.Count; i++) {
                lb.Append(i == catIndex ? "<color=#ffd34d>> " : "  ");
                lb.Append(categories[i]);
                if (i == catIndex) {
                    lb.Append("</color>");
                }
                lb.AppendLine();
            }
            left.text = lb.ToString();

            var list = InCategory();
            var rb = new StringBuilder();
            foreach (var (p, i) in Pairs(list)) {
                rb.Append(i == propIndex ? "<color=#ffd34d>> " : "  ");
                rb.Append($"<mspace=0.55em>{p.Id:000}</mspace>  ");
                rb.Append(p.Name.PadRight(28));
                rb.Append("  ").Append(p.Value != null ? p.Value() : "");
                if (i == propIndex) {
                    rb.Append("</color>");
                }
                rb.AppendLine();
            }
            right.text = rb.ToString();

            if (footer) {
                footer.text = $"{props.Count} properties · left/right: category · up/down: property · E: activate · Tab: close";
            }
        }

        private static IEnumerable<(Prop, int)> Pairs(List<Prop> list) {
            for (int i = 0; i < list.Count; i++) {
                yield return (list[i], i);
            }
        }

        private void Click(AudioClip clip) {
            if (ui && clip) {
                ui.PlayOneShot(clip, 0.55f);
            }
        }
    }
}
