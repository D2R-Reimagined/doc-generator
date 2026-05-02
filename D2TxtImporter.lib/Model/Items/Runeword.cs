using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using D2TxtImporter.lib.Exceptions;
using D2TxtImporter.lib.Model.Dictionaries;
using D2TxtImporter.lib.Model.Types;

namespace D2TxtImporter.lib.Model.Items
{

    public class Runeword : Item
    {

        public List<Misc> Runes { get; set; }
        public List<ItemType> Types { get; set; }
        public string Vanilla { get; set; }
        public static List<Runeword> Import(string excelFolder) 
        {

            var result = new List<Runeword>();
            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/Runes.txt");

            // Load canonical rune name strings strictly from the required constants path
            var canonicalRuneNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var canonicalPath = ResolveConstantsFilePath("runes.txt");
            if (!File.Exists(canonicalPath))
                throw new FileNotFoundException($"Expected dependency file no located: \"{canonicalPath}\"");

            var cascRows = Importer.ReadTxtFileToDictionaryList(canonicalPath);
            foreach (var crow in cascRows)
            {
                string val = null;
                if (crow.ContainsKey("*Rune Names")) val = crow["*Rune Names"];
                else if (crow.ContainsKey("*Rune Name")) val = crow["*Rune Name"];
                else if (crow.ContainsKey("*RunesUsed")) val = crow["*RunesUsed"];

                var isComplete = crow.ContainsKey("complete") && string.Equals(crow["complete"], "1", StringComparison.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(val) && isComplete)
                {
                    canonicalRuneNames.Add(val.Trim());
                }
            }

            foreach (var row in table) 
            {
                if (string.IsNullOrEmpty(row["Rune1"])) 
                {
                    continue;
                }

                // Add the runes
                var runeArray = new[]
                    { row["Rune1"], row["Rune2"], row["Rune3"], row["Rune4"], row["Rune5"], row["Rune6"] };
                var runes = new List<Misc>();

                for (int i = 0; i < runeArray.Count(); i++) 
                {
                    if (!string.IsNullOrEmpty(runeArray[i]) && !runeArray[i].StartsWith("*")) 
                    {
                        runes.Add(Misc.MiscItems[runeArray[i]]);
                    }
                }

                // Add the types
                var typeArray = new[]
                    { row["itype1"], row["itype2"], row["itype3"], row["itype4"], row["itype5"], row["itype6"] };
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
                        if (type.Equiv1 == "shld" || type.Code == "shld") 
                        {
                            if (!shieldCounted) 
                            {
                                typeCount++;
                            }

                            shieldCounted = true;
                        }
                        
                        else if (type.BodyLoc1 == "rarm" || type.Code == "weap") 
                        {
                            if (!weaponCounted) 
                            {
                                typeCount++;
                            }

                            weaponCounted = true;
                        }

                        else 
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
                    Runes = runes,
                    Vanilla = "N"
                };

                // Determine Vanilla flag from the imported row's rune names token compared to canonical list
                string importedRuneNames = null;
                if (row.ContainsKey("*Rune Names")) importedRuneNames = row["*Rune Names"];
                else if (row.ContainsKey("*Rune Name")) importedRuneNames = row["*Rune Name"];
                else if (row.ContainsKey("*RunesUsed")) importedRuneNames = row["*RunesUsed"];

                if (!string.IsNullOrWhiteSpace(importedRuneNames) && canonicalRuneNames.Contains(importedRuneNames.Trim()))
                {
                    runeword.Vanilla = "Y";
                }

                var propList = new List<PropertyInfo>();

                // Add the properties
                for (int i = 1; i <= 7; i++) 
                {
                    propList.Add(new PropertyInfo(row[$"T1Code{i}"], row[$"T1Param{i}"], row[$"T1Min{i}"],
                        row[$"T1Max{i}"]));
                }

                try 
                {
                    var properties = ItemProperty.GetProperties(propList)
                        .OrderByDescending(x => x.ItemStatCost == null ? 0 : x.ItemStatCost.DescriptionPriority)
                        .ToList();

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
                        if (type.Equiv1 == "shld" || type.Code == "shld")
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
                        
                        else if (type.BodyLoc1 == "rarm" || type.Code == "weap"|| type.Code == "mele"|| type.Code == "miss" )
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

                        else 
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

                if (runeword.Properties.Count > 0) 
                {
                    ItemProperty.CleanupDublicates(runeword.Properties);
                    // Adjust required level
                    runeword.RequiredLevel = RequiredLevelReport.ComputeAdjustedRequiredLevel("Runeword", runeword.Name, runeword.RequiredLevel, runeword.Properties);

                    result.Add(runeword);
                }
            }

            return result.OrderBy(x => x.RequiredLevel).ToList();
        }

        private static string ResolveConstantsFilePath(string fileName)
        {
            // Probe likely bases and walk up to find project root, then utilities\constants
            var bases = new List<string>();
            try { bases.Add(AppDomain.CurrentDomain.BaseDirectory); } catch { }
            try { bases.Add(Directory.GetCurrentDirectory()); } catch { }
            try { bases.Add(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)); } catch { }

            foreach (var b in bases.Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var dir = new DirectoryInfo(b);
                for (int i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
                {
                    // Prefer detecting by solution file at project root
                    var sln = Path.Combine(dir.FullName, "D2TxtImporter.sln");
                    if (File.Exists(sln))
                    {
                        var expect = Path.Combine(dir.FullName, "utilities", "constants", fileName);
                        if (File.Exists(expect)) return expect;
                        return expect; // return expected path even if missing (for error message)
                    }

                    // Or directly if utilities\constants exists
                    var constantsDir = Path.Combine(dir.FullName, "utilities", "constants");
                    if (Directory.Exists(constantsDir))
                    {
                        var expect = Path.Combine(constantsDir, fileName);
                        if (File.Exists(expect)) return expect;
                        return expect;
                    }
                }
            }

            // Last resort for message consistency
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "utilities", "constants", fileName);
        }
    }
}
