using System;
using System.Collections.Generic;

using D2TxtImporter.lib.Exceptions;
using D2TxtImporter.lib.Model.Dictionaries;
using Newtonsoft.Json;
using D2TxtImporter.lib.Model.Types;

namespace D2TxtImporter.lib.Model.Equipment
{
    public class Weapon : Equipment
    {
        public List<DamageType> DamageTypes { get; set; }
        public int Speed { get; set; }
        public int StrBonus { get; set; }
        public int DexBonus { get; set; }

        // Aggregated Automagic group properties attached at export time
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<AutoMagicExportProperty> Properties { get; set; }

        // New grouped representation: groups by Name + Level + RequiredLevel across the entire list
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<AutoMagicExportPropertyGroup> AutoMagicGroups { get; set; }

        [JsonIgnore]
        public static Dictionary<string, Weapon> Weapons;
        // Preserve import order (Weapons.txt order)
        [JsonIgnore]
        public static List<Weapon> WeaponOrder;

        public static void Import(string excelFolder)
        {
            Weapons = new Dictionary<string, Weapon>();
            WeaponOrder = new List<Weapon>();

            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/Weapons.txt");

            foreach (var row in table)
            {
                var damageTypes = new List<DamageType>();

                var isOneOrTwoHanded = row["1or2handed"] == "1";
                var isTwoHanded = row["2handed"] == "1";
                var isThrown = !string.IsNullOrEmpty(row["minmisdam"]);
                var name = row["name"];

                if (!isTwoHanded)
                {
                    try
                    {
                        damageTypes.Add(new DamageType { Type = DamageTypeEnum.Normal, MinDamage = int.Parse(row["mindam"]), MaxDamage = int.Parse(row["maxdam"]) });
                    }
                    catch (Exception)
                    {
                        ExceptionHandler.LogException(new Exception($"Could not get min or max damage for weapon: '{name}' in Weapons.txt"));
                    }
                }
                else if (isOneOrTwoHanded)
                {
                    try
                    {
                        damageTypes.Add(new DamageType { Type = DamageTypeEnum.OneHanded, MinDamage = int.Parse(row["mindam"]), MaxDamage = int.Parse(row["maxdam"]) });
                    }
                    catch (Exception)
                    {
                        ExceptionHandler.LogException(new Exception($"Could not get min or max one handed damage for weapon: '{name}' in Weapons.txt"));
                    }
                }

                if (isTwoHanded)
                {
                    try
                    {
                        damageTypes.Add(new DamageType { Type = DamageTypeEnum.TwoHanded, MinDamage = int.Parse(row["2handmindam"]), MaxDamage = int.Parse(row["2handmaxdam"]) });
                    }
                    catch (Exception)
                    {
                        ExceptionHandler.LogException(new Exception($"Could not get min or max two handed damage for weapon: '{name}' in Weapons.txt"));
                    }
                }

                if (isThrown)
                {
                    try
                    {
                        damageTypes.Add(new DamageType { Type = DamageTypeEnum.Thrown, MinDamage = int.Parse(row["minmisdam"]), MaxDamage = int.Parse(row["maxmisdam"]) });
                    }
                    catch (Exception)
                    {
                        ExceptionHandler.LogException(new Exception($"Could not get min or max thrown damage for weapon: '{name}' in Weapons.txt"));
                    }
                }

                var itemLevel = Utility.ToNullableInt(row["level"]);
                if (!itemLevel.HasValue)
                {
                    ExceptionHandler.LogException(new Exception($"Could not find item level for weapon '{name}' in Weapons.txt"));
                }

                if (!ItemType.ItemTypes.ContainsKey(row["type"]))
                {
                    ExceptionHandler.LogException(new Exception($"Could not find type '{row["type"]}' in ItemTypes.txt for weapon '{name}' in Weapons.txt"));
                }

                var weapon = new Weapon
                {
                    DamageTypes = damageTypes,
                    Code = row["code"],
                    BaseRequiredLevel = Utility.ToNullableInt(row.ContainsKey("levelreq") ? row["levelreq"] : (row.ContainsKey("lvl req") ? row["lvl req"] : null)),
                    EquipmentType = EquipmentType.Weapon,
                    RequiredStrength = !string.IsNullOrEmpty(row["reqstr"]) ? int.Parse(row["reqstr"]) : 0,
                    RequiredDexterity = !string.IsNullOrEmpty(row["reqdex"]) ? int.Parse(row["reqdex"]) : 0,
                    StrBonus = Utility.ToNullableInt(row.ContainsKey("StrBonus") ? row["StrBonus"] : "0") ?? 0,
                    DexBonus = Utility.ToNullableInt(row.ContainsKey("DexBonus") ? row["DexBonus"] : "0") ?? 0,
                    Durability = row["nodurability"] == "1" ? 0 : int.Parse(row["durability"]),
                    ItemLevel = itemLevel.Value,
                    Type = ItemType.ItemTypes[row["type"]],
                    Speed = Utility.ToNullableInt(row.ContainsKey("speed") ? row["speed"] : "0") ?? 0,
                    NormCode = row.ContainsKey("normcode") ? row["normcode"] : null,
                    UberCode = row.ContainsKey("ubercode") ? row["ubercode"] : null,
                    UltraCode = row.ContainsKey("ultracode") ? row["ultracode"] : null,
                    GemSockets = Utility.ToNullableInt(row.ContainsKey("gemsockets") ? row["gemsockets"] : "0") ?? 0,
                    AutoPrefix = row.ContainsKey("auto prefix") ? row["auto prefix"] : (row.ContainsKey("autoprefix") ? row["autoprefix"] : null)
                };
                
                foreach (var dt in weapon.DamageTypes)
                {
                    if (dt.MinDamage == dt.MaxDamage)
                    {
                        dt.DamageString = $"{dt.MinDamage} to {dt.MaxDamage}";
                    }
                    else
                    {
                        dt.DamageString = $"{dt.MinDamage} to {dt.MaxDamage}";
                    }
                }

                Weapons[weapon.Code] = weapon;
                WeaponOrder.Add(weapon);
            }
        }

        public new object Clone()
        {
            var dmgTypes = new List<DamageType>();
            DamageTypes.ForEach(x => dmgTypes.Add((DamageType)x.Clone()));

            return new Weapon
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
                DamageTypes = dmgTypes,
                Speed = Speed
            };
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
