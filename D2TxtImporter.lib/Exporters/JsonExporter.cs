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
        // Shared, case-insensitive list of properties to exclude from uniques.json and sets.json
        private static readonly HashSet<string> ExcludedExportProperties =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                // Equipment common
                "NormCode", "UberCode", "UltraCode", "GemSockets", "AutoPrefix", "BaseRequiredLevel",

                // Armor / Weapon specifics
                "Block", "StrBonus", "DexBonus", "Speed"
            };

        // Custom resolver that filters out properties by name (case-insensitive)
        private class ExcludingPropertiesContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
        {
            private readonly ISet<string> _excludedByName;
            private readonly System.Collections.Generic.IDictionary<System.Type, ISet<string>> _excludedByDeclaringType;

            public ExcludingPropertiesContractResolver(ISet<string> excludedByName,
                System.Collections.Generic.IDictionary<System.Type, ISet<string>> excludedByDeclaringType = null)
            {
                _excludedByName = excludedByName ?? new HashSet<string>();
                _excludedByDeclaringType = excludedByDeclaringType;
            }

            protected override IList<Newtonsoft.Json.Serialization.JsonProperty> CreateProperties(System.Type type, MemberSerialization memberSerialization)
            {
                var props = base.CreateProperties(type, memberSerialization);
                if (( _excludedByName == null || _excludedByName.Count == 0) && (_excludedByDeclaringType == null || _excludedByDeclaringType.Count == 0))
                {
                    return props;
                }

                var list = new List<Newtonsoft.Json.Serialization.JsonProperty>();
                foreach (var p in props)
                {
                    bool exclude = false;
                    if (_excludedByName != null && _excludedByName.Contains(p.PropertyName))
                    {
                        exclude = true;
                    }
                    if (!exclude && _excludedByDeclaringType != null && _excludedByDeclaringType.Count > 0)
                    {
                        foreach (var kv in _excludedByDeclaringType)
                        {
                            var dt = kv.Key;
                            var names = kv.Value;
                            if (dt.IsAssignableFrom(p.DeclaringType) && names.Contains(p.PropertyName))
                            {
                                exclude = true;
                                break;
                            }
                        }
                    }
                    if (!exclude)
                    {
                        list.Add(p);
                    }
                }
                return list;
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

        public static void ExportJson(string outputPath, List<Unique> uniques, List<Runeword> runewords, List<CubeRecipe> cubeRecipes, List<Set> sets, bool prettyPrint)
        {
            if (!Directory.Exists(outputPath))
            {
                throw new System.Exception("Could not find output directory");
            }

            var txtOutputDirectory = Path.Combine(outputPath, "json");

            if (!Directory.Exists(txtOutputDirectory))
            {
                Directory.CreateDirectory(txtOutputDirectory);
            }

            Uniques(Path.Combine(txtOutputDirectory, "uniques.json"), uniques, prettyPrint);
            Runewords(Path.Combine(txtOutputDirectory, "runewords.json"), runewords, prettyPrint);
            CubeRecipes(Path.Combine(txtOutputDirectory, "cube_recipes.json"), cubeRecipes, prettyPrint);
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
            var typedEx = new Dictionary<System.Type, ISet<string>>
            {
                { typeof(D2TxtImporter.lib.Model.Equipment.Equipment), new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "Code" } }
            };
            var settings = new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
                ContractResolver = new ExcludingPropertiesContractResolver(ExcludedExportProperties, typedEx)
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

        private static void CubeRecipes(string destination, List<CubeRecipe> cubeRecipes, bool prettyPrint)
        {
            var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
            var json = SerializeWithIndent(cubeRecipes, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        private static void Sets(string destination, List<Set> sets, bool prettyPrint)
        {
            var typedEx = new Dictionary<System.Type, ISet<string>>
            {
                { typeof(D2TxtImporter.lib.Model.Equipment.Equipment), new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "Code" } }
            };
            var settings = new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
                ContractResolver = new ExcludingPropertiesContractResolver(ExcludedExportProperties, typedEx)
            };
            var json = SerializeWithIndent(sets, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
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

        // Note: Automagic rows are no longer exported as a standalone file.
        // Their effects are still applied to Armor and Weapon exports via BuildAutoMagicGroupMap().

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
