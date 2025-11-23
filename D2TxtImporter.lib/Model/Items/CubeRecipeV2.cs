using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using D2TxtImporter.lib.Exceptions;
using D2TxtImporter.lib.Model.Dictionaries;
using D2TxtImporter.lib.Model.Types;
using D2TxtImporter.lib.Model.Equipment;

namespace D2TxtImporter.lib.Model.Items
{
    public sealed class CubeRecipeV2
    {
        public int Index { get; set; } // 1-based, editor compatible
        public string Description { get; set; } // present only when UseDescription = true
        public int? Op { get; set; }
        public int? Param { get; set; }
        public int? Value { get; set; }
        public string Class { get; set; }
        public int NumInputs { get; set; }
        public int ResolvedInputsCount { get; set; }
        public List<CubeIngredientV2> Inputs { get; set; }
        public CubeOutputsV2 Outputs { get; set; }
        public List<string> Notes { get; set; }

        // While debugging/QA it is useful to disable the early-stop sentinel so all rows are processed.
        // Default: disabled (false). Set to true only if you explicitly want to stop at the sentinel row.
        public static bool EnableEarlyStopSentinel { get; set; } = false;

        // Lookups for validating named unique/set keys
        private static List<Unique> _uniques;
        private static List<SetItem> _setItems;

        public static List<CubeRecipeV2> Import(string excelFolder, List<Unique> uniques, List<SetItem> setItems)
        {
            var result = new List<CubeRecipeV2>();
            var lines = Importer.ReadTxtFileToDictionaryList(excelFolder + "/CubeMain.txt");
            if (lines == null) return result;

            _uniques = uniques ?? new List<Unique>();
            _setItems = setItems ?? new List<SetItem>();

            var qmaps = CubeQualifiers.Load();

            int rowIndex = 0; // will increment per row processed (excluding header already)
            foreach (var row in lines)
            {
                rowIndex++;

                // Filters
                if (!IsEnabled(row)) continue;

                var input1 = Get(row, "input 1");
                var outA = Get(row, "output");
                // Early-stop sentinel is disabled by default for QA to allow full-file validation.
                if (EnableEarlyStopSentinel && IsEarlyStopSentinel(input1, outA))
                {
                    break; // finish import and export whatever we have
                }

                // Additional skip rules
                var op = ToInt(Get(row, "op"));
                var par = ToInt(Get(row, "op param"));
                var val = ToInt(Get(row, "op value")) ?? ToInt(Get(row, "value"));
                if ((op == 18 && par == 361 && val == 0) || (op == 15 && par == 362 && val == 101))
                {
                    continue;
                }

                var recipe = new CubeRecipeV2
                {
                    Index = rowIndex,
                    Description = CubeRecipe.UseDescription ? Get(row, "description") : null,
                    Op = op,
                    Param = par,
                    Value = val,
                    Class = ResolveClass(Get(row, "class")),
                    Notes = new List<string>()
                };

                // Notes mapping (can be extended)
                if (op == 18 && par == 361) recipe.Notes.Add("Corruption Outcome");
                if (op == 15 && par == 362) recipe.Notes.Add("Item Enchantment");

                // Inputs
                var numInputs = ToInt(Get(row, "numinputs")) ?? 0;
                var inputs = new List<CubeIngredientV2>();
                int qtySum = 0;
                for (int i = 1; i <= 7; i++)
                {
                    var token = Get(row, $"input {i}");
                    if (string.IsNullOrWhiteSpace(token)) continue;
                    var ing = ParseIngredient(token, qmaps, isInput:true);
                    // Troubleshooting strictness: validate qualifiers and token
                    ValidateMainToken(ing.MainToken, context: "input", rowIndex: rowIndex, rawToken: ing.Raw);
                    ValidateQualifiers(ing.QualifiersRaw, qmaps.InputTokens, "input", rowIndex, ing.Raw);

                    inputs.Add(new CubeIngredientV2
                    {
                        Name = ing.DisplayName,
                        Quantity = ing.Quantity,
                        Qualifiers = ing.QualifiersFriendly.ToList(),
                        RawToken = ing.Raw,
                    });
                    qtySum += Math.Max(1, ing.Quantity);
                }

                recipe.Inputs = inputs;
                recipe.NumInputs = numInputs;
                recipe.ResolvedInputsCount = qtySum;

                if (qtySum < numInputs)
                {
                    throw new Exception($"Cube row {rowIndex}: sum of input quantities ({qtySum}) is less than numinputs ({numInputs})");
                }

                // Outputs A/B/C
                var outputA = ParseOutput(row, qmaps, inputs, "output", new[] { "prob", "prob a", "chance", "chance a" }, rowIndex);
                var outputB = ParseOutput(row, qmaps, inputs, "output b", new[] { "prob b", "chance b" }, rowIndex);
                var outputC = ParseOutput(row, qmaps, inputs, "output c", new[] { "prob c", "chance c" }, rowIndex);

                // properties/mods: attach to the first non-empty output
                var targetOut = outputA ?? outputB ?? outputC;

                // Ensure error messages reference the Cube row rather than a previously processed item
                var prevItemCtx = Item.CurrentItem;
                Item.CurrentItem = new Item { Index = $"Cube row {rowIndex}" };
                try
                {
                    var props = ParseProperties(row);
                    if (props != null && props.Count > 0 && targetOut != null)
                    {
                        if (targetOut.Properties == null) targetOut.Properties = new List<CubePropertyV2>();
                        targetOut.Properties.AddRange(props);
                    }
                }
                catch (Exception ex)
                {
                    // Build a compact mods summary to aid debugging
                    string ModsSummary()
                    {
                        try
                        {
                            var list = new List<string>();
                            for (int i = 1; i <= 5; i++)
                            {
                                var code = Get(row, $"mod {i}");
                                if (string.IsNullOrWhiteSpace(code)) continue;
                                var paramStr = Get(row, $"mod {i} param");
                                var min = Get(row, $"mod {i} min");
                                var max = Get(row, $"mod {i} max");
                                var chance  = Get(row, $"mod {i} chance");
                                list.Add($"[{i}] code='{code}', param='{paramStr}', min='{min}', max='{max}', chance='{chance}'");
                            }
                            return string.Join("; ", list);
                        }
                        catch { return "<unavailable>"; }
                    }

                    var wrapped = new Exception($"Cube row {rowIndex}: failed to parse cube properties. Mods: {ModsSummary()}", ex);
                    Exceptions.ExceptionHandler.WriteException(wrapped);
                    if (!Exceptions.ExceptionHandler.ContinueOnException) throw;
                }
                finally
                {
                    // Restore previous context
                    Item.CurrentItem = prevItemCtx;
                }

                recipe.Outputs = new CubeOutputsV2 { A = outputA, B = outputB, C = outputC };

                result.Add(recipe);
            }

            return result;
        }

        private static bool IsEnabled(Dictionary<string, string> row)
        {
            var en = Get(row, "enabled");
            return string.Equals(en, "1", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEarlyStopSentinel(string input1, string output)
        {
            if (string.IsNullOrWhiteSpace(input1) || string.IsNullOrWhiteSpace(output)) return false;
            var sent = new HashSet<string>(new[] { "agr", "tgr", "sgr", "egr", "mgr", "rgr", "kgr" }, StringComparer.OrdinalIgnoreCase);
            return sent.Contains(NormalizeToken(input1)) && sent.Contains(NormalizeToken(output));
        }

        private static string NormalizeToken(string raw)
        {
            var r = raw?.Trim().Trim('"');
            if (string.IsNullOrEmpty(r)) return r;
            var first = r.Split(',')[0].Trim();
            return first;
        }

        private static string ResolveClass(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var key = raw.Trim().ToLowerInvariant();
            if (D2TxtImporter.lib.Model.Dictionaries.CharStat.CharStats != null && D2TxtImporter.lib.Model.Dictionaries.CharStat.CharStats.TryGetValue(key, out var cs))
            {
                return cs.Class;
            }
            return raw;
        }

        private sealed class TokenParse
        {
            public string Raw { get; set; }
            public string MainToken { get; set; }
            public int Quantity { get; set; }
            public IEnumerable<string> QualifiersRaw { get; set; }
            public IEnumerable<string> QualifiersFriendly { get; set; }
            public string DisplayName { get; set; }
        }

        private static TokenParse ParseIngredient(string token, CubeQualifiers.Maps qmaps, bool isInput)
        {
            var raw = (token ?? string.Empty).Trim();
            raw = raw.Trim('"');
            var parts = raw.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            if (parts.Count == 0)
            {
                throw new Exception($"Invalid input token: '{token}'");
            }

            var main = parts[0];
            int qty = 1;
            var quals = new List<string>();
            for (int i = 1; i < parts.Count; i++)
            {
                var p = parts[i];
                if (p.StartsWith("qty=", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(p.Substring(4), NumberStyles.Integer, CultureInfo.InvariantCulture, out qty))
                    {
                        throw new Exception($"Invalid qty in token '{token}'");
                    }
                }
                else
                {
                    quals.Add(p);
                }
            }

            // Inputs may use 'usetype'/'useitem' and must resolve to a concrete token for debugging/validation.
            if (isInput && (string.Equals(main, "usetype", StringComparison.OrdinalIgnoreCase) || string.Equals(main, "useitem", StringComparison.OrdinalIgnoreCase)))
            {
                // Try to find a qualifier that is a resolvable main token (3/4/>4 rules)
                string resolved = null;
                foreach (var q in quals)
                {
                    // Special-case: some item type codes can be 3 letters (e.g., "any")
                    if (string.Equals(q, "any", StringComparison.OrdinalIgnoreCase)) { resolved = q; break; }
                    if (ItemType.ItemTypes.ContainsKey(q)) { resolved = q; break; }
                    if (q.Length == 3 && Misc.MiscItems.ContainsKey(q)) { resolved = q; break; }
                    if (q.Length > 4 && (IsUniqueKey(q) || IsSetItemKey(q))) { resolved = q; break; }
                }
                if (resolved == null)
                {
                    throw new Exception($"Input token '{token}': could not resolve '{main}' to a concrete item/type");
                }
                // Remove the resolved specifier from qualifiers and substitute as main
                quals.Remove(resolved);
                main = resolved;
            }

            // Resolve display name based on main token length/type
            // Special-case for outputs using keywords 'usetype'/'useitem' and known portal tokens:
            // do NOT attempt to resolve them here (would throw), let ParseOutput
            // handle their semantics using the first input; for portal tokens, the
            // display name is the token itself.
            string display;
            if (!isInput && (string.Equals(main, "usetype", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(main, "useitem", StringComparison.OrdinalIgnoreCase)))
            {
                display = main; // placeholder; final name will be set in ParseOutput
            }
            else if (!isInput && IsSpecialOutputMain(main))
            {
                // Portal/special outputs are not items; use the token as-is for display
                display = main;
            }
            else
            {
                display = ResolveDisplayFromMain(main);
            }

            // Friendly qualifiers (fallback to title-cased when not specified)
            var friendly = quals.Select(q =>
            {
                if (qmaps.InputDisplay != null && qmaps.InputDisplay.TryGetValue(q, out var f)) return f;
                if (qmaps.OutputDisplay != null && qmaps.OutputDisplay.TryGetValue(q, out var fo)) return fo; // sometimes shared
                return FallbackFriendly(q);
            });

            return new TokenParse
            {
                Raw = token,
                MainToken = main,
                Quantity = qty,
                QualifiersRaw = quals,
                QualifiersFriendly = friendly,
                DisplayName = display
            };
        }

        private static string FallbackFriendly(string q)
        {
            if (string.Equals(q, "mod", StringComparison.OrdinalIgnoreCase)) return "Keep Modifiers";
            try
            {
                return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(q.Replace('_', ' '));
            }
            catch { return q; }
        }

        private static void ValidateQualifiers(IEnumerable<string> tokens, ISet<string> allowed, string section, int rowIndex, string rawToken)
        {
            if (tokens == null) return;
            foreach (var t in tokens)
            {
                if (t.StartsWith("qty=", StringComparison.OrdinalIgnoreCase)) continue;
                // allow special 'mod' keyword even if not in the map
                if (string.Equals(t, "mod", StringComparison.OrdinalIgnoreCase)) continue;
                // Accept parameterized tokens when a corresponding pattern token "prefix=#" exists in the allowed set
                if (allowed != null && t.IndexOf('=') > 0)
                {
                    var eq = t.IndexOf('=');
                    var head = t.Substring(0, eq + 1); // include '='
                    var tail = t.Substring(eq + 1);
                    var pattern = head + "#"; // e.g., "sock=" + "#" => "sock=#"
                    int num;
                    if (allowed.Contains(pattern) && int.TryParse(tail, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out num))
                    {
                        continue; // parameterized qualifier accepted
                    }
                }
                if (allowed == null || !allowed.Contains(t))
                {
                    throw new Exception($"Unknown {section} qualifier token '{t}' at row {rowIndex} in token '{rawToken}'");
                }
            }
        }

        private static void ValidateMainToken(string main, string context, int rowIndex, string rawToken)
        {
            if (string.IsNullOrWhiteSpace(main)) throw new Exception($"Empty {context} token");
            // If main is usetype/useitem in inputs, we cannot resolve without additional info → throw
            if (string.Equals(main, "usetype", StringComparison.OrdinalIgnoreCase) || string.Equals(main, "useitem", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"{context} token '{main}' is not resolvable without a concrete reference at row {rowIndex} in token '{rawToken}'");
            }

            // Allow known special output mains (e.g., portal creators). They are valid only for outputs.
            if (string.Equals(context, "output", StringComparison.OrdinalIgnoreCase) && IsSpecialOutputMain(main))
            {
                return;
            }

            // Accept if any of these resolve:
            // Accept any known ItemType code first (handles special 3-letter codes like 'any')
            if (string.Equals(main, "any", StringComparison.OrdinalIgnoreCase)) return;
            if (ItemType.ItemTypes.ContainsKey(main)) return;
            if (main.Length == 3)
            {
                if (Misc.MiscItems.ContainsKey(main)) return;
                if (Armor.Armors.ContainsKey(main)) return;
                if (Weapon.Weapons.ContainsKey(main)) return;
            }
            if (main.Length > 4 && (IsUniqueKey(main) || IsSetItemKey(main))) return;

            throw new Exception($"Unknown {context} main token '{main}' at row {rowIndex} in token '{rawToken}'");
        }

        // Known special output main tokens that are not items but directives to create portals/etc.
        private static readonly System.Collections.Generic.HashSet<string> _specialOutputMains =
            new System.Collections.Generic.HashSet<string>(new[]
            {
                "Cow Portal",
                "Pandemonium Portal",
                "Pandemonium Finale Portal",
                "Red Portal"
            }, StringComparer.OrdinalIgnoreCase);

        private static bool IsSpecialOutputMain(string main)
        {
            return _specialOutputMains.Contains(main ?? string.Empty);
        }

        private static bool IsUniqueKey(string key)
        {
            // Unique items are imported in ImportModel(); Unique.Uniques is a List<Unique>
            return _uniques != null && _uniques.Any(u => string.Equals(u.Index, key, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsSetItemKey(string key)
        {
            return _setItems != null && _setItems.Any(s => string.Equals(s.Index, key, StringComparison.OrdinalIgnoreCase));
        }

        private static CubeOutputV2 ParseOutput(Dictionary<string, string> row, CubeQualifiers.Maps qmaps, List<CubeIngredientV2> inputs, string outputKey, string[] chanceKeys, int rowIndex)
        {
            var raw = Get(row, outputKey);
            if (string.IsNullOrWhiteSpace(raw)) return null;

            // chance
            int? chance = null;
            foreach (var ck in chanceKeys)
            {
                var v = ToInt(Get(row, ck));
                if (v.HasValue) { chance = v; break; }
            }

            var parsed = ParseIngredient(raw, qmaps, isInput:false);

            // Special output keywords
            if (string.Equals(parsed.MainToken, "usetype", StringComparison.OrdinalIgnoreCase))
            {
                // Always resolve to the TYPE of input1
                var first = inputs?.FirstOrDefault();
                if (first == null)
                {
                    throw new Exception($"Cube row {rowIndex}: 'usetype' output cannot be resolved because there is no input1. Raw output token: '{raw}'");
                }

                var typeName = ResolveTypeFromInput(first);
                ValidateQualifiers(parsed.QualifiersRaw, qmaps.OutputTokens, "output", rowIndex, raw);

                return new CubeOutputV2
                {
                    Name = typeName,
                    Quantity = parsed.Quantity,
                    Qualifiers = parsed.QualifiersFriendly.ToList(),
                    OutputChance = chance,
                    Properties = new List<CubePropertyV2>()
                };
            }
            else if (string.Equals(parsed.MainToken, "useitem", StringComparison.OrdinalIgnoreCase))
            {
                // Always resolve to the exact ITEM from input1
                var first = inputs?.FirstOrDefault();
                if (first == null)
                {
                    throw new Exception($"Cube row {rowIndex}: 'useitem' output cannot be resolved because there is no input1. Raw output token: '{raw}'");
                }
                var itemName = first.Name; // resolved display of first input item
                ValidateQualifiers(parsed.QualifiersRaw, qmaps.OutputTokens, "output", rowIndex, raw);
                return new CubeOutputV2
                {
                    Name = itemName,
                    Quantity = parsed.Quantity,
                    Qualifiers = parsed.QualifiersFriendly.ToList(),
                    OutputChance = chance,
                    Properties = new List<CubePropertyV2>()
                };
            }
            else
            {
                // Normal output: validate token and qualifiers
                ValidateMainToken(parsed.MainToken, context: "output", rowIndex: rowIndex, rawToken: raw);
                ValidateQualifiers(parsed.QualifiersRaw, qmaps.OutputTokens, "output", rowIndex, raw);
                return new CubeOutputV2
                {
                    Name = parsed.DisplayName,
                    Quantity = parsed.Quantity,
                    Qualifiers = parsed.QualifiersFriendly.ToList(),
                    OutputChance = chance,
                    Properties = new List<CubePropertyV2>()
                };
            }
        }

        private static string ResolveTypeFromInput(CubeIngredientV2 first)
        {
            // If first input was an item type already, keep it; otherwise resolve its underlying type
            // We cannot reverse-resolve display name reliably; rely on known types in ItemType to match
            // Best effort: if the raw token looked like a 4-letter code, that was a type and Name already holds display type
            if (!string.IsNullOrEmpty(first.RawToken))
            {
                var main = NormalizeToken(first.RawToken);
                if (main != null && string.Equals(main, "any", StringComparison.OrdinalIgnoreCase))
                {
                    return "Any Item";
                }
                if (main != null && ItemType.ItemTypes.ContainsKey(main))
                {
                    return ItemType.ItemTypes[main].Name;
                }
                // If it was a misc code or named item, attempt to find its Equipment.Type
                // Out of scope to map every misc to type reliably; fallback to first.Name
            }
            return first.Name;
        }

        private static List<CubePropertyV2> ParseProperties(Dictionary<string, string> row)
        {
            var list = new List<CubePropertyV2>();
            var propInfos = new List<PropertyInfo>();
            var chances = new List<int?>();
            for (int i = 1; i <= 5; i++)
            {
                var code = Get(row, $"mod {i}");
                if (string.IsNullOrWhiteSpace(code)) continue;
                var par = Get(row, $"mod {i} param");
                var min = Get(row, $"mod {i} min");
                var max = Get(row, $"mod {i} max");
                propInfos.Add(new PropertyInfo(code, par, min, max));
                chances.Add(ToInt(Get(row, $"mod {i} chance")));
            }
            if (propInfos.Count == 0) return list;

            var props = ItemProperty.GetProperties(propInfos, 0); // validate & format; will throw on invalid
            int idx = 0;
            foreach (var p in props)
            {
                list.Add(new CubePropertyV2
                {
                    PropertyString = p.PropertyString,
                    ModChance = chances.ElementAtOrDefault(idx),
                    Index = idx
                });
                idx++;
            }

            return list;
        }

        private static int? ToInt(string s)
        {
            return Model.Utility.ToNullableInt(s);
        }

        private static string Get(Dictionary<string, string> row, string key)
        {
            if (row == null || key == null) return null;
            // Case-insensitive lookup: try exact first, then case-insensitive
            if (row.ContainsKey(key)) return row[key];
            var kv = row.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(kv.Key)) return kv.Value;
            return null;
        }
        private static string ResolveDisplayFromMain(string main)
        {
            if (string.IsNullOrWhiteSpace(main)) return string.Empty;
            // Defensive: special output keywords should never be resolved as item keys
            if (string.Equals(main, "usetype", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(main, "useitem", StringComparison.OrdinalIgnoreCase))
            {
                return main; // let caller (ParseOutput) determine final display
            }
            // Prefer ItemType if code is known there (handles special 3-letter codes like 'any')
            if (string.Equals(main, "any", StringComparison.OrdinalIgnoreCase))
            {
                return "Any Item";
            }
            if (ItemType.ItemTypes.ContainsKey(main))
            {
                return ItemType.ItemTypes[main].Name; // display type (e.g., Any Armor)
            }
            if (main.Length == 3)
            {
                // Try resolution order: Misc → Armor → Weapon
                if (Misc.MiscItems.ContainsKey(main))
                {
                    return Misc.MiscItems[main].Name;
                }
                if (Armor.Armors.ContainsKey(main))
                {
                    return Armor.Armors[main].Name;
                }
                if (Weapon.Weapons.ContainsKey(main))
                {
                    return Weapon.Weapons[main].Name;
                }
                throw new Exception($"Unknown 3-letter item code '{main}' (not found in Misc, Armor, or Weapons)");
            }
            // assume unique/set item key
            var u = _uniques?.FirstOrDefault(x => string.Equals(x.Index, main, StringComparison.OrdinalIgnoreCase));
            if (u != null) return u.Name;
            var s = _setItems?.FirstOrDefault(x => string.Equals(x.Index, main, StringComparison.OrdinalIgnoreCase));
            if (s != null) return s.Name;
            throw new Exception($"Unknown set/unique key '{main}'");
        }
    }

    public sealed class CubeIngredientV2
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
        public List<string> Qualifiers { get; set; }
        public string RawToken { get; set; }
    }

    public sealed class CubeOutputsV2
    {
        public CubeOutputV2 A { get; set; }
        public CubeOutputV2 B { get; set; }
        public CubeOutputV2 C { get; set; }
    }

    public sealed class CubeOutputV2
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
        public List<string> Qualifiers { get; set; }
        public int? OutputChance { get; set; }
        public List<CubePropertyV2> Properties { get; set; }
    }

    public sealed class CubePropertyV2
    {
        public string PropertyString { get; set; }
        public int? ModChance { get; set; }
        public int Index { get; set; }
    }
}
