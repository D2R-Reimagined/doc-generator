using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using D2TxtImporter.lib.Model.Types;

namespace D2TxtImporter.lib.Model.Dictionaries
{
    public class MagicPrefix
    {
        // Original table key for the affix name (e.g., "sounding"). Not exported.
        [JsonIgnore]
        public string NameKey { get; set; }

        // Resolved, localized name via Tables. Throws (via pre-validation) if the key is missing.
        public string Name { get { return Table.GetValue(NameKey); } }
        public int Level { get; set; }
        public int MaxLevel { get; set; }
        public int LevelReq { get; set; }
        public int Group { get; set; }
        public string ClassSpecific { get; set; }
        public string Class { get; set; }
        public int ClassLevelReq { get; set; }
        public List<ItemProperty> Properties { get; set; }
        public List<string> Types { get; set; }
        public List<string> ETypes { get; set; }
        public string PType { get { return "Prefix"; } }

        [JsonIgnore]
        public int Index { get; set; }

        [JsonIgnore]
        public static Dictionary<int, MagicPrefix> MagicPrefixes;

        public static void Import(string excelFolder)
        {
            MagicPrefixes = new Dictionary<int, MagicPrefix>();

            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/MagicPrefix.txt");

            var index = 0;
            foreach (var row in table)
            {
                // Skip entries that are not spawnable
                if (row.ContainsKey("spawnable") && row["spawnable"] == "0")
                {
                    continue;
                }
                index++;
                var level = Utility.ToNullableInt(row.ContainsKey("level") ? row["level"] : null) ?? 0;

                var magicPrefix = new MagicPrefix
                {
                    NameKey = row.ContainsKey("name") ? row["name"] : row.ContainsKey("Name") ? row["Name"] : $"prefix_{index}",
                    Index = index - 1,
                    Level = level,
                    MaxLevel = Utility.ToNullableInt(row.ContainsKey("maxlevel") ? row["maxlevel"] : null) ?? 0,
                    LevelReq = Utility.ToNullableInt(row.ContainsKey("levelreq") ? row["levelreq"] : null) ?? 0,
                    Group = Utility.ToNullableInt(row.ContainsKey("group") ? row["group"] : null) ?? 0,
                    ClassSpecific = ResolveClassName(GetFirstPresent(row, new []{"classspecific","class specific"})),
                    Class = ResolveClassName(GetFirstPresent(row, new []{"class","Class"})),
                    ClassLevelReq = Utility.ToNullableInt(GetFirstPresent(row, new []{"classlevelreq","class levelreq","classlvlreq"})) ?? 0,
                    Types = ExtractTypes(row, "itype", 7),
                    ETypes = ExtractTypes(row, "etype", 5)
                };

                // Build properties via centralized helper to apply global filters (GetProperties)
                var propInfos = new List<PropertyInfo>();
                for (int i = 1; i <= 3; i++)
                {
                    var codeKey = $"mod{i}code";
                    if (!row.ContainsKey(codeKey) || string.IsNullOrWhiteSpace(row[codeKey]))
                        continue;

                    var paramKey = $"mod{i}param";
                    var minKey = $"mod{i}min";
                    var maxKey = $"mod{i}max";
                    propInfos.Add(new PropertyInfo(row[codeKey], row.ContainsKey(paramKey) ? row[paramKey] : null,
                        row.ContainsKey(minKey) ? row[minKey] : null,
                        row.ContainsKey(maxKey) ? row[maxKey] : null));
                }

                if (propInfos.Count > 0)
                {
                    try
                    {
                        var properties = ItemProperty.GetProperties(propInfos, level)
                            .OrderByDescending(x => x.ItemStatCost == null ? 0 : (x.ItemStatCost.DescriptionPriority ?? 0))
                            .ToList();
                        for (int i = 0; i < properties.Count; i++)
                        {
                            properties[i].Index = i; // zero-based index
                        }
                        magicPrefix.Properties = properties;
                    }
                    catch (Exception ex)
                    {
                        var affix = row.ContainsKey("name") ? row["name"] : (row.ContainsKey("Name") ? row["Name"] : "<unknown>");
                        throw new Exception($"Failed to build properties for MagicPrefix '{affix}'", ex);
                    }
                }

                MagicPrefixes[index -1] = magicPrefix;
            }
        }

        private static string GetFirstPresent(Dictionary<string,string> row, string[] keys)
        {
            foreach (var k in keys)
            {
                if (row.ContainsKey(k)) return row[k];
            }
            return null;
        }

        private static List<string> ExtractTypes(Dictionary<string,string> row, string prefix, int count)
        {
            var list = new List<string>();
            for (int i = 1; i <= count; i++)
            {
                var key = $"{prefix}{i}";
                if (row.ContainsKey(key) && !string.IsNullOrWhiteSpace(row[key]))
                {
                    var code = row[key];
                    if (ItemType.ItemTypes != null && ItemType.ItemTypes.ContainsKey(code))
                    {
                        list.Add(ItemType.ItemTypes[code].Name);
                    }
                    else
                    {
                        list.Add(code);
                    }
                }
            }
            return list;
        }

        private static string ResolveClassName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var key = raw.Trim().ToLower();
            // Accept both 3-letter codes and already full names
            if (CharStat.CharStats != null)
            {
                if (CharStat.CharStats.TryGetValue(key, out var cs))
                {
                    return cs.Class;
                }
            }
            return raw;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
