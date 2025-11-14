using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Dictionaries
{
    public class ItemType
    {
        public string Name
        {
            get
            {
                if (Table.Tables.ContainsKey(Index))
                {
                    return Table.GetValue(Index);
                }
                return Index;
            }
        }
        public string Index { get; set; }

        [JsonIgnore]
        public string Code { get; set; }
        public string Class { get; set; }

        [JsonIgnore]
        public string Equiv2 { get; set; }

        [JsonIgnore]
        public string Equiv1 { get; set; }

        [JsonIgnore]
        public string BodyLoc1 { get; set; }

        [JsonIgnore]
        public static Dictionary<string, ItemType> ItemTypes;

        public static void Import(string excelFolder)
        {
            ItemTypes = new Dictionary<string, ItemType>();

            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/ItemTypes.txt");

            foreach (var row in table)
            {
                // Normalize specific ItemType names before using them
                var rawIndex = row["ItemType"];
                var normalizedIndex = rawIndex;
                if (!string.IsNullOrWhiteSpace(rawIndex))
                {
                    var idx = rawIndex.Trim();
                    if (idx.Equals("Merc Equip", StringComparison.OrdinalIgnoreCase))
                    {
                        idx = "Helm";
                    }
                    else if (idx.Equals("medium charm", StringComparison.OrdinalIgnoreCase))
                    {
                        idx = "Large Charm";
                    }
                    else if (idx.Equals("large charm", StringComparison.OrdinalIgnoreCase))
                    {
                        idx = "Grand Charm";
                    }
                    normalizedIndex = idx;
                }

                var itemType = new ItemType
                {
                    Index = normalizedIndex,
                    Code = row["Code"],
                    Equiv1 = row["Equiv1"],
                    Equiv2 = row["Equiv2"],
                    Class = row["Class"],
                    BodyLoc1 = row["BodyLoc1"]
                };

                ItemTypes[itemType.Code] = itemType;
            }
        }

        public override string ToString()
        {
            return Index;
        }
    }
}
