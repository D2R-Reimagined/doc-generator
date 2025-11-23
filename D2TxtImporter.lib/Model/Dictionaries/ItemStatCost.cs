using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public int? SaveBits { get; set; }
        [JsonIgnore]
        public int? SaveAdd { get; set; }
        [JsonIgnore]
        public int? ValShift { get; set; }
        [JsonIgnore]
        public int? SaveParam { get; set; }
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
        public string DescStrPosKey { get; set; }
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
                    // Common header variants across D2 versions/mod tool exports
                    SaveBits = Utility.ToNullableInt(row["Save Bits"]),
                    SaveAdd = Utility.ToNullableInt(row["Save Add"]),
                    ValShift = Utility.ToNullableInt(row["ValShift"]) ?? 0,
                    SaveParam = Utility.ToNullableInt(row["Save Param Bits"]) ?? 0,
                    Op = Utility.ToNullableInt(row["op"]),
                    OpParam = Utility.ToNullableInt(row["op param"]),
                    DescriptionPriority = Utility.ToNullableInt(row["descpriority"]),
                    DescriptionFunction = Utility.ToNullableInt(row["descfunc"]),
                    DescriptionValue = Utility.ToNullableInt(row["descval"]),
                    DescStrPosKey = row["descstrpos"],
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
                DescriptionFunction = 19, // +val%
                DescriptonStringPositive = Table.GetValue("strModEnhancedDamage"),
                DescriptionStringNegative = Table.GetValue("strModEnhancedDamage"),
                DescriptionValue = 3 // Add value before
            };

            ItemStatCosts[enhancedDamage.Stat] = enhancedDamage;

            var ethereal = new ItemStatCost
            {
                Stat = "ethereal",
                DescriptionPriority = 1, // Min priority
                DescriptionFunction = 1, // lstValue
                DescriptonStringPositive = Table.GetValue("strethereal"),
                DescriptionStringNegative = Table.GetValue("strethereal"),
                DescriptionValue = 0 // Do not add value
            };

            ItemStatCosts[ethereal.Stat] = ethereal;

            var eledam = new ItemStatCost
            {
                Stat = "eledam",
                DescriptionPriority = ItemStatCosts["firemindam"].DescriptionPriority,
                DescriptionFunction = 30,
                DescriptonStringPositive = "Adds %d Weapon %s Damage",
                DescriptionValue = 3
            };

            ItemStatCosts["eledam"] = eledam;
            
            var dmgLtng = new ItemStatCost
            {
                Stat = "dmg-ltng",
                DescriptionPriority = ItemStatCosts["lightmindam"].DescriptionPriority,
                DescriptionFunction = 31,
                DescriptonStringPositive = Table.GetValue("strModLightningDamageRange"),
                DescriptionStringNegative = Table.GetValue("strModLightningDamageRange"),
                DescriptionValue = 3
            };

            ItemStatCosts["dmg-ltng"] = dmgLtng;
            
            var dmgCold = new ItemStatCost
            {
                Stat = "dmg-cold",
                DescriptionPriority = ItemStatCosts["coldmindam"].DescriptionPriority,
                DescriptionFunction = 31,
                DescriptonStringPositive = Table.GetValue("strModColdDamageRange"),
                DescriptionStringNegative = Table.GetValue("strModColdDamageRange"),
                DescriptionValue = 3
            };

            ItemStatCosts["dmg-cold"] = dmgCold;
            
            var dmgFire = new ItemStatCost
            {
                Stat = "dmg-fire",
                DescriptionPriority = ItemStatCosts["firemindam"].DescriptionPriority,
                DescriptionFunction = 31,
                DescriptonStringPositive = Table.GetValue("strModFireDamageRange"),
                DescriptionStringNegative = Table.GetValue("strModFireDamageRange"),
                DescriptionValue = 3
            };

            ItemStatCosts["dmg-fire"] = dmgFire;
            
            var poisDamage = new ItemStatCost
            {
                Stat = "dmg-pois",
                DescriptionPriority = 92,
                DescriptionFunction = 30,
                DescriptonStringPositive = Table.GetValue("strModPoisonDamageRange"),
                DescriptionStringNegative = Table.GetValue("strModPoisonDamageRange"),
                DescriptionValue = 3
            };

            ItemStatCosts["dmg-pois"] = poisDamage;
            
            var dmgMag = new ItemStatCost
            {
                Stat = "dmg-mag",
                DescriptionPriority = ItemStatCosts["magicmindam"].DescriptionPriority,
                DescriptionFunction = 31,
                DescriptonStringPositive = Table.GetValue("strModMagicDamageRange"),
                DescriptionStringNegative = Table.GetValue("strModMagicDamageRange"),
                DescriptionValue = 3
            };

            ItemStatCosts["dmg-mag"] = dmgMag;

            var dmgNorm = new ItemStatCost
            {
                Stat = "dmg-norm",
                DescriptionPriority = ItemStatCosts["mindamage"].DescriptionPriority,
                DescriptionFunction = 31,
                DescriptonStringPositive = Table.GetValue("strModMinDamageRange"),
                DescriptionStringNegative = Table.GetValue("strModMinDamageRange"),
                DescriptionValue = 3
            };

            ItemStatCosts["dmg-norm"] = dmgNorm;

            var resAll = new ItemStatCost
            {
                Stat = "res-all",
                DescriptionPriority = 34,
                DescriptionFunction = 19, // lstValue
                DescriptonStringPositive = Table.GetValue("strModAllResistances"),
                DescriptionStringNegative = Table.GetValue("strModAllResistances"),
                DescriptionValue = 3 // Do not add value
            };

            ItemStatCosts["res-all"] = resAll;

            var resAllMax = new ItemStatCost
            {
                Stat = "res-all-max",
                DescriptionPriority = 34,
                DescriptionFunction = 19, // lstValue
                DescriptonStringPositive = Table.GetValue("ModStrAllMaxRes"),
                DescriptionStringNegative = Table.GetValue("ModStrAllMaxResN"),
                DescriptionValue = 3 // Do not add value
            };

            ItemStatCosts["res-all-max"] = resAllMax;

            var allStats = new ItemStatCost
            {
                Stat = "all-stats",
                DescriptionPriority = 34,
                DescriptionFunction = 19, // lstValue
                DescriptonStringPositive = Table.GetValue("Moditem2allattrib"),
                DescriptionStringNegative = Table.GetValue("Moditem2allattrib"),
                DescriptionValue = 3 // Do not add value
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
            sockets.DescriptonStringPositive = Table.GetValue("Socketable");
            sockets.GroupDescriptionStringNegative = Table.GetValue("Socketable");
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
            else {
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
                            valueString = lstValue.Replace("%+d%", '+' + valueString);
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
                            }
                            else 
                            {
                                if (!CharStat.CharStats.ContainsKey(parameter)) 
                                {
                                    throw ItemStatCostException.Create($"Could not find character class '{parameter}'\nNote: if you have made a custom version of 'randclassskill' to support different amount of skills change them to 'randclassskill<d>' for example 'randclassskill5' is supported.");
                                }
                                classReplace = CharStat.CharStats[parameter].Class;
                            }

                            valueString = lstValue.Replace("%+d", valueString).Replace("%s", classReplace);
                            break;

                        case 14:
                            var par = Utility.ToNullableInt(parameter);
                            if (!par.HasValue) 
                            {
                                throw ItemStatCostException.Create(
                                    $"Could not convert parameter '{parameter}' to a valid integer");
                            }

                            if (!CharStat.SkillTabs.ContainsKey(par.Value)) 
                            {
                                throw ItemStatCostException.Create(
                                    $"Could not find skill tab with id {par.Value}");
                            }

                            var skillTab = CharStat.SkillTabs[par.Value];
                            var className = CharStat.CharStats.Values.First(x => x.StrSkillTab1 == skillTab ||x.StrSkillTab2 == skillTab || x.StrSkillTab3 == skillTab).Class;

                            if (!Table.Tables.ContainsKey(skillTab)) 
                            {
                                throw ItemStatCostException.Create(
                                    $"Could not find translation key '{skillTab}' in any .tbl or .json file");
                            }

                            lstValue = Table.Tables[skillTab];
                            valueString = $"+{lstValue.Replace("%+d", valueString)} ({className} Only)";
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

                                var opMath = OpParam != null ? Math.Pow(2, OpParam.Value) : 0;
                                double.TryParse(parameter, out var lvlGrowth);
                                string perLvlCalc;

                                // Try to use 'parameter' first
                                if (!string.IsNullOrEmpty(parameter)) 
                                {
                                    perLvlCalc = Math.Round(lvlGrowth / opMath, 2).ToString(CultureInfo.InvariantCulture);
                                }
                                // Fallback: check value1 and value2
                                else if (value == value2)
                                {
                                    perLvlCalc = Math.Round(value.Value / opMath, 2).ToString(CultureInfo.InvariantCulture);
                                }
                                else if (value != value2) 
                                {
                                    perLvlCalc = Math.Round(value.Value / opMath, 2).ToString(CultureInfo.InvariantCulture) + " - " + Math.Round(value2.Value / opMath, 2).ToString(CultureInfo.InvariantCulture);
                                }
                                else {
                                    throw new Exception(
                                        $"Invalid parameter and fallback values for level-based stat: parameter='{parameter}', value1='{value}', value2='{value2}'");
                                }
                                valueString = lstValue.Replace("%+d%", '+' + perLvlCalc).Replace("%+d", '+' + perLvlCalc).Replace("%d%", perLvlCalc).Replace("%d", perLvlCalc) + " (Per Character Level)";
                            }

                            else 
                            {
                                valueString = lstValue.Replace("%d%", valueString).Replace("%+d%", '+'+valueString).Replace("%d", valueString).Replace("%+d", '+'+valueString);
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

                            valueString = lstValue.Replace("%0%", valueString).Replace("%1", MonStat.MonStats[parameter].NameStr);
                            DescriptionValue = 3;
                            break;

                        case 24:
                            valueString = lstValue.Replace("%d/%d", $"{value}").Replace("%d", $"{value2}").Replace("%s", Skill.GetSkill(parameter).Name);
                            
                            break;

                        case 27:

                            var skillRandSkill = Skill.GetSkill(parameter);
                            var charClass = skillRandSkill.CharClass;
                            string reqString;

                            if (!string.IsNullOrEmpty(parameter)) 
                            {
                                if (int.TryParse(parameter, out _)) 
                                {
                                    var skillRandName = GetClassNameForRange(value, value2);
                                    valueString = $"+{parameter} Random {skillRandName} Skill";
                                }
                                else 
                                {
                                    // Named class / skill
                                    if (!CharStat.CharStats.TryGetValue(charClass, out var charInfo)) {
                                        throw ItemStatCostException.Create($"Could not find character skill tab '{charClass}' property");
                                    }

                                    reqString = $" ({charInfo.Class} Only)";
                                    valueString = $"+{valueString} to {skillRandSkill.Name}{reqString}";
                                }
                            }

                            else 
                            {
                                // No CharClass given — fallback to normal
                                valueString = $"+{valueString} to {skillRandSkill.Name}";
                            }
                            break;

                        case 28:
                            valueString = lstValue.Replace("%+d", '+' + valueString).Replace("%s", Skill.GetSkill(parameter).Name);
                            break;

                        case 29: // Custom for sockets
                            if (string.IsNullOrEmpty(valueString)) 
                            {
                                valueString = parameter;
                            }

                            valueString = lstValue.Replace("%i", valueString);
                            break;

                        case 30: // Custom for poison damage
                            int lenFrames = Utility.ToNullableInt(parameter) ?? 0;
                            int minPois = value.HasValue ? (int)Math.Ceiling(value.Value  * lenFrames / 256.0) : 0;
                            int maxPois = value2.HasValue ? (int)Math.Ceiling(value2.Value * lenFrames / 256.0) : 0;
                            double seconds = lenFrames / 25.0;

                           if (minPois == maxPois)
                            {
                                valueString = $"{minPois}";
                            }
                            else
                            {
                                valueString = $"{minPois}-{maxPois}";
                            }
                            
                            valueString = lstValue.Replace("%d-%d", valueString).Replace("%d", $"{seconds}");
                            break;                    
                        
                        case 31: // Custom for damage spread
                            valueString = lstValue.Replace("%d-%d", valueString);
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
                        break;
                }
            }

            switch (lstValue) 
            {
                case "item_levelreq":
                    valueString = "+" + value + " to Required Level";
                    break;

                case "fade":
                    valueString = "Fade";
                    break;

                case "item_extrablood":
                    valueString = "Extra Blood";
                    break;

                case "item_nonclassskill":
                    valueString = "PASS THROUGH, I DID, LUL";
                    break;
                
            }

            // Remove duplicate symbols in case it happens
            valueString = valueString.Replace("+-", "-");
            valueString = valueString.Replace("++", "+");
            valueString = valueString.Replace("--", "-");
            valueString = valueString.Replace("%%", "%");

            // Trim whitespace and remove trailing newline as we sometimes see those in the properties
            valueString = valueString.Trim().Replace("\\n", "");
            return valueString;
        }

        private static double CalculatePerLevel(string parameter, int? op, int? opParam, string stat) {
            var para = Utility.ToNullableInt(parameter);
            if (!para.HasValue) 
            {
                throw ItemStatCostException.Create($"Could not calculate per level, as parameter '{parameter}' is not a valid integer");
            }

            var val = para.Value / 8d;
            return val;
        }

        private static string GetValueString(double? value = null, double? value2 = null) {

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

        private static string GetClassNameForRange(int? value, int? value2) {
            if (value >= 6   && value2 <= 35)  return "Amazon";
            if (value >= 36  && value2 <= 65)  return "Sorceress";
            if (value >= 66  && value2 <= 95)  return "Necromancer";
            if (value >= 96  && value2 <= 125) return "Paladin";
            if (value >= 126 && value2 <= 155) return "Barbarian";
            if (value >= 221 && value2 <= 250) return "Druid";
            if (value >= 251 && value2 <= 280) return "Assassin";

            throw ItemStatCostException.Create(
                $"Invalid skill ID range {value} - {value2} for Random Class Skill. Verify the range in Skills.txt "
            );
        }

        private static string UnimplementedFunction(int? value1, int? value2, string paramter, int? op, int? opParam, int function)
        {
            // Sad face :(
            return $"TODO: Unimplemented function: '{function}' value1: '{(value1.HasValue ? value1.Value.ToString() : "null")}' value2: '{(value2.HasValue ? value2.Value.ToString() : "null")}' parameter: '{paramter}' op: '{(op.HasValue ? op.Value.ToString() : "null")}' op_param: '{(opParam.HasValue ? opParam.Value.ToString() : "null")}'";
        }
    }
}
