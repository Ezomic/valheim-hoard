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
        public static ConfigEntry<bool> ScaleWithProgression;
        public static ConfigEntry<float> ProgressionBase;
        public static ConfigEntry<string> ProgressionTiers;
        public static ConfigEntry<string> ProgressionOrder;
        public static ConfigEntry<float> ProgressionStep;
        public static ConfigEntry<bool> DeferToUtangard;
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
                + "deliberate part of the game's pacing, not an oversight.\n"
                + "Leaving this off does not mean metal never stacks: an earned metal tier "
                + "in ProgressionTiers lifts the rule, which is the intended way for ore "
                + "stacking to arrive. This switch is for wanting it sooner than that.");

            IncludeTrophies = config.Bind("Stacks", "IncludeTrophies", true,
                "Also affect trophies. Harmless - they are decoration and turn-ins.");

            ExcludeItems = config.Bind("Stacks", "ExcludeItems", "",
                "Comma-separated item prefab names to leave completely alone.");

            ScaleWithProgression = config.Bind("Progression", "ScaleWithProgression", true,
                "Each boss raises one group of stacks instead of everything being multiplied "
                + "from the first minute. Meadows scarcity is the game teaching you to plan; "
                + "your ninth trip to the same copper deposit is not teaching you anything, "
                + "and a flat multiplier cannot tell those two apart. Off falls back to "
                + "StackMultiplier for everything.");

            ProgressionBase = config.Bind("Progression", "ProgressionBase", 1f,
                "The multiplier for a group no boss has unlocked yet. 1 is vanilla, which is "
                + "the point: the early game is supposed to be tight.");

            ProgressionTiers = config.Bind("Progression", "ProgressionTiers",
                "defeated_eikthyr:building:2, defeated_gdking:farming:2, "
                + "defeated_bonemass:metal:2, defeated_bonemass:trophy:2",
                "boss:group:multiplier, comma separated. The highest earned entry naming a "
                + "group wins, so entries never compound. One boss may name several groups.\n"
                + "Ammo, food and everything else are deliberately absent: a group with no "
                + "entry stays at vanilla forever, and most of the game's stackables are "
                + "meant to. What is here is the hauling that is genuinely repetitive.\n"
                + "Groups: building (anything the Hammer asks for), farming (anything the "
                + "Cultivator asks for), metal (anything a portal refuses), food, ammo, "
                + "trophy, other, and all.\n"
                + "The groups are read off those tools' piece tables rather than a list here, "
                + "so an item a content mod adds lands in the right one by itself.\n"
                + "An earned metal tier also lifts the portal rule - see "
                + "IncludeNonTeleportable. Any global key works, not only boss keys, so a "
                + "modded key can be named here.");

            ProgressionOrder = config.Bind("Progression", "ProgressionOrder",
                "defeated_eikthyr, defeated_gdking, defeated_bonemass, defeated_dragon, "
                + "defeated_goblinking, defeated_queen, defeated_fader",
                "The order bosses come in, which is what makes ProgressionStep mean "
                + "'later'. A key not named here still unlocks its own group; it just never "
                + "counts as later than anything.");

            ProgressionStep = config.Bind("Progression", "ProgressionStep", 0.1f,
                "How much every boss after the one that unlocked a group raises it again. "
                + "0.1 is ten percent, compounding, so building unlocked at Eikthyr is 2x, "
                + "2.2x once The Elder is down, 2.42x after Bonemass.\n"
                + "This is what the late bosses are for. Only three of them unlock a group, "
                + "and without this the whole feature would be over by Bonemass - a reward "
                + "table that stops paying halfway through the game. Set 0 for flat tiers.");

            DeferToUtangard = config.Bind("Progression", "DeferToUtangard", true,
                "When Utangard is installed, ask it whether the group has earned a boss "
                + "rather than reading the world key. Utangard opens a biome only when every "
                + "member of the group was at that boss's death, so the two answers differ "
                + "whenever somebody was offline for a kill - and without this, stacks would "
                + "arrive for a biome Utangard still has fenced off. No effect when Utangard "
                + "is not installed.");

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
