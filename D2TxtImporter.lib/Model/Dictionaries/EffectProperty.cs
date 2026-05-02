using System.Collections.Generic;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Dictionaries
{
    public class EffectProperty
    {
        [JsonIgnore]
        public string Code { get; set; }
        [JsonIgnore]
        public string Stat { get; set; }
        [JsonIgnore]
        public static Dictionary<string, EffectProperty> EffectProperties;

        public static void Import(string excelFolder)
        {
            // Use case-insensitive keys so lookups work regardless of casing in data
            EffectProperties = new Dictionary<string, EffectProperty>();

            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/Properties.txt");

            foreach (var row in table)
            {
                var effect = new EffectProperty
                {
                    Code = row["code"],
                    Stat = row["stat1"]
                };

                EffectProperties[effect.Code] = effect;
            }
        }

        public override string ToString()
        {
            return Code;
        }
    }
}
