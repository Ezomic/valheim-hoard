using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hoard
{
    /// <summary>
    /// Which biome each item comes from, worked out from the tables the game already keeps.
    ///
    /// Nothing here is a list of items. The world's vegetation table says a copper deposit
    /// belongs to the Black Forest and the deposit says it drops copper ore, so copper ore is
    /// a Black Forest item; the spawn table says Fulings belong to the Plains and the Fuling
    /// says it drops black metal scrap, so black metal is a Plains item. Both facts are the
    /// game's, which means they stay true through an update and cover whatever a content mod
    /// adds without anyone typing it here.
    ///
    /// Four routes in:
    ///
    ///   ZoneSystem.m_vegetation  - berries, mushrooms, thistle, rocks, ore deposits, trees
    ///   SpawnSystem spawn lists  - every creature, through its CharacterDrop
    ///   smelter/cooking recipes  - bars and cooked food, inheriting from what they are made of
    ///   BiomeOverrides           - the handful none of the above can reach
    ///
    /// That last one exists because of iron. Iron scrap is neither placed in the world nor
    /// dropped by anything that spawns in it - it is inside Sunken Crypts, and a location
    /// holds its prefab as a SoftReference that is not loaded until the game wants it. Walking
    /// dungeon interiors to find it would mean forcing asset loads on the way into a world, to
    /// learn a fact that fits on one config line.
    ///
    /// Earliest biome wins. Plenty of items appear in several - wood is everywhere - and the
    /// one that matters is where you first have to haul it.
    /// </summary>
    internal static class BiomeIndex
    {
        public const string None = "none";

        /// <summary>The pseudo-group a tier may name to mean "everything at once".</summary>
        public const string Everything = "all";

        /// <summary>
        /// Biomes in progression order, which is what "earliest" means. Ocean and Deep North
        /// are here so their items are visible in the item list rather than silently unsorted;
        /// no boss unlocks them by default.
        /// </summary>
        private static readonly KeyValuePair<string, Heightmap.Biome>[] Ordered =
        {
            new KeyValuePair<string, Heightmap.Biome>("meadows", Heightmap.Biome.Meadows),
            new KeyValuePair<string, Heightmap.Biome>("blackforest", Heightmap.Biome.BlackForest),
            new KeyValuePair<string, Heightmap.Biome>("ocean", Heightmap.Biome.Ocean),
            new KeyValuePair<string, Heightmap.Biome>("swamp", Heightmap.Biome.Swamp),
            new KeyValuePair<string, Heightmap.Biome>("mountain", Heightmap.Biome.Mountain),
            new KeyValuePair<string, Heightmap.Biome>("plains", Heightmap.Biome.Plains),
            new KeyValuePair<string, Heightmap.Biome>("mistlands", Heightmap.Biome.Mistlands),
            new KeyValuePair<string, Heightmap.Biome>("ashlands", Heightmap.Biome.AshLands),
            new KeyValuePair<string, Heightmap.Biome>("deepnorth", Heightmap.Biome.DeepNorth)
        };

        public static readonly string[] All = BuildNames();

        private static string[] BuildNames()
        {
            var names = new string[Ordered.Length + 1];
            for (var i = 0; i < Ordered.Length; i++) names[i] = Ordered[i].Key;
            names[Ordered.Length] = None;
            return names;
        }

        /// <summary>Prefab name to its index in Ordered. Absent means no biome was found.</summary>
        private static Dictionary<string, int> _biomeOf;

        /// <summary>
        /// Whether the last build had its sources. ZoneSystem, SpawnSystem and ZNetScene only
        /// exist once a world is loading, so the first pass of a session - which runs from
        /// ObjectDB.Awake, long before any of that - would otherwise cache an empty index and
        /// keep answering "no biome" for the rest of the session.
        /// </summary>
        private static bool _complete;

        public static void Invalidate()
        {
            _biomeOf = null;
            _complete = false;
        }

        public static bool Complete
        {
            get { return _complete; }
        }

        /// <summary>
        /// A pure lookup. Building is Prepare's job and is done once per pass, not once per
        /// item: while the index is incomplete there is nothing to stop a build-on-demand
        /// running again for every item in the database, and the first version of this walked
        /// the vegetation table and every prefab in the scene a thousand times over.
        /// </summary>
        public static string BiomeOf(string prefabName)
        {
            int index;
            if (_biomeOf != null && _biomeOf.TryGetValue(prefabName, out index)) return Ordered[index].Key;

            return None;
        }

        /// <summary>Builds the index if it is not already complete. Called once per pass.</summary>
        public static void Prepare()
        {
            Build();
        }

        /// <summary>How many items the index has placed, for the log and the item list.</summary>
        public static int Count
        {
            get { return _biomeOf == null ? 0 : _biomeOf.Count; }
        }

        private static void Build()
        {
            if (_complete && _biomeOf != null) return;

            var zone = ZoneSystem.instance;
            var spawn = UnityEngine.Object.FindObjectOfType<SpawnSystem>();
            var scene = ZNetScene.instance;

            // Rebuilt from scratch each attempt rather than topped up, so a partial early
            // build cannot leave a wrong earliest-biome behind once the rest arrives.
            var found = new Dictionary<string, int>();

            if (zone != null && zone.m_vegetation != null)
                foreach (var vegetation in zone.m_vegetation)
                    if (vegetation != null && vegetation.m_enable)
                        Harvest(found, vegetation.m_prefab, vegetation.m_biome);

            if (spawn != null && spawn.m_spawnLists != null)
                foreach (var list in spawn.m_spawnLists)
                {
                    if (list == null || list.m_spawners == null) continue;
                    foreach (var spawner in list.m_spawners)
                        if (spawner != null && spawner.m_enabled)
                            Harvest(found, spawner.m_prefab, spawner.m_biome);
                }

            if (scene != null && scene.m_prefabs != null) Bosses(found, scene);

            // Before the conversions and recipes, not only after. An override is a root fact -
            // copper ore is Black Forest - and everything made from it should inherit that.
            // Applied last as well, so a hand-written answer still beats a derived one.
            // Copper bars came out unplaced when this only ran at the end: the smelter pass
            // went looking for copper ore before the override had put it on the map.
            ApplyOverrides(found);

            // Together and until it settles, rather than one after the other. The chains cross
            // between the two: barley is milled to flour by a station, the flour is made into
            // dough by a recipe, and the dough is baked back at a station. Running all the
            // conversions and then all the recipes leaves bread unplaced however many passes
            // each gets, because the step it is waiting on happens in the other list.
            for (var pass = 0; pass < 8; pass++)
            {
                var moved = Craft(found);
                if (scene != null && scene.m_prefabs != null) moved |= Convert(found, scene);
                if (!moved) break;
            }

            ApplyOverrides(found);

            _biomeOf = found;
            _complete = zone != null && spawn != null && scene != null;

            if (_complete)
            {
                HoardPlugin.Log.LogInfo("Biome index built: " + found.Count + " item(s) placed.");
                return;
            }

            // Named, because "incomplete" on its own sends you looking in the wrong place. The
            // three appear at different moments on the way into a world and this says which
            // one has not turned up yet.
            var missing = new List<string>();
            if (zone == null) missing.Add("ZoneSystem");
            if (spawn == null) missing.Add("SpawnSystem");
            if (scene == null) missing.Add("ZNetScene");

            HoardPlugin.Log.LogInfo(
                "Biome index partial: " + found.Count + " item(s) placed, still waiting on "
                + string.Join(" and ", missing.ToArray()) + ".");
        }

        /// <summary>
        /// Everything this prefab yields, recorded against the earliest biome it belongs to.
        ///
        /// In children, not just on the root: an ore deposit carries its MineRock5 on a child,
        /// and a creature its CharacterDrop beside the body rather than on it.
        /// </summary>
        private static void Harvest(Dictionary<string, int> found, GameObject prefab, Heightmap.Biome mask)
        {
            if (prefab == null) return;

            var index = Earliest(mask);
            if (index < 0) return;

            foreach (var pickable in prefab.GetComponentsInChildren<Pickable>(true))
                if (pickable != null) Record(found, pickable.m_itemPrefab, index);

            foreach (var rock in prefab.GetComponentsInChildren<MineRock5>(true))
                if (rock != null) Record(found, rock.m_dropItems, index);

            foreach (var destroyed in prefab.GetComponentsInChildren<DropOnDestroyed>(true))
                if (destroyed != null) Record(found, destroyed.m_dropWhenDestroyed, index);

            foreach (var drop in prefab.GetComponentsInChildren<CharacterDrop>(true))
            {
                if (drop == null || drop.m_drops == null) continue;
                foreach (var entry in drop.m_drops)
                    if (entry != null) Record(found, entry.m_prefab, index);
            }

            // Spawners: a greydwarf nest, a surtling geyser, the skeleton piles in a crypt.
            // The creature is not in the world's spawn table at all - the thing that makes it
            // is - so without this every trophy and drop that only comes from a nest is
            // invisible to the index.
            foreach (var area in prefab.GetComponentsInChildren<SpawnArea>(true))
            {
                if (area == null || area.m_prefabs == null) continue;

                foreach (var spawn in area.m_prefabs)
                {
                    if (spawn == null || spawn.m_prefab == null) continue;

                    foreach (var drop in spawn.m_prefab.GetComponentsInChildren<CharacterDrop>(true))
                    {
                        if (drop == null || drop.m_drops == null) continue;
                        foreach (var entry in drop.m_drops)
                            if (entry != null) Record(found, entry.m_prefab, index);
                    }
                }
            }

            foreach (var tree in prefab.GetComponentsInChildren<TreeBase>(true))
            {
                if (tree == null) continue;
                Record(found, tree.m_dropWhenDestroyed, index);

                // A felled tree becomes a log, and the log is what actually drops the wood.
                if (tree.m_logPrefab == null) continue;
                foreach (var log in tree.m_logPrefab.GetComponentsInChildren<TreeLog>(true))
                    if (log != null) Record(found, log.m_dropWhenDestroyed, index);
            }
        }

        /// <summary>
        /// Bars and cooked food, taking the biome of what they are made from.
        ///
        /// Without this the mapping covers the ore you mine and not the bar you carry home,
        /// which is backwards - the bar is the load.
        /// </summary>
        private static bool Convert(Dictionary<string, int> found, ZNetScene scene)
        {
            var moved = false;

            foreach (var prefab in scene.m_prefabs)
            {
                if (prefab == null) continue;

                var smelter = prefab.GetComponent<Smelter>();
                if (smelter != null && smelter.m_conversion != null)
                    foreach (var conversion in smelter.m_conversion)
                        moved |= Inherit(found, conversion.m_from, conversion.m_to);

                var cooking = prefab.GetComponent<CookingStation>();
                if (cooking != null && cooking.m_conversion != null)
                    foreach (var conversion in cooking.m_conversion)
                        moved |= Inherit(found, conversion.m_from, conversion.m_to);

                // Every mead in the game is a fermenter conversion and nothing else, so
                // without this the whole shelf stays unplaced.
                var fermenter = prefab.GetComponent<Fermenter>();
                if (fermenter != null && fermenter.m_conversion != null)
                    foreach (var conversion in fermenter.m_conversion)
                        moved |= Inherit(found, conversion.m_from, conversion.m_to);
            }

            return moved;
        }

        /// <summary>
        /// A boss's own drops - its trophy, and the thing you put on the sacrificial stones.
        ///
        /// Bosses are not in the spawn tables. They stand in a location that the world places
        /// deliberately, so nothing else here can see them. But the boss prefab knows the
        /// global key its death sets, and the tier table already says which biome that key
        /// belongs to, so the two together place its drops in its own biome without either
        /// side naming an item. Moder's trophy is a Mountain item because Moder's key is the
        /// Mountain's key.
        ///
        /// It also means a modded boss added to ProgressionTiers brings its own drops with it.
        /// </summary>
        private static void Bosses(Dictionary<string, int> found, ZNetScene scene)
        {
            foreach (var prefab in scene.m_prefabs)
            {
                if (prefab == null) continue;

                var character = prefab.GetComponent<Character>();
                if (character == null || string.IsNullOrEmpty(character.m_defeatSetGlobalKey)) continue;

                var biome = Progression.BiomeFor(character.m_defeatSetGlobalKey);
                if (biome == null) continue;

                var index = IndexOf(biome);
                if (index < 0) continue;

                foreach (var drop in prefab.GetComponentsInChildren<CharacterDrop>(true))
                {
                    if (drop == null || drop.m_drops == null) continue;
                    foreach (var entry in drop.m_drops)
                        if (entry != null) Record(found, entry.m_prefab, index);
                }
            }
        }

        private static int IndexOf(string biome)
        {
            for (var i = 0; i < Ordered.Length; i++)
                if (Ordered[i].Key == biome) return i;

            return -1;
        }

        /// <summary>
        /// Everything craftable, taking the biome of its **latest** ingredient.
        ///
        /// Latest and not earliest, which is the opposite of the rule everywhere else, and for
        /// a reason: a found item belongs to the first place it turns up, but a made item
        /// belongs to the point you could first make it, and that is decided by the ingredient
        /// you get last. Bread is a Plains thing because barley is, however common the rest of
        /// it is.
        ///
        /// This is most of what the world's tables cannot reach on their own - every mead,
        /// every cooked meal, nails, arrows - and it lands them without a list. It also quietly
        /// corrects a wrong answer: charred warriors drop bronze, so the spawn tables place it
        /// in the Ashlands, and the recipe placing it in the Black Forest wins because the
        /// index keeps whichever is earlier.
        /// </summary>
        private static bool Craft(Dictionary<string, int> found)
        {
            var db = ObjectDB.instance;
            if (db == null || db.m_recipes == null) return false;

            var placedSomething = false;

            foreach (var recipe in db.m_recipes)
            {
                if (recipe == null || recipe.m_item == null || recipe.m_resources == null) continue;
                if (recipe.m_item.gameObject == null) continue;

                var latest = -1;
                foreach (var requirement in recipe.m_resources)
                {
                    if (requirement == null || requirement.m_resItem == null) continue;
                    if (requirement.m_resItem.gameObject == null) continue;

                    int index;
                    if (!found.TryGetValue(requirement.m_resItem.gameObject.name, out index)) continue;
                    if (index > latest) latest = index;
                }

                // Nothing it is made of has a biome yet. It may on a later pass.
                if (latest < 0) continue;

                var name = recipe.m_item.gameObject.name;
                int existing;
                if (found.TryGetValue(name, out existing) && existing <= latest) continue;

                found[name] = latest;
                placedSomething = true;
            }

            return placedSomething;
        }

        /// <summary>Returns whether this actually placed something, so the loop knows to stop.</summary>
        private static bool Inherit(Dictionary<string, int> found, ItemDrop from, ItemDrop to)
        {
            if (from == null || to == null || from.gameObject == null || to.gameObject == null) return false;

            int index;
            if (!found.TryGetValue(from.gameObject.name, out index)) return false;

            int existing;
            if (found.TryGetValue(to.gameObject.name, out existing) && existing <= index) return false;

            found[to.gameObject.name] = index;
            return true;
        }

        /// <summary>"IronScrap:swamp, Coins:blackforest" - the ones no table can reach.</summary>
        private static void ApplyOverrides(Dictionary<string, int> found)
        {
            var raw = HoardConfig.BiomeOverrides.Value ?? "";

            foreach (var entry in raw.Split(','))
            {
                var text = entry.Trim();
                if (text.Length == 0) continue;

                var parts = text.Split(':');
                if (parts.Length != 2)
                {
                    HoardPlugin.Log.LogWarning(
                        "Ignoring biome override '" + text + "': expected prefab:biome.");
                    continue;
                }

                var biome = parts[1].Trim().ToLowerInvariant();
                var index = -1;
                for (var i = 0; i < Ordered.Length; i++)
                    if (Ordered[i].Key == biome) { index = i; break; }

                if (index < 0)
                {
                    HoardPlugin.Log.LogWarning(
                        "Ignoring biome override '" + text + "': '" + biome + "' is not a biome.");
                    continue;
                }

                // Overwrites rather than taking the earliest, because it is the answer someone
                // typed on purpose and the whole reason it exists is that the tables are wrong
                // or silent about that item.
                found[parts[0].Trim()] = index;
            }
        }

        private static void Record(Dictionary<string, int> found, DropTable table, int index)
        {
            if (table == null || table.m_drops == null) return;

            foreach (var drop in table.m_drops)
                if (drop.m_item != null) Record(found, drop.m_item.name, index);
        }

        private static void Record(Dictionary<string, int> found, GameObject prefab, int index)
        {
            if (prefab != null) Record(found, prefab.name, index);
        }

        private static void Record(Dictionary<string, int> found, string prefabName, int index)
        {
            if (string.IsNullOrEmpty(prefabName)) return;

            int existing;
            if (found.TryGetValue(prefabName, out existing) && existing <= index) return;

            found[prefabName] = index;
        }

        /// <summary>
        /// The first biome in progression order named by this mask.
        ///
        /// A mask, not a value: an entry may name several biomes at once, and the earliest is
        /// the one where you first have to carry the thing home.
        /// </summary>
        private static int Earliest(Heightmap.Biome mask)
        {
            for (var i = 0; i < Ordered.Length; i++)
                if ((mask & Ordered[i].Value) != 0) return i;

            return -1;
        }
    }
}
