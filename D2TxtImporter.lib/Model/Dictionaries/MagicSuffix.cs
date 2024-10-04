using System.Collections.Generic;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Dictionaries
{
    public class MagicSuffix
    {
        public string Name { get; set; }

        [JsonIgnore]
        public int Index { get; set; }

        [JsonIgnore]
        public static Dictionary<int, MagicSuffix> MagicSuffixes;

        public static void Import(string excelFolder)
        {
            MagicSuffixes = new Dictionary<int, MagicSuffix>();

            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/MagicSuffix.txt");

            var index = 0;
            foreach (var row in table)
            {
                index++;

                var magicSuffix = new MagicSuffix
                {
                    Name = row["Name"],
                    Index = index - 1
                };

                MagicSuffixes[index - 1] = magicSuffix;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
