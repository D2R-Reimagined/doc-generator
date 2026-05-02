using System;
using System.Linq;
using System.Text;

namespace D2TxtImporter.lib.Diff
{
    public static class MarkdownPatchNotesExporter
    {
        public static string Render(SemanticDiffResult diff)
        {
            var sb = new StringBuilder(4096);

            sb.AppendLine("# Patch Notes");
            sb.AppendLine();

            var uAdd = diff.Uniques.Added.Count; var uRem = diff.Uniques.Removed.Count; var uMod = diff.Uniques.Modified.Count;
            var sAdd = diff.Sets.Added.Count; var sRem = diff.Sets.Removed.Count; var sMod = diff.Sets.Modified.Count;
            int FilteredRunewordCount(System.Collections.Generic.IEnumerable<RunewordChange> list, System.Collections.Generic.HashSet<string> paired)
            {
                return list.Count(r => !paired.Contains(NormalizeRunewordName(r.Name)));
            }
            var rwAddedNorm = diff.Runewords.Added.Select(r => NormalizeRunewordName(r.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var rwRemovedNorm = diff.Runewords.Removed.Select(r => NormalizeRunewordName(r.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var rwPaired = new System.Collections.Generic.HashSet<string>(rwAddedNorm.Intersect(rwRemovedNorm), StringComparer.OrdinalIgnoreCase);
            var rAdd = FilteredRunewordCount(diff.Runewords.Added, rwPaired);
            var rRem = FilteredRunewordCount(diff.Runewords.Removed, rwPaired);
            var rMod = diff.Runewords.Modified.Count;
            var bAdd = diff.BaseItems.Added.Count; var bRem = diff.BaseItems.Removed.Count; var bMod = diff.BaseItems.Modified.Count;

            sb.AppendLine("## Summary");
            sb.AppendLine($"- Uniques: {uAdd} added, {uRem} removed, {uMod} modified");
            sb.AppendLine($"- Sets: {sAdd} added, {sRem} removed, {sMod} modified");
            sb.AppendLine($"- Runewords: {rAdd} added, {rRem} removed, {rMod} modified");
            sb.AppendLine($"- Base Items: {bAdd} added, {bRem} removed, {bMod} modified");
            sb.AppendLine();

            RenderUniques(sb, diff);
            RenderSets(sb, diff);
            RenderRunewords(sb, diff);
            RenderBaseItems(sb, diff);
            
            return sb.ToString();
        }

        // Helper: render base code as "{BaseName} [Tier]" using lookup maps.
        // If the BaseName already contains a trailing tier marker (e.g., "Cap [N]"),
        // do NOT append another one to avoid duplicates like "[E] [E]".
        private static string BaseWithTier(SemanticDiffResult diff, string baseCode)
        {
            if (string.IsNullOrWhiteSpace(baseCode)) return "?";
            string name;
            if (!diff.BaseCodeToName.TryGetValue(baseCode, out name) || string.IsNullOrWhiteSpace(name))
            {
                name = baseCode;
            }
            // Detect if name already ends with a tier suffix like " [N]", " [X]", or " [E]".
            bool HasTierSuffix(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return false;
                var t = s.Trim();
                if (t.Length < 4) return false;
                if (t[t.Length - 1] != ']' || t[t.Length - 4] != ' ' || t[t.Length - 3] != '[') return false;
                var c = char.ToUpperInvariant(t[t.Length - 2]);
                return c == 'N' || c == 'X' || c == 'E';
            }

            if (HasTierSuffix(name))
            {
                return name;
            }

            if (diff.BaseCodeToTier.TryGetValue(baseCode, out var tier) && !string.IsNullOrWhiteSpace(tier))
            {
                tier = tier.Trim().ToUpperInvariant();
                return $"{name} [{tier}]";
            }
            return name;
        }

        // Helper: write an H4 header with a guaranteed blank line before it.
        private static void WriteH4Header(StringBuilder sb, string text)
        {
            sb.AppendLine();
            sb.AppendLine($"#### {text}");
        }

        private static void RenderBaseItems(StringBuilder sb, SemanticDiffResult diff)
        {
            sb.AppendLine("## Base Items");
            sb.AppendLine();
            sb.AppendLine("<details>");
            sb.AppendLine("<summary>Show Base Item Changes</summary>");
            sb.AppendLine();
            if (diff.BaseItems.Added.Any())
            {
                sb.AppendLine("### Added");
                foreach (var b in diff.BaseItems.Added.OrderBy(x => x.DisplayName ?? x.Name))
                {
                    var disp = string.IsNullOrWhiteSpace(b.DisplayName) ? b.Name : b.DisplayName;
                    sb.AppendLine($"- {disp} [{b.Name}] ({b.Category})");
                    var baseParts = new System.Collections.Generic.List<string>();
                    AppendFieldCurrent(baseParts, "Base Required Level", b.BaseReqLevelAfter);
                    AppendFieldCurrent(baseParts, "Required Strength", b.ReqStrAfter);
                    AppendFieldCurrent(baseParts, "Required Dexterity", b.ReqDexAfter);
                    AppendFieldCurrent(baseParts, "Durability", b.DurabilityAfter);
                    AppendFieldCurrent(baseParts, "Item Level", b.ItemLevelAfter);
                    AppendFieldCurrent(baseParts, "Sockets", b.SocketsAfter);
                    if (string.Equals(b.Category, "Weapon", StringComparison.OrdinalIgnoreCase))
                    {
                        AppendFieldCurrent(baseParts, "Speed", b.SpeedAfter);
                    }
                    if (string.Equals(b.Category, "Armor", StringComparison.OrdinalIgnoreCase))
                    {
                        AppendFieldCurrent(baseParts, "Armor", b.ArmorAfter);
                        AppendFieldCurrent(baseParts, "Block", b.BlockAfter);
                        AppendFieldCurrent(baseParts, "Damage Prefix", b.DmgPrefixAfter);
                        AppendFieldCurrent(baseParts, "Damage", b.DmgStrAfter);
                    }
                    if (baseParts.Count > 0) sb.AppendLine("  - Base: " + string.Join(", ", baseParts));
                    if (b.DamageAdded.Any())
                    {
                        sb.AppendLine("  - Damage:");
                        foreach (var d in b.DamageAdded)
                        {
                            sb.AppendLine($"    - {d}");
                        }
                    }
                    if (b.AutoMagicAdded.Any())
                    {
                        sb.AppendLine("  - Automagic:");
                        foreach (var p in b.AutoMagicAdded)
                        {
                            sb.AppendLine($"    - {p}");
                        }
                    }
                }
                sb.AppendLine();
            }
            if (diff.BaseItems.Removed.Any())
            {
                sb.AppendLine("### Removed");
                foreach (var b in diff.BaseItems.Removed.OrderBy(x => x.DisplayName ?? x.Name))
                {
                    var disp = string.IsNullOrWhiteSpace(b.DisplayName) ? b.Name : b.DisplayName;
                    sb.AppendLine($"- {disp}");
                }
                sb.AppendLine();
            }
            if (diff.BaseItems.Modified.Any())
            {
                sb.AppendLine("### Modified");
                foreach (var b in diff.BaseItems.Modified.OrderBy(x => x.DisplayName ?? x.Name))
                {
                    var disp = string.IsNullOrWhiteSpace(b.DisplayName) ? b.Name : b.DisplayName;
                    sb.AppendLine($"- {disp} [{b.Name}] ({b.Category})");

                    var baseParts = new System.Collections.Generic.List<string>();
                    AppendFieldChange(baseParts, "Base Required Level", b.BaseReqLevelBefore, b.BaseReqLevelAfter);
                    AppendFieldChangeIfStringChanged(baseParts, "Required Strength", b.ReqStrBefore, b.ReqStrAfter);
                    AppendFieldChangeIfStringChanged(baseParts, "Required Dexterity", b.ReqDexBefore, b.ReqDexAfter);
                    AppendFieldChange(baseParts, "Durability", b.DurabilityBefore, b.DurabilityAfter);
                    AppendFieldChange(baseParts, "Item Level", b.ItemLevelBefore, b.ItemLevelAfter);
                    AppendFieldChangeIfStringChanged(baseParts, "Sockets", b.SocketsBefore, b.SocketsAfter);
                    if (string.Equals(b.Category, "Weapon", StringComparison.OrdinalIgnoreCase))
                    {
                        AppendFieldChange(baseParts, "Speed", b.SpeedBefore, b.SpeedAfter);
                    }
                    if (string.Equals(b.Category, "Armor", StringComparison.OrdinalIgnoreCase))
                    {
                        // Armor range and block
                        AppendFieldChangeIfStringChanged(baseParts, "Armor", b.ArmorBefore, b.ArmorAfter);
                        AppendFieldChange(baseParts, "Block", b.BlockBefore, b.BlockAfter);
                        AppendFieldChangeIfStringChanged(baseParts, "Damage Prefix", b.DmgPrefixBefore, b.DmgPrefixAfter);
                        AppendFieldChangeIfStringChanged(baseParts, "Damage", b.DmgStrBefore, b.DmgStrAfter);
                    }
                    if (baseParts.Count > 0)
                    {
                        sb.AppendLine("  - Base: " + string.Join(", ", baseParts));
                    }

                    if (b.DamageAdded.Any() || b.DamageRemoved.Any() || (b.DamageChanged != null && b.DamageChanged.Any()))
                    {
                        sb.AppendLine("  - Damage:");
                        if (b.DamageChanged != null)
                        {
                            foreach (var d in b.DamageChanged)
                            {
                                if (d == null) continue;
                                sb.AppendLine($"    - Changed: {d.After} (was {NumericOnly(d.Before)})");
                            }
                        }
                        foreach (var d in b.DamageAdded)
                        {
                            sb.AppendLine($"    - Added: {d}");
                        }
                        foreach (var d in b.DamageRemoved)
                        {
                            sb.AppendLine($"    - Removed: {d}");
                        }
                    }

                    if (b.AutoMagicAdded.Any() || b.AutoMagicRemoved.Any() || (b.AutoMagicChanged != null && b.AutoMagicChanged.Any()))
                    {
                        sb.AppendLine("  - Automagic:");
                        if (b.AutoMagicChanged != null)
                        {
                            foreach (var p in b.AutoMagicChanged)
                            {
                                if (p == null) continue;
                                sb.AppendLine($"    - Changed: {p.After} (was {NumericOnly(p.Before)})");
                            }
                        }
                        foreach (var p in b.AutoMagicAdded)
                        {
                            sb.AppendLine($"    - Added: {p}");
                        }
                        foreach (var p in b.AutoMagicRemoved)
                        {
                            sb.AppendLine($"    - Removed: {p}");
                        }
                    }
                }
                sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();
        }


        private static void RenderUniques(StringBuilder sb, SemanticDiffResult diff)
        {
            sb.AppendLine("## Uniques");
            sb.AppendLine();
            sb.AppendLine("<details>");
            sb.AppendLine("<summary>Show Unique Changes</summary>");
            sb.AppendLine();
            string DerivedTypeFromBase(string baseCode)
            {
                if (string.IsNullOrWhiteSpace(baseCode)) return "(Unknown)";
                if (!diff.BaseCodeToName.TryGetValue(baseCode, out var baseName) || string.IsNullOrWhiteSpace(baseName)) return "(Unknown)";
                var parts = baseName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                return parts.Length > 0 ? parts[parts.Length - 1] : baseName;
            }
            if (diff.Uniques.Added.Any())
            {
                sb.AppendLine("### Added");
                string ItemType(UniqueChange u)
                {
                    var t = u.TypeAfter ?? u.TypeBefore;
                    if (!string.IsNullOrWhiteSpace(t)) return t;
                    return DerivedTypeFromBase(u.BaseCode);
                }
                var ga = diff.Uniques.Added.GroupBy(u => ItemType(u)).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
                foreach (var g in ga)
                {
                    sb.AppendLine("<details>");
                    sb.AppendLine($"<summary>{g.Key}</summary>");
                    sb.AppendLine();
                    foreach (var u in g.OrderBy(x => x.Name))
                    {
                        // Header: H4 with spacing
                        WriteH4Header(sb, u.Name);
                        // Base line
                        var baseDisp = BaseWithTier(diff, u.BaseCode);
                        if (!string.IsNullOrWhiteSpace(baseDisp))
                            sb.AppendLine($"- Base Item: {baseDisp}");
                        // Individual lines (omit Enabled)
                        var baseLines = new System.Collections.Generic.List<string>();
                        AppendFieldCurrent(baseLines, "Item Level", u.ItemLevelAfter);
                        AppendFieldCurrent(baseLines, "Required Level", u.ReqLevelAfter);
                        AppendFieldCurrent(baseLines, "Rarity", u.RarityAfter);
                        foreach (var line in baseLines)
                        {
                            sb.AppendLine($"- {line}");
                        }
                        if (u.PropertiesAdded.Any())
                        {
                            sb.AppendLine("- Properties:");
                            foreach (var p in u.PropertiesAdded)
                            {
                                sb.AppendLine($"  - {p}");
                            }
                        }
                    }
                    sb.AppendLine("</details>");
                }
                sb.AppendLine();
            }
            if (diff.Uniques.Removed.Any())
            {
                sb.AppendLine("### Removed");
                string ItemTypeR(UniqueChange u)
                {
                    var t = u.TypeAfter ?? u.TypeBefore;
                    if (!string.IsNullOrWhiteSpace(t)) return t;
                    return DerivedTypeFromBase(u.BaseCode);
                }
                var gr = diff.Uniques.Removed.GroupBy(u => ItemTypeR(u)).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
                foreach (var g in gr)
                {
                    sb.AppendLine("<details>");
                    sb.AppendLine($"<summary>{g.Key}</summary>");
                    sb.AppendLine();
                    foreach (var u in g.OrderBy(x => x.Name))
                    {
                        sb.AppendLine($"- {u.Name}");
                    }
                    sb.AppendLine("</details>");
                }
                sb.AppendLine();
            }
            if (diff.Uniques.Modified.Any())
            {
                sb.AppendLine("### Modified");
                string ItemTypeM(UniqueChange u)
                {
                    var t = u.TypeAfter ?? u.TypeBefore;
                    if (!string.IsNullOrWhiteSpace(t)) return t;
                    return DerivedTypeFromBase(u.BaseCode);
                }
                var groups = diff.Uniques.Modified.GroupBy(u => ItemTypeM(u))
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
                foreach (var g in groups)
                {
                    sb.AppendLine("<details>");
                    sb.AppendLine($"<summary>{g.Key}</summary>");
                    sb.AppendLine();
                    foreach (var u in g.OrderBy(x => x.Name))
                    {
                        // Header: H4 with spacing
                        WriteH4Header(sb, u.Name);
                        // Base item line; include (was ...) when base changed
                        var beforeCode = u.BaseCodeBefore;
                        var afterCode = u.BaseCodeAfter;
                        var beforeDisp = BaseWithTier(diff, beforeCode);
                        var afterDisp = BaseWithTier(diff, afterCode);
                        var codesEqual = string.Equals(beforeCode ?? string.Empty, afterCode ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                        if (!codesEqual)
                        {
                            if (!string.IsNullOrWhiteSpace(afterDisp))
                            {
                                var line = $"- Base Item: {afterDisp}";
                                if (!string.IsNullOrWhiteSpace(beforeDisp)) line += $" (was {beforeDisp})";
                                sb.AppendLine(line);
                            }
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(afterDisp)) sb.AppendLine($"- Base Item: {afterDisp}");
                        }
                        // Individual base fields on their own lines (omit Enabled)
                        var baseLines = new System.Collections.Generic.List<string>();
                        AppendFieldChange(baseLines, "Item Level", u.ItemLevelBefore, u.ItemLevelAfter);
                        AppendFieldChange(baseLines, "Required Level", u.ReqLevelBefore, u.ReqLevelAfter);
                        AppendFieldChange(baseLines, "Rarity", u.RarityBefore, u.RarityAfter);
                        foreach (var line in baseLines)
                        {
                            sb.AppendLine($"- {line}");
                        }
                        // Properties grouped by Changed / Added / Removed
                        var uChanged = u.PropertiesChanged != null
                            ? u.PropertiesChanged
                                .Where(p => p != null && !AreTextsEquivalent(p.Before, p.After))
                                .ToList()
                            : null;
                        if ((uChanged != null && uChanged.Any()) || u.PropertiesAdded.Any() || u.PropertiesRemoved.Any())
                        {
                            sb.AppendLine("- Properties:");
                            if (uChanged != null && uChanged.Any())
                            {
                                sb.AppendLine("  - Changed:");
                                foreach (var p in uChanged)
                                {
                                    if (p == null) continue;
                                    sb.AppendLine($"    - {p.After} (was {NumericOnly(p.Before)})");
                                }
                            }
                            if (u.PropertiesAdded.Any())
                            {
                                sb.AppendLine("  - Added:");
                                foreach (var p in u.PropertiesAdded)
                                {
                                    sb.AppendLine($"    - {p}");
                                }
                            }
                            if (u.PropertiesRemoved.Any())
                            {
                                sb.AppendLine("  - Removed:");
                                foreach (var p in u.PropertiesRemoved)
                                {
                                    sb.AppendLine($"    - {p}");
                                }
                            }
                        }
                    }
                    sb.AppendLine("</details>");
                }
                sb.AppendLine();
            }
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        private static void RenderSets(StringBuilder sb, SemanticDiffResult diff)
        {
            sb.AppendLine("## Sets");
            sb.AppendLine();
            sb.AppendLine("<details>");
            sb.AppendLine("<summary>Show Set Changes</summary>");
            sb.AppendLine();
            if (diff.Sets.Added.Any())
            {
                sb.AppendLine("### Added");
                foreach (var s in diff.Sets.Added.OrderBy(x => x.Name))
                {
                    sb.AppendLine("<details>");
                    sb.AppendLine($"<summary>{s.Name}</summary>");
                    sb.AppendLine();
                    // Intentionally omit base-level line for added sets to reduce redundancy
                    if (s.PartialAdded.Any())
                    {
                        // H4 header to match item name treatment
                        WriteH4Header(sb, "Partial Set Bonuses");
                        foreach (var p in s.PartialAdded)
                        {
                            sb.AppendLine($"  - {p}");
                        }
                    }
                    if (s.FullAdded.Any())
                    {
                        // H4 header to match item name treatment
                        WriteH4Header(sb, "Full Set Bonuses");
                        foreach (var p in s.FullAdded)
                        {
                            sb.AppendLine($"  - {p}");
                        }
                    }
                    if (s.ItemsAdded.Any())
                    {
                        // Omit redundant "Items:" label; list items directly
                        foreach (var i in s.ItemsAdded.OrderBy(x => x.Name))
                        {
                            // Item header: H4 with spacing
                            WriteH4Header(sb, i.Name);
                            // Base item line
                            var baseDisp = BaseWithTier(diff, i.BaseCode);
                            if (!string.IsNullOrWhiteSpace(baseDisp))
                                sb.AppendLine($"- Base Item: {baseDisp}");
                            // Individual fields (omit Enabled)
                            var partsI = new System.Collections.Generic.List<string>();
                            AppendFieldCurrent(partsI, "Item Level", i.ItemLevelAfter);
                            AppendFieldCurrent(partsI, "Required Level", i.ReqLevelAfter);
                            AppendFieldCurrent(partsI, "Rarity", i.RarityAfter);
                            foreach (var line in partsI)
                            {
                                sb.AppendLine($"- {line}");
                            }
                            if (i.PropertiesAdded.Any())
                            {
                                sb.AppendLine("- Properties:");
                                foreach (var p in i.PropertiesAdded)
                                {
                                    sb.AppendLine($"  - {p}");
                                }
                            }
                            if (i.SetPropsAdded.Any())
                            {
                                sb.AppendLine("- Set Bonuses:");
                                foreach (var p in i.SetPropsAdded)
                                {
                                    sb.AppendLine($"  - {p}");
                                }
                            }
                        }
                    }
                    sb.AppendLine();
                    sb.AppendLine("</details>");
                }
                sb.AppendLine();
            }
            if (diff.Sets.Removed.Any())
            {
                sb.AppendLine("### Removed");
                foreach (var s in diff.Sets.Removed.OrderBy(x => x.Name))
                {
                    sb.AppendLine($"- {s.Name}");
                }
                sb.AppendLine();
            }
            if (diff.Sets.Modified.Any())
            {
                sb.AppendLine("### Modified");
                foreach (var s in diff.Sets.Modified.OrderBy(x => x.Name))
                {
                    sb.AppendLine("<details>");
                    sb.AppendLine($"<summary>{s.Name}</summary>");
                    sb.AppendLine();
                    var baseParts = new System.Collections.Generic.List<string>();
                    AppendFieldChange(baseParts, "Level", s.LevelBefore, s.LevelAfter);
                    if (baseParts.Count > 0)
                    {
                        sb.AppendLine("- Base: " + string.Join(", ", baseParts));
                    }
                    var sPartialChanged = s.PartialChanged != null
                        ? s.PartialChanged
                            .Where(p => p != null && !AreTextsEquivalent(p.Before, p.After))
                            .ToList()
                        : null;
                    if (s.PartialAdded.Any() || s.PartialRemoved.Any() || (sPartialChanged != null && sPartialChanged.Any()))
                    {
                        // H4 header to match item name treatment
                        WriteH4Header(sb, "Partial Set Bonuses");
                        if (sPartialChanged != null)
                        {
                            foreach (var p in sPartialChanged)
                            {
                                if (p == null) continue;
                                sb.AppendLine($"  - Changed: {p.After} (was {NumericOnly(p.Before)})");
                            }
                        }
                        foreach (var p in s.PartialAdded)
                        {
                            sb.AppendLine($"  - Added: {p}");
                        }
                        foreach (var p in s.PartialRemoved)
                        {
                            sb.AppendLine($"  - Removed: {p}");
                        }
                    }
                    var sFullChanged = s.FullChanged != null
                        ? s.FullChanged
                            .Where(p => p != null && !AreTextsEquivalent(p.Before, p.After))
                            .ToList()
                        : null;
                    if (s.FullAdded.Any() || s.FullRemoved.Any() || (sFullChanged != null && sFullChanged.Any()))
                    {
                        // H4 header to match item name treatment
                        WriteH4Header(sb, "Full Set Bonuses");
                        if (sFullChanged != null)
                        {
                            foreach (var p in sFullChanged)
                            {
                                if (p == null) continue;
                                sb.AppendLine($"  - Changed: {p.After} (was {NumericOnly(p.Before)})");
                            }
                        }
                        foreach (var p in s.FullAdded)
                        {
                            sb.AppendLine($"  - Added: {p}");
                        }
                        foreach (var p in s.FullRemoved)
                        {
                            sb.AppendLine($"  - Removed: {p}");
                        }
                    }
                    if (s.ItemsAdded.Any() || s.ItemsRemoved.Any() || s.ItemsModified.Any())
                    {
                        string DerivedTypeFromBaseLocal(string baseCode)
                        {
                            if (string.IsNullOrWhiteSpace(baseCode)) return "(Unknown)";
                            if (!diff.BaseCodeToName.TryGetValue(baseCode, out var baseName) || string.IsNullOrWhiteSpace(baseName)) return "(Unknown)";
                            var parts = baseName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            return parts.Length > 0 ? parts[parts.Length - 1] : baseName;
                        }
                        string ItemType(SetItemChange i)
                        {
                            var t = i.TypeAfter ?? i.TypeBefore;
                            if (!string.IsNullOrWhiteSpace(t)) return t;
                            return DerivedTypeFromBaseLocal(i.BaseCode);
                        }

                        if (s.ItemsAdded.Any())
                        {
                            sb.AppendLine("Items Added:");
                            var ga = s.ItemsAdded.GroupBy(i => ItemType(i)).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
                            foreach (var g in ga)
                            {
                                foreach (var i in g.OrderBy(x => x.Name))
                                {
                                    // Header: H4 with spacing
                                    WriteH4Header(sb, i.Name);
                                    // Base item line
                                    var baseDisp = BaseWithTier(diff, i.BaseCode);
                                    if (!string.IsNullOrWhiteSpace(baseDisp)) sb.AppendLine($"- Base Item: {baseDisp}");
                                    // Individual fields (omit Enabled)
                                    var partsI = new System.Collections.Generic.List<string>();
                                    AppendFieldCurrent(partsI, "Item Level", i.ItemLevelAfter);
                                    AppendFieldCurrent(partsI, "Required Level", i.ReqLevelAfter);
                                    AppendFieldCurrent(partsI, "Rarity", i.RarityAfter);
                                    foreach (var line in partsI)
                                    {
                                        sb.AppendLine($"- {line}");
                                    }
                                    if (i.PropertiesAdded.Any())
                                    {
                                        sb.AppendLine("- Properties:");
                                        foreach (var p in i.PropertiesAdded)
                                        {
                                            sb.AppendLine($"  - {p}");
                                        }
                                    }
                                    if (i.SetPropsAdded.Any())
                                    {
                                        sb.AppendLine("- Set Bonuses:");
                                        foreach (var p in i.SetPropsAdded)
                                        {
                                            sb.AppendLine($"  - {p}");
                                        }
                                    }
                                }
                            }
                        }
                        if (s.ItemsRemoved.Any())
                        {
                            sb.AppendLine("- Items Removed:");
                            var gr = s.ItemsRemoved.GroupBy(i => ItemType(i)).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
                            foreach (var g in gr)
                            {
                                foreach (var i in g.OrderBy(x => x.Name))
                                {
                                    var baseDisp = BaseWithTier(diff, i.BaseCode);
                                    if (string.IsNullOrWhiteSpace(baseDisp))
                                    {
                                        sb.AppendLine($"  - {i.Name}");
                                    }
                                    else
                                    {
                                        sb.AppendLine($"  - {i.Name} ({baseDisp})");
                                    }
                                    var partsI = new System.Collections.Generic.List<string>();
                                    AppendFieldCurrent(partsI, "Enabled", i.EnabledBefore);
                                    AppendFieldCurrent(partsI, "Item Level", i.ItemLevelBefore);
                                    AppendFieldCurrent(partsI, "Required Level", i.ReqLevelBefore);
                                    AppendFieldCurrent(partsI, "Rarity", i.RarityBefore);
                                    if (partsI.Count > 0) sb.AppendLine("    - Base: " + string.Join(", ", partsI));
                                    if (i.PropertiesRemoved.Any())
                                    {
                                        sb.AppendLine("    - Properties:");
                                        foreach (var p in i.PropertiesRemoved)
                                        {
                                            sb.AppendLine($"      - {p}");
                                        }
                                    }
                                    if (i.SetPropsRemoved.Any())
                                    {
                                        sb.AppendLine("    - Set Bonuses:");
                                        foreach (var p in i.SetPropsRemoved)
                                        {
                                            sb.AppendLine($"      - {p}");
                                        }
                                    }
                                }
                            }
                        }
                        if (s.ItemsModified.Any())
                        {
                            var gm = s.ItemsModified.GroupBy(i => ItemType(i)).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
                            foreach (var g in gm)
                            {
                                foreach (var i in g.OrderBy(x => x.Name))
                                {
                                    // Header: H4 with spacing
                                    WriteH4Header(sb, i.Name);
                                    // Base item line for set pieces; include (was ...) when base changed
                                    var beforeCode = i.BaseCodeBefore;
                                    var afterCode = i.BaseCodeAfter;
                                    var beforeDisp = BaseWithTier(diff, beforeCode);
                                    var afterDisp = BaseWithTier(diff, afterCode);
                                    var codesEqual = string.Equals(beforeCode ?? string.Empty, afterCode ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                                    if (!codesEqual)
                                    {
                                        if (!string.IsNullOrWhiteSpace(afterDisp))
                                        {
                                            var line = $"- Base Item: {afterDisp}";
                                            if (!string.IsNullOrWhiteSpace(beforeDisp)) line += $" (was {beforeDisp})";
                                            sb.AppendLine(line);
                                        }
                                    }
                                    else
                                    {
                                        if (!string.IsNullOrWhiteSpace(afterDisp)) sb.AppendLine($"- Base Item: {afterDisp}");
                                    }
                                    // Base fields (omit Enabled)
                                    var parts = new System.Collections.Generic.List<string>();
                                    AppendFieldChange(parts, "Item Level", i.ItemLevelBefore, i.ItemLevelAfter);
                                    AppendFieldChange(parts, "Required Level", i.ReqLevelBefore, i.ReqLevelAfter);
                                    AppendFieldChange(parts, "Rarity", i.RarityBefore, i.RarityAfter);
                                    foreach (var line in parts)
                                    {
                                        sb.AppendLine($"- {line}");
                                    }
                                    // Properties grouping
                                    var iChanged = i.PropertiesChanged != null
                                        ? i.PropertiesChanged
                                            .Where(p => p != null && !AreTextsEquivalent(p.Before, p.After))
                                            .ToList()
                                        : null;
                                    if ((iChanged != null && iChanged.Any()) || i.PropertiesAdded.Any() || i.PropertiesRemoved.Any())
                                    {
                                        sb.AppendLine("- Properties:");
                                        if (iChanged != null && iChanged.Any())
                                        {
                                            sb.AppendLine("  - Changed:");
                                            foreach (var p in iChanged)
                                            {
                                                if (p == null) continue; // safety
                                                sb.AppendLine($"    - {p.After} (was {NumericOnly(p.Before)})");
                                            }
                                        }
                                        if (i.PropertiesAdded.Any())
                                        {
                                            sb.AppendLine("  - Added:");
                                            foreach (var p in i.PropertiesAdded)
                                            {
                                                sb.AppendLine($"    - {p}");
                                            }
                                        }
                                        if (i.PropertiesRemoved.Any())
                                        {
                                            sb.AppendLine("  - Removed:");
                                            foreach (var p in i.PropertiesRemoved)
                                            {
                                                sb.AppendLine($"    - {p}");
                                            }
                                        }
                                    }
                                    // Set bonuses grouping
                                    var iSetChanged = i.SetPropsChanged != null
                                        ? i.SetPropsChanged
                                            .Where(p => p != null && !AreTextsEquivalent(p.Before, p.After))
                                            .ToList()
                                        : null;
                                    if ((iSetChanged != null && iSetChanged.Any()) || i.SetPropsAdded.Any() || i.SetPropsRemoved.Any())
                                    {
                                        sb.AppendLine("- Set Bonuses:");
                                        if (iSetChanged != null && iSetChanged.Any())
                                        {
                                            sb.AppendLine("  - Changed:");
                                            foreach (var p in iSetChanged)
                                            {
                                                if (p == null) continue; // safety
                                                sb.AppendLine($"    - {p.After} (was {NumericOnly(p.Before)})");
                                            }
                                        }
                                        if (i.SetPropsAdded.Any())
                                        {
                                            sb.AppendLine("  - Added:");
                                            foreach (var p in i.SetPropsAdded)
                                            {
                                                sb.AppendLine($"    - {p}");
                                            }
                                        }
                                        if (i.SetPropsRemoved.Any())
                                        {
                                            sb.AppendLine("  - Removed:");
                                            foreach (var p in i.SetPropsRemoved)
                                            {
                                                sb.AppendLine($"    - {p}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    sb.AppendLine();
                    sb.AppendLine("</details>");
                }
                sb.AppendLine();
            }
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        private static void RenderRunewords(StringBuilder sb, SemanticDiffResult diff)
        {
            sb.AppendLine("## Runewords");
            sb.AppendLine();
            sb.AppendLine("<details>");
            sb.AppendLine("<summary>Show Runeword Changes</summary>");
            sb.AppendLine();
            System.Collections.Generic.HashSet<string> Paired()
            {
                var addedNorm = diff.Runewords.Added.Select(x => NormalizeRunewordName(x.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var removedNorm = diff.Runewords.Removed.Select(x => NormalizeRunewordName(x.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                return new System.Collections.Generic.HashSet<string>(addedNorm.Intersect(removedNorm), StringComparer.OrdinalIgnoreCase);
            }
            var paired = Paired();
            if (diff.Runewords.Added.Any())
            {
                sb.AppendLine("### Added");
                foreach (var r in diff.Runewords.Added.OrderBy(x => x.Name))
                {
                    if (paired.Contains(NormalizeRunewordName(r.Name))) continue;
                    // Header: H4 with spacing
                    WriteH4Header(sb, r.Name);
                    // Individual lines (omit Enabled)
                    var parts = new System.Collections.Generic.List<string>();
                    AppendFieldCurrent(parts, "Item Level", r.ItemLevelAfter);
                    AppendFieldCurrent(parts, "Required Level", r.ReqLevelAfter);
                    foreach (var line in parts)
                    {
                        sb.AppendLine($"- {line}");
                    }
                    if (r.PropertiesAdded.Any())
                    {
                        sb.AppendLine("- Properties:");
                        foreach (var p in r.PropertiesAdded)
                        {
                            sb.AppendLine($"  - {p}");
                        }
                    }
                }
                sb.AppendLine();
            }
            if (diff.Runewords.Removed.Any())
            {
                sb.AppendLine("### Removed");
                foreach (var r in diff.Runewords.Removed.OrderBy(x => x.Name))
                {
                    if (paired.Contains(NormalizeRunewordName(r.Name))) continue;
                    sb.AppendLine($"- {r.Name}");
                }
                sb.AppendLine();
            }
            if (diff.Runewords.Modified.Any())
            {
                sb.AppendLine("### Modified");
                foreach (var r in diff.Runewords.Modified.OrderBy(x => x.Name))
                {
                    // Header: H4 with spacing
                    WriteH4Header(sb, r.Name);
                    // Individual lines for levels (omit Enabled)
                    var levelLines = new System.Collections.Generic.List<string>();
                    AppendFieldChange(levelLines, "Item Level", r.ItemLevelBefore, r.ItemLevelAfter);
                    AppendFieldChange(levelLines, "Required Level", r.ReqLevelBefore, r.ReqLevelAfter);
                    foreach (var line in levelLines)
                    {
                        sb.AppendLine($"- {line}");
                    }
                    // Properties grouped
                    var rChanged = r.PropertiesChanged != null
                        ? r.PropertiesChanged
                            .Where(p => p != null && !AreTextsEquivalent(p.Before, p.After))
                            .ToList()
                        : null;
                    if ((rChanged != null && rChanged.Any()) || r.PropertiesAdded.Any() || r.PropertiesRemoved.Any())
                    {
                        sb.AppendLine("- Properties:");
                        if (rChanged != null && rChanged.Any())
                        {
                            sb.AppendLine("  - Changed:");
                            foreach (var p in rChanged)
                            {
                                if (p == null) continue;
                                sb.AppendLine($"    - {p.After} (was {NumericOnly(p.Before)})");
                            }
                        }
                        if (r.PropertiesAdded.Any())
                        {
                            sb.AppendLine("  - Added:");
                            foreach (var p in r.PropertiesAdded)
                            {
                                sb.AppendLine($"    - {p}");
                            }
                        }
                        if (r.PropertiesRemoved.Any())
                        {
                            sb.AppendLine("  - Removed:");
                            foreach (var p in r.PropertiesRemoved)
                            {
                                sb.AppendLine($"    - {p}");
                            }
                        }
                    }
                }
                sb.AppendLine();
            }
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        // Suppression of base codes is no longer required; friendly names are resolved in SemanticDiffService.

        private static void AppendFieldChange(System.Collections.Generic.List<string> list, string label, bool? before, bool? after)
        {
            if (before.HasValue || after.HasValue)
            {
                var b = before.HasValue ? (before.Value ? "Enabled" : "Disabled") : "?";
                var a = after.HasValue ? (after.Value ? "Enabled" : "Disabled") : "?";
                if (!string.Equals(b, a, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add($"{label}: {a} (was {b})");
                }
            }
        }

        private static void AppendFieldChange(System.Collections.Generic.List<string> list, string label, int? before, int? after)
        {
            if (before.HasValue || after.HasValue)
            {
                var b = before.HasValue ? before.Value.ToString() : "?";
                var a = after.HasValue ? after.Value.ToString() : "?";
                if (!string.Equals(b, a, StringComparison.Ordinal))
                {
                    list.Add($"{label}: {a} (was {b})");
                }
            }
        }

        private static void AppendFieldChangeIfStringChanged(System.Collections.Generic.List<string> list, string label, string before, string after)
        {
            var b = before ?? string.Empty;
            var a = after ?? string.Empty;
            if (!string.Equals(b, a, StringComparison.OrdinalIgnoreCase))
            {
                var was = !string.IsNullOrEmpty(b) ? NumericOnly(b) : "?";
                list.Add($"{label}: {(!string.IsNullOrEmpty(a) ? a : "?")} (was {was})");
            }
        }

        private static void AppendFieldCurrent(System.Collections.Generic.List<string> list, string label, int? value)
        {
            if (value.HasValue) list.Add($"{label}: {value.Value}");
        }
        private static void AppendFieldCurrent(System.Collections.Generic.List<string> list, string label, bool? value)
        {
            if (value.HasValue) list.Add($"{label}: {(value.Value ? "Enabled" : "Disabled")}");
        }
        private static void AppendFieldCurrent(System.Collections.Generic.List<string> list, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) list.Add($"{label}: {value}");
        }

        private static string NumericOnly(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s ?? string.Empty;
            try
            {
                var text = s;
                var m = System.Text.RegularExpressions.Regex.Match(text, @"[\+\-]?\d+(?:\.\d+)?\s*(?:-|to)\s*[\+\-]?\d+(?:\.\d+)?%?");
                if (m.Success) return m.Value.Trim();
                var mc = System.Text.RegularExpressions.Regex.Matches(text, @"[\+\-]?\d+(?:\.\d+)?%?");
                if (mc.Count > 0)
                {
                    var parts = mc.Cast<System.Text.RegularExpressions.Match>().Select(x => x.Value.Trim()).Where(x => x.Length > 0).ToList();
                    return string.Join(" ", parts);
                }
                return s;
            }
            catch { return s; }
        }

        private static string NormalizeRunewordName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = s.Trim();
            // Treat '&' and 'and' as equivalent and collapse spaces
            t = t.Replace("&", "and");
            t = System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ");
            // Drop trailing numbers like "Name 2"
            t = System.Text.RegularExpressions.Regex.Replace(t, @"\s*\d+$", string.Empty);
            return t.Trim();
        }

        private static bool AreTextsEquivalent(string before, string after)
        {
            var nb = NormalizeTextEquivalence(before);
            var na = NormalizeTextEquivalence(after);
            return string.Equals(nb, na, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeTextEquivalence(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = s.Trim();
            t = t.Replace("&", "and");
            t = System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ");
            return t.Trim();
        }
    }
}
