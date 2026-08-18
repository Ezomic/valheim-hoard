using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;

namespace Yoke
{
    /// <summary>
    /// Which stacks the world has earned.
    ///
    /// A flat multiplier cannot tell the difference between the two halves of the argument
    /// this mod is built on. Meadows scarcity is the game teaching you to plan; your ninth
    /// trip to the same copper deposit is not teaching you anything. So each boss raises one
    /// group of stacks - Eikthyr the timber and stone you build with, Bonemass the metal you
    /// have by then earned the right to haul - and everything else waits its turn.
    ///
    /// It hangs on world progression rather than per-player progression, and that is not the
    /// lazy choice. Stack size lives on the item prefab in ObjectDB, so "different players,
    /// different stacks" means handing two clients different item databases: one drops a
    /// hundred wood in the shared chest, the other opens it holding a slot bigger than its own
    /// maximum, and the next move writes back through the smaller rules. Silent loss out of a
    /// shared chest is the worst bug a storage mod can have. Global keys are world state that
    /// the server pushes to every client, so everybody computes the same answer from the same
    /// input with no new networking at all.
    ///
    /// Keys only ever accumulate, so the multiplier only ever rises. The ramp can never
    /// produce a stack that is suddenly over its own limit.
    /// </summary>
    internal static class Progression
    {
        private struct Tier
        {
            public string BossKey;
            public string Group;
            public float Multiplier;
        }

        private static List<Tier> _tiers;
        private static string _parsedFrom;

        /// <summary>The group multipliers as of the last Refresh, for detecting real change.</summary>
        private static string _signature;

        /// <summary>Utangard's own answer is used when Utangard is loaded. See EarnedThroughUtangard.</summary>
        private static bool _utangardChecked;
        private static bool _utangardPresent;

        public static void Invalidate()
        {
            _tiers = null;
            _order = null;
            _signature = null;
        }

        /// <summary>
        /// Recomputes and reports whether anything actually moved.
        ///
        /// Every global key arriving on connect comes through GlobalKeyAdd one at a time, so
        /// without this a five-key world would retune the whole item database five times
        /// before the player is even in it.
        /// </summary>
        public static bool Refresh()
        {
            var signature = Signature();
            if (signature == _signature) return false;

            _signature = signature;
            return true;
        }

        /// <summary>
        /// The multiplier this group has earned, or the base when it has earned none.
        ///
        /// Two parts. A tier unlocks the group outright, and then every boss that comes after
        /// that one in the order raises it again by ProgressionStep, compounding.
        ///
        /// The second part is what the late bosses are for. Only three of the seven unlock a
        /// group, so without it the whole feature is over by Bonemass and the back half of
        /// the game gets a reward table that has stopped paying. Ten percent is small enough
        /// that it never turns into the x10 mod by accident: six of them on a doubled stack
        /// is 3.5x, and StackCap is still the ceiling over all of it.
        /// </summary>
        public static float MultiplierFor(string group)
        {
            var best = YokeConfig.ProgressionBase.Value;
            var unlockedAt = -1;

            foreach (var tier in Tiers())
            {
                if (!Applies(tier, group)) continue;
                if (!Earned(tier.BossKey)) continue;
                if (tier.Multiplier <= best) continue;

                best = tier.Multiplier;
                unlockedAt = OrderIndex(tier.BossKey);
            }

            // Nothing has unlocked it, so nothing compounds on top. A group still at the base
            // is not "unlocked at zero percent", it is untouched.
            if (unlockedAt < 0) return best;

            var step = YokeConfig.ProgressionStep.Value;
            if (step <= 0f) return best;

            var order = Order();
            for (var i = unlockedAt + 1; i < order.Length; i++)
                if (Earned(order[i])) best *= 1f + step;

            return best;
        }

        /// <summary>
        /// Where a boss sits in the configured order, or -1 when it is not in it.
        ///
        /// -1 rather than "first" or "last" on purpose: an unlisted key still unlocks its own
        /// group, it simply never counts as before or after anything. That is the honest
        /// answer for a modded boss key somebody added to the tier table and not the order,
        /// and it fails by doing less rather than by silently reordering the vanilla ones.
        /// </summary>
        private static int OrderIndex(string bossKey)
        {
            return Array.IndexOf(Order(), bossKey);
        }

        private static string[] _order;
        private static string _orderFrom;

        private static string[] Order()
        {
            var raw = YokeConfig.ProgressionOrder.Value ?? "";
            if (_order != null && raw == _orderFrom) return _order;

            var parts = raw.Split(',');
            var order = new List<string>(parts.Length);
            foreach (var part in parts)
            {
                var key = part.Trim().ToLowerInvariant();
                if (key.Length > 0) order.Add(key);
            }

            _orderFrom = raw;
            _order = order.ToArray();
            return _order;
        }

        /// <summary>
        /// The next boss that would raise this group, or null when none is left.
        ///
        /// The item list prints it, which turns the file from a record of what happened into
        /// a map of what is coming - and answers "why is my wood still 50" without anyone
        /// having to know the tier table by heart.
        /// </summary>
        public static string PendingFor(string group)
        {
            var best = YokeConfig.ProgressionBase.Value;
            string pending = null;

            foreach (var tier in Tiers())
            {
                if (!Applies(tier, group)) continue;
                if (Earned(tier.BossKey)) continue;
                if (tier.Multiplier <= best) continue;

                // The cheapest unearned improvement, not the biggest: it is the next thing
                // that would change this number.
                if (pending == null) pending = tier.BossKey;
            }

            return pending;
        }

        /// <summary>
        /// Whether the biome that owns the portal rule has been earned - the one place a tier
        /// overrides a rule rather than a number.
        ///
        /// The portal rule is the mod's thesis - hauling ore by cart and boat is pacing, not
        /// an oversight - and a flat IncludeNonTeleportable is a blunt way to hold it. Making
        /// the metal tier the thing that lifts it says the same thing better: the pacing is
        /// real while you are doing your first copper runs, and by the boss that hands you the
        /// iron age you have already paid it. IncludeNonTeleportable stays as the switch for
        /// anyone who disagrees and does not want to wait.
        /// </summary>
        public static bool PortalRuleLifted()
        {
            if (!YokeConfig.ScaleWithProgression.Value) return false;

            var owner = PortalRuleOwner();
            if (string.IsNullOrEmpty(owner)) return false;

            foreach (var tier in Tiers())
            {
                if (tier.Group != owner && tier.Group != BiomeIndex.Everything) continue;
                if (Earned(tier.BossKey)) return true;
            }

            return false;
        }

        /// <summary>The biome whose tier lifts the portal rule - the Swamp, by default.</summary>
        public static string PortalRuleOwner()
        {
            var owner = YokeConfig.LiftPortalRuleAt.Value;
            return owner == null ? "" : owner.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// The biome a boss key is tied to, or null. Used to place a boss's own drops: the
        /// boss prefab knows which key its death sets, and this table knows which biome that
        /// key belongs to, so its trophy lands in its own biome without being named anywhere.
        /// </summary>
        public static string BiomeFor(string bossKey)
        {
            if (string.IsNullOrEmpty(bossKey)) return null;

            var key = bossKey.ToLowerInvariant();
            foreach (var tier in Tiers())
                if (tier.BossKey == key) return tier.Group;

            return null;
        }

        /// <summary>Whether any tier names this group at all, earned or not.</summary>
        private static bool HasTier(string group)
        {
            foreach (var tier in Tiers())
                if (Applies(tier, group)) return true;

            return false;
        }

        private static bool Applies(Tier tier, string group)
        {
            return tier.Group == group || tier.Group == BiomeIndex.Everything;
        }

        /// <summary>
        /// Whether the world has this boss.
        ///
        /// The string overload on purpose. GetGlobalKey(GlobalKeys) reads m_globalKeysEnums,
        /// and the enum in this build runs out at defeated_goblinking - there is no
        /// defeated_queen and no defeated_fader member. Tier off the enum and the ramp
        /// silently stops at the Plains, which strands exactly the players whose hauling
        /// problem is worst. The string form reads m_globalKeysValues and finds any key.
        /// </summary>
        private static bool Earned(string bossKey)
        {
            if (string.IsNullOrEmpty(bossKey)) return false;

            if (UtangardPresent()) return EarnedThroughUtangard(bossKey);

            var zone = ZoneSystem.instance;
            return zone != null && zone.GetGlobalKey(bossKey);
        }

        private static bool UtangardPresent()
        {
            if (!_utangardChecked)
            {
                _utangardChecked = true;
                _utangardPresent = YokeConfig.DeferToUtangard.Value
                                   && Chainloader.PluginInfos.ContainsKey(UtangardGuid);

                if (_utangardPresent)
                    YokePlugin.Log.LogInfo(
                        "Utangard is installed; stacks follow what the group has earned, not what the world has seen.");
            }

            return _utangardPresent;
        }

        private const string UtangardGuid = "ezomic.valheim.utangard";

        /// <summary>
        /// Utangard gates a biome on every member of the group having personally been at that
        /// boss's death, not on the boss having died in the world. Those two answers diverge
        /// the moment one player is offline for a kill, and a Yoke that read the raw key
        /// would hand out plains-era stacks for a biome Utangard still has fenced off - the
        /// two mods disagreeing out loud about the same word.
        ///
        /// So when it is loaded, it is asked. GroupHasKey already folds in the never-regresses
        /// latch and the catch-up deadline, which reading utangard_open_ keys directly would
        /// miss: a biome opened by the deadline is deliberately not latched.
        ///
        /// Isolated and never inlined for the same reason the Core call is - the JIT resolves
        /// a method's assemblies when it first compiles that method, so this must not sit in
        /// anything that runs on a machine without Utangard.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool EarnedThroughUtangard(string bossKey)
        {
            return Utangard.UtangardApi.GroupHasKey(bossKey);
        }

        private static List<Tier> Tiers()
        {
            var raw = YokeConfig.ProgressionTiers.Value ?? "";
            if (_tiers != null && raw == _parsedFrom) return _tiers;

            _parsedFrom = raw;
            _tiers = Parse(raw);
            return _tiers;
        }

        /// <summary>
        /// "defeated_eikthyr:building:2, defeated_bonemass:metal:2".
        ///
        /// A malformed entry is logged and dropped rather than throwing. This is a string a
        /// person types into a .cfg, and the failure that matters is a typo in one of seven
        /// entries taking the other six down with it.
        /// </summary>
        private static List<Tier> Parse(string raw)
        {
            var tiers = new List<Tier>();

            foreach (var entry in raw.Split(','))
            {
                var text = entry.Trim();
                if (text.Length == 0) continue;

                var parts = text.Split(':');
                if (parts.Length != 3)
                {
                    YokePlugin.Log.LogWarning(
                        "Ignoring tier '" + text + "': expected boss:group:multiplier.");
                    continue;
                }

                float multiplier;
                if (!float.TryParse(parts[2].Trim(), NumberStyles.Float,
                                    CultureInfo.InvariantCulture, out multiplier))
                {
                    YokePlugin.Log.LogWarning(
                        "Ignoring tier '" + text + "': '" + parts[2].Trim() + "' is not a number.");
                    continue;
                }

                var group = parts[1].Trim().ToLowerInvariant();
                if (Array.IndexOf(BiomeIndex.All, group) < 0 && group != BiomeIndex.Everything)
                {
                    YokePlugin.Log.LogWarning(
                        "Ignoring tier '" + text + "': '" + group + "' is not one of "
                        + string.Join(", ", BiomeIndex.All) + ", " + BiomeIndex.Everything + ".");
                    continue;
                }

                tiers.Add(new Tier
                {
                    // Lowercased because that is how ZoneSystem stores and looks them up.
                    BossKey = parts[0].Trim().ToLowerInvariant(),
                    Group = group,
                    Multiplier = multiplier
                });
            }

            return tiers;
        }

        /// <summary>What every group is worth right now, as one comparable string.</summary>
        private static string Signature()
        {
            if (!YokeConfig.ScaleWithProgression.Value)
                return "flat:" + YokeConfig.StackMultiplier.Value.ToString(CultureInfo.InvariantCulture);

            var sb = new System.Text.StringBuilder();
            foreach (var group in BiomeIndex.All)
            {
                sb.Append(group).Append(':')
                  .Append(MultiplierFor(group).ToString(CultureInfo.InvariantCulture))
                  .Append('|');
            }

            return sb.Append(PortalRuleLifted() ? "portal-on" : "portal-off").ToString();
        }

        /// <summary>The earned tiers, for the item list header.</summary>
        public static string Describe()
        {
            if (!YokeConfig.ScaleWithProgression.Value) return "off - one flat multiplier";

            // Only groups some tier can actually raise. Listing the rest at 1x reads as a
            // promise that a boss will come for them, and with the default table none will.
            var sb = new System.Text.StringBuilder();
            foreach (var group in BiomeIndex.All)
            {
                if (!HasTier(group)) continue;

                if (sb.Length > 0) sb.Append(", ");
                sb.Append(group).Append(' ')
                  .Append(MultiplierFor(group).ToString("0.##", CultureInfo.InvariantCulture))
                  .Append('x');
            }

            return sb.Length == 0 ? "nothing - no tiers are configured" : sb.ToString();
        }
    }
}
