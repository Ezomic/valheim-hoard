using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;

namespace Hoard
{
    /// <summary>
    /// Defaults are deliberately conservative.
    ///
    /// The usual version of this mod ships x10 stacks and halved weight, which does not
    /// make the game more convenient so much as delete a system from it: Valheim's mid
    /// game is largely a logistics problem, and the tedium and the difficulty are the same
    /// mechanic seen from two sides. So the defaults here fix the annoying half and leave
    /// the interesting half alone - stacks double, weight is untouched, and the metals that
    /// cannot be teleported are not modified at all.
    ///
    /// Everything is a knob, so if you decide you want the easy version it is one edit away.
    /// </summary>
    internal static class HoardConfig
    {
        public static ConfigEntry<float> StackMultiplier;
        public static ConfigEntry<int> StackCap;
        public static ConfigEntry<float> WeightMultiplier;
        public static ConfigEntry<bool> IncludeNonTeleportable;
        public static ConfigEntry<bool> IncludeTrophies;
        public static ConfigEntry<string> ExcludeItems;
        public static ConfigEntry<bool> WriteItemList;
        public static ConfigEntry<bool> Verbose;

        /// <summary>This mod's own .cfg, and the item list written beside it.</summary>
        public static string ConfigPath { get; private set; }
        public static string ItemListPath { get; private set; }

        public static void Bind(ConfigFile config)
        {
            // Derived rather than bound. A path setting for a diagnostic file is a knob whose
            // only sensible value is this one, and it would be the first thing to go stale
            // when someone moves a profile.
            ConfigPath = config.ConfigFilePath;
            ItemListPath = Path.ChangeExtension(config.ConfigFilePath, ".items.txt");

            StackMultiplier = config.Bind("Stacks", "StackMultiplier", 2f,
                "Multiplies the vanilla stack size of anything that already stacks. "
                + "Always applied to the original value, so raising and lowering it is safe.");

            StackCap = config.Bind("Stacks", "StackCap", 200,
                "Hard ceiling on the resulting stack size, whatever the multiplier says.");

            WeightMultiplier = config.Bind("Stacks", "WeightMultiplier", 1f,
                "Multiplies item weight. Left at 1 by default: carry weight is the main "
                + "thing standing between you and hauling a mountain home in one trip, and "
                + "bigger stacks already ease the inventory-slot half of that problem.");

            IncludeNonTeleportable = config.Bind("Stacks", "IncludeNonTeleportable", false,
                "Also affect items that cannot go through a portal - ore, metal bars and "
                + "the like. Off by default because hauling metal by cart and boat is a "
                + "deliberate part of the game's pacing, not an oversight.");

            IncludeTrophies = config.Bind("Stacks", "IncludeTrophies", true,
                "Also affect trophies. Harmless - they are decoration and turn-ins.");

            ExcludeItems = config.Bind("Stacks", "ExcludeItems", "",
                "Comma-separated item prefab names to leave completely alone.");

            WriteItemList = config.Bind("Diagnostics", "WriteItemList", true,
                "Write ezomic.valheim.hoard.items.txt beside this file: every item in the "
                + "game, what Hoard did to it, and which rule left it alone when it did "
                + "nothing. On by default because the prefab names ExcludeItems takes are "
                + "not guessable, and because it answers 'why did this item not change' "
                + "without anyone having to read a log.");

            Verbose = config.Bind("Diagnostics", "Verbose", false,
                "Log every item whose stack size or weight was changed. The item list above "
                + "is usually the better answer; this is for watching a single pass happen.");
        }

        private static HashSet<string> _excluded;

        public static bool IsExcluded(string prefabName)
        {
            if (_excluded == null)
            {
                _excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in (ExcludeItems.Value ?? "").Split(','))
                {
                    var name = entry.Trim();
                    if (name.Length > 0) _excluded.Add(name);
                }
            }

            return _excluded.Count > 0 && _excluded.Contains(prefabName);
        }
    }
}
