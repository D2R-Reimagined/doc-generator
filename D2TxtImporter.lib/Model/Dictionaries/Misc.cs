using System;
using System.Collections.Generic;
using System.Globalization;
using D2TxtImporter.lib.Exceptions;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Dictionaries
{
    public class Misc
    {
        public string Name { get; set; }
        public int ItemLevel { get; set; }
        public int RequiredLevel { get; set; }
        [JsonIgnore]
        public string Code { get; set; }
        [JsonIgnore]
        public string NameStr { get; set; }
        public ItemType Type { get; set; }
        [JsonIgnore]
        public string Type2 { get; set; }
        [JsonIgnore]
        public string DropConditionCalc { get; set; }
        [JsonIgnore]
        public string AutoPrefix { get; set; }
        [JsonIgnore]
        public static Dictionary<string, Misc> MiscItems;

        public static void Import(string excelFolder)
        {
            MiscItems = new Dictionary<string, Misc>();

            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/Misc.txt");

            foreach (var row in table)
            {
                if (string.IsNullOrEmpty(row["code"]))
                {
                    continue;
                }

                var name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(row["name"].Replace(" Rune", ""));

                if (!ItemType.ItemTypes.ContainsKey(row["type"]))
                {
                    ExceptionHandler.LogException(new Exception($"Could not find code '{row["type"]}' in ItemTypes.txt for type field in Misc.txt item {name}"));
                }

                var itemLevel = Utility.ToNullableInt(row["level"]);
                if (!itemLevel.HasValue)
                {
                    ExceptionHandler.LogException(new Exception($"Could not find item level for '{name}' in Misc.txt"));
                }

                var requiredLevel = Utility.ToNullableInt(row["levelreq"]);
                if (!requiredLevel.HasValue)
                {
                    ExceptionHandler.LogException(new Exception($"Could not find required level for '{name}' in Misc.txt"));
                }

                var misc = new Misc
                {
                    Name = row["name"],
                    ItemLevel = itemLevel.Value,
                    RequiredLevel = requiredLevel.Value,
                    Code = row["code"],
                    NameStr = row.ContainsKey("namestr") ? row["namestr"] : null,
                    Type = ItemType.ItemTypes[row["type"]],
                    Type2 = row["type2"],
                    DropConditionCalc = row.ContainsKey("DropConditionCalc") ? row["DropConditionCalc"] : null,
                    AutoPrefix = row.ContainsKey("auto prefix") ? row["auto prefix"] : (row.ContainsKey("autoprefix") ? row["autoprefix"] : null)
                };

                MiscItems[misc.Code] = misc;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
