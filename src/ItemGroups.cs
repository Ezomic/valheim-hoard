using System.Collections.Generic;
using UnityEngine;

namespace Hoard
{
    /// <summary>
    /// Sorts every item into one group, so a boss can raise the stacks of building material
    /// without raising the stacks of arrows.
    ///
    /// The groups are read off the game's own systems rather than written down here. The
    /// Hammer's piece table already knows what building material is, and it knows it for
    /// whatever a content mod added this morning; a list in this file would know it for the
    /// vanilla items of the day it was typed. Same argument for the Cultivator and crops, and
    /// for the portal rule and metal.
    ///
    /// One group per item, because the whole point is that a boss upgrades one thing. An item
    /// that qualifies twice - iron nails are both building material and metal - is settled by
    /// the order in Classify, which is written worst-consequence-first: being wrong about
    /// metal matters more than being wrong about timber, because metal is the group the
    /// portal rule is protecting.
    /// </summary>
    internal static class ItemGroups
    {
        public const string Metal = "metal";
        public const string Ammo = "ammo";
        public const string Food = "food";
        public const string Trophy = "trophy";
        public const string Farming = "farming";
        public const string Building = "building";
        public const string Other = "other";

        /// <summary>Every group, in the order Classify tests them. Also the order the item list prints.</summary>
        public static readonly string[] All =
        {
            Building, Farming, Metal, Food, Ammo, Trophy, Other
        };

        /// <summary>The pseudo-group a tier may name to mean "everything at once".</summary>
        public const string Everything = "all";

        private static HashSet<string> _building;
        private static HashSet<string> _farming;

        /// <summary>
        /// Dropped whenever ObjectDB is rebuilt. The piece tables are read out of it, so a
        /// set built against the previous database is describing items that no longer exist -
        /// the same trap as any other static cache across a world change.
        /// </summary>
        public static void Invalidate()
        {
            _building = null;
            _farming = null;
        }

        public static string Classify(string prefabName, ItemDrop.ItemData.SharedData shared)
        {
            // Portal-blocked first. This is the group the mod's whole pacing argument is
            // about, and iron nails being called metal would be a smaller mistake than iron
            // being called building material.
            if (!shared.m_teleportable) return Metal;

            if (shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo) return Ammo;
            if (shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable) return Food;
            if (shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy) return Trophy;

            // Crops before building, because the Hammer's table also wants a few of them.
            if (Cultivated().Contains(prefabName)) return Farming;
            if (Buildable().Contains(prefabName)) return Building;

            return Other;
        }

        private static HashSet<string> Buildable()
        {
            return _building ?? (_building = ResourcesOf("Hammer"));
        }

        private static HashSet<string> Cultivated()
        {
            return _farming ?? (_farming = ResourcesOf("Cultivator"));
        }

        /// <summary>
        /// Every item named as a build cost by any piece on this tool's piece table.
        ///
        /// Empty rather than thrown when the tool cannot be found. The result is that its
        /// group is empty and those items fall through to "other", which loses a boss its
        /// theme - a bad outcome, but a far better one than an exception on the load path of
        /// a mod whose job is to widen stacks.
        /// </summary>
        private static HashSet<string> ResourcesOf(string toolName)
        {
            var found = new HashSet<string>();

            var db = ObjectDB.instance;
            if (db == null) return found;

            var tool = db.GetItemPrefab(toolName);
            if (tool == null)
            {
                HoardPlugin.Log.LogWarning(
                    toolName + " was not in ObjectDB, so its item group is empty this pass.");
                return found;
            }

            var drop = tool.GetComponent<ItemDrop>();
            var table = drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null
                ? drop.m_itemData.m_shared.m_buildPieces
                : null;
            if (table == null || table.m_pieces == null) return found;

            foreach (var pieceObject in table.m_pieces)
            {
                if (pieceObject == null) continue;

                var piece = pieceObject.GetComponent<Piece>();
                if (piece == null || piece.m_resources == null) continue;

                foreach (var requirement in piece.m_resources)
                {
                    if (requirement == null || requirement.m_resItem == null) continue;

                    var name = requirement.m_resItem.gameObject != null
                        ? requirement.m_resItem.gameObject.name
                        : null;

                    if (!string.IsNullOrEmpty(name)) found.Add(name);
                }
            }

            return found;
        }
    }
}
