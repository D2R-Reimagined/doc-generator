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

            if (value.Equals("medium charm", StringComparison.OrdinalIgnoreCase))
            {
                return "Large Charm";
            }

            if (value.Equals("large charm", StringComparison.OrdinalIgnoreCase))
            {
                return "Grand Charm";
            }

            if (value.Equals("merc equip", StringComparison.OrdinalIgnoreCase))
            {
                return "Helm";
            }

            return value;
        }

        public override string ToString()
        {
            return Index;
        }
    }
}
