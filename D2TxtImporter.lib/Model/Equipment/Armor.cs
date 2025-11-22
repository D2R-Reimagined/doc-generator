using System;
using System.Collections.Generic;

using D2TxtImporter.lib.Exceptions;
using D2TxtImporter.lib.Model.Dictionaries;
using Newtonsoft.Json;
using D2TxtImporter.lib.Model.Types;

namespace D2TxtImporter.lib.Model.Equipment
{
    class Armor : Equipment
    {
        [JsonIgnore]
        public int MinAc { get; set; }
        [JsonIgnore]
        public int MaxAc { get; set; }
        [JsonIgnore]
        public int? MinDamage { get; set; }
        [JsonIgnore]
        public int? MaxDamage { get; set; }
        public string DamageString { get; set; }
        public string DamageStringPrefix { get; set; }
        [JsonIgnore]
        public static Dictionary<string, Armor> Armors;
        public string ArmorString { get; set; }
        public int? Block { get; set; }
        public int StrBonus { get; set; }
        public int DexBonus { get; set; }

        // Aggregated Automagic group properties attached at export time
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<AutoMagicExportProperty> Properties { get; set; }

        // New grouped representation: groups by Name + Level + RequiredLevel across the entire list
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<AutoMagicExportPropertyGroup> AutoMagicGroups { get; set; }

        public static void Import(string excelFolder)
        {
            Armors = new Dictionary<string, Armor>();

            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/Armor.txt");

            foreach (var row in table)
            {
                var name = row["name"];

                if (!ItemType.ItemTypes.ContainsKey(row["type"]))
                {
                    ExceptionHandler.LogException(new Exception($"Could not find type '{row["type"]}' in ItemTypes.txt for armor '{name}' in Armor.txt"));

                }
                var type = ItemType.ItemTypes[row["type"]];

                var minAc = Utility.ToNullableInt(row["minac"]);
                if (!minAc.HasValue)
                {
                    ExceptionHandler.LogException(new Exception($"Could not find MinAC for armor '{name}' in Armor.txt"));
                }

                var maxAc = Utility.ToNullableInt(row["maxac"]);
                if (!maxAc.HasValue)
                {
                    ExceptionHandler.LogException(new Exception($"Could not find MaxAC for armor '{name}' in Armor.txt"));
                }

                var requiredStrength = Utility.ToNullableInt(row["reqstr"]);
                if (!requiredStrength.HasValue)
                {
                    ExceptionHandler.LogException(new Exception($"Could not find Required Strength for armor '{name}' in Armor.txt"));
                }

                var durability = Utility.ToNullableInt(row["durability"]);
                if (!durability.HasValue)
                {
                    ExceptionHandler.LogException(new Exception($"Could not find Durability for armor '{name}' in Armor.txt"));
                }


                var itemLevel = Utility.ToNullableInt(row["level"]);
                if (!itemLevel.HasValue)
                {
                    ExceptionHandler.LogException(new Exception($"Could not find Item Level for armor '{name}' in Armor.txt"));
                }

                var armor = new Armor
                {
                    Code = row["code"],
                    BaseRequiredLevel = Utility.ToNullableInt(row.ContainsKey("levelreq") ? row["levelreq"] : (row.ContainsKey("lvl req") ? row["lvl req"] : null)),
                    MinAc = minAc.Value,
                    MaxAc = maxAc.Value,
                    RequiredStrength = !string.IsNullOrEmpty(row["reqstr"]) ? int.Parse(row["reqstr"]) : 0,
                    RequiredDexterity = !string.IsNullOrEmpty(row["reqdex"]) ? int.Parse(row["reqdex"]) : 0,
                    StrBonus = Utility.ToNullableInt(row.ContainsKey("StrBonus") ? row["StrBonus"] : "0") ?? 0,
                    DexBonus = Utility.ToNullableInt(row.ContainsKey("DexBonus") ? row["DexBonus"] : "0") ?? 0,
                    Type = type,
                    EquipmentType = EquipmentType.Armor,
                    Durability = durability.Value,
                    MinDamage = Utility.ToNullableInt(row["mindam"]),
                    MaxDamage = Utility.ToNullableInt(row["maxdam"]),
                    ItemLevel = itemLevel.Value,
                    NormCode = row.ContainsKey("normcode") ? row["normcode"] : null,
                    UberCode = row.ContainsKey("ubercode") ? row["ubercode"] : null,
                    UltraCode = row.ContainsKey("ultracode") ? row["ultracode"] : null,
                    GemSockets = Utility.ToNullableInt(row.ContainsKey("gemsockets") ? row["gemsockets"] : "0") ?? 0,
                    AutoPrefix = row.ContainsKey("auto prefix") ? row["auto prefix"] : (row.ContainsKey("autoprefix") ? row["autoprefix"] : null)
                };

                // Set block only for shields or shield-equivalent types
                if (type != null && (string.Equals(type.Equiv1, "shld", StringComparison.OrdinalIgnoreCase) || string.Equals(type.Code, "shld", StringComparison.OrdinalIgnoreCase)))
                {
                    armor.Block = Utility.ToNullableInt(row.ContainsKey("block") ? row["block"] : "0") ?? 0;
                }

                // Populate damage strings for armor that can deal damage (e.g., shields/boots)
                if (armor.MinDamage.HasValue && armor.MinDamage.Value > 0)
                {
                    armor.DamageStringPrefix = GetDamagePrefix(type) ?? armor.DamageStringPrefix;
                    if (armor.MinDamage.Value == armor.MaxDamage.Value)
                    {
                        armor.DamageString = $"{armor.MinDamage.Value}";
                    }
                    else
                    {
                        armor.DamageString = $"{armor.MinDamage.Value} to {armor.MaxDamage.Value}";
                    }
                }

                // Populate base armor string so armors.json does not contain null values
                if (armor.MinAc == armor.MaxAc)
                {
                    armor.ArmorString = $"{armor.MaxAc}";
                }
                else
                {
                    armor.ArmorString = $"{armor.MinAc}-{armor.MaxAc}";
                }

                Armors[armor.Code] = armor;
            }
        }

        public new object Clone()
        {
            return new Armor
            {
                EquipmentType = EquipmentType,
                Code = Code,
                BaseRequiredLevel = BaseRequiredLevel,
                RequiredStrength = RequiredStrength,
                RequiredDexterity = RequiredDexterity,
                StrBonus = StrBonus,
                DexBonus = DexBonus,
                Durability = Durability,
                ItemLevel = ItemLevel,
                Type = Type,
                NormCode = NormCode,
                UberCode = UberCode,
                UltraCode = UltraCode,
                GemSockets = GemSockets,
                AutoPrefix = AutoPrefix,
                MinAc = MinAc,
                MaxAc = MaxAc,
                MinDamage = MinDamage,
                MaxDamage = MaxDamage,
                DamageString = DamageString,
                DamageStringPrefix = DamageStringPrefix,
                ArmorString = ArmorString,
                Block = Block
            };
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
