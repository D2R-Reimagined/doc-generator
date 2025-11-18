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

        private static readonly Dictionary<string, string> Normalizations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "medium charm", "Large Charm" },
                { "armor", "Body Armor" },
                { "large charm", "Grand Charm" },
                { "merc equip", "Helm" }
            };

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
                    BodyLoc1 = row["BodyLoc1"]
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

        public override string ToString()
        {
            return Index;
        }
    }
}
