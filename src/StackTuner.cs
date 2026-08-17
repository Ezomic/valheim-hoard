using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Hoard
{
    /// <summary>
    /// Rewrites stack sizes and weights on the item prefabs in ObjectDB.
    ///
    /// Two rules do most of the safety work here:
    ///
    /// Only items that already stack are touched. An item with a vanilla stack size of 1
    /// is equipment - a weapon, a piece of armour, a tool - and those carry per-item
    /// durability and quality. Stacking them collapses several distinct objects into a
    /// count, and the durability of all but one of them is simply lost.
    ///
    /// Everything is computed from the item's original values, which are captured the
    /// first time it is seen and never overwritten. ObjectDB is set up more than once -
    /// Awake, then CopyOtherDB when a world loads - so a mod that multiplies whatever it
    /// currently finds ends up squaring its own multiplier on the second pass.
    /// </summary>
    internal static class StackTuner
    {
        private readonly struct Original
        {
            public readonly int Stack;
            public readonly float Weight;

            public Original(int stack, float weight)
            {
                Stack = stack;
                Weight = weight;
            }
        }

        // The reasons an item is left alone. They are constants because the generated item
        // list prints them in its last column and counts them in its header, and a reason
        // spelled two ways would be two rows in that summary. Worded for someone reading
        // that file rather than for a log: the question is always "why did this not change".
        internal const string Excluded = "excluded";
        internal const string Equipment = "equipment";
        internal const string PortalBlocked = "portal-blocked";
        internal const string Trophy = "trophy";

        private static readonly Dictionary<string, Original> Originals =
            new Dictionary<string, Original>();

        public static void Apply()
        {
            var db = ObjectDB.instance;
            if (db == null || db.m_items == null || db.m_items.Count == 0) return;

            var changed = 0;
            var alreadyRight = 0;
            var skipped = 0;

            // Built as the pass runs rather than by a second scan, so the file cannot
            // describe rules other than the ones actually applied. See ItemDump.
            var rows = new List<ItemDump.Row>(db.m_items.Count);

            foreach (var prefab in db.m_items)
            {
                if (prefab == null) continue;

                var drop = prefab.GetComponent<ItemDrop>();
                var shared = drop != null && drop.m_itemData != null ? drop.m_itemData.m_shared : null;
                if (shared == null) continue;

                if (!Originals.TryGetValue(prefab.name, out var original))
                {
                    original = new Original(shared.m_maxStackSize, shared.m_weight);
                    Originals[prefab.name] = original;
                }

                // Never from the current value - see the note on CopyOtherDB above.
                var reason = SkipReason(prefab.name, shared, original);

                if (reason == null)
                {
                    var stack = Mathf.Clamp(
                        Mathf.RoundToInt(original.Stack * HoardConfig.StackMultiplier.Value),
                        1,
                        Mathf.Max(1, HoardConfig.StackCap.Value));

                    var weight = Mathf.Max(0f, original.Weight * HoardConfig.WeightMultiplier.Value);

                    if (shared.m_maxStackSize == stack && Mathf.Approximately(shared.m_weight, weight))
                    {
                        alreadyRight++;
                    }
                    else
                    {
                        shared.m_maxStackSize = stack;
                        shared.m_weight = weight;
                        changed++;

                        if (HoardConfig.Verbose.Value)
                            HoardPlugin.Log.LogInfo(
                                prefab.name + ": stack " + original.Stack + " -> " + stack
                                + ", weight " + original.Weight.ToString("0.##", CultureInfo.InvariantCulture)
                                + " -> " + weight.ToString("0.##", CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    skipped++;
                }

                // Read back rather than assume. An item excluded after a previous pass already
                // tuned it keeps that value for the rest of the session, and the file should
                // say what the item is now, not what the rules would have made it.
                rows.Add(new ItemDump.Row(
                    prefab.name,
                    DisplayName(shared.m_name, prefab.name),
                    shared.m_itemType.ToString(),
                    original.Stack, shared.m_maxStackSize,
                    original.Weight, shared.m_weight,
                    reason));
            }

            // All three counts, because they add up to the item count and the old pair did
            // not. "Retuned 0" on the second pass of a session reads like a failure until you
            // know the rest of the items were already at the right numbers.
            HoardPlugin.Log.LogInfo(
                "Retuned " + changed + " item(s); " + alreadyRight
                + " already correct; left " + skipped + " alone.");

            ItemDump.Write(rows);
        }

        /// <summary>
        /// The rule that stops this item being touched, or null when nothing does.
        ///
        /// A reason rather than a bool because the generated item list prints it. The two
        /// callers would otherwise ask the same question in two different ways, and the file
        /// would start explaining decisions the mod did not make.
        /// </summary>
        private static string SkipReason(string prefabName, ItemDrop.ItemData.SharedData shared, Original original)
        {
            if (HoardConfig.IsExcluded(prefabName)) return Excluded;

            // Equipment. Stacking it would silently discard per-item durability.
            if (original.Stack <= 1) return Equipment;

            // Ore, bars, and anything else the game refuses to send through a portal.
            // Hauling those is a pacing decision, so it stays opt-in.
            if (!shared.m_teleportable && !HoardConfig.IncludeNonTeleportable.Value)
                return PortalBlocked;

            if (shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy
                && !HoardConfig.IncludeTrophies.Value)
                return Trophy;

            return null;
        }

        /// <summary>
        /// The item's readable name, falling back to the prefab name.
        ///
        /// Defensive because this runs from ObjectDB.Awake, and the localisation singleton is
        /// not guaranteed to exist that early. A missing name is a cosmetic loss in one column
        /// of a diagnostic file, which is not worth an exception on the load path.
        /// </summary>
        private static string DisplayName(string token, string fallback)
        {
            if (string.IsNullOrEmpty(token)) return fallback;

            try
            {
                var localization = Localization.instance;
                if (localization != null)
                {
                    var name = localization.Localize(token);
                    if (!string.IsNullOrEmpty(name)) return name;
                }
            }
            catch (Exception)
            {
                // Fall through to the token below.
            }

            return token;
        }
    }
}
