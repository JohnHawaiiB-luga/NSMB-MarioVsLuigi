using Quantum;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NSMB.World {
    // The Lab: the stage audit, run inside the game.
    //
    // Every check here exists because it caught something. A stage whose music
    // list is empty divides by zero the instant play begins; one with no star
    // spawns does the same in the star system; one whose tile data does not
    // match its baked dimensions indexes past the end of the array. Each of
    // those shipped to production once and died in a browser, half an hour and
    // a deploy after the mistake was made. Reading the same invariants back out
    // of the asset database at runtime means anyone can see, in a few seconds,
    // whether the stages this build carries are sound.
    public static class WorldLab {

        private static string cached;

        public static void Invalidate() {
            cached = null;
        }

        public static string Report() {
            if (!string.IsNullOrEmpty(cached)) {
                return cached;
            }

            var builder = new StringBuilder();
            builder.Append("<b>STAGE AUDIT</b>\n");

            List<AssetGuid> guids;
            try {
                guids = QuantumUnityDB.FindGlobalAssetGuids(new AssetObjectQuery { Type = typeof(VersusStageData) });
            } catch (System.Exception e) {
                return "<b>STAGE AUDIT</b>\nCould not read the asset database.\n" + e.Message;
            }

            int passed = 0, failed = 0;
            var lines = new StringBuilder();
            foreach (AssetGuid guid in guids) {
                if (!QuantumUnityDB.TryGetGlobalAsset(new AssetRef<VersusStageData>(guid), out VersusStageData stage) || !stage) {
                    continue;
                }

                string fault = Fault(stage);
                if (fault == null) {
                    passed++;
                    lines.Append("OK   ");
                } else {
                    failed++;
                    lines.Append("FAIL ");
                }

                // One stage per line, or the board wraps every row into three and
                // the whole report becomes unreadable.
                lines.Append(ShortName(stage).PadRight(12))
                    .Append(' ').Append($"{stage.TileDimensions.X}x{stage.TileDimensions.Y}".PadRight(8))
                    .Append(stage.MainMusic == null ? 0 : stage.MainMusic.Length).Append("m ")
                    .Append(stage.BigStarSpawnpoints == null ? 0 : stage.BigStarSpawnpoints.Length).Append('s');
                if (fault != null) {
                    lines.Append(" — ").Append(fault);
                }
                lines.Append('\n');
            }

            builder.Append(passed + failed).Append(" stages checked · ")
                .Append(passed).Append(" passed");
            if (failed > 0) {
                builder.Append(" · ").Append(failed).Append(" FAILED");
            }
            builder.Append("\n\n").Append(lines);

            builder.Append("\n<i>size · m=music tracks · s=star spawns. Each check guards a field the "
                + "simulation reads without guarding: an empty music list or spawn table is a modulo by "
                + "zero the instant play begins.</i>\n");

            cached = builder.ToString();
            return cached;
        }

        // "levels.custom.worldhub" is mostly prefix; the leaf is what identifies it.
        private static string ShortName(VersusStageData stage) {
            string key = stage.TranslationKey;
            if (string.IsNullOrEmpty(key)) {
                return "(unnamed)";
            }
            int dot = key.LastIndexOf('.');
            return dot >= 0 && dot < key.Length - 1 ? key[(dot + 1)..] : key;
        }

        private static string Fault(VersusStageData stage) {
            if (stage.MainMusic == null || stage.MainMusic.Length == 0) {
                return "no music: GetCurrentMusic divides by zero";
            }
            if (stage.BigStarSpawnpoints == null || stage.BigStarSpawnpoints.Length == 0) {
                return "no star spawns: the spawner divides by zero";
            }
            if (stage.TileDimensions.X <= 0 || stage.TileDimensions.Y <= 0) {
                return "no tile dimensions: the stage was never baked";
            }
            int expected = stage.TileDimensions.X * stage.TileDimensions.Y;
            if (stage.TileData == null || stage.TileData.Length != expected) {
                return $"tile data is {(stage.TileData == null ? 0 : stage.TileData.Length)}, expected {expected}";
            }
            if (stage.Spawnpoint == Photon.Deterministic.FPVector2.Zero) {
                return "spawn point sits at the origin";
            }
            return null;
        }
    }
}
