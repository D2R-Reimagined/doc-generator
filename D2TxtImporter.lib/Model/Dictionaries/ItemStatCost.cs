using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using D2TxtImporter.lib.Exceptions;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Dictionaries
{
    public class ItemStatCost
    {
        [JsonIgnore]
        public string Stat { get; set; }

        [JsonIgnore]
        public int Id { get; set; }

        [JsonIgnore]
        public int? Op { get; set; }

        [JsonIgnore]
        public int? OpParam { get; set; }

        [JsonIgnore]
        public int? DescriptionPriority { get; set; }

        [JsonIgnore]
        public int? DescriptionFunction { get; set; }

        [JsonIgnore]
        public int? DescriptionValue { get; set; }

        [JsonIgnore]
        public string DescriptonStringPositive { get; set; }

        [JsonIgnore]
        public string DescriptionStringNegative { get; set; }

        [JsonIgnore]
        public string DescriptionString2 { get; set; }

        [JsonIgnore]
        public int? GroupDescription { get; set; }

        [JsonIgnore]
        public int? GroupDescriptionFunction { get; set; }

        [JsonIgnore]
        public int? GroupDescriptionValue { get; set; }

        [JsonIgnore]
        public string GroupDescriptonStringPositive { get; set; }

        [JsonIgnore]
        public string GroupDescriptionStringNegative { get; set; }

        [JsonIgnore]
        public string GroupDescriptionString2 { get; set; }

        [JsonIgnore]
        public static Dictionary<string, ItemStatCost> ItemStatCosts;

        public static void Import(string excelFolder)
        {
            ItemStatCosts = new Dictionary<string, ItemStatCost>();

            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/ItemStatCost.txt");

            foreach (var row in table)
            {
                var itemStatCost = new ItemStatCost
                {
                    Stat = row["Stat"],
                    Id = int.Parse(row["*ID"]),
                    Op = Utility.ToNullableInt(row["op"]),
                    OpParam = Utility.ToNullableInt(row["op param"]),
                    DescriptionPriority = Utility.ToNullableInt(row["descpriority"]),
                    DescriptionFunction = Utility.ToNullableInt(row["descfunc"]),
                    DescriptionValue = Utility.ToNullableInt(row["descval"]),
                    DescriptonStringPositive = Table.GetValue(row["descstrpos"]),
                    DescriptionStringNegative = Table.GetValue(row["descstrneg"]),
                    DescriptionString2 = Table.GetValue(row["descstr2"]),
                    GroupDescription = Utility.ToNullableInt(row["dgrp"]),
                    GroupDescriptionFunction = Utility.ToNullableInt(row["dgrpfunc"]),
                    GroupDescriptionValue = Utility.ToNullableInt(row["dgrpval"]),
                    GroupDescriptonStringPositive = Table.GetValue(row["dgrpstrpos"]),
                    GroupDescriptionStringNegative = Table.GetValue(row["dgrpstrneg"]),
                    GroupDescriptionString2 = Table.GetValue(row["dgrpstr2"])
                };

                ItemStatCosts[itemStatCost.Stat] = itemStatCost;
            }

            HardcodedTableStats();
            FixBrokenEntries();
        }

        public override string ToString()
        {
            return Stat;
        }

        public static void HardcodedTableStats()
        {
            var enhancedDamage = new ItemStatCost
            {
                Stat = "dmg%",
                DescriptionPriority = 144, // 1 below attack speed (seems right)
                DescriptionFunction = 4, // +val%
                DescriptonStringPositive = "Enhanced Damage",
                DescriptionStringNegative = "Enhanced Damage",
                DescriptionValue = 1 // Add value before
            };

            ItemStatCosts[enhancedDamage.Stat] = enhancedDamage;

            var ethereal = new ItemStatCost
            {
                Stat = "ethereal",
                DescriptionPriority = 1, // Min priority
                DescriptionFunction = 1, // lstValue
                DescriptonStringPositive = "Ethereal (Cannot Be Repaired)",
                DescriptionStringNegative = "Ethereal (Cannot Be Repaired)",
                DescriptionValue = 0 // Do not add value
            };

            ItemStatCosts[ethereal.Stat] = ethereal;

            var eledam = new ItemStatCost
            {
                Stat = "eledam",
                DescriptionPriority = ItemStatCosts["firemindam"].DescriptionPriority,
                DescriptionFunction = 30,
                DescriptonStringPositive = "Adds %d %s damage",
                DescriptionValue = 3
            };

            ItemStatCosts["eledam"] = eledam;
            
            var dmgNorm = new ItemStatCost
            {
                Stat = "dmg-norm",
                DescriptionPriority = ItemStatCosts["mindamage"].DescriptionPriority,
                DescriptionFunction = 31,
                DescriptonStringPositive = "Adds %min-%max to Damage",
                DescriptionValue = 3
            };

            ItemStatCosts["dmg-norm"] = dmgNorm;

            var resAll = new ItemStatCost
            {
                Stat = "res-all",
                DescriptionPriority = 34,
                DescriptionFunction = 4, // lstValue
                DescriptonStringPositive = Table.GetValue("strModAllResistances"),
                DescriptionStringNegative = Table.GetValue("strModAllResistances"),
                DescriptionValue = 2 // Do not add value
            };

            ItemStatCosts["res-all"] = resAll;
            
            var resAllMax = new ItemStatCost
            {
                Stat = "res-all-max",
                DescriptionPriority = 34,
                DescriptionFunction = 4, // lstValue
                DescriptonStringPositive = Table.GetValue("ModStrAllMaxRes"),
                DescriptionStringNegative = Table.GetValue("ModStrAllMaxRes"),
                DescriptionValue = 2 // Do not add value
            };
            
            ItemStatCosts["res-all-max"] = resAllMax;

            var allStats = new ItemStatCost
            {
                Stat = "all-stats",
                DescriptionPriority = 34,
                DescriptionFunction = 1, // lstValue
                DescriptonStringPositive = Table.GetValue("Moditem2allattrib"),
                DescriptionStringNegative = Table.GetValue("Moditem2allattrib"),
                DescriptionValue = 2 // Do not add value
            };
            
            ItemStatCosts["all-stats"] = allStats;
            
            var allElemDmg = new ItemStatCost
            {
                Stat = "extra-elem",
                DescriptionPriority = 88,
                DescriptionFunction = 19,
                DescriptonStringPositive = Table.GetValue("allelemskilldmg"),
                DescriptionStringNegative = Table.GetValue("allelemskilldmg"),
                DescriptionValue = 3
            };
            
            ItemStatCosts["extra-elem"] = allElemDmg;
            
            var pierceAllElem = new ItemStatCost
            {
                Stat = "pierce-elem",
                DescriptionPriority = 87,
                DescriptionFunction = 19,
                DescriptonStringPositive = Table.GetValue("PenetrateElePen"),
                DescriptionStringNegative = Table.GetValue("PenetrateElePen"),
                DescriptionValue = 3
            };
            
            ItemStatCosts["pierce-elem"] = pierceAllElem;

            var poisDamage = new ItemStatCost
            {
                Stat = "dmg-pois",
                DescriptionPriority = 92,
                DescriptionFunction = 19,
                DescriptonStringPositive = Table.GetValue("strModPoisonDamage"),
                DescriptionStringNegative = Table.GetValue("strModPoisonDamage"),
                DescriptionValue = 2
            };
            
            ItemStatCosts["dmg-pois"] = poisDamage;
            
            var fireSkillStat = new ItemStatCost
            {
                Stat = "fireskill",
                DescriptionPriority = 157,
                DescriptionFunction = 19,
                DescriptonStringPositive = Table.GetValue("ModStr3i"),
                DescriptionStringNegative = Table.GetValue("ModStr3i"),
                DescriptionValue = 3
            };
            
            ItemStatCosts["fireskill"] = fireSkillStat;
            
            var coldSkillStat = new ItemStatCost
            {
                Stat = "coldskill",
                DescriptionPriority = 157,
                DescriptionFunction = 19,
                DescriptonStringPositive = Table.GetValue("ModStrcoldskill"),
                DescriptionStringNegative = Table.GetValue("ModStrcoldskill"),
                DescriptionValue = 3
            };
            
            ItemStatCosts["coldskill"] = coldSkillStat;
            
            var lightningSkillStat = new ItemStatCost
            {
                Stat = "lightningskill",
                DescriptionPriority = 157,
                DescriptionFunction = 19,
                DescriptonStringPositive = Table.GetValue("ModStrlightningskill"),
                DescriptionStringNegative = Table.GetValue("ModStrlightningskill"),
                DescriptionValue = 3
            };
            
            ItemStatCosts["lightningskill"] = lightningSkillStat;
            
            var poisonSkillStat = new ItemStatCost
            {
                Stat = "poisonskill",
                DescriptionPriority = 157,
                DescriptionFunction = 19,
                DescriptonStringPositive = Table.GetValue("ModStrpoisonskill"),
                DescriptionStringNegative = Table.GetValue("ModStrpoisonskill"),
                DescriptionValue = 3
            };
            
            ItemStatCosts["poisonskill"] = poisonSkillStat;
            
            var magicSkillStat = new ItemStatCost
            {
                Stat = "magicskill",
                DescriptionPriority = 157,
                DescriptionFunction = 19,
                DescriptonStringPositive = Table.GetValue("ModStrmagicskill"),
                DescriptionStringNegative = Table.GetValue("ModStrmagicskill"),
                DescriptionValue = 3
            };
            
            ItemStatCosts["magicskill"] = magicSkillStat;
        }
        

        public static void FixBrokenEntries()
        {
            var sockets = ItemStatCosts["item_numsockets"];
            sockets.DescriptionPriority = 1;
            sockets.DescriptionFunction = 29;
            sockets.DescriptonStringPositive = "Socketed";
            sockets.GroupDescriptionStringNegative = "Socketed";
            sockets.DescriptionValue = 3; // Use value as is
        }

        public string PropertyString(int? value, int? value2, string parameter, int itemLevel)
        {
            string lstValue;
            var valueString = GetValueString(value, value2);

            if (DescriptonStringPositive == null)
            {
                lstValue = Stat;
            }
            else if (value2.HasValue && value2.Value < 0)
            {
                lstValue = DescriptionStringNegative;
            }
            else
            {
                lstValue = DescriptonStringPositive;
            }

            if (DescriptionFunction.HasValue)
            {
                if (DescriptionFunction.Value >= 1 && DescriptionFunction.Value <= 4 && lstValue.Contains("%d"))
                {
                    valueString = lstValue.Replace("%d", valueString);
                    DescriptionValue = 3;
                }
                else
                {
                    switch (DescriptionFunction.Value)
                    {
                        case 1:
                            valueString = $"+{valueString}";
                            break;
                        case 2:
                            valueString = $"{valueString}%";
                            break;
                        case 3:
                            valueString = $"{valueString}";
                            break;
                        case 4:
                            valueString = $"+{valueString}%";
                            break;
                        case 5:
                            if (value.HasValue)
                            {
                                value = value * 100 / 128;
                            }
                            if (value2.HasValue)
                            {
                                value2 = value2 * 100 / 128;
                            }
                            valueString = GetValueString(value, value2);
                            valueString = $"{valueString}% {lstValue}";
                            break;
                        case 6:
                            double val1 = 0;
                            double val2 = 0;

                            if (value.HasValue && value2.HasValue)
                            {
                                val1 = CalculatePerLevel(value.Value.ToString(), Op, OpParam, Stat);
                                val2 = CalculatePerLevel(value2.Value.ToString(), Op, OpParam, Stat);
                            }
                            else
                            {
                                val1 = CalculatePerLevel(parameter, Op, OpParam, Stat);
                                val2 = CalculatePerLevel(parameter, Op, OpParam, Stat);
                            }

                            valueString = GetValueString(val1, val2);
                            lstValue = DescriptonStringPositive;
                            DescriptionValue = 3;
                            valueString = $"+({valueString} Per Character Level) {Math.Floor(val1).ToString(CultureInfo.InvariantCulture)}-{Math.Floor(val2 * 99).ToString(CultureInfo.InvariantCulture)} {lstValue} (Based on Character Level)";
                            break;
                        case 7:
                            val1 = 0;
                            val2 = 0;

                            if (value.HasValue && value2.HasValue)
                            {
                                val1 = CalculatePerLevel(value.Value.ToString(), Op, OpParam, Stat);
                                val2 = CalculatePerLevel(value2.Value.ToString(), Op, OpParam, Stat);
                            }
                            else
                            {
                                val1 = CalculatePerLevel(parameter, Op, OpParam, Stat);
                                val2 = CalculatePerLevel(parameter, Op, OpParam, Stat);
                            }

                            valueString = GetValueString(val1, val2);
                            lstValue = DescriptonStringPositive;
                            DescriptionValue = 3;
                            valueString = $"({valueString}% Per Character Level) {Math.Floor(val1).ToString(CultureInfo.InvariantCulture)}-{Math.Floor(val2 * 99).ToString(CultureInfo.InvariantCulture)}% {lstValue} (Based on Character Level)";
                            break;
                        case 8:
                            val1 = 0;
                            val2 = 0;

                            if (value.HasValue && value2.HasValue)
                            {
                                val1 = CalculatePerLevel(value.Value.ToString(), Op, OpParam, Stat);
                                val2 = CalculatePerLevel(value2.Value.ToString(), Op, OpParam, Stat);
                            }
                            else
                            {
                                val1 = CalculatePerLevel(parameter, Op, OpParam, Stat);
                                val2 = CalculatePerLevel(parameter, Op, OpParam, Stat);
                            }

                            valueString = GetValueString(val1, val2);
                            lstValue = DescriptonStringPositive;
                            DescriptionValue = 3;
                            valueString = $"+({valueString} Per Character Level) {Math.Floor(val1).ToString(CultureInfo.InvariantCulture)}-{Math.Floor(val2 * 99).ToString(CultureInfo.InvariantCulture)} {lstValue} (Based on Character Level)";
                            break;
                        case 9:
                            val1 = 0;
                            val2 = 0;

                            if (value.HasValue && value2.HasValue)
                            {
                                val1 = CalculatePerLevel(value.Value.ToString(), Op, OpParam, Stat);
                                val2 = CalculatePerLevel(value2.Value.ToString(), Op, OpParam, Stat);
                            }
                            else
                            {
                                val1 = CalculatePerLevel(parameter, Op, OpParam, Stat);
                                val2 = CalculatePerLevel(parameter, Op, OpParam, Stat);
                            }

                            valueString = GetValueString(val1, val2);
                            lstValue = DescriptonStringPositive;
                            DescriptionValue = 3;
                            valueString = $"{lstValue} {Math.Floor(val1).ToString(CultureInfo.InvariantCulture)}-{Math.Floor(val2 * 99).ToString(CultureInfo.InvariantCulture)} ({valueString} Per Character Level)";
                            break;
                        case 11:
                            valueString = lstValue.Replace("%d", $"{((double)(Utility.ToNullableInt(parameter).Value / 100f)).ToString(CultureInfo.InvariantCulture)}");
                            DescriptionValue = 3;
                            break;
                        case 12:
                            valueString = $"+{valueString}";
                            break;
                        case 13:
                            var classReplace = "";
                            valueString = $"+{valueString}";

                            var regex = Regex.Match(parameter, @"randclassskill(\d+)"); // Work with custom randclasskill(digit) 
                            if (regex.Success)
                            {
                                classReplace = "(Random Class)";
                                valueString = $"+{regex.Groups[1].Value}";
                            }
                            else if (parameter == "randclassskill")
                            {
                                classReplace = "(Random Class)";
                                valueString = "+3";
                            }
                            else
                            {
                                if (!CharStat.CharStats.ContainsKey(parameter))
                                {
                                    throw ItemStatCostException.Create($"Could not find character class '{parameter}'\nNote: if you have made a custom version of 'randclassskill' to support different amount of skills change them to 'randclassskill<d>' for example 'randclassskill5' is supported.");
                                }
                                classReplace = CharStat.CharStats[parameter].Class;
                            }
                            lstValue = lstValue.Replace("%d", classReplace);
                            valueString = $"{valueString} {lstValue}";
                            break;
                        case 14:
                            var par = Utility.ToNullableInt(parameter);
                            if (!par.HasValue)
                            {
                                throw ItemStatCostException.Create($"Could not convert parameter '{parameter}' to a valid integer");
                            }

                            if (!CharStat.SkillTabs.ContainsKey(par.Value))
                            {
                                throw ItemStatCostException.Create($"Could not find skill tab with id {par.Value}");
                            }

                            var skillTab = CharStat.SkillTabs[par.Value];

                            var className = CharStat.CharStats.Values.First(x => x.StrSkillTab1 == skillTab || x.StrSkillTab2 == skillTab || x.StrSkillTab3 == skillTab).Class;

                            if (!Table.Tables.ContainsKey(skillTab))
                            {
                                throw ItemStatCostException.Create($"Could not find translation key '{skillTab}' in any .tbl file");
                            }

                            lstValue = Table.Tables[skillTab];
                            valueString = $"{lstValue.Replace("%d", valueString)} ({className} only)";
                            break;
                        case 15:
                            valueString = value2.Value.ToString();
                            var skill = Skill.GetSkill(parameter);

                            if (value2.Value == 0)
                            {
                                val1 = Math.Min(Math.Ceiling((itemLevel - (skill.RequiredLevel - 1)) / 3.9), 20);
                                val2 = Math.Min(Math.Round((99 - (skill.RequiredLevel - 1)) / 3.9), 20);

                                valueString = GetValueString(val1, val2);
                            }

                            valueString = lstValue.Replace("%d%", value.Value.ToString())
                                .Replace("%d", valueString)
                                .Replace("%s", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(skill.SkillDesc));

                            if (string.IsNullOrEmpty(skill.SkillDesc))
                            {
                                throw ItemStatCostException.Create($"Skill for property has missing 'skilldesc' in Skills.txt: name: '{skill.Name}', id: '{skill.Id}'");
                            }

                            break;
                        case 16:
                            valueString = lstValue.Replace("%d", valueString)
                                .Replace("%s", Skill.GetSkill(parameter).Name);
                            DescriptionValue = 3;
                            break;
                        case 19:
                            // Splash Charm Damage Display Fix
                            if (Stat.Contains("pl_mindamage"))
                            {
                                if (value.Value != value2.Value)
                                {
                                    valueString = $"+{value}% Min / +{value2}% Max Player Damage";
                                }
                                else
                                {
                                    valueString = $"+{value}% Player Damage";
                                }
                            }
                            //Per Level Display Fix
                            else if (Stat.Contains("perlevel"))
                            {
                                double opMath;
                                double opMath2;

                                // Try to use 'parameter' first
                                if (!string.IsNullOrEmpty(parameter) && double.TryParse(parameter, out opMath))
                                {
                                    opMath = Math.Round(opMath / 8d, 2);
                                    valueString = $"+{opMath}% {lstValue} (Per Character Level)";
                                }
                                // Fallback: check value1 and value2
                                else if (value == value2)
                                {
                                    opMath = Math.Round(value.Value / 8d, 2);
                                    valueString = $"+{opMath}% {lstValue} (Per Character Level)";
                                }
                                else if (value != value2)
                                {
                                    opMath = Math.Round(value.Value / 8d, 2);
                                    opMath2 = Math.Round(value2.Value / 8d, 2);
                                    valueString = $"+{opMath}%-{opMath2}% {lstValue} (Per Character Level)";
                                }
                                else
                                {
                                    throw new Exception(
                                        $"Invalid parameter and fallback values for level-based stat: parameter='{parameter}', value1='{value}', value2='{value2}'");
                                }

                            }
                            // Handle All Elemental Skills
                            else if (Stat.Contains("item_elemskill"))
                            {
                                valueString = parameter;
                            }
                            // Handle weapon poison damage here because I can't get it to work properly anywhere else and ChatGPT can't either. :D
                           else if (lstValue.Contains("Poison Damage Over") && value.HasValue && value2.HasValue && parameter != null)
                            {
                                int len = int.Parse(parameter);
                                int minPoison = (int)Math.Ceiling(value.Value * len / 256.0);
                                int maxPoison = (int)Math.Ceiling(value2.Value * len / 256.0);
                                int seconds = len / 25;

                                if (minPoison == maxPoison)
                                {
                                    valueString = $"+{minPoison} Poison Damage Over {seconds} Seconds";
                                }
                                else
                                {
                                    valueString = $"Adds {minPoison}-{maxPoison} Poison Damage Over {seconds} Seconds";
                                }

                                lstValue = null; //Empty this to prevent string duplication
                                
                            }
                            else if (lstValue.Contains("to Raven D") || lstValue.Contains("Max Ravens"))
                            {
                                valueString = lstValue;
                            }
                            else if (lstValue.Contains("Damage to") || lstValue.Contains("bonus to Attack"))
                            {
                                valueString = '+' + valueString + '%' + ' ' + lstValue.Replace("%d", valueString).Replace("%+d", valueString);
                            }
                            else if (lstValue.Contains(" Damage of"))
                            {
                                valueString = lstValue.Replace("%d", valueString).Replace("%+d", valueString) + ' ' + '+' + valueString;
                            }
                            else if (lstValue.Contains("to Mana") || lstValue.Contains("to Life") || lstValue.Contains("to Max") || lstValue.Contains("to Min") || lstValue.Contains("to Attack Rating") || lstValue.Contains("to Light ") || lstValue.Contains(" Skills")|| lstValue.Contains("to Raven"))
                            {
                                valueString = '+' + valueString + ' ' + lstValue.Replace("%d", valueString).Replace("%+d", valueString);
                            }
                            else if ((lstValue.Contains("Target Defense") || lstValue.Contains("to Enemy ")) && !lstValue.Contains("Ignore") || lstValue.Contains("to All Enemy"))
                            {
                                valueString = '-' + valueString + '%' + ' ' + lstValue.Replace("%d", valueString).Replace("%+d", valueString);
                            }
                            else if (lstValue.Contains("Resist"))
                            {
                                valueString = lstValue.Replace("%d", valueString).Replace("%+d", valueString) + ' ' + '+' + valueString + '%';
                            }
                            else if (lstValue.Contains("Stamina Drain") || lstValue.IndexOf("chance of", StringComparison.OrdinalIgnoreCase) >= 0 || lstValue.IndexOf("chance for", StringComparison.OrdinalIgnoreCase) >= 0 || lstValue.Contains("Experience Gained") || lstValue.Contains("extra gold") || lstValue.Contains("Damage Taken") || lstValue.Contains("Deadly Strike") || lstValue.Contains("Faster ") || lstValue.Contains("Increased A") || lstValue.Contains("Enhanced De") || lstValue.Contains(" Skill Damage") || lstValue.Contains(" Maximum Life") || lstValue.Contains(" Maximum Mana") || lstValue.Contains(" Damage Reduction") || lstValue.Contains("stolen per hit") || lstValue.Contains("Piercing "))
                            {
                                valueString = '+' + valueString + '%' + ' ' + lstValue.Replace("%d", valueString).Replace("%+d", valueString);
                            }
                            else if ((lstValue.Contains("to Dexterity") || lstValue.Contains("to Strength") || lstValue.Contains("to Vitality") || lstValue.Contains("to Energy")) && value < 0) 
                            {
                                valueString = "-" + valueString + ' ' + lstValue.Replace("%d", valueString).Replace("%+d", valueString);
                            }
                            else if ((lstValue.Contains("to Dexterity") || lstValue.Contains("to Strength") || lstValue.Contains("to Vitality") || lstValue.Contains("to Energy")) && value > 0) 
                            {
                                valueString = "+" + valueString + ' ' + lstValue.Replace("%d", valueString).Replace("%+d", valueString);
                            }
                            else if (lstValue.Contains("to "))
                            {
                                valueString = valueString + ' ' + lstValue.Replace("%d", valueString).Replace("%+d", valueString);
                            }
                            else if (lstValue.Contains("Regenerate Mana"))
                            {
                                valueString = lstValue.Replace("%d", valueString).Replace("%+d", valueString) + ' ' + '+' + value + '%';
                            }
                            else if (lstValue.Contains("Damage Reduced by"))
                            {
                                valueString = lstValue.Replace("%d", valueString).Replace("%+d", valueString) + ' ' + value;
                            }
                            else if (lstValue.Contains("Requirements") && value < 0 || lstValue.Contains("Length Reduced by") || lstValue.Contains("Slows target by") || lstValue.Contains("Reduces all Vendor"))
                            {
                                if (value!=value2) 
                                {
                                    valueString = lstValue.Replace("%d", valueString).Replace("%+d", valueString) + ' ' + value + '-' + value2 + '%';
                                    break;
                                }
                                valueString = lstValue.Replace("%d", valueString).Replace("%+d", valueString) + ' ' + value + '%';
                            }
                            else if (lstValue.Contains("Requirements") && value > 0)
                            {
                                valueString = lstValue.Replace("%d", valueString).Replace("%+d", valueString) + ' ' + '+' + value + '%';
                            }
                            else if (lstValue.Contains("Slain Monsters Rest in Peace") || lstValue.Contains("Cannot Be Frozen") || lstValue.Contains("Half Freeze Duration") || lstValue.Contains("Half Freeze Duration") || lstValue.Contains("Ignore") || lstValue.Contains("Prevent Monster") || lstValue.Contains("You feel inco") || lstValue.Contains("Knockback") || lstValue.Contains("Replenishes") || lstValue.Contains("Indestructible"))
                            {
                                valueString = lstValue;
                            }
                            else if (lstValue.Contains("% Fire") || lstValue.Contains("% Cold") || lstValue.Contains("% Lightning") || lstValue.Contains("% Magic"))
                            {
                                valueString = '+' + lstValue.Replace("%", valueString + '%');
                            }
                            else
                            {
                                valueString = '+' + valueString + ' ' + lstValue.Replace("%d", valueString).Replace("%+d", valueString);
                            }

                            break;
                        case 20:
                            valueString = $"{GetValueString(value * -1, value2 * -1)}%";
                            break;
                        case 23:
                            if (!MonStat.MonStats.ContainsKey(parameter))
                            {
                                throw ItemStatCostException.Create($"Could not find monster with id '{parameter}' in MonStats.txt");
                            }
                            valueString = $"{valueString}% {lstValue} {MonStat.MonStats[parameter].NameStr}";
                            DescriptionValue = 3;
                            break;
                        case 24:
                            valueString = $"Level {value2} {Skill.GetSkill(parameter).Name} ({value} Charges)";
                            break;
                        case 27:
                            var charClass = Skill.GetSkill(parameter).CharClass;
                            var reqString = "";

                            if (!string.IsNullOrEmpty(parameter))
                            {
                                int classNumber;
                                bool isNumber = int.TryParse(parameter, out classNumber);

                                if (isNumber)
                                {
                                    // Ensure value1 and value2 are in scope and valid
                                    if (value >= 6 && value2 <= 35)
                                    {
                                        reqString = $"Amazon";
                                    }
                                    else if (value >= 36 && value2 <= 65)
                                    {
                                        reqString = $"Sorceress";
                                    }
                                    else if (value >= 66 && value2 <= 95)
                                    {
                                        reqString = $"Necromancer";
                                    }
                                    else if (value >= 96 && value2 <= 125)
                                    {
                                        reqString = $"Paladin";
                                    }
                                    else if (value >= 126 && value2 <= 155)
                                    {
                                        reqString = $"Barbarian";
                                    }
                                    else if (value >= 221 && value2 <= 250)
                                    {
                                        reqString = $"Druid";
                                    }
                                    else if (value >= 251 && value2 <= 280)
                                    {
                                        reqString = $"Assassin";
                                    }
                                    else
                                    {
                                        throw ItemStatCostException.Create(
                                            $"Invalid skill ID range {value} - {value2} for Random Class Skill. Verify the range in Skills.txt "
                                        );
                                    }
                                    //with IDs {value} to {value2} <- Considered adding to string in the cases where skills don't span the full class IDs but did not to reduce confusion.
                                    valueString = $"+{parameter} to random {reqString} Skill";
                                }
                                else
                                {
                                    // CharClass is a named class (non-numeric)
                                    if (!CharStat.CharStats.ContainsKey(charClass))
                                    {
                                        throw ItemStatCostException.Create(
                                            $"Could not find character skill tab '{charClass}' property"
                                        );
                                    }

                                    reqString = $" ({CharStat.CharStats[charClass].Class} Only)";
                                    valueString = $"+{valueString} to {Skill.GetSkill(parameter).Name}{reqString}";
                                }
                            }
                            else
                            {
                                // No CharClass given — fallback to normal
                                valueString = $"+{valueString} to {Skill.GetSkill(parameter).Name}";
                            }

                            break;
                        case 28:
                            valueString = $"+{valueString} to {Skill.GetSkill(parameter).Name}";
                            break;
                        case 29: // Custom for sockets
                            if (string.IsNullOrEmpty(valueString))
                            {
                                valueString = parameter;
                                break;
                            }

                            valueString = $"{lstValue} ({valueString})";
                            break;
                        case 30: // Custom for elemental damage
                            valueString = lstValue.Replace("%d", valueString).Replace("%s", parameter);
                            break;
                        case 31: // Custom for normal damage spread
                            if (value!=value2)
                            { 
                                valueString = lstValue.Replace("%min", value.ToString()).Replace("%max", value2.ToString());
                                break;
                            } 
                            
                            valueString = lstValue.Replace("%min", value.ToString()).Replace("-%max", "");
                            break;
                        default:
                            // Not implemented function
                            valueString = UnimplementedFunction(value, value2, parameter, Op, OpParam, DescriptionFunction.Value);
                            DescriptionValue = 3;
                            break;
                    }
                }
            }

            // Check for and replace Skill Names for those reworked but not renamed in Skills.txt or are mispelled or just completely different
            if (!string.IsNullOrEmpty(valueString))
            {
                var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "stun", "Tectonic Slam" },
                    { "concentrate", "Carnage" },
                    { "poison explosion", "Poison Volley" },
                    { "hunger", "Toxic Fangs" },
                    { "quickness", "Burst of Speed" },
                    { "wearwolf", "Werewolf" },
                    { "wearbear", "Werebear" },
                    { "shape shifting", "Lycanthropy" },
                    { "dopplezon", "Decoy" },
                    { "bloodgolem", "Blood Golem" },
                    { "irongolem", "Iron Golem" },
                    { "firegolem", "Fire Golem" },
                    { "plague poppy", "Poison Creeper" },
                    { "cycle of life", "Carrion Vine" },
                    { "fenris", "Dire Wolf" },
                    { "vines", "Solar Creeper" },
                    { "fire trauma", "Fire Blast" },
                    { "royal strike", "Phoenix Strike" }
                };

                foreach (var pair in replacements)
                {
                    if (valueString.IndexOf(" skills", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        valueString = Regex.Replace(valueString, $@"\b{Regex.Escape(pair.Key)}\b", pair.Value, RegexOptions.IgnoreCase);
                    }
                }
            }
            
            // Remove +- in case it happens
            valueString = valueString.Replace("+-", "-");
            valueString = valueString.Replace("++", "+");
            valueString = valueString.Replace("--", "-");

            if (DescriptionValue.HasValue && !string.IsNullOrEmpty(lstValue))
            {
                switch (DescriptionValue.Value)
                {
                    case 0:
                        valueString = lstValue;
                        break;
                    case 1:
                        valueString = $"{valueString} {lstValue}";
                        break;
                    case 2:
                        valueString = $"{lstValue} {valueString}";
                        break;
                    case 3:
                        // Used if the value already contain all information
                        break;
                }
            }

            if (lstValue == "item_levelreq")
            {
                valueString = "+" + value + " To Required Level";
            }

            if (lstValue == "fade")
            {
                valueString = "Fade";
            }

            if (lstValue == "item_extrablood")
            {
                valueString = "Extra Blood";
            }
            
            //Assign value to verify removal after export via search
            if (lstValue == "item_nonclassskill")
            {
                valueString = "I_Shall_Not_Pass";  
            }

            // Trim whitespace and remove trailing newline as we sometimes see those in the properties
            valueString = valueString.Trim().Replace("\\n", "");
            return valueString;
        }

        private static double CalculatePerLevel(string parameter, int? op, int? op_param, string stat)
        {
            var para = Utility.ToNullableInt(parameter);
            if (!para.HasValue)
            {
                throw ItemStatCostException.Create($"Could not calculate per level, as parameter '{parameter}' is not a valid integer");
            }

            var val = para.Value / 8d;
            return val;
        }

        private static string GetValueString(double? value = null, double? value2 = null)
        {
            var valueString = "";

            if (value.HasValue)
            {
                valueString += value.Value.ToString(CultureInfo.InvariantCulture);

                if (value2.HasValue && value.Value != value2.Value)
                {
                    if (value2.Value >= 0)
                    {
                        valueString += $"-{value2.Value.ToString(CultureInfo.InvariantCulture)}";
                    }
                    else
                    {
                        valueString += $" to {value2.Value.ToString(CultureInfo.InvariantCulture)}";
                    }
                }
            }

            return valueString;
        }

        private static string UnimplementedFunction(int? value1, int? value2, string paramter, int? op, int? opParam, int function)
        {
            // Sad face :(
            return $"TODO: Unimplemented function: '{function}' value1: '{(value1.HasValue ? value1.Value.ToString() : "null")}' value2: '{(value2.HasValue ? value2.Value.ToString() : "null")}' parameter: '{paramter}' op: '{(op.HasValue ? op.Value.ToString() : "null")}' op_param: '{(opParam.HasValue ? opParam.Value.ToString() : "null")}'";
        }
    }
}