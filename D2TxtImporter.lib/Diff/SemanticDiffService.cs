using System;
using System.Collections.Generic;
using System.Linq;

namespace D2TxtImporter.lib.Diff
{
    public static class SemanticDiffService
    {
        // Normalize keys: trim, '&' -> 'and', collapse spaces (runewords additionally drop trailing numbers elsewhere)
        private static string NormalizeKeyCommon(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = s.Trim();
            t = t.Replace("&", "and");
            t = System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ");
            return t.Trim();
        }
        public static SemanticDiffResult Compute(JsonModelLoader.Models models, DiffFromPatchService.Targets targets, bool includeUnmentioned)
        {
            var result = new SemanticDiffResult();

            // Uniques (pair by stable key: use Unique Index only)
            string UniqueKey(JsonModelLoader.UniqueSimple u)
            {
                if (u == null) return string.Empty;
                return NormalizeKeyCommon(u.Index);
            }
            Dictionary<string, JsonModelLoader.UniqueSimple> MapU(Dictionary<string, JsonModelLoader.UniqueSimple> src)
            {
                var dst = new Dictionary<string, JsonModelLoader.UniqueSimple>(StringComparer.OrdinalIgnoreCase);
                if (src != null)
                {
                    foreach (var u in src.Values)
                    {
                        var k = UniqueKey(u);
                        if (string.IsNullOrWhiteSpace(k)) k = u?.Index ?? string.Empty;
                        if (!dst.ContainsKey(k)) dst[k] = u; // keep first if collision
                    }
                }
                return dst;
            }
            var oldUniquesByKey = MapU(models.OldUniques);
            var newUniquesByKey = MapU(models.NewUniques);
            var uniqueKeys = new HashSet<string>((targets?.UniqueKeys ?? Enumerable.Empty<string>()).Select(NormalizeKeyCommon), StringComparer.OrdinalIgnoreCase);
            if (includeUnmentioned)
            {
                foreach (var k in EnumerateChangedKeys(oldUniquesByKey, newUniquesByKey, AreUniqueDifferent))
                {
                    uniqueKeys.Add(k);
                }
            }
            result.Uniques = BuildUniqueChanges(uniqueKeys, oldUniquesByKey, newUniquesByKey);

            // Sets (track by set Index). Also include set item keys by mapping to parent set
            var setKeys = new HashSet<string>((targets?.SetKeys ?? Enumerable.Empty<string>()).Select(NormalizeKeyCommon), StringComparer.OrdinalIgnoreCase);
            // Build normalized maps for sets and set item -> parent set
            Dictionary<string, JsonModelLoader.SetSimple> MapS(Dictionary<string, JsonModelLoader.SetSimple> src)
            {
                var dst = new Dictionary<string, JsonModelLoader.SetSimple>(StringComparer.OrdinalIgnoreCase);
                if (src != null)
                {
                    foreach (var s in src.Values)
                    {
                        var k = NormalizeKeyCommon(s?.Index);
                        if (string.IsNullOrWhiteSpace(k)) k = s?.Index ?? string.Empty;
                        if (!dst.ContainsKey(k)) dst[k] = s;
                    }
                }
                return dst;
            }
            var oldSetsByKey = MapS(models.OldSets);
            var newSetsByKey = MapS(models.NewSets);
            Dictionary<string, string> BuildSetItemToSetNorm(Dictionary<string, JsonModelLoader.SetSimple> map)
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in map?.Values ?? Enumerable.Empty<JsonModelLoader.SetSimple>())
                {
                    var parent = NormalizeKeyCommon(s?.Index);
                    foreach (var it in s?.SetItems ?? new List<JsonModelLoader.SetItemSimple>())
                    {
                        var key = NormalizeKeyCommon(it?.Index);
                        if (string.IsNullOrWhiteSpace(key)) continue;
                        if (!d.ContainsKey(key)) d[key] = parent;
                    }
                }
                return d;
            }
            var oldSetItemToSetNorm = BuildSetItemToSetNorm(oldSetsByKey);
            var newSetItemToSetNorm = BuildSetItemToSetNorm(newSetsByKey);
            // Some keys might be set item indices; map to set indices using both old and new maps
            foreach (var k in targets?.SetKeys ?? Enumerable.Empty<string>())
            {
                // Try raw maps
                if (models.OldSetItemToSet != null)
                {
                    string parent;
                    if (models.OldSetItemToSet.TryGetValue(k, out parent)) setKeys.Add(NormalizeKeyCommon(parent));
                }
                if (models.NewSetItemToSet != null)
                {
                    string parent;
                    if (models.NewSetItemToSet.TryGetValue(k, out parent)) setKeys.Add(NormalizeKeyCommon(parent));
                }
                // Try normalized maps
                var nk = NormalizeKeyCommon(k);
                if (oldSetItemToSetNorm.TryGetValue(nk, out var parentOld)) setKeys.Add(parentOld);
                if (newSetItemToSetNorm.TryGetValue(nk, out var parentNew)) setKeys.Add(parentNew);
            }
            if (includeUnmentioned)
            {
                foreach (var k in EnumerateChangedKeys(oldSetsByKey, newSetsByKey, AreSetDifferent))
                {
                    setKeys.Add(k);
                }
            }
            result.Sets = BuildSetChanges(setKeys, oldSetsByKey, newSetsByKey);

            // Runewords (pair by normalized name): ignore trailing numbers; treat '&' and 'and' as equivalent; collapse spaces
            string NormalizeRunewordName(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                var t = s.Trim();
                // Textual equivalence: '&' -> 'and', collapse spaces
                t = t.Replace("&", "and");
                t = System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ");
                // Drop trailing number suffixes (e.g., "Name 2")
                t = System.Text.RegularExpressions.Regex.Replace(t, @"\s*\d+$", string.Empty);
                return t.Trim();
            }
            Dictionary<string, JsonModelLoader.RunewordSimple> MapR(Dictionary<string, JsonModelLoader.RunewordSimple> src)
            {
                var dst = new Dictionary<string, JsonModelLoader.RunewordSimple>(StringComparer.OrdinalIgnoreCase);
                if (src != null)
                {
                    foreach (var r in src.Values)
                    {
                        var k = NormalizeRunewordName(r?.Index);
                        if (string.IsNullOrWhiteSpace(k)) k = r?.Index ?? string.Empty;
                        if (!dst.ContainsKey(k)) dst[k] = r; // keep first if collision
                    }
                }
                return dst;
            }
            var oldRunesByKey = MapR(models.OldRunewords);
            var newRunesByKey = MapR(models.NewRunewords);
            var runeKeys = new HashSet<string>(targets?.RunewordKeys ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (includeUnmentioned)
            {
                foreach (var k in EnumerateChangedKeys(oldRunesByKey, newRunesByKey, AreRunewordDifferent))
                {
                    runeKeys.Add(k);
                }
            }
            result.Runewords = BuildRunewordChanges(runeKeys, oldRunesByKey, newRunesByKey);

            // Base Items (Weapons + Armors) by base code
            var baseCodes = new HashSet<string>(targets?.BaseCodes ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (includeUnmentioned)
            {
                foreach (var k in EnumerateBaseChangedCodes(models))
                {
                    baseCodes.Add(k);
                }
            }
            result.BaseItems = BaseAndCubeDiffBuilders.BuildBaseChanges(baseCodes, models);

            // Cube Recipes by description or index key
            var cubeKeys = new HashSet<string>((targets?.CubeKeys ?? Enumerable.Empty<string>()).Select(NormalizeKeyCommon), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, JsonModelLoader.CubeRecipeSimple> MapC(Dictionary<string, JsonModelLoader.CubeRecipeSimple> src)
            {
                var dst = new Dictionary<string, JsonModelLoader.CubeRecipeSimple>(StringComparer.OrdinalIgnoreCase);
                if (src != null)
                {
                    foreach (var c in src.Values)
                    {
                        var k = NormalizeKeyCommon(c?.Key);
                        if (string.IsNullOrWhiteSpace(k)) k = c?.Key ?? string.Empty;
                        if (!dst.ContainsKey(k)) dst[k] = c;
                    }
                }
                return dst;
            }
            var oldCubesByKey = MapC(models.OldCubes);
            var newCubesByKey = MapC(models.NewCubes);
            if (includeUnmentioned)
            {
                foreach (var k in EnumerateChangedKeys(oldCubesByKey, newCubesByKey, (a,b) => BaseAndCubeDiffBuilders.AreCubeDifferent(a,b)))
                {
                    cubeKeys.Add(k);
                }
            }
            result.CubeRecipes = BaseAndCubeDiffBuilders.BuildCubeChanges(cubeKeys, oldCubesByKey, newCubesByKey);

            // Populate base code maps for exporter grouping
            // Weapons
            foreach (var kv in models.NewWeapons ?? new Dictionary<string, JsonModelLoader.WeaponSimple>())
            {
                var code = kv.Key;
                var w = kv.Value;
                if (string.IsNullOrWhiteSpace(code)) continue;
                if (!result.BaseCodeToName.ContainsKey(code)) result.BaseCodeToName[code] = w?.Name ?? string.Empty;
                if (!result.BaseCodeToCategory.ContainsKey(code)) result.BaseCodeToCategory[code] = "Weapon";
                // Tier mapping: default Code=N, UberCode=X, UltraCode=E (prefer higher tiers)
                SetTier(result, code, "N");
                if (!string.IsNullOrWhiteSpace(w?.UberCode)) SetTier(result, w.UberCode, "X");
                if (!string.IsNullOrWhiteSpace(w?.UltraCode)) SetTier(result, w.UltraCode, "E");
            }
            foreach (var kv in models.OldWeapons ?? new Dictionary<string, JsonModelLoader.WeaponSimple>())
            {
                var code = kv.Key;
                var w = kv.Value;
                if (!result.BaseCodeToName.ContainsKey(code)) result.BaseCodeToName[code] = kv.Value?.Name ?? string.Empty;
                if (!result.BaseCodeToCategory.ContainsKey(code)) result.BaseCodeToCategory[code] = "Weapon";
                SetTier(result, code, "N");
                if (!string.IsNullOrWhiteSpace(w?.UberCode)) SetTier(result, w.UberCode, "X");
                if (!string.IsNullOrWhiteSpace(w?.UltraCode)) SetTier(result, w.UltraCode, "E");
            }
            // Armors
            foreach (var kv in models.NewArmors ?? new Dictionary<string, JsonModelLoader.ArmorSimple>())
            {
                var code = kv.Key;
                var a = kv.Value;
                if (string.IsNullOrWhiteSpace(code)) continue;
                if (!result.BaseCodeToName.ContainsKey(code)) result.BaseCodeToName[code] = a?.Name ?? string.Empty;
                if (!result.BaseCodeToCategory.ContainsKey(code)) result.BaseCodeToCategory[code] = "Armor";
                SetTier(result, code, "N");
                if (!string.IsNullOrWhiteSpace(a?.UberCode)) SetTier(result, a.UberCode, "X");
                if (!string.IsNullOrWhiteSpace(a?.UltraCode)) SetTier(result, a.UltraCode, "E");
            }
            foreach (var kv in models.OldArmors ?? new Dictionary<string, JsonModelLoader.ArmorSimple>())
            {
                var code = kv.Key;
                var a = kv.Value;
                if (!result.BaseCodeToName.ContainsKey(code)) result.BaseCodeToName[code] = kv.Value?.Name ?? string.Empty;
                if (!result.BaseCodeToCategory.ContainsKey(code)) result.BaseCodeToCategory[code] = "Armor";
                SetTier(result, code, "N");
                if (!string.IsNullOrWhiteSpace(a?.UberCode)) SetTier(result, a.UberCode, "X");
                if (!string.IsNullOrWhiteSpace(a?.UltraCode)) SetTier(result, a.UltraCode, "E");
            }

            // Misc base-type codes used by Uniques/Sets (e.g., rings, amulets, charms, jewels)
            // These do not appear in weapons/armors JSONs, so seed friendly names here to improve rendering
            var miscBaseMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "amu", "Amulet" },
                { "rin", "Ring" },
                { "cm1", "Small Charm" },
                { "cm2", "Large Charm" },
                { "cm3", "Grand Charm" },
                { "jew", "Jewel" }
            };
            foreach (var kv in miscBaseMap)
            {
                if (!result.BaseCodeToName.ContainsKey(kv.Key)) result.BaseCodeToName[kv.Key] = kv.Value;
                if (!result.BaseCodeToCategory.ContainsKey(kv.Key)) result.BaseCodeToCategory[kv.Key] = "Misc";
            }

            return result;
        }

        private static void SetTier(SemanticDiffResult res, string code, string tier)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(tier)) return;
            int Rank(string t)
            {
                switch ((t ?? string.Empty).ToUpperInvariant())
                {
                    case "E": return 3; // Elite
                    case "X": return 2; // Exceptional
                    case "N": return 1; // Normal
                    default: return 0;
                }
            }
            if (!res.BaseCodeToTier.TryGetValue(code, out var existing))
            {
                res.BaseCodeToTier[code] = tier;
                return;
            }
            if (Rank(tier) > Rank(existing))
            {
                res.BaseCodeToTier[code] = tier;
            }
        }

        private static IEnumerable<string> EnumerateChangedKeys<T>(Dictionary<string, T> oldMap, Dictionary<string, T> newMap, Func<T, T, bool> hasChanged)
        {
            oldMap = oldMap ?? new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            newMap = newMap ?? new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            var keys = new HashSet<string>(oldMap.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var k in newMap.Keys)
            {
                keys.Add(k);
            }
            foreach (var k in keys)
            {
                T o;
                if (oldMap != null && oldMap.TryGetValue(k, out o)) { }
                else { o = default(T); }
                T n;
                if (newMap != null && newMap.TryGetValue(k, out n)) { }
                else { n = default(T); }
                if (hasChanged(o, n)) yield return k;
            }
        }

        private static IEnumerable<string> EnumerateBaseChangedCodes(JsonModelLoader.Models models)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in models.OldWeapons?.Keys ?? Enumerable.Empty<string>()) { keys.Add(k); }
            foreach (var k in models.NewWeapons?.Keys ?? Enumerable.Empty<string>()) { keys.Add(k); }
            foreach (var k in models.OldArmors?.Keys ?? Enumerable.Empty<string>()) { keys.Add(k); }
            foreach (var k in models.NewArmors?.Keys ?? Enumerable.Empty<string>()) { keys.Add(k); }
            foreach (var k in keys)
            {
                JsonModelLoader.WeaponSimple ow = null; JsonModelLoader.WeaponSimple nw = null;
                JsonModelLoader.ArmorSimple oa = null; JsonModelLoader.ArmorSimple na = null;
                if (models.OldWeapons != null && models.OldWeapons.TryGetValue(k, out var _ow)) { ow = _ow; }
                if (models.NewWeapons != null && models.NewWeapons.TryGetValue(k, out var _nw)) { nw = _nw; }
                if (models.OldArmors != null && models.OldArmors.TryGetValue(k, out var _oa)) { oa = _oa; }
                if (models.NewArmors != null && models.NewArmors.TryGetValue(k, out var _na)) { na = _na; }
                if (AreWeaponDifferent(ow, nw) || AreArmorDifferent(oa, na))
                {
                    yield return k;
                }
            }
        }

        private static Changes<UniqueChange> BuildUniqueChanges(HashSet<string> keys, Dictionary<string, JsonModelLoader.UniqueSimple> oldMap, Dictionary<string, JsonModelLoader.UniqueSimple> newMap)
        {
            var changes = new Changes<UniqueChange>();
            foreach (var key in keys)
            {
                JsonModelLoader.UniqueSimple oldU;
                if (oldMap != null && oldMap.TryGetValue(key, out oldU)) { }
                else { oldU = null; }
                JsonModelLoader.UniqueSimple newU;
                if (newMap != null && newMap.TryGetValue(key, out newU)) { }
                else { newU = null; }
                if (oldU == null && newU == null) continue;
                if (oldU == null)
                {
                    changes.Added.Add(UniqueChange.FromNew(newU));
                }
                else if (newU == null)
                {
                    changes.Removed.Add(UniqueChange.FromOld(oldU));
                }
                else
                {
                    if (AreUniqueDifferent(oldU, newU))
                    {
                        changes.Modified.Add(UniqueChange.FromBoth(oldU, newU));
                    }
                }
            }
            changes.SortByName();
            return changes;
        }

        private static Changes<SetChange> BuildSetChanges(HashSet<string> keys, Dictionary<string, JsonModelLoader.SetSimple> oldMap, Dictionary<string, JsonModelLoader.SetSimple> newMap)
        {
            var changes = new Changes<SetChange>();
            foreach (var key in keys)
            {
                JsonModelLoader.SetSimple oldS;
                if (oldMap != null && oldMap.TryGetValue(key, out oldS)) { }
                else { oldS = null; }
                JsonModelLoader.SetSimple newS;
                if (newMap != null && newMap.TryGetValue(key, out newS)) { }
                else { newS = null; }
                if (oldS == null && newS == null) continue;
                if (oldS == null)
                {
                    changes.Added.Add(SetChange.FromNew(newS));
                }
                else if (newS == null)
                {
                    changes.Removed.Add(SetChange.FromOld(oldS));
                }
                else
                {
                    if (AreSetDifferent(oldS, newS))
                    {
                        changes.Modified.Add(SetChange.FromBoth(oldS, newS));
                    }
                }
            }
            changes.SortByName();
            return changes;
        }

        private static Changes<RunewordChange> BuildRunewordChanges(HashSet<string> keys, Dictionary<string, JsonModelLoader.RunewordSimple> oldMap, Dictionary<string, JsonModelLoader.RunewordSimple> newMap)
        {
            var changes = new Changes<RunewordChange>();
            foreach (var key in keys)
            {
                JsonModelLoader.RunewordSimple oldR;
                if (oldMap != null && oldMap.TryGetValue(key, out oldR)) { }
                else { oldR = null; }
                JsonModelLoader.RunewordSimple newR;
                if (newMap != null && newMap.TryGetValue(key, out newR)) { }
                else { newR = null; }
                if (oldR == null && newR == null) continue;
                if (oldR == null)
                {
                    changes.Added.Add(RunewordChange.FromNew(newR));
                }
                else if (newR == null)
                {
                    changes.Removed.Add(RunewordChange.FromOld(oldR));
                }
                else
                {
                    if (AreRunewordDifferent(oldR, newR))
                    {
                        changes.Modified.Add(RunewordChange.FromBoth(oldR, newR));
                    }
                }
            }
            changes.SortByName();
            return changes;
        }

        private static bool AreUniqueDifferent(JsonModelLoader.UniqueSimple a, JsonModelLoader.UniqueSimple b)
        {
            if (a == null && b != null) return true;
            if (a != null && b == null) return true;
            if (a == null && b == null) return false;
            // Base (code) change should be considered a modification
            if (!string.Equals(a.Code ?? string.Empty, b.Code ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return true;
            if (a.Enabled != b.Enabled) return true;
            if (a.ItemLevel != b.ItemLevel) return true;
            if (a.RequiredLevel != b.RequiredLevel) return true;
            if (a.Rarity != b.Rarity) return true;
            return !SetEquals(ToSet(a?.Properties), ToSet(b?.Properties));
        }

        private static bool AreSetDifferent(JsonModelLoader.SetSimple a, JsonModelLoader.SetSimple b)
        {
            if (a == null && b != null) return true;
            if (a != null && b == null) return true;
            if (a == null && b == null) return false;
            if (a.Level != b.Level) return true; // limited, but include
            if (!SetEquals(ToSet(a?.PartialProperties), ToSet(b?.PartialProperties))) return true;
            if (!SetEquals(ToSet(a?.FullProperties), ToSet(b?.FullProperties))) return true;
            // Compare set items by index
            var aItems = new HashSet<string>((a?.SetItems ?? new List<JsonModelLoader.SetItemSimple>()).Select(x => NormalizeKeyCommon(x.Index)), StringComparer.OrdinalIgnoreCase);
            var bItems = new HashSet<string>((b?.SetItems ?? new List<JsonModelLoader.SetItemSimple>()).Select(x => NormalizeKeyCommon(x.Index)), StringComparer.OrdinalIgnoreCase);
            if (!aItems.SetEquals(bItems)) return true;
            // Deep compare items
            var mapA = (a?.SetItems ?? new List<JsonModelLoader.SetItemSimple>()).ToDictionary(x => NormalizeKeyCommon(x.Index), x => x, StringComparer.OrdinalIgnoreCase);
            var mapB = (b?.SetItems ?? new List<JsonModelLoader.SetItemSimple>()).ToDictionary(x => NormalizeKeyCommon(x.Index), x => x, StringComparer.OrdinalIgnoreCase);
            foreach (var k in aItems.Union(bItems))
            {
                mapA.TryGetValue(k, out var ai);
                mapB.TryGetValue(k, out var bi);
                if (ai == null || bi == null) return true;
                if (AreSetItemDifferentInternal(ai, bi)) return true;
            }
            return false;
        }

        private static bool AreRunewordDifferent(JsonModelLoader.RunewordSimple a, JsonModelLoader.RunewordSimple b)
        {
            if (a == null && b != null) return true;
            if (a != null && b == null) return true;
            if (a == null && b == null) return false;
            if (a.Enabled != b.Enabled) return true;
            if (a.ItemLevel != b.ItemLevel) return true;
            if (a.RequiredLevel != b.RequiredLevel) return true;
            if (!SetEquals(ToSet(a?.Properties), ToSet(b?.Properties))) return true;
            return false;
        }

        internal static bool AreWeaponDifferent(JsonModelLoader.WeaponSimple a, JsonModelLoader.WeaponSimple b)
        {
            if (a == null && b == null) return false;
            if (a == null || b == null) return true;
            if ((a.BaseRequiredLevel ?? 0) != (b.BaseRequiredLevel ?? 0)) return true;
            if (a.RequiredStrength != b.RequiredStrength) return true;
            if (a.RequiredDexterity != b.RequiredDexterity) return true;
            if (a.Durability != b.Durability) return true;
            if (a.ItemLevel != b.ItemLevel) return true;
            if (a.GemSockets != b.GemSockets) return true;
            if (a.Speed != b.Speed) return true;
            // Compare damages by string list
            var aD = new HashSet<string>((a.Damages ?? new List<JsonModelLoader.DamageSimple>()).Select(d => d?.ToString() ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            var bD = new HashSet<string>((b.Damages ?? new List<JsonModelLoader.DamageSimple>()).Select(d => d?.ToString() ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            if (!aD.SetEquals(bD)) return true;
            // Automagic properties
            var aP = new HashSet<string>(a.AutoMagicProps ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var bP = new HashSet<string>(b.AutoMagicProps ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            if (!aP.SetEquals(bP)) return true;
            return false;
        }

        internal static bool AreArmorDifferent(JsonModelLoader.ArmorSimple a, JsonModelLoader.ArmorSimple b)
        {
            if (a == null && b == null) return false;
            if (a == null || b == null) return true;
            if ((a.BaseRequiredLevel ?? 0) != (b.BaseRequiredLevel ?? 0)) return true;
            if (a.RequiredStrength != b.RequiredStrength) return true;
            if (a.RequiredDexterity != b.RequiredDexterity) return true;
            if (a.Durability != b.Durability) return true;
            if (a.ItemLevel != b.ItemLevel) return true;
            if (a.GemSockets != b.GemSockets) return true;
            if (!string.Equals(a.ArmorString ?? string.Empty, b.ArmorString ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return true;
            if ((a.Block ?? 0) != (b.Block ?? 0)) return true;
            if (!string.Equals(a.DamageStringPrefix ?? string.Empty, b.DamageStringPrefix ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.Equals(a.DamageString ?? string.Empty, b.DamageString ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return true;
            var aP = new HashSet<string>(a.AutoMagicProps ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var bP = new HashSet<string>(b.AutoMagicProps ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            if (!aP.SetEquals(bP)) return true;
            return false;
        }

        private static HashSet<string> ToSet(IEnumerable<string> list)
        {
            var hs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in list ?? Enumerable.Empty<string>())
            {
                hs.Add(s ?? string.Empty);
            }
            return hs;
        }

        private static bool SetEquals(HashSet<string> a, HashSet<string> b)
        {
            a = a ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            b = b ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return a.SetEquals(b);
        }

        // Helper to compare set items (simple DTOs) for differences
        private static bool AreSetItemDifferentInternal(JsonModelLoader.SetItemSimple a, JsonModelLoader.SetItemSimple b)
        {
            if (a == null && b != null) return true;
            if (a != null && b == null) return true;
            if (a == null && b == null) return false;
            // Consider base (code) change as a modification
            if (!string.Equals(a.Code ?? string.Empty, b.Code ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return true;
            if (a.Enabled != b.Enabled) return true;
            if (a.ItemLevel != b.ItemLevel) return true;
            if (a.RequiredLevel != b.RequiredLevel) return true;
            if (a.Rarity != b.Rarity) return true;
            var aProps = new HashSet<string>(a.Properties ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var bProps = new HashSet<string>(b.Properties ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            if (!aProps.SetEquals(bProps)) return true;
            var aSetProps = new HashSet<string>(a.SetPropertiesString ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var bSetProps = new HashSet<string>(b.SetPropertiesString ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            if (!aSetProps.SetEquals(bSetProps)) return true;
            return false;
        }

        // Normalization for property strings to detect value-only changes
        private static string NormalizeProp(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var x = s.Trim().ToLowerInvariant();
            // Canonicalize common trigger phrasing to align CTC lines
            x = x.Replace(" when struck", " on ctc")
                 .Replace(" on striking", " on ctc")
                 .Replace(" when you die", " on ctc")
                 .Replace(" on attack", " on ctc")
                 .Replace(" on kill", " on ctc")
                 .Replace(" on level-up", " on ctc");
            // Remove numbers, +/- signs, % symbols; keep words
            x = System.Text.RegularExpressions.Regex.Replace(x, @"[\+\-]", " ");
            x = System.Text.RegularExpressions.Regex.Replace(x, @"\d+(\.\d+)?", " ");
            x = x.Replace("%", " ");
            // Collapse multiple whitespace
            x = System.Text.RegularExpressions.Regex.Replace(x, @"\s+", " ").Trim();
            return x;
        }

        private static void ComputeStringDiff(
            System.Collections.Generic.IEnumerable<string> oldList,
            System.Collections.Generic.IEnumerable<string> newList,
            out System.Collections.Generic.List<string> added,
            out System.Collections.Generic.List<string> removed,
            out System.Collections.Generic.List<PropertyDelta> changed)
        {
            added = new System.Collections.Generic.List<string>();
            removed = new System.Collections.Generic.List<string>();
            changed = new System.Collections.Generic.List<PropertyDelta>();

            var oldItems = (oldList ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            var newItems = (newList ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            // Bucket by normalized key
            var oldBuckets = new Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in oldItems)
            {
                var k = NormalizeProp(s);
                if (!oldBuckets.TryGetValue(k, out var list))
                {
                    list = new System.Collections.Generic.List<string>();
                    oldBuckets[k] = list;
                }
                list.Add(s);
            }
            var newBuckets = new Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in newItems)
            {
                var k = NormalizeProp(s);
                if (!newBuckets.TryGetValue(k, out var list))
                {
                    list = new System.Collections.Generic.List<string>();
                    newBuckets[k] = list;
                }
                list.Add(s);
            }

            // Keys to process
            var keys = new HashSet<string>(oldBuckets.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var k in newBuckets.Keys)
            {
                keys.Add(k);
            }

            foreach (var k in keys)
            {
                oldBuckets.TryGetValue(k, out var aL);
                newBuckets.TryGetValue(k, out var bL);
                aL = aL ?? new System.Collections.Generic.List<string>();
                bL = bL ?? new System.Collections.Generic.List<string>();

                var pairs = Math.Min(aL.Count, bL.Count);
                for (int i = 0; i < pairs; i++)
                {
                    var a = aL[i];
                    var b = bL[i];
                    if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                    {
                        changed.Add(new PropertyDelta { Before = a, After = b });
                    }
                }
                // Leftovers beyond the pairs are pure removed/added
                for (int i = pairs; i < aL.Count; i++)
                {
                    removed.Add(aL[i]);
                }
                for (int i = pairs; i < bL.Count; i++)
                {
                    added.Add(bL[i]);
                }
            }
        }
    }

    public sealed class SemanticDiffResult
    {
        public Changes<UniqueChange> Uniques { get; set; } = new Changes<UniqueChange>();
        public Changes<SetChange> Sets { get; set; } = new Changes<SetChange>();
        public Changes<RunewordChange> Runewords { get; set; } = new Changes<RunewordChange>();
        public Changes<BaseItemChange> BaseItems { get; set; } = new Changes<BaseItemChange>();
        public Changes<CubeRecipeChange> CubeRecipes { get; set; } = new Changes<CubeRecipeChange>();

        // Lookup maps to assist rendering/grouping in Markdown exporter
        public System.Collections.Generic.Dictionary<string, string> BaseCodeToName { get; set; } = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public System.Collections.Generic.Dictionary<string, string> BaseCodeToCategory { get; set; } = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public System.Collections.Generic.Dictionary<string, string> BaseCodeToTier { get; set; } = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class Changes<T> where T : IName
    {
        public List<T> Added { get; } = new List<T>();
        public List<T> Removed { get; } = new List<T>();
        public List<T> Modified { get; } = new List<T>();
        public void SortByName()
        {
            Added.Sort((a,b)=>string.Compare(a.Name,b.Name,StringComparison.OrdinalIgnoreCase));
            Removed.Sort((a,b)=>string.Compare(a.Name,b.Name,StringComparison.OrdinalIgnoreCase));
            Modified.Sort((a,b)=>string.Compare(a.Name,b.Name,StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class BaseItemChange : IName
    {
        public string Name { get; set; } // Code
        public string DisplayName { get; set; }
        public string Category { get; set; } // Weapon or Armor
        // Shared fields
        public int? BaseReqLevelBefore { get; set; }
        public int? BaseReqLevelAfter { get; set; }
        public int? ReqStrBefore { get; set; }
        public int? ReqStrAfter { get; set; }
        public int? ReqDexBefore { get; set; }
        public int? ReqDexAfter { get; set; }
        public int? DurabilityBefore { get; set; }
        public int? DurabilityAfter { get; set; }
        public int? ItemLevelBefore { get; set; }
        public int? ItemLevelAfter { get; set; }
        public string SocketsBefore { get; set; }
        public string SocketsAfter { get; set; }
        // Weapon-specific
        public int? SpeedBefore { get; set; }
        public int? SpeedAfter { get; set; }
        public List<string> DamageAdded { get; set; } = new List<string>();
        public List<string> DamageRemoved { get; set; } = new List<string>();
        public List<PropertyDelta> DamageChanged { get; set; } = new List<PropertyDelta>();
        // Armor-specific
        public string ArmorBefore { get; set; }
        public string ArmorAfter { get; set; }
        public int? BlockBefore { get; set; }
        public int? BlockAfter { get; set; }
        public string DmgPrefixBefore { get; set; }
        public string DmgPrefixAfter { get; set; }
        public string DmgStrBefore { get; set; }
        public string DmgStrAfter { get; set; }
        // Automagic properties
        public List<string> AutoMagicAdded { get; set; } = new List<string>();
        public List<string> AutoMagicRemoved { get; set; } = new List<string>();
        public List<PropertyDelta> AutoMagicChanged { get; set; } = new List<PropertyDelta>();
    }

    public sealed class CubeRecipeChange : IName
    {
        public string Name { get; set; } // Key
        public string DescriptionBefore { get; set; }
        public string DescriptionAfter { get; set; }
        public string NotesBefore { get; set; }
        public string NotesAfter { get; set; }
        public List<string> InputsAdded { get; set; } = new List<string>();
        public List<string> InputsRemoved { get; set; } = new List<string>();
        public List<PropertyDelta> InputsChanged { get; set; } = new List<PropertyDelta>();
        public List<string> OutputsAdded { get; set; } = new List<string>();
        public List<string> OutputsRemoved { get; set; } = new List<string>();
        public List<PropertyDelta> OutputsChanged { get; set; } = new List<PropertyDelta>();
        // Human-readable property strings under recipe outputs
        public List<string> PropertiesAdded { get; set; } = new List<string>();
        public List<string> PropertiesRemoved { get; set; } = new List<string>();
        public List<PropertyDelta> PropertiesChanged { get; set; } = new List<PropertyDelta>();
    }

    // Builders for new categories
    internal static class BaseAndCubeDiffBuilders
    {
        public static Changes<BaseItemChange> BuildBaseChanges(HashSet<string> keys, JsonModelLoader.Models models)
        {
            var changes = new Changes<BaseItemChange>();
            foreach (var code in keys)
            {
                JsonModelLoader.WeaponSimple ow = null; JsonModelLoader.WeaponSimple nw = null;
                JsonModelLoader.ArmorSimple oa = null; JsonModelLoader.ArmorSimple na = null;
                if (models.OldWeapons != null) models.OldWeapons.TryGetValue(code, out ow);
                if (models.NewWeapons != null) models.NewWeapons.TryGetValue(code, out nw);
                if (models.OldArmors != null) models.OldArmors.TryGetValue(code, out oa);
                if (models.NewArmors != null) models.NewArmors.TryGetValue(code, out na);

                // Determine category present
                var had = (ow != null || oa != null);
                var has = (nw != null || na != null);
                if (!had && !has) continue;
                if (!had)
                {
                    // Added
                    if (nw != null)
                    {
                        changes.Added.Add(CreateBaseChangeFromWeapon(null, nw));
                    }
                    else if (na != null)
                    {
                        changes.Added.Add(CreateBaseChangeFromArmor(null, na));
                    }
                    continue;
                }
                if (!has)
                {
                    // Removed
                    if (ow != null)
                    {
                        changes.Removed.Add(CreateBaseChangeFromWeapon(ow, null));
                    }
                    else if (oa != null)
                    {
                        changes.Removed.Add(CreateBaseChangeFromArmor(oa, null));
                    }
                    continue;
                }

                // Modified
                if (nw != null && ow != null)
                {
                    if (SemanticDiffService.AreWeaponDifferent(ow, nw))
                    {
                        changes.Modified.Add(CreateBaseChangeFromWeapon(ow, nw));
                    }
                    continue;
                }
                if (na != null && oa != null)
                {
                    if (SemanticDiffService.AreArmorDifferent(oa, na))
                    {
                        changes.Modified.Add(CreateBaseChangeFromArmor(oa, na));
                    }
                    continue;
                }
                // Category swap (rare): treat as removed + added
                if (ow != null && na != null)
                {
                    changes.Removed.Add(CreateBaseChangeFromWeapon(ow, null));
                    changes.Added.Add(CreateBaseChangeFromArmor(null, na));
                }
                else if (oa != null && nw != null)
                {
                    changes.Removed.Add(CreateBaseChangeFromArmor(oa, null));
                    changes.Added.Add(CreateBaseChangeFromWeapon(null, nw));
                }
            }
            changes.SortByName();
            return changes;
        }

        private static BaseItemChange CreateBaseChangeFromWeapon(JsonModelLoader.WeaponSimple oldW, JsonModelLoader.WeaponSimple newW)
        {
            var ch = new BaseItemChange
            {
                Name = (newW ?? oldW)?.Code,
                DisplayName = (newW ?? oldW)?.Name,
                Category = "Weapon",
                BaseReqLevelBefore = oldW?.BaseRequiredLevel,
                BaseReqLevelAfter = newW?.BaseRequiredLevel,
                ReqStrBefore = oldW?.RequiredStrength,
                ReqStrAfter = newW?.RequiredStrength,
                ReqDexBefore = oldW?.RequiredDexterity,
                ReqDexAfter = newW?.RequiredDexterity,
                DurabilityBefore = oldW?.Durability,
                DurabilityAfter = newW?.Durability,
                ItemLevelBefore = oldW?.ItemLevel,
                ItemLevelAfter = newW?.ItemLevel,
                SocketsBefore = oldW?.GemSockets,
                SocketsAfter = newW?.GemSockets,
                SpeedBefore = oldW?.Speed,
                SpeedAfter = newW?.Speed
            };
            // Damage diffs
            var a = (oldW?.Damages ?? new List<JsonModelLoader.DamageSimple>()).Select(d => d?.ToString() ?? string.Empty);
            var b = (newW?.Damages ?? new List<JsonModelLoader.DamageSimple>()).Select(d => d?.ToString() ?? string.Empty);
            DiffStringUtil.ComputeStringDiff(a, b, out var dAdd, out var dRem, out var dChg);
            ch.DamageAdded = dAdd;
            ch.DamageRemoved = dRem;
            ch.DamageChanged = dChg;
            // AutoMagic diffs
            DiffStringUtil.ComputeStringDiff(oldW?.AutoMagicProps, newW?.AutoMagicProps, out var pAdd, out var pRem, out var pChg);
            ch.AutoMagicAdded = pAdd;
            ch.AutoMagicRemoved = pRem;
            ch.AutoMagicChanged = pChg;
            return ch;
        }

        private static BaseItemChange CreateBaseChangeFromArmor(JsonModelLoader.ArmorSimple oldA, JsonModelLoader.ArmorSimple newA)
        {
            var ch = new BaseItemChange
            {
                Name = (newA ?? oldA)?.Code,
                DisplayName = (newA ?? oldA)?.Name,
                Category = "Armor",
                BaseReqLevelBefore = oldA?.BaseRequiredLevel,
                BaseReqLevelAfter = newA?.BaseRequiredLevel,
                ReqStrBefore = oldA?.RequiredStrength,
                ReqStrAfter = newA?.RequiredStrength,
                ReqDexBefore = oldA?.RequiredDexterity,
                ReqDexAfter = newA?.RequiredDexterity,
                DurabilityBefore = oldA?.Durability,
                DurabilityAfter = newA?.Durability,
                ItemLevelBefore = oldA?.ItemLevel,
                ItemLevelAfter = newA?.ItemLevel,
                SocketsBefore = oldA?.GemSockets,
                SocketsAfter = newA?.GemSockets,
                ArmorBefore = oldA?.ArmorString,
                ArmorAfter = newA?.ArmorString,
                BlockBefore = oldA?.Block,
                BlockAfter = newA?.Block,
                DmgPrefixBefore = oldA?.DamageStringPrefix,
                DmgPrefixAfter = newA?.DamageStringPrefix,
                DmgStrBefore = oldA?.DamageString,
                DmgStrAfter = newA?.DamageString
            };
            // AutoMagic diffs
            DiffStringUtil.ComputeStringDiff(oldA?.AutoMagicProps, newA?.AutoMagicProps, out var pAdd, out var pRem, out var pChg);
            ch.AutoMagicAdded = pAdd;
            ch.AutoMagicRemoved = pRem;
            ch.AutoMagicChanged = pChg;
            return ch;
        }

        public static Changes<CubeRecipeChange> BuildCubeChanges(HashSet<string> keys, Dictionary<string, JsonModelLoader.CubeRecipeSimple> oldMap, Dictionary<string, JsonModelLoader.CubeRecipeSimple> newMap)
        {
            var changes = new Changes<CubeRecipeChange>();
            foreach (var key in keys)
            {
                JsonModelLoader.CubeRecipeSimple o = null; JsonModelLoader.CubeRecipeSimple n = null;
                if (oldMap != null) oldMap.TryGetValue(key, out o);
                if (newMap != null) newMap.TryGetValue(key, out n);
                if (o == null && n == null) continue;
                if (o == null)
                {
                    changes.Added.Add(CubeFromBoth(null, n));
                }
                else if (n == null)
                {
                    changes.Removed.Add(CubeFromBoth(o, null));
                }
                else
                {
                    if (AreCubeDifferent(o, n))
                    {
                        changes.Modified.Add(CubeFromBoth(o, n));
                    }
                }
            }
            changes.SortByName();
            return changes;
        }

        public static bool AreCubeDifferent(JsonModelLoader.CubeRecipeSimple a, JsonModelLoader.CubeRecipeSimple b)
        {
            if (a == null && b == null) return false;
            if (a == null || b == null) return true;
            if (!string.Equals(a.Description ?? string.Empty, b.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return true;
            // Ignore note-only changes: differences in Notes do not by themselves mark a recipe as modified.
            // If other fields change (inputs/outputs/properties), Notes deltas will still be rendered in Markdown.
            // Compare inputs by canonical key and quantities
            if (!CubeInputsEqual(a?.Inputs, b?.Inputs)) return true;
            if (!CubeOutputsEqual(a?.Outputs, b?.Outputs)) return true;
            // Compare flattened property string sets
            if (!PropertyStringsEqual(a?.Properties, b?.Properties)) return true;
            return false;
        }

        private static bool PropertyStringsEqual(IEnumerable<string> a, IEnumerable<string> b)
        {
            a = a ?? Enumerable.Empty<string>();
            b = b ?? Enumerable.Empty<string>();
            // Use case-insensitive comparison and ignore trivial whitespace diffs
            var sa = new HashSet<string>(a.Select(s => (s ?? string.Empty).Trim()), StringComparer.OrdinalIgnoreCase);
            var sb = new HashSet<string>(b.Select(s => (s ?? string.Empty).Trim()), StringComparer.OrdinalIgnoreCase);
            if (sa.Count != sb.Count) return false;
            foreach (var s in sa) if (!sb.Contains(s)) return false;
            return true;
        }

        private static bool CubeInputsEqual(IEnumerable<JsonModelLoader.CubeIngredientSimple> a, IEnumerable<JsonModelLoader.CubeIngredientSimple> b)
        {
            a = a ?? Enumerable.Empty<JsonModelLoader.CubeIngredientSimple>();
            b = b ?? Enumerable.Empty<JsonModelLoader.CubeIngredientSimple>();
            var mapA = a.GroupBy(CanonIngKey).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity), StringComparer.OrdinalIgnoreCase);
            var mapB = b.GroupBy(CanonIngKey).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity), StringComparer.OrdinalIgnoreCase);
            if (mapA.Count != mapB.Count) return false;
            foreach (var kv in mapA)
            {
                if (!mapB.TryGetValue(kv.Key, out var q) || q != kv.Value) return false;
            }
            return true;
        }

        private static bool CubeOutputsEqual(IEnumerable<JsonModelLoader.CubeOutputSimple> a, IEnumerable<JsonModelLoader.CubeOutputSimple> b)
        {
            a = a ?? Enumerable.Empty<JsonModelLoader.CubeOutputSimple>();
            b = b ?? Enumerable.Empty<JsonModelLoader.CubeOutputSimple>();
            var mapA = a.GroupBy(x => (x.Name ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity), StringComparer.OrdinalIgnoreCase);
            var mapB = b.GroupBy(x => (x.Name ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity), StringComparer.OrdinalIgnoreCase);
            if (mapA.Count != mapB.Count) return false;
            foreach (var kv in mapA)
            {
                if (!mapB.TryGetValue(kv.Key, out var q) || q != kv.Value) return false;
            }
            return true;
        }

        private static string CanonIngKey(JsonModelLoader.CubeIngredientSimple ing)
        {
            var name = (ing?.Name ?? string.Empty).Trim().ToLowerInvariant();
            var q = string.Join("|", (ing?.Qualifiers ?? new List<string>()).Select(s => (s ?? string.Empty).Trim().ToLowerInvariant()).Where(s => s.Length > 0).OrderBy(s => s, StringComparer.Ordinal));
            return $"{name}|{q}";
        }

        private static CubeRecipeChange CubeFromBoth(JsonModelLoader.CubeRecipeSimple oldC, JsonModelLoader.CubeRecipeSimple newC)
        {
            var ch = new CubeRecipeChange
            {
                Name = (newC ?? oldC)?.Key,
                DescriptionBefore = oldC?.Description,
                DescriptionAfter = newC?.Description,
                NotesBefore = oldC?.Notes,
                NotesAfter = newC?.Notes
            };

            // Inputs
            var a = oldC?.Inputs ?? new List<JsonModelLoader.CubeIngredientSimple>();
            var b = newC?.Inputs ?? new List<JsonModelLoader.CubeIngredientSimple>();
            var mapA = a.GroupBy(CanonIngKey).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity), StringComparer.OrdinalIgnoreCase);
            var mapB = b.GroupBy(CanonIngKey).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity), StringComparer.OrdinalIgnoreCase);
            var keys = new HashSet<string>(mapA.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var k in mapB.Keys)
            {
                keys.Add(k);
            }
            foreach (var k in keys)
            {
                mapA.TryGetValue(k, out var qa);
                mapB.TryGetValue(k, out var qb);
                if (qa == 0 && qb > 0)
                {
                    ch.InputsAdded.Add(RenderIng(b.FirstOrDefault(x => CanonIngKey(x) == k), qb));
                }
                else if (qb == 0 && qa > 0)
                {
                    ch.InputsRemoved.Add(RenderIng(a.FirstOrDefault(x => CanonIngKey(x) == k), qa));
                }
                else if (qa != qb)
                {
                    var before = RenderIng(a.FirstOrDefault(x => CanonIngKey(x) == k), qa);
                    var after = RenderIng(b.FirstOrDefault(x => CanonIngKey(x) == k), qb);
                    ch.InputsChanged.Add(new PropertyDelta { Before = before, After = after });
                }
            }

            // Outputs
            var oa = oldC?.Outputs ?? new List<JsonModelLoader.CubeOutputSimple>();
            var ob = newC?.Outputs ?? new List<JsonModelLoader.CubeOutputSimple>();
            var mA = oa.GroupBy(x => (x.Name ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity), StringComparer.OrdinalIgnoreCase);
            var mB = ob.GroupBy(x => (x.Name ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity), StringComparer.OrdinalIgnoreCase);
            var okeys = new HashSet<string>(mA.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var k in mB.Keys)
            {
                okeys.Add(k);
            }
            foreach (var k in okeys)
            {
                mA.TryGetValue(k, out var qa);
                mB.TryGetValue(k, out var qb);
                if (qa == 0 && qb > 0)
                {
                    ch.OutputsAdded.Add(RenderOut(ob.FirstOrDefault(x => string.Equals((x.Name ?? string.Empty).Trim(), k, StringComparison.OrdinalIgnoreCase)), qb));
                }
                else if (qb == 0 && qa > 0)
                {
                    ch.OutputsRemoved.Add(RenderOut(oa.FirstOrDefault(x => string.Equals((x.Name ?? string.Empty).Trim(), k, StringComparison.OrdinalIgnoreCase)), qa));
                }
                else if (qa != qb)
                {
                    var before = RenderOut(oa.FirstOrDefault(x => string.Equals((x.Name ?? string.Empty).Trim(), k, StringComparison.OrdinalIgnoreCase)), qa);
                    var after = RenderOut(ob.FirstOrDefault(x => string.Equals((x.Name ?? string.Empty).Trim(), k, StringComparison.OrdinalIgnoreCase)), qb);
                    ch.OutputsChanged.Add(new PropertyDelta { Before = before, After = after });
                }
            }

            // Properties (flattened property strings across outputs)
            DiffStringUtil.ComputeStringDiff(oldC?.Properties, newC?.Properties, out var propAdd, out var propRem, out var propChg);
            ch.PropertiesAdded = propAdd;
            ch.PropertiesRemoved = propRem;
            ch.PropertiesChanged = propChg;

            return ch;
        }

        private static string RenderIng(JsonModelLoader.CubeIngredientSimple ing, int totalQty)
        {
            if (ing == null) return totalQty > 1 ? $"{totalQty}x ?" : "?";
            var clone = new JsonModelLoader.CubeIngredientSimple
            {
                Name = ing.Name,
                Qualifiers = ing.Qualifiers ?? new List<string>(),
                Quantity = totalQty
            };
            return clone.ToString();
        }

        private static string RenderOut(JsonModelLoader.CubeOutputSimple o, int totalQty)
        {
            if (o == null) return totalQty > 1 ? $"{totalQty}x ?" : "?";
            var clone = new JsonModelLoader.CubeOutputSimple { Name = o.Name, Quantity = totalQty };
            return clone.ToString();
        }
    }

    public interface IName { string Name { get; } }

    public sealed class UniqueChange : IName
    {
        public string Name { get; set; }
        public string BaseCode { get; set; }
        public string BaseCodeBefore { get; set; }
        public string BaseCodeAfter { get; set; }
        // Item type from JSON export (e.g., Axe, Sword, Ring)
        public string TypeBefore { get; set; }
        public string TypeAfter { get; set; }
        public bool? EnabledBefore { get; set; }
        public bool? EnabledAfter { get; set; }
        public int? ItemLevelBefore { get; set; }
        public int? ItemLevelAfter { get; set; }
        public int? ReqLevelBefore { get; set; }
        public int? ReqLevelAfter { get; set; }
        public int? RarityBefore { get; set; }
        public int? RarityAfter { get; set; }
        public List<string> PropertiesAdded { get; set; } = new List<string>();
        public List<string> PropertiesRemoved { get; set; } = new List<string>();
        public List<PropertyDelta> PropertiesChanged { get; set; } = new List<PropertyDelta>();

        public static UniqueChange FromNew(JsonModelLoader.UniqueSimple u)
        {
            return FromBoth(null, u);
        }
        public static UniqueChange FromOld(JsonModelLoader.UniqueSimple u)
        {
            return FromBoth(u, null);
        }
        public static UniqueChange FromBoth(JsonModelLoader.UniqueSimple oldU, JsonModelLoader.UniqueSimple newU)
        {
            var ch = new UniqueChange
            {
                Name = (newU ?? oldU)?.Index,
                BaseCode = (newU ?? oldU)?.Code,
                BaseCodeBefore = oldU?.Code,
                BaseCodeAfter = newU?.Code,
                TypeBefore = oldU?.Type,
                TypeAfter = newU?.Type,
                EnabledBefore = oldU?.Enabled,
                EnabledAfter = newU?.Enabled,
                ItemLevelBefore = oldU?.ItemLevel,
                ItemLevelAfter = newU?.ItemLevel,
                ReqLevelBefore = oldU?.RequiredLevel,
                ReqLevelAfter = newU?.RequiredLevel,
                RarityBefore = oldU?.Rarity,
                RarityAfter = newU?.Rarity
            };

            DiffStringUtil.ComputeStringDiff(oldU?.Properties, newU?.Properties, out var added, out var removed, out var changed);
            ch.PropertiesAdded = added.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            ch.PropertiesRemoved = removed.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            ch.PropertiesChanged = changed
                .OrderBy(p => p.After ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return ch;
        }
    }

    public sealed class SetChange : IName
    {
        public string Name { get; set; }
        public int? LevelBefore { get; set; }
        public int? LevelAfter { get; set; }
        public List<string> PartialAdded { get; set; } = new List<string>();
        public List<string> PartialRemoved { get; set; } = new List<string>();
        public List<PropertyDelta> PartialChanged { get; set; } = new List<PropertyDelta>();
        public List<string> FullAdded { get; set; } = new List<string>();
        public List<string> FullRemoved { get; set; } = new List<string>();
        public List<PropertyDelta> FullChanged { get; set; } = new List<PropertyDelta>();
        public List<SetItemChange> ItemsAdded { get; set; } = new List<SetItemChange>();
        public List<SetItemChange> ItemsRemoved { get; set; } = new List<SetItemChange>();
        public List<SetItemChange> ItemsModified { get; set; } = new List<SetItemChange>();

        public static SetChange FromNew(JsonModelLoader.SetSimple s) => FromBoth(null, s);
        public static SetChange FromOld(JsonModelLoader.SetSimple s) => FromBoth(s, null);
        public static SetChange FromBoth(JsonModelLoader.SetSimple oldS, JsonModelLoader.SetSimple newS)
        {
            var ch = new SetChange
            {
                Name = (newS ?? oldS)?.Index,
                LevelBefore = oldS?.Level,
                LevelAfter = newS?.Level
            };

            DiffStringUtil.ComputeStringDiff(oldS?.PartialProperties, newS?.PartialProperties,
                out var pAdd, out var pRem, out var pChg);
            ch.PartialAdded = pAdd.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            ch.PartialRemoved = pRem.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            ch.PartialChanged = pChg
                .OrderBy(x => x.After ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            DiffStringUtil.ComputeStringDiff(oldS?.FullProperties, newS?.FullProperties,
                out var fAdd, out var fRem, out var fChg);
            ch.FullAdded = fAdd.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            ch.FullRemoved = fRem.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            ch.FullChanged = fChg
                .OrderBy(x => x.After ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Items
            // Pair items by a stable key to avoid false add/remove on simple rename/spelling changes.
            // Prefer stable `Index`; fall back to Base `Code` only when `Index` is missing.
            // Important: Some sets may contain duplicate keys (same Code/Index used for multiple pieces).
            // We must not use ToDictionary here; instead, group and pair by position within each key group.
            string KeyOf(JsonModelLoader.SetItemSimple it)
            {
                if (it == null) return string.Empty;
                // Primary identity is item Index
                var key = !string.IsNullOrWhiteSpace(it.Index) ? it.Index : (it.Code ?? string.Empty);
                return key?.Trim() ?? string.Empty;
            }

            System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<JsonModelLoader.SetItemSimple>> GroupByKey(System.Collections.Generic.IEnumerable<JsonModelLoader.SetItemSimple> src)
            {
                var map = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<JsonModelLoader.SetItemSimple>>(StringComparer.OrdinalIgnoreCase);
                foreach (var it in src ?? Enumerable.Empty<JsonModelLoader.SetItemSimple>())
                {
                    var k = KeyOf(it);
                    if (!map.TryGetValue(k, out var list))
                    {
                        list = new System.Collections.Generic.List<JsonModelLoader.SetItemSimple>();
                        map[k] = list;
                    }
                    list.Add(it);
                }
                return map;
            }

            var aGroups = GroupByKey(oldS?.SetItems);
            var bGroups = GroupByKey(newS?.SetItems);
            var keySet = new HashSet<string>(aGroups.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var k in bGroups.Keys)
            {
                keySet.Add(k);
            }

            foreach (var k in keySet)
            {
                aGroups.TryGetValue(k, out var aList);
                bGroups.TryGetValue(k, out var bList);
                aList = aList ?? new System.Collections.Generic.List<JsonModelLoader.SetItemSimple>();
                bList = bList ?? new System.Collections.Generic.List<JsonModelLoader.SetItemSimple>();

                // Pair by index within the key group
                var pairs = Math.Min(aList.Count, bList.Count);
                for (int i = 0; i < pairs; i++)
                {
                    var ai = aList[i];
                    var bi = bList[i];
                    if (AreSetItemDifferent(ai, bi))
                    {
                        ch.ItemsModified.Add(SetItemChange.FromBoth(ai, bi));
                    }
                }
                // Leftovers are added/removed
                for (int i = pairs; i < aList.Count; i++)
                {
                    ch.ItemsRemoved.Add(SetItemChange.FromOld(aList[i]));
                }
                for (int i = pairs; i < bList.Count; i++)
                {
                    ch.ItemsAdded.Add(SetItemChange.FromNew(bList[i]));
                }
            }

            return ch;
        }

        private static bool AreSetItemDifferent(JsonModelLoader.SetItemSimple a, JsonModelLoader.SetItemSimple b)
        {
            if (a == null && b != null) return true;
            if (a != null && b == null) return true;
            if (a == null && b == null) return false;
            // Consider base (code) change as a modification
            if (!string.Equals(a.Code ?? string.Empty, b.Code ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return true;
            if (a.Enabled != b.Enabled) return true;
            if (a.ItemLevel != b.ItemLevel) return true;
            if (a.RequiredLevel != b.RequiredLevel) return true;
            if (a.Rarity != b.Rarity) return true;
            var aProps = new HashSet<string>(a.Properties ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var bProps = new HashSet<string>(b.Properties ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            if (!aProps.SetEquals(bProps)) return true;
            var aSetProps = new HashSet<string>(a.SetPropertiesString ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var bSetProps = new HashSet<string>(b.SetPropertiesString ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            if (!aSetProps.SetEquals(bSetProps)) return true;
            return false;
        }

    }

    public sealed class PropertyDelta
    {
        public string Before { get; set; }
        public string After { get; set; }
    }

    public sealed class SetItemChange : IName
    {
        public string Name { get; set; }
        public string BaseCode { get; set; }
        public string BaseCodeBefore { get; set; }
        public string BaseCodeAfter { get; set; }
        // Item type from JSON export
        public string TypeBefore { get; set; }
        public string TypeAfter { get; set; }
        public bool? EnabledBefore { get; set; }
        public bool? EnabledAfter { get; set; }
        public int? ItemLevelBefore { get; set; }
        public int? ItemLevelAfter { get; set; }
        public int? ReqLevelBefore { get; set; }
        public int? ReqLevelAfter { get; set; }
        public int? RarityBefore { get; set; }
        public int? RarityAfter { get; set; }
        public List<string> PropertiesAdded { get; set; } = new List<string>();
        public List<string> PropertiesRemoved { get; set; } = new List<string>();
        public List<PropertyDelta> PropertiesChanged { get; set; } = new List<PropertyDelta>();
        public List<string> SetPropsAdded { get; set; } = new List<string>();
        public List<string> SetPropsRemoved { get; set; } = new List<string>();
        public List<PropertyDelta> SetPropsChanged { get; set; } = new List<PropertyDelta>();

        public static SetItemChange FromNew(JsonModelLoader.SetItemSimple s) => FromBoth(null, s);
        public static SetItemChange FromOld(JsonModelLoader.SetItemSimple s) => FromBoth(s, null);
        public static SetItemChange FromBoth(JsonModelLoader.SetItemSimple oldI, JsonModelLoader.SetItemSimple newI)
        {
            var ch = new SetItemChange
            {
                Name = (newI ?? oldI)?.Index,
                BaseCode = (newI ?? oldI)?.Code,
                BaseCodeBefore = oldI?.Code,
                BaseCodeAfter = newI?.Code,
                TypeBefore = oldI?.Type,
                TypeAfter = newI?.Type,
                EnabledBefore = oldI?.Enabled,
                EnabledAfter = newI?.Enabled,
                ItemLevelBefore = oldI?.ItemLevel,
                ItemLevelAfter = newI?.ItemLevel,
                ReqLevelBefore = oldI?.RequiredLevel,
                ReqLevelAfter = newI?.RequiredLevel,
                RarityBefore = oldI?.Rarity,
                RarityAfter = newI?.Rarity
            };

            DiffStringUtil.ComputeStringDiff(oldI?.Properties, newI?.Properties, out var add, out var rem, out var chg);
            ch.PropertiesAdded = add.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            ch.PropertiesRemoved = rem.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            ch.PropertiesChanged = chg
                .OrderBy(x => x.After ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            DiffStringUtil.ComputeStringDiff(oldI?.SetPropertiesString, newI?.SetPropertiesString, out var sAdd, out var sRem, out var sChg);
            ch.SetPropsAdded = sAdd.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            ch.SetPropsRemoved = sRem.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            ch.SetPropsChanged = sChg
                .OrderBy(x => x.After ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return ch;
        }
    }

    public sealed class RunewordChange : IName
    {
        public string Name { get; set; }
        public bool? EnabledBefore { get; set; }
        public bool? EnabledAfter { get; set; }
        public int? ItemLevelBefore { get; set; }
        public int? ItemLevelAfter { get; set; }
        public int? ReqLevelBefore { get; set; }
        public int? ReqLevelAfter { get; set; }
        public List<string> PropertiesAdded { get; set; } = new List<string>();
        public List<string> PropertiesRemoved { get; set; } = new List<string>();
        public List<PropertyDelta> PropertiesChanged { get; set; } = new List<PropertyDelta>();

        public static RunewordChange FromNew(JsonModelLoader.RunewordSimple r) => FromBoth(null, r);
        public static RunewordChange FromOld(JsonModelLoader.RunewordSimple r) => FromBoth(r, null);
        public static RunewordChange FromBoth(JsonModelLoader.RunewordSimple oldR, JsonModelLoader.RunewordSimple newR)
        {
            var ch = new RunewordChange
            {
                Name = (newR ?? oldR)?.Index,
                EnabledBefore = oldR?.Enabled,
                EnabledAfter = newR?.Enabled,
                ItemLevelBefore = oldR?.ItemLevel,
                ItemLevelAfter = newR?.ItemLevel,
                ReqLevelBefore = oldR?.RequiredLevel,
                ReqLevelAfter = newR?.RequiredLevel
            };
            DiffStringUtil.ComputeStringDiff(oldR?.Properties, newR?.Properties, out var add, out var rem, out var chg);
            ch.PropertiesAdded = add.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            ch.PropertiesRemoved = rem.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            ch.PropertiesChanged = chg
                .OrderBy(x => x.After ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return ch;
        }
    }

    internal static class DiffStringUtil
    {
        public static string NormalizeProp(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var x = s.Trim().ToLowerInvariant();
            x = x.Replace(" when struck", " on ctc")
                 .Replace(" on striking", " on ctc")
                 .Replace(" when you die", " on ctc")
                 .Replace(" on attack", " on ctc")
                 .Replace(" on kill", " on ctc")
                 .Replace(" on level-up", " on ctc");
            x = System.Text.RegularExpressions.Regex.Replace(x, @"[\+\-]", " ");
            x = System.Text.RegularExpressions.Regex.Replace(x, @"\d+(\.\d+)?", " ");
            x = x.Replace("%", " ");
            x = System.Text.RegularExpressions.Regex.Replace(x, @"\s+", " ").Trim();
            return x;
        }

        public static void ComputeStringDiff(
            System.Collections.Generic.IEnumerable<string> oldList,
            System.Collections.Generic.IEnumerable<string> newList,
            out System.Collections.Generic.List<string> added,
            out System.Collections.Generic.List<string> removed,
            out System.Collections.Generic.List<PropertyDelta> changed)
        {
            added = new System.Collections.Generic.List<string>();
            removed = new System.Collections.Generic.List<string>();
            changed = new System.Collections.Generic.List<PropertyDelta>();

            var oldItems = (oldList ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            var newItems = (newList ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            var oldBuckets = new Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in oldItems)
            {
                var k = NormalizeProp(s);
                if (!oldBuckets.TryGetValue(k, out var list))
                {
                    list = new System.Collections.Generic.List<string>();
                    oldBuckets[k] = list;
                }
                list.Add(s);
            }
            var newBuckets = new Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in newItems)
            {
                var k = NormalizeProp(s);
                if (!newBuckets.TryGetValue(k, out var list))
                {
                    list = new System.Collections.Generic.List<string>();
                    newBuckets[k] = list;
                }
                list.Add(s);
            }

            var keys = new HashSet<string>(oldBuckets.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var k in newBuckets.Keys) { keys.Add(k); }

            foreach (var k in keys)
            {
                oldBuckets.TryGetValue(k, out var aL);
                newBuckets.TryGetValue(k, out var bL);
                aL = aL ?? new System.Collections.Generic.List<string>();
                bL = bL ?? new System.Collections.Generic.List<string>();

                var pairs = Math.Min(aL.Count, bL.Count);
                for (int i = 0; i < pairs; i++)
                {
                    var a = aL[i];
                    var b = bL[i];
                    if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                    {
                        changed.Add(new PropertyDelta { Before = a, After = b });
                    }
                }
                for (int i = pairs; i < aL.Count; i++) { removed.Add(aL[i]); }
                for (int i = pairs; i < bL.Count; i++) { added.Add(bL[i]); }
            }
        }
    }
}
