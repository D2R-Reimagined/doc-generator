using System;
using System.Collections.Generic;
using System.Linq;

using D2TxtImporter.lib.Exceptions;
using D2TxtImporter.lib.Model.Dictionaries;
using D2TxtImporter.lib.Model.Equipment;
using D2TxtImporter.lib.Model.Types;

namespace D2TxtImporter.lib.Model.Items
{
    public class Unique : Item
    {
        public string Type { get; set; }
        public string Vanilla { get; set; }

        public static List<Unique> Import(string excelFolder)
        {
            var result = new List<Unique>();

            // Build a map of raw line numbers (including header) from the source file
            var rawLines = Importer.ReadTxtFileToList(excelFolder + "/UniqueItems.txt");
            var rawIndexByName = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rawLines.Count; i++)
            {
                var line = rawLines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('\t');
                if (parts.Length == 0) continue;
                var idx = parts[0]; // index column
                if (!string.IsNullOrWhiteSpace(idx))
                {
                    if (!rawIndexByName.ContainsKey(idx)) rawIndexByName[idx] = i + 1; // 1-based including header
                }
            }

            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/UniqueItems.txt");
            var sunderNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Cold Rupture", "Flame Rift", "Crack of the Heavens", "Rotting Fissure", "Bone Break", "Black Cleft"
            };

            foreach (var row in table)
            {
                if (string.IsNullOrEmpty(row["lvl"]))
                {
                    continue;
                }

                var name = row["index"];

                // Compare the name ignoring case and ignore items that are in the ItemsToIgnore list
                if (name != null && ItemsToIgnore.Any(item => name.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    continue;
                }

                var rarity = Utility.ToNullableInt(row["rarity"]);
                
                var itemLevel = Utility.ToNullableInt(row["lvl"]);
                if (!itemLevel.HasValue)
                {
                    ExceptionHandler.LogException(new Exception($"Could not find item level for '{name}' in UniqueItems.txt"));
                }

                var requiredLevel = Utility.ToNullableInt(row["lvl req"]);
                if (!requiredLevel.HasValue)
                {
                    ExceptionHandler.LogException(new Exception($"Could not find required level for '{name}' in UniqueItems.txt"));
                }

                var code = row["code"];
                
                var unique = new Unique
                {
                    Index = name,
                    Enabled = row["enabled"] == "1",
                    Rarity = rarity.Value,
                    ItemLevel = itemLevel.Value,
                    RequiredLevel = requiredLevel.Value,
                    Code = code,
                    DamageArmorEnhanced = false,
                    // Determine Vanilla using raw line number (header counted as line 1)
                    Vanilla = (rawIndexByName.TryGetValue(name, out var rawRow) && rawRow <= 403) || sunderNames.Contains(name) ? "Y" : "N",
                };
                // no debug logging

                Equipment.Equipment eq = null;

                if (Armor.Armors.ContainsKey(code))
                {
                    eq = (Armor)Armor.Armors[code].Clone();
                }
                
                else if (Weapon.Weapons.ContainsKey(code))
                {
                    eq = (Weapon)Weapon.Weapons[code].Clone();
                }
                
                else if (Misc.MiscItems.ContainsKey(code))
                {
                    var misc = Misc.MiscItems[code];

                    eq = new Equipment.Equipment
                    {
                        Code = misc.Code,
                        EquipmentType = EquipmentType.Jewelery,
                        Type = misc.Type
                    };
                }
                
                else
                {
                    ExceptionHandler.LogException(new Exception($"Could not find code '{code}' in Weapons.txt, Armor.txt, or Misc.txt for set item '{unique.Name}' in UniqueItems.txt"));
                }

                unique.Equipment = eq;
                unique.Type = eq.Type.Index;

                var propList = new List<PropertyInfo>();
                // Add the properties
                for (int i = 1; i <= 12; i++)
                {
                    propList.Add(new PropertyInfo(row[$"prop{i}"], row[$"par{i}"], row[$"min{i}"], row[$"max{i}"]));
                }

                try
                {
                    var properties = ItemProperty.GetProperties(propList, unique.ItemLevel).OrderByDescending(x => x.ItemStatCost == null ? 0 : x.ItemStatCost.DescriptionPriority).ToList();
                    unique.Properties = properties;
                }
                
                catch (Exception e)
                {
                    ExceptionHandler.LogException(new Exception($"Could not get properties for unique '{unique.Name}' in UniqueItems.txt", e));
                }

                // Adjust required level using consolidated evaluator (explicit + implied skill/oskill) in a single pass
                // Ensure it's at least the base armor/weapon required level before applying item_levelreq or skill/oskill effects
                unique.RequiredLevel = RequiredLevelReport.ComputeAdjustedRequiredLevel("Unique", unique.Name, unique.RequiredLevel, unique.Properties, unique?.Equipment?.BaseRequiredLevel);

                AddDamageArmorString(unique);

                result.Add(unique);
            }

            return result.OrderBy(x => x.RequiredLevel).ToList();
        }

        public static void AddDamageArmorString(Item unique)
        {
            if (unique == null)
            {
                throw new Exception("The unique item does not exist, something is wrong with one of your unique items.");
            }

            if (unique.Equipment == null)
            {
                throw new Exception($"Could not find equipment for item '{unique.Name}'");
            }

            unique.AdjustRequirements();

            // Handle Ethereal property bonus to base damage/armor before other calculations
            var ethProp = unique.Properties.FirstOrDefault(x => x.Property.Code == "ethereal");
            if (ethProp != null && (ethProp.Min ?? 0) >= 1)
            {
                if (unique.Equipment is Weapon weapon)
                {
                    foreach (var dt in weapon.DamageTypes)
                    {
                        dt.MinDamage = (int)(dt.MinDamage * 1.5f);
                        dt.MaxDamage = (int)(dt.MaxDamage * 1.5f);
                    }
                }
                else if (unique.Equipment is Armor armor)
                {
                    armor.MinAc = (int)(armor.MinAc * 1.5f);
                    armor.MaxAc = (int)(armor.MaxAc * 1.5f);

                    if (armor.MinDamage.HasValue) armor.MinDamage = (int)(armor.MinDamage.Value * 1.5f);
                    if (armor.MaxDamage.HasValue) armor.MaxDamage = (int)(armor.MaxDamage.Value * 1.5f);
                }
            }

            if (unique.Equipment.EquipmentType == EquipmentType.Weapon)
            {
                var weapon = unique.Equipment as Weapon;

                int lowLevel = Math.Max(1, unique.RequiredLevel);
                int highLevel = 100;

                // Aggregate modifiers in three phases to ensure correct order of operations and consolidation.
                // Phase 1: Enhanced Damage
                float edLowMin = 0, edHighMin = 0;
                float edLowMax = 0, edHighMax = 0;

                // Phase 2: Normal Flat Damage
                int flatLowMin = 0, flatHighMin = 0;
                int flatLowMax = 0, flatHighMax = 0;

                // Phase 3: Per-Level Flat Damage
                int plLowMin = 0, plHighMin = 0;
                int plLowMax = 0, plHighMax = 0;

                foreach (var property in unique.Properties)
                {
                    switch (property.Property.Code)
                    {
                        case "dmg%":
                            {
                                float valMin = property.Min ?? 0;
                                float valMax = property.Max ?? property.Min ?? 0;
                                edLowMin += valMin; edHighMin += valMax;
                                edLowMax += valMin; edHighMax += valMax;
                                unique.DamageArmorEnhanced = true;
                            }
                            break;
                        case "dmg%/lvl":
                            {
                                var opMath = Math.Pow(2, property.ItemStatCost.OpParam ?? 0);
                                float growthMin = property.Min ?? (float.TryParse(property.Parameter, out var p1) ? p1 : 0);
                                float growthMax = property.Max ?? growthMin;
                                float valLow = (float)(growthMin / opMath * lowLevel);
                                float valHigh = (float)(growthMax / opMath * highLevel);
                                // User said: "maximum dmg% per character level"
                                edLowMax += valLow; edHighMax += valHigh;
                                unique.DamageArmorEnhanced = true;
                            }
                            break;
                        case "dmg-norm":
                            {
                                int valMin = property.Min ?? 0;
                                int valMax = property.Max ?? 0;
                                flatLowMin += valMin; flatHighMin += valMin;
                                flatLowMax += valMax; flatHighMax += valMax;
                                unique.DamageArmorEnhanced = true;
                            }
                            break;
                        case "dmg-min":
                            {
                                int valMin = property.Min ?? 0;
                                int valMax = property.Max ?? property.Min ?? 0;
                                flatLowMin += valMin;
                                flatHighMin += valMax;

                                unique.DamageArmorEnhanced = true;
                            }
                            break;
                        case "dmg-max":
                            {
                                flatLowMax += property.Min ?? 0;
                                flatHighMax += property.Max ?? property.Min ?? 0;
                                unique.DamageArmorEnhanced = true;
                            }
                            break;
                        case "dmg/lvl":
                        case "dmg-max/lvl":
                            {
                                var opMath = Math.Pow(2, property.ItemStatCost.OpParam ?? 0);
                                float growthMin = property.Min ?? (float.TryParse(property.Parameter, out var p1) ? p1 : 0);
                                float growthMax = property.Max ?? growthMin;
                                int valLow = (int)(growthMin / opMath * lowLevel);
                                int valHigh = (int)(growthMax / opMath * highLevel);
                                plLowMax += valLow; plHighMax += valHigh;
                                unique.DamageArmorEnhanced = true;
                            }
                            break;
                        case "dmg-min/lvl":
                            {
                                var opMath = Math.Pow(2, property.ItemStatCost.OpParam ?? 0);
                                float growthMin = property.Min ?? (float.TryParse(property.Parameter, out var p1) ? p1 : 0);
                                float growthMax = property.Max ?? growthMin;
                                int valLow = (int)(growthMin / opMath * lowLevel);
                                int valHigh = (int)(growthMax / opMath * highLevel);
                                plLowMin += valLow; plHighMin += valHigh;
                                unique.DamageArmorEnhanced = true;
                            }
                            break;
                        case "flat-dmg/lvl":
                            {
                                var opMath = Math.Pow(2, property.ItemStatCost.OpParam ?? 0);
                                float growthMin = property.Min ?? (float.TryParse(property.Parameter, out var p1) ? p1 : 0);
                                float growthMax = property.Max ?? growthMin;
                                int valLowMin = (int)(growthMin / opMath * lowLevel);
                                int valHighMin = (int)(growthMin / opMath * highLevel);
                                int valLowMax = (int)(growthMax / opMath * lowLevel);
                                int valHighMax = (int)(growthMax / opMath * highLevel);
                                plLowMin += valLowMin; plHighMin += valHighMin;
                                plLowMax += valLowMax; plHighMax += valHighMax;
                                unique.DamageArmorEnhanced = true;
                            }
                            break;
                    }
                }

                foreach (var damageType in weapon.DamageTypes)
                {
                    // 1. Apply ED and consolidate (round/int)
                    int minDam1 = (int)(damageType.MinDamage * (100f + edLowMin) / 100f);
                    int minDam2 = (int)(damageType.MinDamage * (100f + edHighMin) / 100f);

                    int maxDam1 = (int)(damageType.MaxDamage * (100f + edLowMax) / 100f);
                    int maxDam2 = (int)(damageType.MaxDamage * (100f + edHighMax) / 100f);

                    // 2. Apply Flat Damage (Normal + Per-Level)
                    minDam1 += flatLowMin + plLowMin;
                    minDam2 += flatHighMin + plHighMin;
                    maxDam1 += flatLowMax + plLowMax;
                    maxDam2 += flatHighMax + plHighMax;

                    maxDam1 = Math.Max(maxDam1, minDam1 + 1);
                    maxDam2 = Math.Max(maxDam2, minDam2 + 1);

                    // Update numeric fields for JSON consistency
                    damageType.MinDamage = minDam1;
                    damageType.MaxDamage = maxDam1;

                    if (minDam1 == minDam2 && maxDam1 == maxDam2)
                    {
                        damageType.DamageString = $"{minDam1} to {maxDam1}";
                    }
                    else
                    {
                        string minStr = minDam1 == minDam2 ? minDam1.ToString() : $"({minDam1}-{minDam2})";
                        string maxStr = maxDam1 == maxDam2 ? maxDam1.ToString() : $"({maxDam1}-{maxDam2})";
                        damageType.DamageString = $"{minStr} to {maxStr}";
                    }
                }
            }
            else if (unique.Equipment.EquipmentType == EquipmentType.Armor)
            {
                // Calculate armor
                var armor = unique.Equipment as Armor;

                int lowLevel = Math.Max(1, unique.RequiredLevel);
                int highLevel = 100;

                float edLowMin = 0, edHighMin = 0;
                float edLowMax = 0, edHighMax = 0;
                int flatLowMin = 0, flatHighMin = 0;
                int flatLowMax = 0, flatHighMax = 0;
                bool hasEd = false;

                foreach (var property in unique.Properties)
                {
                    switch (property.Property.Code)
                    {
                        case "ac%":
                            {
                                float valMin = property.Min ?? 0;
                                float valMax = property.Max ?? property.Min ?? 0;
                                edLowMin += valMin; edHighMin += valMin;
                                edLowMax += valMax; edHighMax += valMax;
                                hasEd = true;
                                unique.DamageArmorEnhanced = true;
                            }
                            break;
                        case "ac%/lvl":
                            {
                                var opMath = Math.Pow(2, property.ItemStatCost.OpParam ?? 0);
                                float growthMin = property.Min ?? (float.TryParse(property.Parameter, out var p1) ? p1 : 0);
                                float growthMax = property.Max ?? growthMin;
                                float valLowMin = (float)(growthMin / opMath * lowLevel);
                                float valHighMin = (float)(growthMin / opMath * highLevel);
                                float valLowMax = (float)(growthMax / opMath * lowLevel);
                                float valHighMax = (float)(growthMax / opMath * highLevel);

                                edLowMin += valLowMin; edHighMin += valHighMin;
                                edLowMax += valLowMax; edHighMax += valHighMax;
                                hasEd = true;
                                unique.DamageArmorEnhanced = true;
                            }
                            break;
                        case "ac":
                            {
                                int valMin = property.Min ?? 0;
                                int valMax = property.Max ?? property.Min ?? 0;
                                flatLowMin += valMin; flatHighMin += valMin;
                                flatLowMax += valMax; flatHighMax += valMax;
                                unique.DamageArmorEnhanced = true;
                            }
                            break;
                        case "ac/lvl":
                            {
                                var opMath = Math.Pow(2, property.ItemStatCost.OpParam ?? 0);
                                float growthMin = property.Min ?? (float.TryParse(property.Parameter, out var p1) ? p1 : 0);
                                float growthMax = property.Max ?? growthMin;
                                int valLowMin = (int)(growthMin / opMath * lowLevel);
                                int valHighMin = (int)(growthMin / opMath * highLevel);
                                int valLowMax = (int)(growthMax / opMath * lowLevel);
                                int valHighMax = (int)(growthMax / opMath * highLevel);

                                flatLowMin += valLowMin; flatHighMin += valHighMin;
                                flatLowMax += valLowMax; flatHighMax += valHighMax;
                                unique.DamageArmorEnhanced = true;
                            }
                            break;
                    }
                }

                int minAc1, minAc2, maxAc1, maxAc2;
                if (hasEd)
                {
                    // In D2, if an item has ED%, the base AC is fixed at MaxAc + 1
                    minAc1 = (int)Math.Floor((armor.MaxAc + 1) * (100f + edLowMin) / 100f) + flatLowMin;
                    minAc2 = (int)Math.Floor((armor.MaxAc + 1) * (100f + edHighMin) / 100f) + flatHighMin;
                    maxAc1 = (int)Math.Floor((armor.MaxAc + 1) * (100f + edLowMax) / 100f) + flatLowMax;
                    maxAc2 = (int)Math.Floor((armor.MaxAc + 1) * (100f + edHighMax) / 100f) + flatHighMax;
                }
                else
                {
                    // No ED%, just add flat AC to the base range
                    minAc1 = armor.MinAc + flatLowMin;
                    minAc2 = armor.MinAc + flatHighMin;
                    maxAc1 = armor.MaxAc + flatLowMax;
                    maxAc2 = armor.MaxAc + flatHighMax;
                }

                armor.MinAc = minAc1;
                armor.MaxAc = maxAc1;

                string minStr = minAc1 == minAc2 ? minAc1.ToString() : $"({minAc1}-{minAc2})";
                string maxStr = maxAc1 == maxAc2 ? maxAc1.ToString() : $"({maxAc1}-{maxAc2})";

                if (minStr == maxStr)
                {
                    armor.ArmorString = minStr;
                }
                else
                {
                    armor.ArmorString = $"{minStr}-{maxStr}";
                }

                // Handle smite/kick damage using a shared helper
                if (armor.MinDamage.HasValue && armor.MinDamage.Value > 0)
                {
                    var prefix = D2TxtImporter.lib.Model.Equipment.Equipment.GetDamagePrefix(armor.Type);
                    armor.DamageStringPrefix = prefix ?? "Unhandled Damage Prefix";

                    if (armor.MinDamage.Value == armor.MaxDamage.Value)
                    {
                        armor.DamageString = $"{armor.MinDamage.Value}";
                    }
                    else
                    {
                        armor.DamageString = $"{armor.MinDamage.Value} to {armor.MaxDamage.Value}";
                    }
                }
            }

            // Handle max durability
            var dur = unique.Properties.FirstOrDefault(x => x.Property.Code == "dur");
            if (dur != null)
            {
                unique.Equipment.Durability += dur.Min.Value;
                unique.Properties.Remove(dur);
            }

            // Handle indestructible items durability
            if (unique.Properties.Any(x => x.Property.Code == "indestruct"))
            {
                unique.Equipment.Durability = 0;
            }
        }
        
        private static readonly HashSet<string> ItemsToIgnore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Add parts of the name for unique items you want to ignore case-insensitive
            "amulet of the viper",
            "staff of kings",
            "horadric staff",
            "hell forge hammer",
            "khalimflail",
            "superkhalimflail",
            "pliers",
            "grabber",
            "gem bag",
            "Keychain",
            "rainbow facet"
        };
    }
}
