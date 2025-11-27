using System.Collections.Generic;
using System.IO;
using System.Linq;

using D2TxtImporter.lib.Model.Items;
using D2TxtImporter.lib.Model.Equipment;
using D2TxtImporter.lib.Model.Dictionaries;
using D2TxtImporter.lib.Model.Types;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Exporters
{
    public class JsonExporter
    {
        // Custom contract resolver used when exporting Uniques/Sets to filter
        // out duplicated base equipment fields that are already exported in
        // dedicated armors/weapons JSON files.
        private sealed class EquipmentFilterContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
        {
            private static readonly System.Collections.Generic.HashSet<string> ExcludedEquipmentFields = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                // Base item identity/tiers and automagic hooks
                "NormCode", "UberCode", "UltraCode", "AutoPrefix",
                // Structural fields not needed on embedded equipment inside uniques/sets
                "GemSockets", "ItemLevel", "BaseRequiredLevel", "Code", "Type",
                // Derived class-only fields (present on Weapon/Armor)
                "StrBonus", "DexBonus"
            };

            protected override System.Collections.Generic.IList<Newtonsoft.Json.Serialization.JsonProperty> CreateProperties(System.Type type, Newtonsoft.Json.MemberSerialization memberSerialization)
            {
                var props = base.CreateProperties(type, memberSerialization);

                // Only filter when serializing equipment (base or derived types)
                if (typeof(D2TxtImporter.lib.Model.Equipment.Equipment).IsAssignableFrom(type))
                {
                    props = props
                        .Where(p => !ExcludedEquipmentFields.Contains(p.PropertyName))
                        .ToList();
                }

                return props;
            }
        }

        // Helper: serialize with 4-space indentation when prettyPrint is true; otherwise compact.
        private static string SerializeWithIndent(object value, JsonSerializerSettings settings, bool prettyPrint)
        {
            if (!prettyPrint)
            {
                return JsonConvert.SerializeObject(value, Formatting.None, settings)
                    .Replace("\\ufffd", "'");
            }

            var sb = new System.Text.StringBuilder(4096);
            using (var sw = new StringWriter(sb))
            using (var writer = new JsonTextWriter(sw)
            {
                Formatting = Formatting.Indented,
                IndentChar = ' ',
                Indentation = 4
            })
            {
                var serializer = JsonSerializer.Create(settings);
                serializer.Serialize(writer, value);
            }

            return sb.ToString().Replace("\\ufffd", "'");
        }

        // Build aggregated Automagic group map for quick lookup when exporting base items
        // Each entry in the list represents one individual property encountered within an Automagic group
        private static Dictionary<int, List<AutoMagicExportProperty>> BuildAutoMagicGroupMap()
        {
            var map = new Dictionary<int, List<AutoMagicExportProperty>>();
            if (AutoMagic.AutoMagics == null || AutoMagic.AutoMagics.Count == 0)
            {
                return map;
            }

            foreach (var am in AutoMagic.AutoMagics.Values)
            {
                // Safety: skip invalid groups although import already filtered many
                if (am.Group <= 0)
                {
                    continue;
                }

                if (am.Properties == null || am.Properties.Count == 0)
                {
                    continue;
                }

                if (!map.TryGetValue(am.Group, out var list))
                {
                    list = new List<AutoMagicExportProperty>();
                    map[am.Group] = list;
                }

                foreach (var prop in am.Properties)
                {
                    if (string.IsNullOrWhiteSpace(prop?.PropertyString))
                    {
                        continue;
                    }

                    list.Add(new AutoMagicExportProperty
                    {
                        Name = am.Name,
                        PropertyString = prop.PropertyString,
                        // Index will be assigned after sorting
                        Index = 0,
                        Level = am.Level,
                        RequiredLevel = am.RequiredLevel
                    });
                }
            }

            // Sort and assign indices per group
            foreach (var kv in map)
            {
                var list = kv.Value;
                list.Sort((a, b) =>
                {
                    var cmp = a.RequiredLevel.CompareTo(b.RequiredLevel);
                    if (cmp != 0) return cmp;
                    // tie-breakers: then by Level, then by PropertyString
                    cmp = a.Level.CompareTo(b.Level);
                    if (cmp != 0) return cmp;
                    return string.Compare(a.PropertyString, b.PropertyString, System.StringComparison.Ordinal);
                });

                for (int i = 0; i < list.Count; i++)
                {
                    list[i].Index = i;
                }
            }

            return map;
        }

        // Group across the entire list by Name + Level + RequiredLevel (not only consecutive)
        // Preserves the order of first occurrence of each group.
        private static List<AutoMagicExportPropertyGroup> GroupAutoMagicProperties(IEnumerable<AutoMagicExportProperty> properties)
        {
            if (properties == null)
            {
                return null;
            }

            var groups = new List<AutoMagicExportPropertyGroup>();
            var indexByKey = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var p in properties)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.PropertyString))
                {
                    continue;
                }

                var key = $"{p.Name}\u0001{p.Level}\u0001{p.RequiredLevel}"; // use unlikely separator
                if (!indexByKey.TryGetValue(key, out var idx))
                {
                    idx = groups.Count;
                    indexByKey[key] = idx;
                    groups.Add(new AutoMagicExportPropertyGroup
                    {
                        Name = p.Name,
                        Level = p.Level,
                        RequiredLevel = p.RequiredLevel,
                        PropertyStrings = new List<string> { p.PropertyString }
                    });
                }
                else
                {
                    var list = groups[idx].PropertyStrings;
                    // Avoid duplicate insertion if the same PropertyString appears multiple times
                    if (list == null)
                    {
                        groups[idx].PropertyStrings = new List<string> { p.PropertyString };
                    }
                    else if (!list.Contains(p.PropertyString))
                    {
                        list.Add(p.PropertyString);
                    }
                }
            }

            return groups.Count > 0 ? groups : null;
        }

        public static void ExportJson(string outputPath, List<Unique> uniques, List<Runeword> runewords, List<Set> sets, bool prettyPrint)
        {
            if (!Directory.Exists(outputPath))
            {
                throw new System.Exception("Could not find output directory");
            }

            // Write all JSON outputs into Docs\item-jsons
            var txtOutputDirectory = Path.Combine(outputPath, "item-jsons");

            if (!Directory.Exists(txtOutputDirectory))
            {
                Directory.CreateDirectory(txtOutputDirectory);
            }

            Uniques(Path.Combine(txtOutputDirectory, "uniques.json"), uniques, prettyPrint);
            Runewords(Path.Combine(txtOutputDirectory, "runewords.json"), runewords, prettyPrint);
            Sets(Path.Combine(txtOutputDirectory, "sets.json"), sets, prettyPrint);
            // Precompute automagic group map once for both armor and weapons
            var autoMagicGroupMap = BuildAutoMagicGroupMap();
            Weapons(Path.Combine(txtOutputDirectory, "weapons.json"), prettyPrint, autoMagicGroupMap);
            Armors(Path.Combine(txtOutputDirectory, "armors.json"), prettyPrint, autoMagicGroupMap);
            MagicPrefixes(Path.Combine(txtOutputDirectory, "magicprefix.json"), prettyPrint);
            MagicSuffixes(Path.Combine(txtOutputDirectory, "magicsuffix.json"), prettyPrint);
        }

        private static void Uniques(string destination, List<Unique> uniques, bool prettyPrint)
        {
            var settings = new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
                ContractResolver = new EquipmentFilterContractResolver()
            };
            var json = SerializeWithIndent(uniques, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        private static void Runewords(string destination, List<Runeword> runewords, bool prettyPrint)
        {
            var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
            var json = SerializeWithIndent(runewords, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }


        private sealed class BaseWithSetItems
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public string BaseType { get; set; } // Weapon or Armor
            public string ItemType { get; set; } // e.g., Sword, Helm
            public List<BaseSetItem> SetItems { get; set; }
        }

        private sealed class BaseSetItem
        {
            public string SetIndex { get; set; }
            public string SetName { get; set; }
            public string ItemName { get; set; }
            public int RequiredLevel { get; set; }
            public List<string> Properties { get; set; }
            public List<string> SetProperties { get; set; }
        }

        // Minimal per-base summary model requested by the user
        private sealed class BaseSetSummary
        {
            public string BaseName { get; set; } // e.g., "Cap [N]"
            public int Count { get; set; }
            public List<string> SetItems { get; set; }
        }

        private static void Sets(string destination, List<Set> sets, bool prettyPrint)
        {
            var settings = new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
                ContractResolver = new EquipmentFilterContractResolver()
            };
            var json = SerializeWithIndent(sets, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        // New optional export: sets grouped by base items written to a separate file
        private static void SetsByBase(string destination, List<Set> sets, bool prettyPrint)
        {
            // Build a fast lookup of set items by their base code
            var setItems = SetItem.SetItems ?? new List<SetItem>();
            var setItemsByCode = setItems
                .GroupBy(si => si.Code, System.StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Name).OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase).ToList(), System.StringComparer.OrdinalIgnoreCase);

            // Helper to compute tier marker [N]/[E]/[X]
            string TierForArmor(Model.Equipment.Armor a)
            {
                // If code equals exceptional or elite hooks, mark accordingly
                if (!string.IsNullOrEmpty(a.UltraCode) && string.Equals(a.Code, a.UltraCode, System.StringComparison.OrdinalIgnoreCase)) return "X";
                if (!string.IsNullOrEmpty(a.UberCode) && string.Equals(a.Code, a.UberCode, System.StringComparison.OrdinalIgnoreCase)) return "E";
                return "N"; // default normal
            }

            string TierForWeapon(Model.Equipment.Weapon w)
            {
                if (!string.IsNullOrEmpty(w.UltraCode) && string.Equals(w.Code, w.UltraCode, System.StringComparison.OrdinalIgnoreCase)) return "X";
                if (!string.IsNullOrEmpty(w.UberCode) && string.Equals(w.Code, w.UberCode, System.StringComparison.OrdinalIgnoreCase)) return "E";
                return "N";
            }

            // Some table display names already include a tier suffix like "Cap [N]".
            // Normalize by stripping any trailing " [N]", " [E]", or " [X]" so we don't duplicate markers.
            string NormalizeBaseName(string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }

                var s = name.Trim();
                // Expect pattern ending with space, bracket, single letter, bracket: " [X]"
                if (s.Length >= 4 && s[s.Length - 1] == ']' && s[s.Length - 4] == ' ' && s[s.Length - 3] == '[')
                {
                    var c = char.ToUpperInvariant(s[s.Length - 2]);
                    if (c == 'N' || c == 'E' || c == 'X')
                    {
                        return s.Substring(0, s.Length - 4).TrimEnd();
                    }
                }

                return s;
            }

            var summaries = new List<BaseSetSummary>();

            // Order: first armors in Armor.txt order, then weapons in Weapons.txt order
            if (Model.Equipment.Armor.Armors != null)
            {
                // Try to preserve import order; Dictionary order is not guaranteed on .NET Framework.
                // If Armor class exposes no ordered list, fallback to Name order to remain deterministic.
                IEnumerable<Model.Equipment.Armor> armorSeq = Model.Equipment.Armor.Armors.Values;
                try
                {
                    // If an ordered list exists (future-proof), prefer it
                    var orderedField = typeof(Model.Equipment.Armor).GetField("ArmorOrder", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (orderedField != null)
                    {
                        var possibleList = orderedField.GetValue(null) as System.Collections.IEnumerable;
                        if (possibleList != null)
                        {
                            var tmp = new List<Model.Equipment.Armor>();
                            foreach (var it in possibleList)
                            {
                                if (it is Model.Equipment.Armor ar)
                                {
                                    tmp.Add(ar);
                                }
                            }
                            if (tmp.Count > 0) armorSeq = tmp;
                        }
                    }
                }
                catch { /* ignore reflection issues; use dictionary values */ }

                foreach (var a in armorSeq)
                {
                    var tier = TierForArmor(a);
                    var baseName = string.IsNullOrWhiteSpace(a.Name) ? a.Code : NormalizeBaseName(a.Name);
                    var key = a.Code;
                    var names = setItemsByCode.TryGetValue(key, out var list) ? list : new List<string>();
                    summaries.Add(new BaseSetSummary
                    {
                        BaseName = $"{baseName} [{tier}]",
                        Count = names.Count,
                        SetItems = names
                    });
                }
            }

            if (Model.Equipment.Weapon.Weapons != null)
            {
                IEnumerable<Model.Equipment.Weapon> weaponSeq = Model.Equipment.Weapon.Weapons.Values;
                try
                {
                    var orderedField = typeof(Model.Equipment.Weapon).GetField("WeaponOrder", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (orderedField != null)
                    {
                        var possibleList = orderedField.GetValue(null) as System.Collections.IEnumerable;
                        if (possibleList != null)
                        {
                            var tmp = new List<Model.Equipment.Weapon>();
                            foreach (var it in possibleList)
                            {
                                if (it is Model.Equipment.Weapon w)
                                {
                                    tmp.Add(w);
                                }
                            }
                            if (tmp.Count > 0) weaponSeq = tmp;
                        }
                    }
                }
                catch { /* ignore reflection issues */ }

                foreach (var w in weaponSeq)
                {
                    var tier = TierForWeapon(w);
                    var baseName = string.IsNullOrWhiteSpace(w.Name) ? w.Code : NormalizeBaseName(w.Name);
                    var key = w.Code;
                    var names = setItemsByCode.TryGetValue(key, out var list) ? list : new List<string>();
                    summaries.Add(new BaseSetSummary
                    {
                        BaseName = $"{baseName} [{tier}]",
                        Count = names.Count,
                        SetItems = names
                    });
                }
            }

            var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
            var json = SerializeWithIndent(summaries, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        // Public helper to write the optional sets_by_base.json under item-jsons
        public static void ExportSetsByBase(string outputPath, List<Set> sets, bool prettyPrint)
        {
            if (!Directory.Exists(outputPath))
            {
                throw new System.Exception("Could not find output directory");
            }

            var txtOutputDirectory = Path.Combine(outputPath, "item-jsons");
            if (!Directory.Exists(txtOutputDirectory))
            {
                Directory.CreateDirectory(txtOutputDirectory);
            }

            var destination = Path.Combine(txtOutputDirectory, "sets_by_base.json");
            SetsByBase(destination, sets, prettyPrint);
        }

        private static void MagicPrefixes(string destination, bool prettyPrint)
        {
            var list = (MagicPrefix.MagicPrefixes != null ? (IEnumerable<MagicPrefix>)MagicPrefix.MagicPrefixes.Values : Enumerable.Empty<MagicPrefix>()).ToList();
            var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
            var json = SerializeWithIndent(list, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        private static void MagicSuffixes(string destination, bool prettyPrint)
        {
            var list = (MagicSuffix.MagicSuffixes != null ? (IEnumerable<MagicSuffix>)MagicSuffix.MagicSuffixes.Values : Enumerable.Empty<MagicSuffix>()).ToList();
            var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
            var json = SerializeWithIndent(list, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        private static void Weapons(string destination, bool prettyPrint, Dictionary<int, List<AutoMagicExportProperty>> autoMagicGroupMap)
        {
            var list = (Weapon.Weapons != null ? (IEnumerable<Weapon>)Weapon.Weapons.Values : Enumerable.Empty<Weapon>()).ToList();
            // Attach aggregated Automagic group properties to each weapon when applicable
            if (autoMagicGroupMap != null && autoMagicGroupMap.Count > 0)
            {
                foreach (var weap in list)
                {
                    if (string.IsNullOrWhiteSpace(weap.AutoPrefix))
                    {
                        continue;
                    }
                    if (int.TryParse(weap.AutoPrefix, out var grp) && autoMagicGroupMap.TryGetValue(grp, out var props) && props != null && props.Count > 0)
                    {
                        // Build grouped representation across the entire list
                        weap.AutoMagicGroups = GroupAutoMagicProperties(props);
                        // Ensure we do not emit the old flat list
                        weap.Properties = null;
                    }
                }
            }
            var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
            var json = SerializeWithIndent(list, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        private static void Armors(string destination, bool prettyPrint, Dictionary<int, List<AutoMagicExportProperty>> autoMagicGroupMap)
        {
            var list = (Armor.Armors != null ? (IEnumerable<Armor>)Armor.Armors.Values : Enumerable.Empty<Armor>()).ToList();
            // Attach aggregated Automagic group properties to each armor when applicable
            if (autoMagicGroupMap != null && autoMagicGroupMap.Count > 0)
            {
                foreach (var armor in list)
                {
                    if (string.IsNullOrWhiteSpace(armor.AutoPrefix))
                    {
                        continue;
                    }
                    if (int.TryParse(armor.AutoPrefix, out var grp) && autoMagicGroupMap.TryGetValue(grp, out var props) && props != null && props.Count > 0)
                    {
                        armor.AutoMagicGroups = GroupAutoMagicProperties(props);
                        armor.Properties = null;
                    }
                }
            }
            var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
            var json = SerializeWithIndent(list, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

    }
}
