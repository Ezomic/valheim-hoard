using System.Collections.Generic;
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

        private static readonly Dictionary<string, Original> Originals =
            new Dictionary<string, Original>();

        public static void Apply()
        {
            var db = ObjectDB.instance;
            if (db == null || db.m_items == null || db.m_items.Count == 0) return;

            var changed = 0;
            var skipped = 0;

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
                if (!Eligible(prefab.name, shared, original)) { skipped++; continue; }

                var stack = Mathf.Clamp(
                    Mathf.RoundToInt(original.Stack * HoardConfig.StackMultiplier.Value),
                    1,
                    Mathf.Max(1, HoardConfig.StackCap.Value));

                var weight = Mathf.Max(0f, original.Weight * HoardConfig.WeightMultiplier.Value);

                if (shared.m_maxStackSize == stack && Mathf.Approximately(shared.m_weight, weight))
                    continue;

                shared.m_maxStackSize = stack;
                shared.m_weight = weight;
                changed++;

                if (HoardConfig.Verbose.Value)
                    HoardPlugin.Log.LogInfo(
                        prefab.name + ": stack " + original.Stack + " -> " + stack
                        + ", weight " + original.Weight.ToString("0.##") + " -> " + weight.ToString("0.##"));
            }

            HoardPlugin.Log.LogInfo(
                "Retuned " + changed + " item(s); left " + skipped + " alone.");
        }

        private static bool Eligible(string prefabName, ItemDrop.ItemData.SharedData shared, Original original)
        {
            if (HoardConfig.IsExcluded(prefabName)) return false;

            // Equipment. Stacking it would silently discard per-item durability.
            if (original.Stack <= 1) return false;

            // Ore, bars, and anything else the game refuses to send through a portal.
            // Hauling those is a pacing decision, so it stays opt-in.
            if (!shared.m_teleportable && !HoardConfig.IncludeNonTeleportable.Value)
                return false;

            if (shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy
                && !HoardConfig.IncludeTrophies.Value)
                return false;

            return true;
        }
    }
}
