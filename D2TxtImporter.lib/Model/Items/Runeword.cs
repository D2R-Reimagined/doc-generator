﻿using System;
using System.Collections.Generic;
using System.Linq;
using D2TxtImporter.lib.Exceptions;
using D2TxtImporter.lib.Model.Dictionaries;
using D2TxtImporter.lib.Model.Types;

namespace D2TxtImporter.lib.Model.Items
{
    public class Runeword : Item
    {
        public List<Misc> Runes { get; set; }
        public List<ItemType> Types { get; set; }

        public static List<Runeword> Import(string excelFolder)
        {
            var result = new List<Runeword>();

            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/Runes.txt");

            foreach (var row in table)
            {
                if (string.IsNullOrEmpty(row["Rune1"]))
                {
                    continue;
                }

                // Add the runes
                var runeArray = new[] { row["Rune1"], row["Rune2"], row["Rune3"], row["Rune4"], row["Rune5"], row["Rune6"] };
                var runes = new List<Misc>();

                for (int i = 0; i < runeArray.Count(); i++)
                {
                    if (!string.IsNullOrEmpty(runeArray[i]) && !runeArray[i].StartsWith("*"))
                    {
                        runes.Add(Misc.MiscItems[runeArray[i]]);
                    }
                }

                // Add the types
                var typeArray = new[] { row["itype1"], row["itype2"], row["itype3"], row["itype4"], row["itype5"], row["itype6"] };
                var types = new List<ItemType>();

                var shieldCounted = false;
                var weaponCounted = false;
                var armorCounted = false;
                var typeCount = 0;

                for (int i = 0; i < typeArray.Count(); i++)
                {
                    if (!string.IsNullOrEmpty(typeArray[i]) && !typeArray[i].StartsWith("*"))
                    {
                        if (!ItemType.ItemTypes.ContainsKey(typeArray[i]))
                            throw new Exception("Cannot find item type " + typeArray[i]);

                        var type = ItemType.ItemTypes[typeArray[i]];
                        types.Add(type);

                        // Count the amount of types, if this is more than 1 we add the type suffix later
                        if (type.Equiv1 == "shld" || type.Code == "shld") // Shield
                        {
                            if (!shieldCounted)
                            {
                                typeCount++;
                            }

                            shieldCounted = true;
                        }
                        else if (type.BodyLoc1 == "rarm" || type.Code == "weap") // Weapon
                        {
                            if (!weaponCounted)
                            {
                                typeCount++;
                            }
                            
                            weaponCounted = true;
                        }
                        else // Armor
                        {
                            if (!armorCounted)
                            {
                                typeCount++;
                            }
                            
                            armorCounted = true;
                        }
                    }
                }

                var runeword = new Runeword
                {
                    Index = row["Name"],
                    Enabled = true,
                    ItemLevel = runes.Max(x => x.ItemLevel),
                    RequiredLevel = runes.Max(x => x.RequiredLevel),
                    Code = row["Name"],
                    Types = types,
                    Runes = runes
                };

                var propList = new List<PropertyInfo>();
                // Add the properties
                for (int i = 1; i <= 7; i++)
                {
                    propList.Add(new PropertyInfo(row[$"T1Code{i}"], row[$"T1Param{i}"], row[$"T1Min{i}"], row[$"T1Max{i}"]));
                }

                try
                {
                    var properties = ItemProperty.GetProperties(propList).OrderByDescending(x => x.ItemStatCost == null ? 0 : x.ItemStatCost.DescriptionPriority).ToList();
                    runeword.Properties = properties;
                }
                catch (Exception e)
                {
                    ExceptionHandler.LogException(new Exception($"Could not get properties for runeword '{runeword.Name}' in Runes.txt", e));
                }

                // Add rune properties
                foreach (var rune in runeword.Runes)
                {
                    if (!Gem.Gems.ContainsKey(rune.Name))
                    {
                        ExceptionHandler.LogException(new Exception($"Could not find rune '{rune.Name}' in Gems.txt"));
                    }

                    var runeGem = Gem.Gems[rune.Name];
                    var wepAdded = false;
                    var shieldAdded = false;
                    var armorAdded = false;

                    foreach (var type in runeword.Types)
                    {
                        if (type.Equiv1 == "shld" || type.Code == "shld") // Shield
                        {
                            if (!shieldAdded)
                            {
                                var properties = runeGem.ShieldProperties.Select(x => new ItemProperty(x)).ToList();

                                if (typeCount > 1)
                                {
                                    properties.ForEach(x => x.Suffix = " (Shield)");
                                }

                                runeword.Properties.AddRange(properties);
                            }
                            
                            shieldAdded = true;
                        }
                        else if (type.BodyLoc1 == "rarm" || type.Code == "weap") // Weapon
                        {
                            if (!wepAdded)
                            {
                                var properties = runeGem.WeaponProperties.Select(x => new ItemProperty(x)).ToList();

                                if (typeCount > 1)
                                {
                                    properties.ForEach(x => x.Suffix = " (Weapon)");
                                }

                                runeword.Properties.AddRange(properties);
                            }
                           
                            wepAdded = true;
                        }
                        else // Armor
                        {
                            if (!armorAdded)
                            {
                                var properties = runeGem.HelmProperties.Select(x => new ItemProperty(x)).ToList();

                                if (typeCount > 1)
                                {
                                    properties.ForEach(x => x.Suffix = " (Armor)");
                                }

                                runeword.Properties.AddRange(properties);
                            }
                            
                            armorAdded = true;
                        }
                    }
                }
                
                foreach (var property in runeword.Properties)
                {
                    switch (property.Parameter)
                    {
                        case "ama":
                            property.PropertyString += " All Amazon Skills";
                            break;
                        case "sor":
                            property.PropertyString += " All Sorceress Skills";
                            break;
                        case "nec":
                            property.PropertyString += " All Necromancer Skills";
                            break;
                        case "pal":
                            property.PropertyString += " All Paladin Skills";
                            break;
                        case "bar":
                            property.PropertyString += " All Barbarian Skills";
                            break;
                        case "dru":
                            property.PropertyString += " All Druid Skills";
                            break;
                        case "ass":
                            property.PropertyString += " All Assassin Skills";
                            break;
                    }

                    if (property.Property.Code.Contains("/lvl"))
                    {
                        property.PropertyString = property.PropertyString.Replace("+", $"+{property.Parameter}");
                        property.PropertyString += $" Per Level {property.ItemStatCost.DescriptionString2}";
                    }
                }

                if (runeword.Properties.Count > 0)
                {
                    ItemProperty.CleanupDublicates(runeword.Properties);
                    result.Add(runeword);
                }

                if (runeword.Name == "Peril")
                {
                    Console.WriteLine("Peril");
                }
            }

            return result.OrderBy(x => x.RequiredLevel).ToList();
        }
    }
}
