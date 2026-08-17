using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Hoard
{
    /// <summary>
    /// Writes a plain-text table of every item in ObjectDB: what it is in vanilla, what Hoard
    /// made it, and - when Hoard left it alone - which rule stopped it.
    ///
    /// Why this exists: ExcludeItems is written in prefab names, and nobody knows that tin
    /// ore is "TinOre" or that a carrot seed is "CarrotSeeds". A per-item setting you cannot
    /// spell is not a setting, so every mod in this category that offers one ships a list
    /// like this beside it.
    ///
    /// It also answers the only support question a mod like this ever gets - "why did this
    /// item not change" - and answers it without asking anyone to turn on Verbose and read a
    /// log. That is the reason the rows are built by StackTuner as it runs rather than by a
    /// second pass over the database: a separate scan would be a second implementation of
    /// the eligibility rules, and the first thing it would do is disagree with the first one.
    /// </summary>
    internal static class ItemDump
    {
        /// <summary>
        /// Prefix for "this group's turn has not come yet". Shared with StackTuner so the
        /// header can tell those apart from the rules that refuse an item outright.
        /// </summary>
        internal const string Awaiting = "awaiting ";

        internal readonly struct Row
        {
            public readonly string Prefab;
            public readonly string Name;
            public readonly string Type;
            public readonly string Group;
            public readonly int OriginalStack;
            public readonly int Stack;
            public readonly float OriginalWeight;
            public readonly float Weight;

            /// <summary>Empty when Hoard owns the values, otherwise the rule that stopped it.</summary>
            public readonly string Note;

            public Row(string prefab, string name, string type, string group,
                       int originalStack, int stack,
                       float originalWeight, float weight, string note)
            {
                Prefab = prefab;
                Name = name;
                Type = type;
                Group = group;
                OriginalStack = originalStack;
                Stack = stack;
                OriginalWeight = originalWeight;
                Weight = weight;
                Note = note ?? "";
            }
        }

        /// <summary>
        /// The reasons, in the order they are counted in the header. Fixed rather than
        /// derived from the rows so the summary line reads the same every run even when a
        /// category is empty - a count that appears and disappears is hard to compare
        /// against yesterday's file.
        /// </summary>
        private static readonly string[] ReasonOrder =
        {
            StackTuner.Equipment,
            StackTuner.PortalBlocked,
            StackTuner.Trophy,
            StackTuner.Excluded
        };

        /// <summary>
        /// The last text written. Apply runs at least twice a session - Awake, then
        /// CopyOtherDB - and again on every world load, so without this the same file is
        /// rewritten several times per session for nothing.
        /// </summary>
        private static string _lastWritten;

        public static void Write(List<Row> rows)
        {
            if (!HoardConfig.WriteItemList.Value || rows == null || rows.Count == 0) return;

            var path = HoardConfig.ItemListPath;
            if (string.IsNullOrEmpty(path)) return;

            var text = Build(rows);
            if (text == _lastWritten) return;

            try
            {
                // No BOM. The file is meant to be opened in whatever the reader has to hand,
                // and a BOM shows up as stray characters in a few of them.
                File.WriteAllText(path, text, new UTF8Encoding(false));
                _lastWritten = text;
                HoardPlugin.Log.LogInfo("Wrote the item list to " + Path.GetFileName(path) + ".");
            }
            catch (Exception e)
            {
                // A diagnostic file that cannot be written is not worth failing a load over -
                // the folder may be read-only, or something may have the file open.
                HoardPlugin.Log.LogWarning("Could not write the item list: " + e.Message);
            }
        }

        private static string Build(List<Row> rows)
        {
            // Group first, because the group is what a boss raises: someone asking "what did
            // Eikthyr just do for me" wants those items adjacent, and someone asking about one
            // item finds it by searching rather than by scrolling.
            rows.Sort((a, b) =>
            {
                var byGroup = Array.IndexOf(ItemGroups.All, a.Group)
                              .CompareTo(Array.IndexOf(ItemGroups.All, b.Group));
                if (byGroup != 0) return byGroup;

                return string.Compare(a.Prefab, b.Prefab, StringComparison.OrdinalIgnoreCase);
            });

            var cells = new List<string[]>(rows.Count);
            var changed = 0;
            var alreadyRight = 0;
            var waiting = 0;
            var counts = new Dictionary<string, int>();

            foreach (var row in rows)
            {
                // "awaiting <boss>" is not a rule refusing the item, it is the item's turn not
                // having come round yet. Counting it with the refusals would put most of the
                // database under "left alone" on a fresh world and make the mod look broken.
                if (row.Note.StartsWith(Awaiting, StringComparison.Ordinal))
                {
                    waiting++;
                }
                else if (row.Note.Length > 0)
                {
                    // By prefix, because a rule may say when it lifts - "portal-blocked until
                    // defeated_bonemass" is still the portal rule and belongs in its tally.
                    var reason = Reason(row.Note);
                    int n;
                    counts.TryGetValue(reason, out n);
                    counts[reason] = n + 1;
                }
                else if (row.Stack != row.OriginalStack || row.Weight != row.OriginalWeight)
                {
                    changed++;
                }
                else
                {
                    alreadyRight++;
                }

                cells.Add(new[]
                {
                    row.Prefab,
                    row.Name,
                    row.Type,
                    row.Group,
                    Range(row.OriginalStack.ToString(), row.Stack.ToString()),
                    Range(Weight(row.OriginalWeight), Weight(row.Weight)),
                    row.Note
                });
            }

            var headers = new[] { "Prefab", "Name", "Type", "Group", "Stack", "Weight", "Status" };
            var widths = new int[headers.Length];
            for (var i = 0; i < headers.Length; i++) widths[i] = headers[i].Length;
            foreach (var row in cells)
                for (var i = 0; i < row.Length; i++)
                    if (row[i].Length > widths[i]) widths[i] = row[i].Length;

            var sb = new StringBuilder(rows.Count * 96);

            sb.AppendLine("Hoard " + HoardPlugin.PluginVersion + " - every item in this session");
            sb.AppendLine();
            sb.AppendLine("  Generated on every run, so edits here are overwritten. The settings are in");
            sb.AppendLine("  " + Path.GetFileName(HoardConfig.ConfigPath) + " beside this file.");
            sb.AppendLine();
            sb.AppendLine("  The first column is the prefab name, which is what ExcludeItems takes.");
            sb.AppendLine("  An arrow means Hoard changed that value. A single number means it did not,");
            sb.AppendLine("  and the last column says why: a rule that refused it, or the boss whose");
            sb.AppendLine("  death will raise that group.");
            sb.AppendLine();
            sb.AppendLine("  Earned so far: " + Progression.Describe());
            sb.AppendLine();
            sb.AppendLine("  Settings: StackMultiplier " + Number(HoardConfig.StackMultiplier.Value)
                          + ", StackCap " + HoardConfig.StackCap.Value
                          + ", WeightMultiplier " + Number(HoardConfig.WeightMultiplier.Value));
            sb.AppendLine("            IncludeNonTeleportable " + Flag(HoardConfig.IncludeNonTeleportable.Value)
                          + ", IncludeTrophies " + Flag(HoardConfig.IncludeTrophies.Value));
            sb.AppendLine();
            // State, not a pass. These count what the items are now, which is why they do not
            // match the "Retuned n" line in the log: that one counts what this particular
            // pass moved, and on the second setup of a session it moves nothing because the
            // first one already did.
            sb.AppendLine("  " + rows.Count + " items: " + changed + " raised above vanilla, "
                          + waiting + " waiting on a boss, "
                          + Total(counts) + " left alone by a rule, "
                          + alreadyRight + " unchanged");
            sb.AppendLine("  " + Breakdown(counts));
            sb.AppendLine();

            AppendRow(sb, headers, widths);
            var rule = new string[headers.Length];
            for (var i = 0; i < rule.Length; i++) rule[i] = new string('-', widths[i]);
            AppendRow(sb, rule, widths);

            foreach (var row in cells) AppendRow(sb, row, widths);

            return sb.ToString();
        }

        private static void AppendRow(StringBuilder sb, string[] cells, int[] widths)
        {
            var line = new StringBuilder();
            for (var i = 0; i < cells.Length; i++)
            {
                // The last column is not padded: trailing spaces on every line of a 1,000
                // line file are invisible and survive being pasted somewhere else.
                if (i == cells.Length - 1) line.Append(cells[i]);
                else line.Append(cells[i].PadRight(widths[i])).Append("  ");
            }

            sb.AppendLine(line.ToString().TrimEnd());
        }

        private static string Range(string from, string to)
        {
            return from == to ? from : from + " -> " + to;
        }

        /// <summary>
        /// Invariant, not the machine's locale. This was written on a Dutch machine, where
        /// the default put a decimal comma in every weight - so resin read "0,3", which is
        /// wrong in a file whose other numbers came out of a .cfg that uses a point, and
        /// actively misleading next to a column of integers.
        /// </summary>
        private static string Weight(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>Lowercase, so the header matches how BepInEx writes bools in the .cfg.</summary>
        private static string Flag(bool value)
        {
            return value ? "true" : "false";
        }

        private static string Number(float value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The bare rule a note begins with, so qualified notes still tally.</summary>
        private static string Reason(string note)
        {
            foreach (var reason in ReasonOrder)
                if (note.StartsWith(reason, StringComparison.Ordinal)) return reason;

            return note;
        }

        private static int Total(Dictionary<string, int> counts)
        {
            var total = 0;
            foreach (var count in counts.Values) total += count;
            return total;
        }

        private static string Breakdown(Dictionary<string, int> counts)
        {
            var sb = new StringBuilder("(");
            for (var i = 0; i < ReasonOrder.Length; i++)
            {
                int n;
                counts.TryGetValue(ReasonOrder[i], out n);
                if (i > 0) sb.Append(", ");
                sb.Append(ReasonOrder[i]).Append(' ').Append(n);
            }

            return sb.Append(')').ToString();
        }
    }
}
