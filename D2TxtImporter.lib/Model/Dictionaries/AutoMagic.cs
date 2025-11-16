using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using D2TxtImporter.lib.Model.Types;

namespace D2TxtImporter.lib.Model.Dictionaries
{
    public class AutoMagic
    {
        // Raw name copied directly from Automagic.txt (not localized, game doesn't read it)
        public string Name { get; set; }

        public int Level { get; set; }
        public int MaxLevel { get; set; }
        public int LevelReq { get; set; }
        public int Group { get; set; }

        // Optional class restrictions similar to MagicPrefix/Suffix
        public string ClassSpecific { get; set; }
        public string Class { get; set; }
        public int ClassLevelReq { get; set; }

        // Built and ordered properties
        public List<ItemProperty> Properties { get; set; }

        public List<string> Types { get; set; }
        public List<string> ETypes { get; set; }

        public string PType { get { return "Automagic"; } }

        [JsonIgnore]
        public int Index { get; set; }

        [JsonIgnore]
        public static Dictionary<int, AutoMagic> AutoMagics;

        public static void Import(string excelFolder)
        {
            AutoMagics = new Dictionary<int, AutoMagic>();

            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/Automagic.txt");

            var index = 0;
            foreach (var row in table)
            {
                // Skip entries that are not spawnable (if column exists)
                if (row.ContainsKey("spawnable") && row["spawnable"] == "0")
                {
                    continue;
                }

                // Skip if group is empty/null, 0, 309, 310, 311, or 312
                var groupVal = Utility.ToNullableInt(row.ContainsKey("group") ? row["group"] : null);
                if (!groupVal.HasValue || groupVal.Value == 0 || groupVal.Value == 309 || groupVal.Value == 310 || groupVal.Value == 311 || groupVal.Value == 312 || groupVal.Value == 316 || groupVal.Value == 319)
                {
                    continue;
                }

                index++;

                var level = Utility.ToNullableInt(row.ContainsKey("level") ? row["level"] : null) ?? 0;

                var automagic = new AutoMagic
                {
                    Name = row.ContainsKey("name") ? row["name"] : row.ContainsKey("Name") ? row["Name"] : $"automagic_{index}",
                    Index = index - 1,
                    Level = level,
                    MaxLevel = Utility.ToNullableInt(row.ContainsKey("maxlevel") ? row["maxlevel"] : null) ?? 0,
                    LevelReq = Utility.ToNullableInt(row.ContainsKey("levelreq") ? row["levelreq"] : null) ?? 0,
                    Group = Utility.ToNullableInt(row.ContainsKey("group") ? row["group"] : null) ?? 0,
                    ClassSpecific = ResolveClassName(GetFirstPresent(row, new[] { "classspecific", "class specific" })),
                    Class = ResolveClassName(GetFirstPresent(row, new[] { "class", "Class" })),
                    ClassLevelReq = Utility.ToNullableInt(GetFirstPresent(row, new[] { "classlevelreq", "class levelreq", "classlvlreq" })) ?? 0,
                    Types = ExtractTypes(row, "itype", 7),
                    ETypes = ExtractTypes(row, "etype", 5)
                };

                // Build properties via central helper to apply global filters (e.g., skip oskill_hide etc.)
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
                            properties[i].Index = i;
                        }
                        automagic.Properties = properties;
                    }
                    catch (Exception ex)
                    {
                        var affix = row.ContainsKey("name") ? row["name"] : (row.ContainsKey("Name") ? row["Name"] : "<unknown>");
                        throw new Exception($"Failed to build properties for AutoMagic '{affix}'", ex);
                    }
                }

                AutoMagics[index - 1] = automagic;
            }
        }

        private static string GetFirstPresent(Dictionary<string, string> row, string[] keys)
        {
            foreach (var k in keys)
            {
                if (row.ContainsKey(k)) return row[k];
            }
            return null;
        }

        private static ItemProperty BuildItemProperty(Dictionary<string, string> row, int modIndex, int level)
        {
            var codeKey = $"mod{modIndex}code";
            var paramKey = $"mod{modIndex}param";
            var minKey = $"mod{modIndex}min";
            var maxKey = $"mod{modIndex}max";

            if (!row.ContainsKey(codeKey) || string.IsNullOrWhiteSpace(row[codeKey]))
            {
                return null;
            }

            var code = row[codeKey];
            var param = row.ContainsKey(paramKey) ? row[paramKey] : null;
            var min = Utility.ToNullableInt(row.ContainsKey(minKey) ? row[minKey] : null);
            var max = Utility.ToNullableInt(row.ContainsKey(maxKey) ? row[maxKey] : null);

            try
            {
                var ip = new ItemProperty(code, param, min, max, modIndex, level);
                return ip;
            }
            catch (Exception ex)
            {
                var affix = row.ContainsKey("name") ? row["name"] : (row.ContainsKey("Name") ? row["Name"] : "<unknown>");
                throw new Exception($"Failed to build item property for AutoMagic '{affix}' (mod{modIndex}: code='{code}', param='{param}', min='{min}', max='{max}')", ex);
            }
        }

        private static List<string> ExtractTypes(Dictionary<string, string> row, string prefix, int count)
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
