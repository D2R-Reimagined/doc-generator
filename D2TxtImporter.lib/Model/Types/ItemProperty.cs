using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using D2TxtImporter.lib.Exceptions;
using D2TxtImporter.lib.Model.Dictionaries;
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

        private string _propertyString;
        public string PropertyString { get => _propertyString + Suffix; set { _propertyString = value; } }
        public int Index { get; set; }

        [JsonIgnore]
        public int ItemLevel { get; set; }

        [JsonIgnore]
        public string Suffix { get; set; }

        [JsonIgnore]
        private static List<string> _ignoredProperties = new List<string> { "state", "bloody", "oskill_hide", "dmg-throw", "uberstone-property" };

        [JsonIgnore]
        public string CompareKey => ItemStatCost.Stat + Parameter;

        public ItemProperty(string property, string parameter, int? min, int? max, int index, int itemLevel = 0, string suffix = "") {
            
            CurrentItemProperty = this;
            Parameter = parameter;
            Min = min;
            Max = max;
            Index = index;
            ItemLevel = ItemLevel;
            Suffix = suffix;

            if (!EffectProperty.EffectProperties.ContainsKey(property.ToLower())) {
                throw ItemPropertyException.Create(
                    $"Could not find property '{property.ToLower()}' parameter '{parameter}' min '{min}' max '{max}' index '{index}' itemlvl '{itemLevel}' in Properties.txt");
            }

            Property = EffectProperty.EffectProperties[property.ToLower()];

            var propStat = Property.Stat;
            var propCode = Property.Code;


            if (string.IsNullOrEmpty(propStat)) {
                // Fix wrongly spelled and hardcoded stats
                switch (Property.Code) {
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
            if (propStat == "item_addclassskills") {
                if (propCode == "randclassskill") {
                    Min = int.TryParse(Parameter, out var parsed) ? parsed : 0;
                    Max = Min;
                }
                
                Parameter = Property.Code;
            }

            // Fix manually set stats
            switch (propCode) {
                case "res-all":
                case "res-all-max":
                case "all-stats":
                case "dmg-pois":
                case "fireskill":
                case "coldskill":
                case "lightningskill":
                case "poisonskill":
                case "magicskill":
                case "extra-elem":
                case "pierce-elem":
                case "dmg-norm":

                    propStat = Property.Code;
                    break;
            }

            if (!ItemStatCost.ItemStatCosts.ContainsKey(propStat)) {
                throw ItemPropertyException.Create($"Could not find stat '{propStat}' in ItemStatCost.txt");
            }

            ItemStatCost = ItemStatCost.ItemStatCosts[propStat];

            try {
                PropertyString = ItemStatCost.PropertyString(Min, Max, Parameter, itemLevel);
            }
            catch (Exception e) {
                if (e as ItemStatCostException == null) {
                    throw ItemPropertyException.Create($"Could not generate properties for property '{property}' with parameter '{parameter}' min '{min}' max '{max}' index '{index}' itemlvl '{itemLevel}'", e);
                }

                throw e;
            }
        }

        public ItemProperty(ItemProperty itemProperty) {
            CurrentItemProperty = this;
            Property = itemProperty.Property;
            Parameter = itemProperty.Parameter;
            Min = itemProperty.Min;
            Max = itemProperty.Max;
            ItemStatCost = itemProperty.ItemStatCost;
            PropertyString = itemProperty.PropertyString;
            Index = itemProperty.Index;
        }

        public static List<ItemProperty> GetProperties(List<PropertyInfo> properties, int itemLevel = 0) {
            var result = new List<ItemProperty>();

            // Build ItemProperty list
            for (int i = 0; i < properties.Count; i++) {
                var property = properties[i];

                if (string.IsNullOrEmpty(property.Property) || property.Property.StartsWith("*"))
                    continue;

                var codeLower = property.Property.Trim().ToLowerInvariant();

                // Replace "dmg-elem" with 3 element-specific props
                if (codeLower == "dmg-elem") {
                    foreach (var elem in new[] { "dmg-fire", "dmg-cold", "dmg-ltng" })
                    {
                        var p = new ItemProperty(
                            elem,
                            property.Parameter,
                            property.Min,
                            property.Max,
                            i,
                            itemLevel
                        );

                        if (!_ignoredProperties.Contains(p.Property.Code.ToLowerInvariant()))
                            result.Add(p);
                    }

                    // Skip adding the original dmg-elem
                    continue;
                }

                // Normal processing
                var prop = new ItemProperty(
                    property.Property,
                    property.Parameter,
                    property.Min,
                    property.Max,
                    i,
                    itemLevel
                );

                if (!_ignoredProperties.Contains(prop.Property.Code.ToLowerInvariant()))
                    result.Add(prop);
            }
            
            // Remove % from non-% /lvl properties, ignoring known exceptions
            var ignoredStats = new HashSet<string> {
                "res-cold/lvl", "res-fire/lvl", "res-ltng/lvl", "res-pois/lvl", "deadly/lvl",
                "crush/lvl", "wounds/lvl", "dmg-dem/lvl", "dmg-und/lvl"
            };

            foreach (var prop in result) {
                if (string.IsNullOrEmpty(prop.PropertyString))
                    continue;

                var stat = prop.Property.Code;
                if (string.IsNullOrEmpty(stat))
                    continue;

                var statLower = stat.ToLowerInvariant();
                bool stripPercent = (statLower.Contains("/lvl") && !statLower.Contains("%/lvl") && !ignoredStats.Contains(statLower)) || statLower.Contains("regen2");

                if (stripPercent) {
                    prop.PropertyString = prop.PropertyString.Replace("%", "");
                }
            }
            
            // Cleanup elemental damage (min / max / length)
            var minDamage = result
                .Where(x => x.Property.Stat.Contains("mindam") && !x.Property.Stat.Contains("level"))
                .ToList();
            var maxDamage = result
                .Where(x => x.Property.Stat.Contains("maxdam") && !x.Property.Stat.Contains("level"))
                .ToList();
            var lenDamage = result
                .Where(x => x.Property.Stat.Contains("length"))
                .ToList();

            if (minDamage.Any() && maxDamage.Any()) {
                
                var toRemove = new HashSet<ItemProperty>();
                var toAdd = new List<ItemProperty>();

                foreach (var minDam in minDamage) {
                    var minDamProperty = minDam.Property.Stat.Replace("mindam", "");

                    foreach (var maxDam in maxDamage) {
                        var maxDamProperty = maxDam.Property.Stat.Replace("maxdam", "");
                        if (!string.Equals(minDamProperty, maxDamProperty, StringComparison.Ordinal))
                            continue;

                        var damagePropertyName = minDamProperty == "light" ? "lightning" : minDamProperty;
                        damagePropertyName = char.ToUpperInvariant(damagePropertyName[0]) + damagePropertyName.Substring(1);

                        var newProp = new ItemProperty(
                            "eledam",
                            damagePropertyName,
                            minDam.Min,
                            maxDam.Max,
                            minDam.Index
                        );

                        toRemove.Add(minDam);
                        toRemove.Add(maxDam);

                        foreach (var lenDam in lenDamage) {
                            var lenDamProperty = lenDam.Property.Stat.Replace("length", "");
                            if (minDamProperty == lenDamProperty)
                                toRemove.Add(lenDam);
                        }

                        toAdd.Add(newProp);
                    }
                }

                result.RemoveAll(toRemove.Contains);
                result.AddRange(toAdd);
            }
            
            lenDamage = result
                .Where(x => x.Property.Stat.Contains("length"))
                .ToList();

            if (lenDamage.Any()) {
                result.RemoveAll(x =>
                    x.Property.Code == "cold-len" && x.Property.Stat.Contains("length"));
            }
            
            // Min damage sometimes contains both elements, weird.
            minDamage = result
                .Where(x => x.Property.Stat.Contains("mindam") && !x.Property.Stat.Contains("level"))
                .ToList();
            maxDamage = result
                .Where(x => x.Property.Stat.Contains("maxdam") && !x.Property.Stat.Contains("level"))
                .ToList();

            if (minDamage.Any() && !maxDamage.Any()) {
                foreach (var minDam in minDamage) {
                    if (minDam.Min == minDam.Max || string.IsNullOrEmpty(minDam.PropertyString))
                        continue;

                    var s = minDam.PropertyString;

                    // Case-insensitive check for life/mana stolen lines
                    if (s.IndexOf("stolen per", StringComparison.OrdinalIgnoreCase) < 0) {
                        s = s.Replace("+", "Adds ");
                    }

                    s = s.Replace("Minimum ", "").Replace("to ", "");
                    minDam.PropertyString = s;
                }
            }

            return result;
        }


        public static void CleanupDublicates(List<ItemProperty> properties) {
            if (properties == null || properties.Count == 0)
                return;

            var baseProps = properties.Where(x => string.IsNullOrEmpty(x.Suffix)).ToList();
            var suffixedProps = properties.Where(x => !string.IsNullOrEmpty(x.Suffix)).ToList();

            var duplicateBaseGroups = baseProps
                .GroupBy(x => x.CompareKey)
                .Where(g => g.Skip(1).Any());

            foreach (var group in duplicateBaseGroups) {
                var merged = MergeItemPropertyGroup(group, transformWeaponMinText: true);

                baseProps.RemoveAll(x =>
                    x.ItemStatCost != null &&
                    merged.ItemStatCost != null &&
                    x.ItemStatCost.Stat == merged.ItemStatCost.Stat);

                baseProps.Add(merged);
            }

            List<ItemProperty> MergeDuplicatesByPropertyString(IEnumerable<ItemProperty> source) {
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
            bool transformWeaponMinText) {
            
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

        public override string ToString() {
            return Property.ToString();
        }
    }
}
