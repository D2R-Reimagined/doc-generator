using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using D2TxtImporter.lib.Exceptions;
using D2TxtImporter.lib.Model.Dictionaries;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Types
{
    public class ItemProperty
    {
        [JsonIgnore]
        public static ItemProperty CurrentItemProperty { get; set; }
        [JsonIgnore]
        public EffectProperty Property { get; set; }
        [JsonIgnore]
        public string Parameter { get; set; }
        [JsonIgnore]
        public int? Min { get; set; }
        [JsonIgnore]
        public int? Max { get; set; }
        [JsonIgnore]
        public ItemStatCost ItemStatCost { get; set; }
        public bool ShouldSerializePropertyString() => GroupProperties == null;
        public bool ShouldSerializeIndex() => GroupProperties == null;
        private string _propertyString;
        public string PropertyString { get => _propertyString + Suffix; private set => _propertyString = value; }
        public int Index { get; set; }
        [JsonIgnore]
        public int ItemLevel { get; set; }
        [JsonIgnore]
        public string Suffix { get; set; }
        [JsonProperty("group-properties", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, List<ItemProperty>> GroupProperties { get; set; }
        [JsonIgnore]
        public string CompareKey => (ItemStatCost != null ? ItemStatCost.Stat : Property?.Code) + Parameter;

        public ItemProperty(string property, string parameter, int? min, int? max, int index, int itemLevel = 0, string suffix = "")
        {
            CurrentItemProperty = this;
            Parameter = parameter;

            // Intercept 'skill' property and convert numeric ID to name
            if (property.Equals("skill", StringComparison.OrdinalIgnoreCase) && int.TryParse(Parameter, out _))
            {
                Parameter = Skill.GetSkill(Parameter).Name;
            }

            Min = min;
            Max = max;
            Index = index;
            ItemLevel = itemLevel;
            Suffix = suffix;

            // Check if it's a group property from propertygroups.txt
            if (PropertyGroup.PropertyGroups != null && PropertyGroup.PropertyGroups.TryGetValue(property, out var groupDef))
            {
                GroupProperties = new Dictionary<string, List<ItemProperty>>(StringComparer.OrdinalIgnoreCase);
                GroupProperties[property] = GetProperties(groupDef.PropertyInfos, itemLevel);
                Property = new EffectProperty { Code = property };
                _propertyString = property;
                return;
            }

            // Look up the property code as-is; dictionary is case-insensitive
            if (!EffectProperty.EffectProperties.ContainsKey(property)) 
            {
                throw ItemPropertyException.Create($"Could not find property '{property}' parameter '{parameter}' min '{min}' max '{max}' index '{index}' itemlvl '{itemLevel}' in Properties.txt");
            }

            Property = EffectProperty.EffectProperties[property];

            var propStat = Property.Stat;
            var propCode = Property.Code;

            if (string.IsNullOrEmpty(propStat)) 
            {
                // Fix wrongly spelled and hardcoded stats
                switch (Property.Code) 
                {
                    case "str":
                        propStat = "strength";
                        break;

                    case "dmg-min":
                        propStat = "mindamage";
                        break;

                    case "dmg-max":
                        propStat = "maxdamage";
                        break;

                    case "indestruct":
                        propStat = "item_indesctructible";
                        break;

                    case "poisonlength":
                        Min = null;
                        Max = null;
                        break;

                    default:
                        propStat = propCode;
                        break;
                }
            }

            // Fix all class skills displaying as amazon and random class skills to show correct value
            if (propStat == "item_addclassskills") 
            {
                if (propCode == "randclassskill")
                {
                    Min = int.TryParse(Parameter, out var parsed) ? parsed : 0;
                    Max = Min;
                }

                Parameter = Property.Code;
            }

            // Fix random skill tabs all showing as Amazon
            if (propStat == "item_addskill_tab")
            {
                if (propCode == "tab-rand")
                {
                    // 0–2 Amazon, 8–10 Sorceress, 16–18 Necro, 24–26 Paladin,
                    // 32–34 Barbarian, 40–42 Druid, 48–50 Assassin
                    var parValue = int.TryParse(Parameter, out var parsed) ? parsed : 0;

                    // Convert the absolute index into a 0-based tab index
                    var tabIndex = (Min.GetValueOrDefault() / 8); // 0..6

                    // Lookup by class order
                    var tabs = new[] { "ama", "sor", "nec", "pal", "bar", "dru", "ass" };

                    if ((uint)tabIndex < (uint)tabs.Length)
                    {
                        Parameter = tabs[tabIndex];
                        Min = Max = parValue;
                    }
                }
            }
            
            // Check hash list for manually set stats
            if (ManualStatCodes.Contains(propCode)) 
            { 
                propStat = Property.Code; 
            }

            if (!ItemStatCost.ItemStatCosts.ContainsKey(propStat))
            {
                throw ItemPropertyException.Create($"Could not find stat '{propStat}' in ItemStatCost.txt");
            }

            ItemStatCost = ItemStatCost.ItemStatCosts[propStat];

            try
            {
                PropertyString = ItemStatCost.PropertyString(Min, Max, Parameter, itemLevel);
            }
            catch (Exception e) 
            {
                if (!(e is ItemStatCostException)) 
                {
                    throw ItemPropertyException.Create($"Could not generate properties for property '{property}' with parameter '{parameter}' min '{min}' max '{max}' index '{index}' itemlvl '{itemLevel}'", e);
                }

                throw;
            }
        }
        
        public ItemProperty(ItemProperty itemProperty)
        {
            CurrentItemProperty = this;
            Property = itemProperty.Property;
            Parameter = itemProperty.Parameter;
            Min = itemProperty.Min;
            Max = itemProperty.Max;
            ItemStatCost = itemProperty.ItemStatCost;
            PropertyString = itemProperty.PropertyString;
            Index = itemProperty.Index;
        }

        public static List<ItemProperty> GetProperties(List<PropertyInfo> properties, int itemLevel = 0)
        {
            var result = new List<ItemProperty>(properties?.Count ?? 0);
            if (properties == null || properties.Count == 0)
                return result;

            // Normalize once (preserve original casing; index map is case-insensitive)
            int n = properties.Count;
            var code = new string[n];
            var param = new string[n];
            var min = new int?[n];
            var max = new int?[n];
            for (int i = 0; i < n; i++)
            {
                var p = properties[i];
                var c = (p?.Property ?? string.Empty).Trim();
                code[i] = c;
                param[i] = p?.Parameter;
                min[i] = p?.Min;
                max[i] = p?.Max;
            }

            // Index by code (first occurrences are enough to mirror previous FindIndex semantics)
            var idxByCode = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            void AddIdx(string k, int i)
            {
                if (!idxByCode.TryGetValue(k, out var lst)) { lst = new List<int>(1); idxByCode[k] = lst; }
                lst.Add(i);
            }
            for (int i = 0; i < n; i++)
            {
                if (!string.IsNullOrEmpty(code[i]))
                    AddIdx(code[i], i);
            }

            bool TryFirst(string k, out int idx)
            {
                if (idxByCode.TryGetValue(k, out var lst) && lst.Count > 0)
                { idx = lst[0]; return true; }
                idx = -1; return false;
            }

            string GetPoisLenParam(PropertyInfo lenProp)
            {
                if (lenProp == null) return string.Empty;
                if (!string.IsNullOrEmpty(lenProp.Parameter) && int.TryParse(lenProp.Parameter, out _))
                    return lenProp.Parameter;
                if (lenProp.Min.HasValue) return lenProp.Min.Value.ToString(CultureInfo.InvariantCulture);
                if (lenProp.Max.HasValue) return lenProp.Max.Value.ToString(CultureInfo.InvariantCulture);
                return string.Empty;
            }

            var consumed = new bool[n];
            var aggregatesAt = new Dictionary<int, List<ItemProperty>>();
            void AddAgg(int atIndex, ItemProperty ip)
            {
                if (!IgnoredProperties.Contains(ip.Property.Code))
                {
                    if (!aggregatesAt.TryGetValue(atIndex, out var lst))
                    { lst = new List<ItemProperty>(1); aggregatesAt[atIndex] = lst; }
                    lst.Add(ip);
                }
            }

            // Cold: aggregate only when all three are present; drop orphaned len
            if (TryFirst("cold-min", out var iCmin) & TryFirst("cold-max", out var iCmax) & TryFirst("cold-len", out var iClen))
            {
                bool hasLower = min[iCmin].HasValue;
                bool hasUpper = max[iCmax].HasValue;
                if (hasLower && hasUpper)
                {
                    int at = Math.Min(iClen, Math.Min(iCmin, iCmax));
                    var ip = new ItemProperty("dmg-cold", string.Empty, min[iCmin], max[iCmax], at, itemLevel);
                    AddAgg(at, ip);
                    consumed[iCmin] = consumed[iCmax] = consumed[iClen] = true;
                }
                else
                {
                    // Keep originals, drop len
                    consumed[iClen] = true;
                }
            }
            else if (TryFirst("cold-len", out var iColdLenOnly))
            {
                // Orphaned len: drop
                consumed[iColdLenOnly] = true;
            }

            // Poison: triplet and pair logic
            bool hasPmin = TryFirst("pois-min", out var iPmin);
            bool hasPmax = TryFirst("pois-max", out var iPmax);
            bool hasPlen = TryFirst("pois-len", out var iPlen);
            if (hasPlen)
            {
                var lenProp = properties[iPlen];
                var lenParam = GetPoisLenParam(lenProp);
                if (hasPmin && hasPmax)
                {
                    bool lower = min[iPmin].HasValue; bool upper = max[iPmax].HasValue;
                    if (lower && upper)
                    {
                        int at = Math.Min(iPlen, Math.Min(iPmin, iPmax));
                        var ip = new ItemProperty("dmg-pois", lenParam, min[iPmin], max[iPmax], at, itemLevel);
                        AddAgg(at, ip);
                        consumed[iPmin] = consumed[iPmax] = consumed[iPlen] = true;
                    }
                    else
                    {
                        // Cannot form proper range; drop len only
                        consumed[iPlen] = true;
                    }
                }
                else if (hasPmin)
                {
                    // Keep pois-min; add aggregate with min's endpoints; consume len
                    bool hasAny = min[iPmin].HasValue || max[iPmin].HasValue;
                    if (hasAny)
                    {
                        int at = Math.Min(iPlen, iPmin);
                        var ip = new ItemProperty("dmg-pois", lenParam, min[iPmin], max[iPmin], at, itemLevel);
                        AddAgg(at, ip);
                    }
                    consumed[iPlen] = true;
                }
                else if (hasPmax)
                {
                    // Keep pois-max; add aggregate with max's endpoints; consume len
                    bool hasAny = min[iPmax].HasValue || max[iPmax].HasValue;
                    if (hasAny)
                    {
                        int at = Math.Min(iPlen, iPmax);
                        var ip = new ItemProperty("dmg-pois", lenParam, min[iPmax], max[iPmax], at, itemLevel);
                        AddAgg(at, ip);
                    }
                    consumed[iPlen] = true;
                }
                else
                {
                    // Orphaned len: drop
                    consumed[iPlen] = true;
                }
            }

            // Build result in a single pass. After finishing index i, append aggregates whose source index <= i.
            var aggKeys = aggregatesAt.Keys.OrderBy(x => x).ToArray();
            int aggPtr = 0;
            for (int i = 0; i < n; i++)
            {
                if (!consumed[i])
                {
                    var p = properties[i];
                    if (string.IsNullOrEmpty(p.Property) || p.Property.StartsWith("*"))
                    {
                        // Do not add, but still consider flushing aggregates for this index
                    }
                    else
                    {
                        var cLower = code[i];
                        if (string.Equals(cLower, "dmg-elem", StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var elem in new[] { "dmg-fire", "dmg-cold", "dmg-ltng" })
                            {
                                var ip = new ItemProperty(elem, param[i], min[i], max[i], i, itemLevel);
                                if (!IgnoredProperties.Contains(ip.Property.Code))
                                    result.Add(ip);
                            }
                        }
                        else
                        {
                            var ip = new ItemProperty(p.Property, p.Parameter, p.Min, p.Max, i, itemLevel);
                            if (!IgnoredProperties.Contains(ip.Property.Code))
                                result.Add(ip);
                        }
                    }
                }

                // Flush aggregates whose at-index is <= current i
                while (aggPtr < aggKeys.Length && aggKeys[aggPtr] <= i)
                {
                    foreach (var ip in aggregatesAt[aggKeys[aggPtr]])
                    {
                        result.Add(ip);
                    }
                    aggPtr++;
                }
            }

            // Flush any remaining aggregates (with at-index greater than any original index)
            while (aggPtr < aggKeys.Length)
            {
                foreach (var ip in aggregatesAt[aggKeys[aggPtr]])
                {
                    result.Add(ip);
                }
                aggPtr++;
            }

            return result;
        }

        public static void CleanupDublicates(List<ItemProperty> properties) 
        {
            if (properties == null || properties.Count == 0)
                return;

            var baseProps = properties.Where(x => string.IsNullOrEmpty(x.Suffix)).ToList();
            var suffixedProps = properties.Where(x => !string.IsNullOrEmpty(x.Suffix)).ToList();

            var duplicateBaseGroups = baseProps
                .GroupBy(x => x.CompareKey)
                .Where(g => g.Skip(1).Any());

            foreach (var group in duplicateBaseGroups) 
            {
                var merged = MergeItemPropertyGroup(group, transformWeaponMinText: true);

                baseProps.RemoveAll(x =>
                    x.ItemStatCost != null &&
                    merged.ItemStatCost != null &&
                    x.ItemStatCost.Stat == merged.ItemStatCost.Stat);

                baseProps.Add(merged);
            }

            List<ItemProperty> MergeDuplicatesByPropertyString(IEnumerable<ItemProperty> source) 
            {
                return source
                    .GroupBy(x =>
                        Regex.Replace(x.PropertyString ?? string.Empty,
                                @"-?\d+(\s*-\s*\d+)?",
                                string.Empty)
                            .Trim())
                    .Select(g => MergeItemPropertyGroup(g, transformWeaponMinText: true))
                    .OrderBy(x => x.PropertyString)
                    .ToList();
            }

            var weaponProps = MergeDuplicatesByPropertyString(
                suffixedProps.Where(x => x.Suffix.Trim() == "(Weapon)"));

            var armorProps = MergeDuplicatesByPropertyString(
                suffixedProps.Where(x => x.Suffix.Trim() == "(Armor)"));

            var shieldProps = MergeDuplicatesByPropertyString(
                suffixedProps.Where(x => x.Suffix.Trim() == "(Shield)"));

            var otherProps = MergeDuplicatesByPropertyString(
                suffixedProps.Where(x =>
                {
                    var suffix = x.Suffix.Trim();
                    return suffix != "(Weapon)" &&
                           suffix != "(Armor)" &&
                           suffix != "(Shield)";
                }));

            properties.Clear();

            properties.AddRange(
                baseProps.OrderByDescending(x =>
                    x.ItemStatCost?.DescriptionPriority ?? 0));

            properties.AddRange(weaponProps);
            properties.AddRange(armorProps);
            properties.AddRange(shieldProps);
            properties.AddRange(otherProps);
        }

        private static ItemProperty MergeItemPropertyGroup(
            IEnumerable<ItemProperty> group,
            bool transformWeaponMinText) 
        {

            var props = group.ToList();
            var first = props[0];
            int? min = props.Sum(p => p.Min ?? 0);
            int? max = props.Sum(p => p.Max ?? 0);

            var merged = new ItemProperty(
                first.Property.Code,
                first.Parameter,
                min,
                max,
                first.Index,
                first.ItemLevel,
                first.Suffix
            );

            if (transformWeaponMinText &&
                !string.IsNullOrEmpty(merged.PropertyString) &&
                merged.PropertyString.Contains("+") &&
                merged.PropertyString.Contains("to") &&
                merged.PropertyString.Contains("Minimum "))
            {
                merged.PropertyString = merged.PropertyString
                    .Replace("+", "Adds ")
                    .Replace("Minimum ", string.Empty)
                    .Replace(" (Weapon)", string.Empty);
            }

            return merged;
        }
        
        private static readonly HashSet<string> IgnoredProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { 
            "state", 
            "bloody", 
            "oskill_hide", 
            "dmg-throw",
            "uberstone-property", 
            "ubertorch-property"
        };
        
        private static readonly HashSet<string> ManualStatCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "res-all",
            "res-all-max",
            "all-stats",
            "fireskill",
            "coldskill",
            "lightningskill",
            "poisonskill",
            "magicskill",
            "extra-elem",
            "pierce-elem",
            "dmg-pois",
            "dmg-norm",
            "dmg-fire",
            "dmg-cold",
            "dmg-ltng",
            "dmg-mag"
        };

        public override string ToString() 
        {
            return Property.ToString();
        }
    }
}
