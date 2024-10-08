﻿using System;
using System.Collections.Generic;
using D2TxtImporter.lib.Exceptions;
using D2TxtImporter.lib.Model.Dictionaries;
using D2TxtImporter.lib.Model.Types;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Equipment
{
    public class Weapon : Equipment
    {
        public List<DamageType> DamageTypes { get; set; }

        [JsonIgnore]
        public static Dictionary<string, Weapon> Weapons;

        public static void Import(string excelFolder)
        {
            Weapons = new Dictionary<string, Weapon>();

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
                    EquipmentType = EquipmentType.Weapon,
                    RequiredStrength = !string.IsNullOrEmpty(row["reqstr"]) ? int.Parse(row["reqstr"]) : 0,
                    RequiredDexterity = !string.IsNullOrEmpty(row["reqdex"]) ? int.Parse(row["reqdex"]) : 0,
                    Durability = row["nodurability"] == "1" ? 0 : int.Parse(row["durability"]),
                    ItemLevel = itemLevel.Value,
                    Type = ItemType.ItemTypes[row["type"]]
                };

                Weapons[weapon.Code] = weapon;
            }
        }

        public new object Clone()
        {
            var dmgTypes = new List<DamageType>();
            this.DamageTypes.ForEach(x => dmgTypes.Add((DamageType)x.Clone()));

            return new Weapon
            {
                EquipmentType = this.EquipmentType,
                Code = this.Code,
                RequiredStrength = this.RequiredStrength,
                RequiredDexterity = this.RequiredDexterity,
                Durability = this.Durability,
                ItemLevel = this.ItemLevel,
                Type = this.Type,
                DamageTypes = dmgTypes
            };
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
