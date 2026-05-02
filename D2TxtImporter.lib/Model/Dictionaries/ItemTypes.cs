using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Dictionaries
{
    public class ItemType
    {
        public string Name { get => Table.Tables.ContainsKey(Index) ? Table.GetValue(Index) : Index; }
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
        public int? MaxSockets1 { get; set; }
        [JsonIgnore]
        public int? MaxSocketsLevelThreshold1 { get; set; }
        [JsonIgnore]
        public int? MaxSockets2 { get; set; }
        [JsonIgnore]
        public int? MaxSocketsLevelThreshold2 { get; set; }
        [JsonIgnore]
        public int? MaxSockets3 { get; set; }
        [JsonIgnore]
        public int? Restricted { get; set; }
        [JsonIgnore]
        public static Dictionary<string, ItemType> ItemTypes;
        
        public static void Import(string excelFolder)
        {
            ItemTypes = new Dictionary<string, ItemType>();

            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/ItemTypes.txt");

            foreach (var row in table)
            {
                var itemType = new ItemType
                {
                    Index = NormalizeIndex(row["ItemType"]),
                    Code = row["Code"],
                    Equiv1 = row["Equiv1"],
                    Equiv2 = row["Equiv2"],
                    Class = row["Class"],
                    BodyLoc1 = row["BodyLoc1"],
                    MaxSockets1 = Model.Utility.ToNullableInt(row.ContainsKey("MaxSockets1") ? row["MaxSockets1"] : null),
                    MaxSocketsLevelThreshold1 = Model.Utility.ToNullableInt(row.ContainsKey("MaxSocketsLevelThreshold1") ? row["MaxSocketsLevelThreshold1"] : null),
                    MaxSockets2 = Model.Utility.ToNullableInt(row.ContainsKey("MaxSockets2") ? row["MaxSockets2"] : null),
                    MaxSocketsLevelThreshold2 = Model.Utility.ToNullableInt(row.ContainsKey("MaxSocketsLevelThreshold2") ? row["MaxSocketsLevelThreshold2"] : null),
                    MaxSockets3 = Model.Utility.ToNullableInt(row.ContainsKey("MaxSockets3") ? row["MaxSockets3"] : null),
                    Restricted = Model.Utility.ToNullableInt(row.ContainsKey("Restricted") ? row["Restricted"] : null)
                };

                ItemTypes[itemType.Code] = itemType;
            }
        }

        private static string NormalizeIndex(string index)
        {
            if (index == null)
            {
                return null;
            }

            var value = index.Trim();

            return Normalizations.TryGetValue(value, out var mapped) ? mapped : value;
        }
        
        private static readonly Dictionary<string, string> Normalizations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "medium charm", "Large Charm" },
            { "armor", "Body Armor" },
            { "large charm", "Grand Charm" },
            { "merc equip", "Helm" },
            { "hand to hand 2", "Hand to Hand" }
        };

        public override string ToString()
        {
            return Index;
        }
    }
}
