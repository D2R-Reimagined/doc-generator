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
        public int Index { get; set; } // Exported row index (editor uses -1 offset)
        public string Description { get; set; } // Only when UseDescription = true
        public int? Op { get; set; }
        public int? Param { get; set; }
        public int? Value { get; set; }
        public string Class { get; set; }
        public int NumInputs { get; set; }
        public int ResolvedInputsCount { get; set; }
        public List<CubeIngredientV2> Inputs { get; set; }
        public CubeOutputsV2 Outputs { get; set; }
        public List<string> Notes { get; set; }

        // Early-stop sentinel is off by default; enable only to stop at sentinel row.
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

            int rowIndex = 0;
            foreach (var row in lines)
            {
                rowIndex++;

                // Filters
                if (!IsEnabled(row)) continue;

                var input1 = Get(row, "input 1");
                var outA = Get(row, "output");
                if (EnableEarlyStopSentinel && IsEarlyStopSentinel(input1, outA))
                {
                    break; // finish import and export whatever we have
                }

                // Skip rows containing blocked input tokens
                var blocked = new HashSet<string>(new[] { "msf", "vip", "qf1", "qhr", "qey", "qbr" }, StringComparer.OrdinalIgnoreCase);
                bool hasBlocked = false;
                for (int bi = 1; bi <= 7 && !hasBlocked; bi++)
                {
                    var tok = Get(row, $"input {bi}");
                    if (string.IsNullOrWhiteSpace(tok)) continue;
                    var parts = tok.Trim().Trim('"').Split(',').Select(p => p.Trim()).Where(p => p.Length > 0);
                    foreach (var p in parts)
                    {
                        if (p.StartsWith("qty=", StringComparison.OrdinalIgnoreCase)) continue;
                        if (blocked.Contains(p))
                        {
                            hasBlocked = true;
                            break;
                        }
                    }
                }
                if (hasBlocked)
                {
                    continue; // skip this row entirely
                }

                // Additional skip rules
                var op = ToInt(Get(row, "op"));
                // Some sheets may use either 'op param' or plain 'param' — support both
                var par = ToInt(Get(row, "op param")) ?? ToInt(Get(row, "param"));
                var val = ToInt(Get(row, "op value")) ?? ToInt(Get(row, "value"));
                // Skip unwanted op/param/value combos:
                // - exactly 18/361/0
                // - 15/362 with value 101 or higher (101+)
                if ((op == 18 && par == 361 && val == 0) || (op == 15 && par == 362 && (val ?? 0) >= 101))
                {
                    continue;
                }

                var recipe = new CubeRecipeV2
                {
                    // Apply -1 offset to match editor expectations
                    Index = rowIndex - 1,
                    Description = CubeRecipe.UseDescription ? Get(row, "description") : null,
                    Op = op,
                    Param = par,
                    Value = val,
                    Class = ResolveClass(Get(row, "class")),
                    Notes = new List<string>()
                };

                // Notes mapping (can be extended)
                //if (op == 15 && par == 362) recipe.Notes.Add("Corruption Outcome Recipe");

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

                // Ensure error messages reference the Cube row rather than a previously processed item
                var prevItemCtx = Item.CurrentItem;
                Item.CurrentItem = new Item { Index = $"Cube row {rowIndex}" };
                try
                {
                    // Attach properties per output (A/B/C) using their own mod columns
                    AttachProps("A", outputA, () => ParseProperties(row, ""));
                    AttachProps("B", outputB, () => ParseProperties(row, "b "));
                    AttachProps("C", outputC, () => ParseProperties(row, "c "));
                }
                catch (Exception ex)
                {
                    // Generic failure wrapper (should be rare since each variant already logs its own)
                    var wrapped = new Exception($"Cube row {rowIndex}: failed to parse cube properties (unexpected aggregate failure)", ex);
                    Exceptions.ExceptionHandler.WriteException(wrapped);
                    if (!Exceptions.ExceptionHandler.ContinueOnException) throw;
                }
                finally
                {
                    // Restore previous context
                    Item.CurrentItem = prevItemCtx;
                }

                // Local helpers to attach props per output and summarize mods for diagnostics
                void AttachProps(string label, CubeOutputV2 outRef, System.Func<List<CubePropertyV2>> parse)
                {
                    try
                    {
                        if (outRef == null) return;
                        var props = parse();
                        if (props != null && props.Count > 0)
                        {
                            if (outRef.Properties == null) outRef.Properties = new List<CubePropertyV2>();
                            outRef.Properties.AddRange(props);
                        }
                    }
                    catch (Exception e)
                    {
                        string ModsSummaryVariant(string pfx)
                        {
                            try
                            {
                                var list = new List<string>();
                                for (int i = 1; i <= 5; i++)
                                {
                                    var code = Get(row, $"{pfx}mod {i}");
                                    if (string.IsNullOrWhiteSpace(code)) continue;
                                    var paramStr = Get(row, $"{pfx}mod {i} param");
                                    var min = Get(row, $"{pfx}mod {i} min");
                                    var max = Get(row, $"{pfx}mod {i} max");
                                    var chance = Get(row, $"{pfx}mod {i} chance");
                                    list.Add($"[{i}] code='{code}', param='{paramStr}', min='{min}', max='{max}', chance='{chance}'");
                                }
                                return string.Join("; ", list);
                            }
                            catch { return "<unavailable>"; }
                        }

                        var pfxArg = label == "A" ? "" : (label.ToLower() + " ");
                        var wrapped = new Exception($"Cube row {rowIndex}: failed to parse properties for Output {label}. Mods: {ModsSummaryVariant(pfxArg)}", e);
                        Exceptions.ExceptionHandler.WriteException(wrapped);
                        if (!Exceptions.ExceptionHandler.ContinueOnException) throw;
                    }
                }

                recipe.Outputs = new CubeOutputsV2 { A = outputA, B = outputB, C = outputC };

                // Notes: infer recipe categories based on inputs/outputs/params
                try
                {
                    var outs = new List<CubeOutputV2>();
                    if (recipe.Outputs != null)
                    {
                        if (recipe.Outputs.A != null) outs.Add(recipe.Outputs.A);
                        if (recipe.Outputs.B != null) outs.Add(recipe.Outputs.B);
                        if (recipe.Outputs.C != null) outs.Add(recipe.Outputs.C);
                    }

                    // Precompute input main codes and quantities once
                    string[] InputMainCodes()
                    {
                        return (recipe.Inputs ?? new List<CubeIngredientV2>())
                            .Select(i => NormalizeToken(i.RawToken))
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToArray();
                    }
                    
                    var mainsAll = InputMainCodes();
                    var qtyByMain = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    if (recipe.Inputs != null)
                    {
                        foreach (var i in recipe.Inputs)
                        {
                            var main = NormalizeToken(i.RawToken);
                            if (string.IsNullOrWhiteSpace(main)) continue;
                            var qty = Math.Max(1, i.Quantity);
                            qtyByMain[main] = qtyByMain.TryGetValue(main, out var cur) ? cur + qty : qty;
                        }
                    }

                    int InputQtyForMain(string code)
                    {
                        return qtyByMain.TryGetValue(code, out var sum) ? sum : 0;
                    }

                    bool AnyOutputNameContains(string substr)
                    {
                        if (outs.Count == 0) return false;
                        return outs.Any(o => !string.IsNullOrEmpty(o?.Name) && o.Name.IndexOf(substr, StringComparison.OrdinalIgnoreCase) >= 0);
                    }

                    bool AnyOutputHasQualifiers(params string[] friendlyNeeded)
                    {
                        if (outs.Count == 0) return false;
                        foreach (var o in outs)
                        {
                            var q = o?.Qualifiers ?? new List<string>();
                            if (friendlyNeeded.All(fn => q.Any(x => string.Equals(x, fn, StringComparison.OrdinalIgnoreCase))))
                                return true;
                        }
                        return false;
                    }

                    bool AnyOutputHasAnyQualifiers(params string[] friendlyAny)
                    {
                        if (outs.Count == 0) return false;
                        foreach (var o in outs)
                        {
                            var q = o?.Qualifiers ?? new List<string>();
                            if (q.Any(x => friendlyAny.Any(fn => string.Equals(x, fn, StringComparison.OrdinalIgnoreCase))))
                                return true;
                        }
                        return false;
                    }

                    bool AnyOutputHasBoth(string friendlyA, params string[] friendlyBOr)
                    {
                        if (outs.Count == 0) return false;
                        foreach (var o in outs)
                        {
                            var q = o?.Qualifiers ?? new List<string>();
                            bool hasA = q.Any(x => string.Equals(x, friendlyA, StringComparison.OrdinalIgnoreCase));
                            bool hasB = q.Any(x => friendlyBOr.Any(b => string.Equals(x, b, StringComparison.OrdinalIgnoreCase)));
                            if (hasA && hasB) return true;
                        }
                        return false;
                    }

                    bool AnyOutputPropertyContains(string substr)
                    {
                        if (outs.Count == 0) return false;
                        foreach (var o in outs)
                        {
                            var props = o?.Properties;
                            if (props == null) continue;
                            if (props.Any(p => !string.IsNullOrEmpty(p?.PropertyString) && p.PropertyString.IndexOf(substr, StringComparison.OrdinalIgnoreCase) >= 0))
                                return true;
                        }
                        return false;
                    }

                    bool OutputEqualsUseType()
                    {
                        // ParseOutput sets these special names
                        return outs.Any(o => string.Equals(o?.Name, "Return Base Type", StringComparison.OrdinalIgnoreCase));
                    }

                    bool OutputEqualsUseItem()
                    {
                        return outs.Any(o => string.Equals(o?.Name, "Return Updated Item", StringComparison.OrdinalIgnoreCase));
                    }

                    bool FirstOutputHasAnyPropertiesAndFirstNotCorrupted()
                    {
                        // Find the first defined output in order A, B, C
                        CubeOutputV2 first = recipe.Outputs?.A ?? recipe.Outputs?.B ?? recipe.Outputs?.C;
                        if (first == null || first.Properties == null || first.Properties.Count == 0) return false;
                        var firstStr = first.Properties[0]?.PropertyString ?? string.Empty;
                        return firstStr.IndexOf("corrupted", StringComparison.OrdinalIgnoreCase) < 0;
                    }

                    bool InputsContainCodes(params string[] codes)
                    {
                        return codes.All(c => mainsAll.Any(m => string.Equals(m, c, StringComparison.OrdinalIgnoreCase)));
                    }

                    bool InputsContainAnyCodes(params string[] codes)
                    {
                        return mainsAll.Any(m => codes.Any(c => string.Equals(m, c, StringComparison.OrdinalIgnoreCase)));
                    }

                    bool InputsQualifiersContain(string friendly)
                    {
                        if (recipe.Inputs == null) return false;
                        foreach (var i in recipe.Inputs)
                        {
                            if ((i.Qualifiers ?? new List<string>()).Any(q => string.Equals(q, friendly, StringComparison.OrdinalIgnoreCase)))
                                return true;
                        }
                        return false;
                    }

                    bool InputsNamesContainExact(params string[] names)
                    {
                        if (recipe.Inputs == null) return false;
                        foreach (var i in recipe.Inputs)
                        {
                            foreach (var n in names)
                            {
                                if (string.Equals(i.Name, n, StringComparison.OrdinalIgnoreCase))
                                    return true;
                            }
                        }
                        return false;
                    }

                    bool InputsNamesContainSubstring(string substr)
                    {
                        if (recipe.Inputs == null) return false;
                        foreach (var i in recipe.Inputs)
                        {
                            if (!string.IsNullOrEmpty(i.Name) && i.Name.IndexOf(substr, StringComparison.OrdinalIgnoreCase) >= 0)
                                return true;
                        }
                        return false;
                    }

                    bool InputsRawTokensContainSubstring(string substr)
                    {
                        if (recipe.Inputs == null) return false;
                        foreach (var i in recipe.Inputs)
                        {
                            if (!string.IsNullOrEmpty(i.RawToken) && i.RawToken.IndexOf(substr, StringComparison.OrdinalIgnoreCase) >= 0)
                                return true;
                        }
                        return false;
                    }

                    void AddNote(string note, HashSet<string> seen)
                    {
                        if (!seen.Contains(note)) { recipe.Notes.Add(note); seen.Add(note); }
                    }

                    var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    // 1) If numinputs = 2 and one of them is ka3 → Orb of Corruption Recipe
                    if (recipe.NumInputs == 2 && InputsContainAnyCodes("ka3"))
                    {
                        AddNote("Orb of Corruption Recipe", added);
                    }

                    // 2) numinputs = 2 and one is ooc → Orb of Conversion Recipe
                    if (recipe.NumInputs == 2 && InputsContainAnyCodes("ooc"))
                    {
                        AddNote("Orb of Conversion Recipe", added);
                    }

                    // 3) numinputs = 2 and one is ooa → Orb of Assemblage Recipe
                    if (recipe.NumInputs == 2 && InputsContainAnyCodes("ooa"))
                    {
                        AddNote("Orb of Assemblage Recipe", added);
                    }

                    // 4) numinputs = 2 and one is ooi → Orb of Infusion Recipe
                    if (recipe.NumInputs == 2 && InputsContainAnyCodes("ooi"))
                    {
                        AddNote("Orb of Infusion Recipe", added);
                    }

                    // 5) numinputs = 2 and one is oos → Orb of Socketing Recipe
                    if (recipe.NumInputs == 2 && InputsContainAnyCodes("oos"))
                    {
                        AddNote("Orb of Socketing Recipe", added);
                    }

                    // 6) numinputs = 2 and one is ooe → Orb of Shadows Recipe
                    if (recipe.NumInputs == 2 && InputsContainAnyCodes("ooe"))
                    {
                        AddNote("Orb of Shadows Recipe", added);
                    }

                    // 7) numinputs = 2 and one is jwp or rup → Pliers Recipe
                    if (recipe.NumInputs == 2 && InputsContainAnyCodes("jwp", "rup"))
                    {
                        AddNote("Pliers Recipe", added);
                    }

                    // 8) usetype output + any of {Magic/Rare/Crafted} + has properties and first not corrupted → crafting
                    if (OutputEqualsUseType() && AnyOutputHasAnyQualifiers("Magic Item", "Rare Item", "Crafted Item") && FirstOutputHasAnyPropertiesAndFirstNotCorrupted())
                    {
                        AddNote("Item Crafting Recipe", added);
                    }

                    // 9) output contains Quiver or Bolt Case → Item Crafting Recipe
                    if (AnyOutputNameContains("Quiver") || AnyOutputNameContains("Bolt Case"))
                    {
                        AddNote("Item Crafting Recipe", added);
                    }

                    // 9.5) outputs contain "Barilzar's Mazed Band" → Item Crafting Recipe
                    if (AnyOutputNameContains("Barilzar's Mazed Band"))
                    {
                        AddNote("Item Crafting Recipe", added);
                    }

                    // 10) param = 386 and input is any Sunder charm → Sunder Item Crafting Recipe
                    if (recipe.Param == 386 && InputsNamesContainExact(
                        "Cold Rupture", "Flame Rift", "Crack of the Heavens",
                        "Rotting Fissure", "Bone Break", "Black Cleft"))
                    {
                        AddNote("Sunder Item Crafting Recipe", added);
                    }

                    // 11) inputs contain at least one mpa or blc or dia → Tristram Uber Souls Recipe
                    if (InputsContainAnyCodes("mpa", "blc", "dia"))
                    {
                        AddNote("Tristram Uber Souls Recipe", added);
                    }

                    // 12) inputs contain pk1 or pk2 or pk3 AND ka3 → Key Conversion Recipe
                    if (InputsContainAnyCodes("pk1", "pk2", "pk3") && InputsContainCodes("ka3"))
                    {
                        AddNote("Key Conversion Recipe", added);
                    }

                    // 13) Rejuvenation Potion recipes
                    //    a) Legacy pattern: inputs contain hpot and outputs contain rvs/rvl
                    //    b) Common pattern: inputs contain 3x rvs (Rejuvenation Potion) and output Full Rejuvenation Potion
                    if (
                        (InputsContainCodes("hpot") && (AnyOutputNameContains("Rejuvenation Potion") || AnyOutputNameContains("Full Rejuvenation Potion")))
                        ||
                        (InputQtyForMain("rvs") >= 3 && AnyOutputNameContains("Full Rejuvenation Potion"))
                    )
                    {
                        AddNote("Rejuvenation Potion Recipe", added);
                    }

                    // 14) inputs have qualifier noe AND outputs contain useitem AND properties contain "ethereal" → Force Ethereal Recipe
                    if (InputsQualifiersContain("Not Ethereal") && OutputEqualsUseItem() && AnyOutputPropertyContains("ethereal"))
                    {
                        AddNote("Force Ethereal Recipe", added);
                    }

                    // 14.5) inputs have qualifier 'Not Ethereal' AND outputs have qualifiers 'Repair Item' → Repair Non-Ethereal Item
                    // Scope per request: strictly require the input qualifier and output qualifier.
                    if (InputsQualifiersContain("Not Ethereal") && AnyOutputHasQualifiers("Repair Item"))
                    {
                        AddNote("Repair Non-Ethereal Item", added);
                    }

                    // 15) inputs contain r01 and r15 AND outputs contain usetype,nor → Force White Recipe
                    // Some datasets label 'nor' as "Normal Quality" instead of "Normal Item"; accept either.
                    if (InputsContainCodes("r01") && InputsContainCodes("r15") && OutputEqualsUseType() &&
                        (AnyOutputHasQualifiers("Normal Item") || AnyOutputHasQualifiers("Normal Quality")))
                    {
                        AddNote("Force White Recipe", added);
                    }

                    // 16) param = 386 and inputs are rin/amu/cm1/cm2/cm3/jew (optionally with bag) → Reroll Item Recipe
                    if (recipe.Param == 386)
                    {
                        var core = new HashSet<string>(new[] { "rin", "amu", "cm1", "cm2", "cm3", "jew" }, StringComparer.OrdinalIgnoreCase);
                        var optionals = new HashSet<string>(new[] { "bag" }, StringComparer.OrdinalIgnoreCase); // Gem Bag often accompanies reroll
                        bool onlyAllowed = mainsAll.Length > 0 && mainsAll.All(m => core.Contains(m) || optionals.Contains(m));
                        bool hasCore = mainsAll.Any(m => core.Contains(m));
                        if (onlyAllowed && hasCore)
                        {
                            AddNote("Reroll Item Recipe", added);
                        }
                    }

                    // 17) numinputs = 3 and input contains rin/amu/cm1/cm2/cm3 and qty=3 (all same type)
                    if (recipe.NumInputs == 3)
                    {
                        var allowed = new[] { "rin", "amu", "cm1", "cm2", "cm3" };
                        var counts = allowed.ToDictionary(a => a, a => InputQtyForMain(a), StringComparer.OrdinalIgnoreCase);
                        var present = counts.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
                        // exactly one of the allowed types present, and its total qty == 3, and no other input types present
                        if (present.Count == 1 && counts[present[0]] == 3 && mainsAll.All(m => allowed.Any(a => a.Equals(m, StringComparison.OrdinalIgnoreCase))))
                        {
                            AddNote("Reroll Item Recipe", added);
                        }
                    }

                    // 18) input contains jew and qty=# only (no other inputs) → Jewel Upgrade Recipe
                    {
                        var jewQty = InputQtyForMain("jew");
                        var onlyJew = mainsAll.Length > 0 && mainsAll.All(m => string.Equals(m, "jew", StringComparison.OrdinalIgnoreCase));
                        bool singleToken = (recipe.Inputs != null && recipe.Inputs.Count == 1);
                        if (singleToken && onlyJew && jewQty > 0)
                        {
                            AddNote("Jewel Upgrade Recipe", added);
                        }
                    }

                    // 19) Recycle Recipe: common pattern outputs Gem Cluster and returns Tome of Identify (100)
                    //     Often inputs include Tome of Identify plus multiple set/rare items
                    {
                        bool hasGemClusterOut = AnyOutputNameContains("Gem Cluster");
                        bool hasTome100 = outs.Any(o => o != null && string.Equals(o.Name, "Tome of Identify", StringComparison.OrdinalIgnoreCase) && o.Quantity >= 100);
                        bool inputsHaveTome = InputsNamesContainExact("Tome of Identify");
                        if (hasGemClusterOut && hasTome100 && inputsHaveTome)
                        {
                            AddNote("Recycle Recipe", added);
                        }
                    }

                    // 19) param = 386 and input contains "splash charm" (any tier like "T1 Splash Charm", etc.) → Splash Charm Upgrade Recipe
                    // Match by name or raw token because tiered tokens may render as named variants in display (e.g., "Collin's Lesser Might").
                    if (recipe.Param == 386 && (InputsNamesContainExact("Splash Charm") ||
                                                InputsNamesContainSubstring("Splash Charm") ||
                                                InputsRawTokensContainSubstring("Splash Charm")))
                    {
                        AddNote("Splash Charm Upgrade Recipe", added);
                    }

                    // 20) inputs contain Hellfire Torch and output mod contains "upgrade" → Uber Charm Upgrade Recipe
                    if (InputsNamesContainExact("Hellfire Torch") && AnyOutputPropertyContains("upgrade"))
                    {
                        AddNote("Uber Charm Upgrade Recipe", added);
                    }
                    // 21) inputs contain Annihilus and output mod contains "upgrade" → Uber Charm Upgrade Recipe
                    if (InputsNamesContainExact("Annihilus") && AnyOutputPropertyContains("upgrade"))
                    {
                        AddNote("Uber Charm Upgrade Recipe", added);
                    }

                    // 21.5) outputs contain "Black Soulstone" or "Obsidian Beacon" → Uber Charm Upgrade Recipe
                    if (AnyOutputNameContains("Black Soulstone") || AnyOutputNameContains("Obsidian Beacon"))
                    {
                        AddNote("Uber Charm Upgrade Recipe", added);
                    }

                    // 22) outputs contain qualifier mod and exc or eli → Base Tier Upgrade Recipe (on the same output)
                    if (AnyOutputHasBoth("Keep Modifiers", new[] { "Exceptional Item", "Elite Item" }))
                    {
                        AddNote("Base Tier Upgrade Recipe", added);
                    }

                    // 23) input contains qualifier eth and output contains qualifier rep → Repair Ethereal Recipe
                    if (InputsQualifiersContain("Ethereal") && AnyOutputHasQualifiers("Repair Item"))
                    {
                        AddNote("Repair Ethereal Recipe", added);
                    }

                    // 24) inputs contain misl and hpot → Replenish Quiver/Bolt Case Recipe
                    if (InputsContainCodes("misl") && InputsContainCodes("hpot"))
                    {
                        AddNote("Replenish Quiver/Bolt Case Recipe", added);
                    }

                    // 25) inputs contain leg → Cow Portal Recipe
                    if (InputsContainCodes("leg"))
                    {
                        AddNote("Cow Portal Recipe", added);
                    }

                    // 26) inputs contain pk1 and pk2 and pk3 → Random Mini-Uber Portal Recipe
                    if (InputsContainCodes("pk1") && InputsContainCodes("pk2") && InputsContainCodes("pk3"))
                    {
                        AddNote("Random Mini-Uber Portal Recipe", added);
                    }

                    // 27) inputs contain dhn and bey and mbr → Tristram Uber Portal Recipe
                    if (InputsContainCodes("dhn") && InputsContainCodes("bey") && InputsContainCodes("mbr"))
                    {
                        AddNote("Tristram Uber Portal Recipe", added);
                    }

                    // 28) outputs contain useitem and mod contains "upgrade" → Item Enchantment Recipe
                    if (OutputEqualsUseItem() && AnyOutputPropertyContains("upgrade") || AnyOutputPropertyContains("enchant"))
                    {
                        AddNote("Item Enchantment Recipe", added);
                    }

                    // 29) numinputs >= 4 and contains ibk or tpk and outputs contain jew or 1gc → Recycle Recipe
                    if (recipe.NumInputs >= 4 && InputsContainAnyCodes("ibk", "tpk") && (AnyOutputNameContains("Jewel") || AnyOutputNameContains("Grand Charm")))
                    {
                        AddNote("Recycle Recipe", added);
                    }
                }
                catch (Exception ex)
                {
                    // Do not fail import because of note heuristics
                    Exceptions.ExceptionHandler.WriteException(ex);
                    if (!Exceptions.ExceptionHandler.ContinueOnException) throw;
                }

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
            public List<string> QualifiersFriendly { get; set; }
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

            // Friendly qualifiers (use merged map; fallback to title case)
            var friendly = new List<string>(quals.Count);
            foreach (var q in quals)
            {
                if (qmaps.CombinedDisplay != null && qmaps.CombinedDisplay.TryGetValue(q, out var disp))
                    friendly.Add(disp);
                else
                    friendly.Add(FallbackFriendly(q));
            }

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

                // Validate as before, but use fixed display string per request
                var _ = ResolveTypeFromInput(first); // ensure resolvable for validation side-effects
                ValidateQualifiers(parsed.QualifiersRaw, qmaps.OutputTokens, "output", rowIndex, raw);

                return new CubeOutputV2
                {
                    Name = "Return Base Type",
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
                // Validate as before, but use fixed display string per request
                var itemName = first.Name; // ensure available; not used for display anymore
                ValidateQualifiers(parsed.QualifiersRaw, qmaps.OutputTokens, "output", rowIndex, raw);
                return new CubeOutputV2
                {
                    Name = "Return Updated Item",
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

        private static List<CubePropertyV2> ParseProperties(Dictionary<string, string> row, string prefix = "")
        {
            var list = new List<CubePropertyV2>();
            var propInfos = new List<PropertyInfo>();
            var chances = new List<int?>();
            for (int i = 1; i <= 5; i++)
            {
                var code = Get(row, $"{prefix}mod {i}");
                if (string.IsNullOrWhiteSpace(code)) continue;
                var par = Get(row, $"{prefix}mod {i} param");
                var min = Get(row, $"{prefix}mod {i} min");
                var max = Get(row, $"{prefix}mod {i} max");
                propInfos.Add(new PropertyInfo(code, par, min, max));
                chances.Add(ToInt(Get(row, $"{prefix}mod {i} chance")));
            }
            if (propInfos.Count == 0) return list;

            var props = ItemProperty.GetProperties(propInfos, 0); // validate & format; will throw on invalid
            // Filter out unwanted properties: empty strings and explicit 'corrupteddummy'
            for (int i = 0; i < props.Count; i++)
            {
                var p = props[i];
                // Ignore properties that render to no string (e.g., corrupteddummy producing nothing)
                if (string.IsNullOrWhiteSpace(p.PropertyString)) continue;
                // Also explicitly ignore the placeholder property code 'corrupteddummy' if present
                if (p.Property != null && string.Equals(p.Property.Code, "corrupteddummy", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                list.Add(new CubePropertyV2
                {
                    PropertyString = p.PropertyString,
                    ModChance = chances.ElementAtOrDefault(i),
                    // Reindex sequentially for the exported list
                    Index = list.Count
                });
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
