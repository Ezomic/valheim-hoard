using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;

namespace Yoke
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
    internal static class YokeConfig
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
        public static ConfigEntry<string> LiftPortalRuleAt;
        public static ConfigEntry<string> BiomeOverrides;
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
                "defeated_eikthyr:meadows:2, defeated_gdking:blackforest:2, "
                + "defeated_bonemass:swamp:2, defeated_dragon:mountain:2, "
                + "defeated_goblinking:plains:2, defeated_queen:mistlands:2, "
                + "defeated_fader:ashlands:2",
                "boss:biome:multiplier, comma separated. Kill a biome's boss and the things "
                + "that biome gives you stack better. The highest earned entry naming a biome "
                + "wins, so entries never compound; one boss may name several biomes.\n"
                + "Biomes: meadows, blackforest, ocean, swamp, mountain, plains, mistlands, "
                + "ashlands, deepnorth, and all.\n"
                + "Which biome an item belongs to is worked out from the game's own tables, "
                + "not a list here: the vegetation table places a copper deposit in the Black "
                + "Forest and the deposit says it drops copper ore, so copper ore is a Black "
                + "Forest item. Creatures come through the spawn table and their drops, and "
                + "bars and cooked food inherit from what they are made of. Items no table "
                + "reaches stay at vanilla - see BiomeOverrides.\n"
                + "An item found in several biomes belongs to the earliest, because that is "
                + "where you first had to carry it home.\n"
                + "Any global key works, not only boss keys, so a modded key can be named.");

            ProgressionOrder = config.Bind("Progression", "ProgressionOrder",
                "defeated_eikthyr, defeated_gdking, defeated_bonemass, defeated_dragon, "
                + "defeated_goblinking, defeated_queen, defeated_fader",
                "The order bosses come in, which is what makes ProgressionStep mean "
                + "'later'. A key not named here still unlocks its own group; it just never "
                + "counts as later than anything.\n"
                + "Does nothing while ProgressionStep is 0, which is the default.");

            ProgressionStep = config.Bind("Progression", "ProgressionStep", 0f,
                "How much every boss after the one that unlocked a group raises it again, "
                + "compounding. 0.1 would make building 2x at Eikthyr, 2.2x once The Elder "
                + "is down, 2.42x after Bonemass, 3.54x with all seven dead.\n"
                + "Off, because ten extra wood in a slot is not a reward anyone can feel, "
                + "and because it produces stack sizes like 133 that read as a bug next to "
                + "vanilla's round numbers. It also drifts past the flat 2x this mod argues "
                + "is the honest amount. Progression is meant to finish at Bonemass: by then "
                + "you have portals, a cart and a longship, which is the game solving hauling "
                + "by itself.");

            LiftPortalRuleAt = config.Bind("Progression", "LiftPortalRuleAt", "swamp",
                "The biome whose tier lifts the portal rule, so ore and bars start stacking "
                + "when its boss falls. The Swamp, because Bonemass is where iron begins and "
                + "by then the copper runs are behind you. Blank to keep the rule until "
                + "IncludeNonTeleportable is turned on by hand.");

            BiomeOverrides = config.Bind("Progression", "BiomeOverrides",
                // Roots. Everything made from these is placed by the recipe pass, so this
                // list is far shorter than the number of items it ends up accounting for.
                "CopperOre:blackforest, SilverOre:mountain, IronScrap:swamp, IronOre:swamp, "
                + "BlackMetalScrap:plains, FlametalOre:ashlands, FlametalOreNew:ashlands, "
                + "Flametal:ashlands, FlametalNew:ashlands, CopperScrap:blackforest, "
                + "BronzeScrap:blackforest, MoltenCore:swamp, YmirRemains:blackforest, "
                + "FineWood:meadows, RoundLog:meadows, SurtlingCore:blackforest, "
                // Foraged and farmed. The seeds and the cooked forms follow from these.
                + "Carrot:blackforest, Turnip:swamp, Onion:mountain, Barley:plains, "
                + "Flax:plains, Vineberry:mistlands, Honey:meadows, QueenBee:meadows, "
                + "MushroomYellow:blackforest, MushroomBlue:mistlands, "
                + "MushroomBzerker:mistlands, Fiddleheadfern:mistlands, Sap:mistlands, "
                + "Tar:plains, WitheredBone:swamp, Wisp:mistlands, Larva:mistlands, "
                // Chest and dungeon loot.
                + "Amber:blackforest, AmberPearl:blackforest, Ruby:blackforest, "
                + "SilverNecklace:mountain, GemstoneBlue:mistlands, GemstoneGreen:mistlands, "
                + "GemstoneRed:mistlands, BlackCore:mistlands, DvergrKeyFragment:mistlands, "
                + "DvergrNeedle:mistlands, Ectoplasm:mistlands, GroveHeartwood:mistlands, "
                + "Thunderstone:mistlands, VegvisirShard_Bonemass:swamp, "
                // Water. Fish are placed rather than derived; nothing spawns them from a table.
                + "Fish1:ocean, Fish2:ocean, Fish3:ocean, Fish5:ocean, Fish6:ocean, "
                + "Fish7:ocean, Fish8:ocean, Fish9:ocean, Fish4_cave:mountain, "
                + "Fish10:ashlands, Fish11:ashlands, Fish12:ashlands, FishRaw:ocean, "
                + "FishAnglerRaw:ocean, FreshSeaweed:ocean, Chitin:ocean, "
                + "BonemawSerpentScale:ashlands, "
                // Creatures that only come out of a location, so their drops are invisible.
                + "TrophyForestTroll:blackforest, TrophySkeletonHildir:blackforest, "
                + "TrophyGhost:swamp, TrophyDraugrFem:swamp, TrophySkeletonPoison:swamp, "
                + "TrophySurtling:swamp, BlobVial:swamp, draugr_arrow:swamp, "
                + "TrophyCultist:mountain, TrophyCultist_Hildir:mountain, TrophyUlv:mountain, "
                + "WolfClaw:mountain, WolfHairBundle:mountain, "
                + "TrophyGoblinShaman:plains, TrophyGoblinBruteBrosBrute:plains, "
                + "TrophyGoblinBruteBrosShaman:plains, GoblinSpear:plains, JuteBlue:plains, "
                + "JuteRed:plains, BombBlob_Tar:plains, "
                + "TrophyGrowth:mistlands, TrophyKvastur:ashlands, TrophyCharredMage:ashlands, "
                + "CuredSquirrelHamstring:mistlands, TurretBoltBone:mistlands, "
                // Ashlands, which is almost entirely location work.
                + "AsksvinEgg:ashlands, AsksvinCarrionNeck:ashlands, "
                + "AsksvinCarrionPelvic:ashlands, AsksvinCarrionRibcage:ashlands, "
                + "AsksvinCarrionSkull:ashlands, ChickenEgg:ashlands, ChickenMeat:ashlands, "
                + "CharredCogwheel:ashlands, BellFragment:ashlands, Pot_Shard_Red:ashlands, "
                + "PungentPebbles:ashlands, CandleWick:ashlands, FragrantBundle:ashlands, "
                + "PowderedDragonEgg:ashlands, "
                // Spices, each named after where it comes from.
                + "SpiceForests:blackforest, SpiceMountains:mountain, SpicePlains:plains, "
                + "SpiceOceans:ocean, SpiceMistlands:mistlands, SpiceAshlands:ashlands, "
                // Seeds the crop recipes do not run backwards to, and the two odd consumables.
                + "OnionSeeds:mountain, VineberrySeeds:mistlands, VineGreenSeeds:mistlands, "
                + "FishingBait:ocean, MeadTrollPheromones:blackforest",
                "prefab:biome, comma separated, for items the game's tables cannot place.\n"
                + "Iron is the reason this exists. Iron scrap is not placed in the world and "
                + "is not dropped by anything that spawns in one - it is inside Sunken Crypts, "
                + "and a location holds its prefab as a soft reference that is not loaded "
                + "until the game wants it. Forcing dungeon interiors to load on the way into "
                + "a world, to learn something that fits on this line, is not a trade worth "
                + "making.\n"
                + "The item list shows every item that ended up with no biome, so this line "
                + "can be filled in from what is actually missing rather than guessed at.");

            DeferToUtangard = config.Bind("Progression", "DeferToUtangard", true,
                "When Utangard is installed, ask it whether the group has earned a boss "
                + "rather than reading the world key. Utangard opens a biome only when every "
                + "member of the group was at that boss's death, so the two answers differ "
                + "whenever somebody was offline for a kill - and without this, stacks would "
                + "arrive for a biome Utangard still has fenced off. No effect when Utangard "
                + "is not installed.");

            WriteItemList = config.Bind("Diagnostics", "WriteItemList", true,
                "Write ezomic.valheim.yoke.items.txt beside this file: every item in the "
                + "game, what Yoke did to it, and which rule left it alone when it did "
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
