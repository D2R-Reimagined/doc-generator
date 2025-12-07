using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace D2TxtImporter.lib.Diff
{
    public static class JsonModelLoader
    {
        // Lightweight DTOs that match the exported JSON shape (so we don't try to deserialize
        // complex runtime types like ItemProperty). We only keep the data needed for diffs.
        public sealed class UniqueSimple
        {
            public string Index { get; set; }
            public bool Enabled { get; set; }
            public int ItemLevel { get; set; }
            public int RequiredLevel { get; set; }
            public int Rarity { get; set; }
            public string Code { get; set; }
            // Explicit item type exported by the generator (e.g., Axe, Sword, Helmet, Ring, etc.)
            public string Type { get; set; }
            public string Vanilla { get; set; }
            public System.Collections.Generic.List<string> Properties { get; set; } = new System.Collections.Generic.List<string>();
        }

        public sealed class SetItemSimple
        {
            public string Index { get; set; }
            public string Code { get; set; }
            public bool Enabled { get; set; }
            public int ItemLevel { get; set; }
            public int RequiredLevel { get; set; }
            public int Rarity { get; set; }
            // Explicit item type exported by the generator
            public string Type { get; set; }
            public string Vanilla { get; set; }
            public System.Collections.Generic.List<string> Properties { get; set; } = new System.Collections.Generic.List<string>();
            public System.Collections.Generic.List<string> SetPropertiesString { get; set; } = new System.Collections.Generic.List<string>();
        }

        public sealed class SetSimple
        {
            public string Index { get; set; }
            public string Name { get; set; }
            public int Level { get; set; }
            public System.Collections.Generic.List<string> PartialProperties { get; set; } = new System.Collections.Generic.List<string>();
            public System.Collections.Generic.List<string> FullProperties { get; set; } = new System.Collections.Generic.List<string>();
            public System.Collections.Generic.List<SetItemSimple> SetItems { get; set; } = new System.Collections.Generic.List<SetItemSimple>();
        }

        public sealed class RunewordSimple
        {
            public string Index { get; set; }
            public bool Enabled { get; set; }
            public int ItemLevel { get; set; }
            public int RequiredLevel { get; set; }
            public string Vanilla { get; set; }
            public System.Collections.Generic.List<string> Properties { get; set; } = new System.Collections.Generic.List<string>();
        }

        public sealed class Models
        {
            public Dictionary<string, UniqueSimple> OldUniques { get; set; }
            public Dictionary<string, UniqueSimple> NewUniques { get; set; }
            public Dictionary<string, SetSimple> OldSets { get; set; }
            public Dictionary<string, SetSimple> NewSets { get; set; }
            public Dictionary<string, RunewordSimple> OldRunewords { get; set; }
            public Dictionary<string, RunewordSimple> NewRunewords { get; set; }

            // Maps for lookup: set item index -> parent set index
            public Dictionary<string, string> OldSetItemToSet { get; set; }
            public Dictionary<string, string> NewSetItemToSet { get; set; }

            // Bases
            public Dictionary<string, WeaponSimple> OldWeapons { get; set; }
            public Dictionary<string, WeaponSimple> NewWeapons { get; set; }
            public Dictionary<string, ArmorSimple> OldArmors { get; set; }
            public Dictionary<string, ArmorSimple> NewArmors { get; set; }

            // Cube recipes
            public Dictionary<string, CubeRecipeSimple> OldCubes { get; set; }
            public Dictionary<string, CubeRecipeSimple> NewCubes { get; set; }
        }

        public static Models Load(string oldJsonDir, string newJsonDir)
        {
            var models = new Models
            {
                OldUniques = LoadUniques(Path.Combine(oldJsonDir, "uniques.json")).ToDictionarySafe(u => u.Index),
                NewUniques = LoadUniques(Path.Combine(newJsonDir, "uniques.json")).ToDictionarySafe(u => u.Index),
                OldSets = LoadSets(Path.Combine(oldJsonDir, "sets.json")).ToDictionarySafe(s => s.Index),
                NewSets = LoadSets(Path.Combine(newJsonDir, "sets.json")).ToDictionarySafe(s => s.Index),
                OldRunewords = LoadRunewords(Path.Combine(oldJsonDir, "runewords.json")).ToDictionarySafe(r => r.Index),
                NewRunewords = LoadRunewords(Path.Combine(newJsonDir, "runewords.json")).ToDictionarySafe(r => r.Index),

                // Bases
                OldWeapons = LoadWeapons(Path.Combine(oldJsonDir, "weapons.json")).ToDictionarySafe(w => w.Code),
                NewWeapons = LoadWeapons(Path.Combine(newJsonDir, "weapons.json")).ToDictionarySafe(w => w.Code),
                OldArmors = LoadArmors(Path.Combine(oldJsonDir, "armors.json")).ToDictionarySafe(a => a.Code),
                NewArmors = LoadArmors(Path.Combine(newJsonDir, "armors.json")).ToDictionarySafe(a => a.Code),

                // Cube (only new filename)
                OldCubes = LoadCubes(Path.Combine(oldJsonDir, "cube_recipes_v2.json")).ToDictionarySafe(c => c.Key),
                NewCubes = LoadCubes(Path.Combine(newJsonDir, "cube_recipes_v2.json")).ToDictionarySafe(c => c.Key),
            };

            models.OldSetItemToSet = BuildSetItemMap(models.OldSets);
            models.NewSetItemToSet = BuildSetItemMap(models.NewSets);

            return models;
        }

        public sealed class WeaponSimple
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public string UberCode { get; set; }
            public string UltraCode { get; set; }
            public int? BaseRequiredLevel { get; set; }
            public int RequiredStrength { get; set; }
            public int RequiredDexterity { get; set; }
            public int Durability { get; set; }
            public int ItemLevel { get; set; }
            public string GemSockets { get; set; }
            public int Speed { get; set; }
            public System.Collections.Generic.List<DamageSimple> Damages { get; set; } = new System.Collections.Generic.List<DamageSimple>();
            public System.Collections.Generic.List<string> AutoMagicProps { get; set; } = new System.Collections.Generic.List<string>();
        }

        public sealed class DamageSimple
        {
            public string Kind { get; set; }
            public int Min { get; set; }
            public int Max { get; set; }
            public override string ToString()
            {
                if (Min <= 0 && Max <= 0) return string.Empty;
                return string.IsNullOrEmpty(Kind)
                    ? $"{Min} to {Max}"
                    : $"{Kind}: {Min} to {Max}";
            }
        }

        public sealed class ArmorSimple
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public string UberCode { get; set; }
            public string UltraCode { get; set; }
            public int? BaseRequiredLevel { get; set; }
            public int RequiredStrength { get; set; }
            public int RequiredDexterity { get; set; }
            public int Durability { get; set; }
            public int ItemLevel { get; set; }
            public string GemSockets { get; set; }
            public string ArmorString { get; set; }
            public int? Block { get; set; }
            public string DamageStringPrefix { get; set; }
            public string DamageString { get; set; }
            public System.Collections.Generic.List<string> AutoMagicProps { get; set; } = new System.Collections.Generic.List<string>();
        }

        public sealed class CubeRecipeSimple
        {
            public string Key { get; set; } // Description when available, else Index string
            public int Index { get; set; }
            public string Description { get; set; }
            public string Notes { get; set; }
            public System.Collections.Generic.List<CubeIngredientSimple> Inputs { get; set; } = new System.Collections.Generic.List<CubeIngredientSimple>();
            public System.Collections.Generic.List<CubeOutputSimple> Outputs { get; set; } = new System.Collections.Generic.List<CubeOutputSimple>();
            // Flattened list of property strings across all outputs (A/B/C)
            public System.Collections.Generic.List<string> Properties { get; set; } = new System.Collections.Generic.List<string>();
        }

        public sealed class CubeIngredientSimple
        {
            public string Name { get; set; }
            public int Quantity { get; set; }
            public System.Collections.Generic.List<string> Qualifiers { get; set; } = new System.Collections.Generic.List<string>();
            public override string ToString()
            {
                var q = (Qualifiers ?? new System.Collections.Generic.List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                var baseName = string.IsNullOrWhiteSpace(Name) ? "?" : Name;
                var qty = Quantity > 1 ? $"{Quantity}x " : string.Empty;
                var qual = q.Count > 0 ? $" ({string.Join(", ", q)})" : string.Empty;
                return qty + baseName + qual;
            }
        }

        public sealed class CubeOutputSimple
        {
            public string Name { get; set; }
            public int Quantity { get; set; }
            public override string ToString()
            {
                var baseName = string.IsNullOrWhiteSpace(Name) ? "?" : Name;
                var qty = Quantity > 1 ? $"{Quantity}x " : string.Empty;
                return qty + baseName;
            }
        }

        private static List<UniqueSimple> LoadUniques(string path)
        {
            var list = new List<UniqueSimple>();
            if (!File.Exists(path)) return list;
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return list;
            var arr = JArray.Parse(json);
            foreach (var t in arr.OfType<JToken>())
            {
                var u = new UniqueSimple
                {
                    Index = t.Value<string>("Index"),
                    Enabled = t.Value<bool?>("Enabled") ?? false,
                    ItemLevel = t.Value<int?>("ItemLevel") ?? 0,
                    RequiredLevel = t.Value<int?>("RequiredLevel") ?? 0,
                    Rarity = t.Value<int?>("Rarity") ?? 0,
                    Code = t.Value<string>("Code"),
                    Type = t.Value<string>("Type") ?? t.Value<string>("type"),
                    Vanilla = t.Value<string>("Vanilla"),
                    Properties = ExtractPropertyStrings(t["Properties"]) ?? new List<string>()
                };
                if (!string.IsNullOrWhiteSpace(u.Index)) list.Add(u);
            }
            return list;
        }

        private static List<SetSimple> LoadSets(string path)
        {
            var list = new List<SetSimple>();
            if (!File.Exists(path)) return list;
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return list;
            var arr = JArray.Parse(json);
            foreach (var t in arr.OfType<JToken>())
            {
                var s = new SetSimple
                {
                    Index = t.Value<string>("Index"),
                    Name = t.Value<string>("Name"),
                    Level = t.Value<int?>("Level") ?? 0,
                    PartialProperties = ExtractPropertyStrings(t["PartialProperties"]) ?? new List<string>(),
                    FullProperties = ExtractPropertyStrings(t["FullProperties"]) ?? new List<string>(),
                    SetItems = new List<SetItemSimple>()
                };

                var itemsToken = t["SetItems"] as JArray;
                if (itemsToken != null)
                {
                    foreach (var it in itemsToken)
                    {
                        var si = new SetItemSimple
                        {
                            Index = it.Value<string>("Index"),
                            Code = it.Value<string>("Code"),
                            Enabled = it.Value<bool?>("Enabled") ?? false,
                            ItemLevel = it.Value<int?>("ItemLevel") ?? 0,
                            RequiredLevel = it.Value<int?>("RequiredLevel") ?? 0,
                            Rarity = it.Value<int?>("Rarity") ?? 0,
                            Type = it.Value<string>("Type") ?? it.Value<string>("type"),
                            Vanilla = it.Value<string>("Vanilla"),
                            Properties = ExtractPropertyStrings(it["Properties"]) ?? new List<string>(),
                            SetPropertiesString = ExtractStringList(it["SetPropertiesString"]) ?? new List<string>()
                        };
                        if (!string.IsNullOrWhiteSpace(si.Index)) s.SetItems.Add(si);
                    }
                }

                if (!string.IsNullOrWhiteSpace(s.Index)) list.Add(s);
            }
            return list;
        }

        private static List<RunewordSimple> LoadRunewords(string path)
        {
            var list = new List<RunewordSimple>();
            if (!File.Exists(path)) return list;
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return list;
            var arr = JArray.Parse(json);
            foreach (var t in arr.OfType<JToken>())
            {
                var r = new RunewordSimple
                {
                    Index = t.Value<string>("Index") ?? t.Value<string>("Name"),
                    Enabled = t.Value<bool?>("Enabled") ?? false,
                    ItemLevel = t.Value<int?>("ItemLevel") ?? 0,
                    RequiredLevel = t.Value<int?>("RequiredLevel") ?? 0,
                    Vanilla = t.Value<string>("Vanilla"),
                    Properties = ExtractPropertyStrings(t["Properties"]) ?? new List<string>()
                };
                if (!string.IsNullOrWhiteSpace(r.Index)) list.Add(r);
            }
            return list;
        }

        private static List<WeaponSimple> LoadWeapons(string path)
        {
            var list = new List<WeaponSimple>();
            if (!File.Exists(path)) return list;
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return list;
            var arr = JArray.Parse(json);
            foreach (var t in arr.OfType<JToken>())
            {
                var w = new WeaponSimple
                {
                    Code = t.Value<string>("Code"),
                    Name = t.Value<string>("Name"),
                    UberCode = t.Value<string>("UberCode"),
                    UltraCode = t.Value<string>("UltraCode"),
                    BaseRequiredLevel = t.Value<int?>("BaseRequiredLevel"),
                    RequiredStrength = t.Value<int?>("RequiredStrength") ?? 0,
                    RequiredDexterity = t.Value<int?>("RequiredDexterity") ?? 0,
                    Durability = t.Value<int?>("Durability") ?? 0,
                    ItemLevel = t.Value<int?>("ItemLevel") ?? 0,
                    GemSockets = t.Value<string>("GemSockets"),
                    Speed = t.Value<int?>("Speed") ?? 0,
                    Damages = new System.Collections.Generic.List<DamageSimple>(),
                    AutoMagicProps = new System.Collections.Generic.List<string>()
                };
                // Damage types
                var dArr = t["DamageTypes"] as JArray;
                if (dArr != null)
                {
                    foreach (var d in dArr)
                    {
                        var kind = d.Value<string>("Type") ?? d.Value<string>("type") ?? string.Empty;
                        var min = d.Value<int?>("MinDamage") ?? d.Value<int?>("Min") ?? 0;
                        var max = d.Value<int?>("MaxDamage") ?? d.Value<int?>("Max") ?? 0;
                        w.Damages.Add(new DamageSimple { Kind = kind, Min = min, Max = max });
                    }
                }
                // AutoMagic groups
                var gArr = t["AutoMagicGroups"] as JArray;
                if (gArr != null)
                {
                    foreach (var g in gArr)
                    {
                        var plist = g["PropertyStrings"] as JArray;
                        if (plist != null)
                        {
                            foreach (var p in plist)
                            {
                                var s = p.Type == JTokenType.String ? (string)p : p.ToString(Formatting.None);
                                if (!string.IsNullOrWhiteSpace(s)) w.AutoMagicProps.Add(s);
                            }
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(w.Code)) list.Add(w);
            }
            return list;
        }

        private static List<ArmorSimple> LoadArmors(string path)
        {
            var list = new List<ArmorSimple>();
            if (!File.Exists(path)) return list;
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return list;
            var arr = JArray.Parse(json);
            foreach (var t in arr.OfType<JToken>())
            {
                var a = new ArmorSimple
                {
                    Code = t.Value<string>("Code"),
                    Name = t.Value<string>("Name"),
                    UberCode = t.Value<string>("UberCode"),
                    UltraCode = t.Value<string>("UltraCode"),
                    BaseRequiredLevel = t.Value<int?>("BaseRequiredLevel"),
                    RequiredStrength = t.Value<int?>("RequiredStrength") ?? 0,
                    RequiredDexterity = t.Value<int?>("RequiredDexterity") ?? 0,
                    Durability = t.Value<int?>("Durability") ?? 0,
                    ItemLevel = t.Value<int?>("ItemLevel") ?? 0,
                    GemSockets = t.Value<string>("GemSockets"),
                    ArmorString = t.Value<string>("ArmorString"),
                    Block = t.Value<int?>("Block"),
                    DamageStringPrefix = t.Value<string>("DamageStringPrefix"),
                    DamageString = t.Value<string>("DamageString"),
                    AutoMagicProps = new System.Collections.Generic.List<string>()
                };
                var gArr = t["AutoMagicGroups"] as JArray;
                if (gArr != null)
                {
                    foreach (var g in gArr)
                    {
                        var plist = g["PropertyStrings"] as JArray;
                        if (plist != null)
                        {
                            foreach (var p in plist)
                            {
                                var s = p.Type == JTokenType.String ? (string)p : p.ToString(Formatting.None);
                                if (!string.IsNullOrWhiteSpace(s)) a.AutoMagicProps.Add(s);
                            }
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(a.Code)) list.Add(a);
            }
            return list;
        }

        private static List<CubeRecipeSimple> LoadCubes(string path)
        {
            var list = new List<CubeRecipeSimple>();
            if (!File.Exists(path)) return list;
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return list;
            var arr = JArray.Parse(json);
            foreach (var t in arr.OfType<JToken>())
            {
                var idx = t.Value<int?>("Index") ?? -1;
                var desc = ReadStringProperty(t, "Description");
                // Ignore stack maintenance recipes to reduce noise
                if (ShouldIgnoreCubeDescription(desc))
                {
                    continue;
                }
                var c = new CubeRecipeSimple
                {
                    Index = idx,
                    Description = desc,
                    Notes = ReadStringProperty(t, "Notes"),
                    Key = !string.IsNullOrWhiteSpace(desc) ? desc : (idx >= 0 ? idx.ToString() : null),
                    Inputs = new System.Collections.Generic.List<CubeIngredientSimple>(),
                    Outputs = new System.Collections.Generic.List<CubeOutputSimple>()
                };

                // Inputs array
                var inArr = t["Inputs"] as JArray;
                if (inArr != null)
                {
                    foreach (var it in inArr)
                    {
                        var ing = new CubeIngredientSimple
                        {
                            Name = ReadStringProperty(it, "Name"),
                            Quantity = it.Value<int?>("Quantity") ?? 1,
                            Qualifiers = ExtractStringList(it["Qualifiers"]) ?? new System.Collections.Generic.List<string>()
                        };
                        c.Inputs.Add(ing);
                    }
                }

                // Outputs object may have A, B, C
                var outputs = t["Outputs"] as JObject;
                if (outputs != null)
                {
                    foreach (var label in new[] { "A", "B", "C" })
                    {
                        var o = outputs[label] as JObject;
                        if (o == null) continue;
                        var outS = new CubeOutputSimple
                        {
                            Name = ReadStringProperty(o, "Name"),
                            Quantity = o.Value<int?>("Quantity") ?? 1
                        };
                        c.Outputs.Add(outS);

                        // Collect property strings under this output (if any)
                        var props = o["Properties"] as JArray;
                        if (props != null)
                        {
                            foreach (var p in props)
                            {
                                var s = (p as JObject)?.Value<string>("PropertyString");
                                if (!string.IsNullOrWhiteSpace(s)) c.Properties.Add(s);
                            }
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(c.Key)) list.Add(c);
            }
            return list;
        }

        private static bool ShouldIgnoreCubeDescription(string desc)
        {
            if (string.IsNullOrWhiteSpace(desc)) return false;
            var s = desc.Trim().ToLowerInvariant();
            return s.Contains("orb stack") || s.Contains("rune stack") || s.Contains("key stack");
        }

        private static List<string> ExtractPropertyStrings(JToken token)
        {
            var list = new List<string>();
            if (token == null)
                return list;

            // Accept multiple shapes:
            // - Array of objects: [{ PropertyString: "..." }, ...]
            // - Array of strings: ["+10% IAS", "+20% ED", ...]
            // - Nested arrays: [["+10% IAS"], ["+20% ED"]]
            // - Single object or single string

            void addIfString(string s)
            {
                if (!string.IsNullOrWhiteSpace(s))
                    list.Add(s);
            }

            void handleToken(JToken t)
            {
                if (t == null) return;
                switch (t.Type)
                {
                    case JTokenType.Array:
                        foreach (var child in (JArray)t)
                        {
                            handleToken(child);
                        }
                        break;
                    case JTokenType.Object:
                        // Properties are usually objects with PropertyString
                        var so = t["PropertyString"]?.ToString();
                        if (string.IsNullOrWhiteSpace(so))
                            so = t["_propertyString"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(so))
                            addIfString(so);
                        else
                            addIfString(t.ToString(Formatting.None));
                        break;
                    case JTokenType.String:
                        addIfString((string)t);
                        break;
                    default:
                        // Fallback to compact string representation
                        addIfString(t.ToString(Formatting.None));
                        break;
                }
            }

            handleToken(token);

            // De-dup preserving case-insensitive uniqueness
            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<string> ExtractStringList(JToken token)
        {
            var list = new List<string>();
            if (token == null)
                return list;

            void handleToken(JToken t)
            {
                if (t == null) return;
                if (t.Type == JTokenType.Array)
                {
                    foreach (var child in (JArray)t)
                    {
                        handleToken(child);
                    }
                    return;
                }
                if (t.Type == JTokenType.String)
                {
                    var s = (string)t;
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                    return;
                }
                // Fallback: serialize compactly
                var text = t.ToString(Formatting.None);
                if (!string.IsNullOrWhiteSpace(text)) list.Add(text);
            }

            handleToken(token);
            return list;
        }

        private static string ReadStringProperty(JToken obj, string name)
        {
            if (obj == null || string.IsNullOrWhiteSpace(name)) return null;
            var token = obj[name];
            if (token == null) return null;

            switch (token.Type)
            {
                case JTokenType.String:
                    return (string)token;
                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.Boolean:
                    return token.ToString(Formatting.None);
                case JTokenType.Array:
                    {
                        var strs = new List<string>();
                        foreach (var ch in (JArray)token)
                        {
                            if (ch == null) continue;
                            if (ch.Type == JTokenType.String)
                                strs.Add((string)ch);
                            else
                                strs.Add(ch.ToString(Formatting.None));
                        }
                        return strs.Count > 0 ? string.Join(", ", strs) : token.ToString(Formatting.None);
                    }
                default:
                    return token.ToString(Formatting.None);
            }
        }

        private static Dictionary<string, string> BuildSetItemMap(Dictionary<string, SetSimple> sets)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in sets.Values)
            {
                if (s?.SetItems == null) continue;
                foreach (var si in s.SetItems)
                {
                    if (!string.IsNullOrWhiteSpace(si?.Index) && !map.ContainsKey(si.Index))
                    {
                        map.Add(si.Index, s.Index);
                    }
                }
            }
            return map;
        }
    }

    internal static class DictionaryExtensions
    {
        public static Dictionary<string, T> ToDictionarySafe<T>(this IEnumerable<T> src, Func<T, string> keySelector)
        {
            var dict = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            if (src == null) return dict;
            foreach (var x in src)
            {
                var key = keySelector(x);
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!dict.ContainsKey(key)) dict.Add(key, x);
            }
            return dict;
        }
    }
}
