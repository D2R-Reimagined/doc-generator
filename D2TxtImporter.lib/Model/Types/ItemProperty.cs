using System;
using System.Collections.Generic;
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
        private static List<string> _ignoredProperties = new List<string> { "state", "bloody", "oskill_hide", "dmg-throw" };

        [JsonIgnore]
        public string CompareKey => ItemStatCost.Stat + Parameter;

        public ItemProperty(string property, string parameter, int? min, int? max, int index, int itemLevel = 0,
            string suffix = "")
        {
            CurrentItemProperty = this;

            Parameter = parameter;
            Min = min;
            Max = max;
            Index = index;
            ItemLevel = ItemLevel;
            Suffix = suffix;

            if (!EffectProperty.EffectProperties.ContainsKey(property.ToLower()))
            {
                throw ItemPropertyException.Create(
                    $"Could not find property '{property.ToLower()}' parameter '{parameter}' min '{min}' max '{max}' index '{index}' itemlvl '{itemLevel}' in Properties.txt");
            }

            Property = EffectProperty.EffectProperties[property.ToLower()];

            var stat = Property.Stat;


            if (string.IsNullOrEmpty(stat))
            {
                // Fix wrongly spelled and hardcoded stats
                switch (Property.Code)
                {
                    case "str":
                        stat = "strength";
                        break;
                    case "dmg-min":
                        stat = "mindamage";
                        break;
                    case "dmg-max":
                        stat = "maxdamage";
                        break;
                    case "indestruct":
                        stat = "item_indesctructible";
                        break;
                    case "poisonlength":
                        Min = null;
                        Max = null;
                        break;
                    default:
                        stat = Property.Code;
                        break;
                }
            }

            switch (stat)
            {
                case "item_addskill_tab": // Fetch skill tab from .lst files
                    break;
            }

            // Fix all class skills displaying as amazon
            if (stat == "item_addclassskills")
            {
                Parameter = Property.Code;
            }

            // Fix manually set stats
            switch (Property.Code)
            {
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
                    
                    stat = Property.Code;
                    break;
            }

            if (!ItemStatCost.ItemStatCosts.ContainsKey(stat))
            {
                throw ItemPropertyException.Create($"Could not find stat '{stat}' in ItemStatCost.txt");
            }

            ItemStatCost = ItemStatCost.ItemStatCosts[stat];

            try
            {
                PropertyString = ItemStatCost.PropertyString(Min, Max, Parameter, itemLevel);
            }
            catch (Exception e)
            {
                if (e as ItemStatCostException == null)
                {
                    throw ItemPropertyException.Create($"Could not generate properties for property '{property}' with parameter '{parameter}' min '{min}' max '{max}' index '{index}' itemlvl '{itemLevel}'", e);
                }

                throw e;
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
            var result = new List<ItemProperty>();

            foreach (var property in properties)
            {
                if (!string.IsNullOrEmpty(property.Property) && !property.Property.StartsWith("*"))
                {
                    string code = property.Property.Trim().ToLower();
                    // Replace "dmg-elem" with 3 props
                    if (code == "dmg-elem")
                    {
                        var elems = new[] { "dmg-fire", "dmg-cold", "dmg-ltng" };

                        foreach (var elem in elems)
                        {
                            var p = new ItemProperty(
                                elem,
                                property.Parameter,
                                property.Min,
                                property.Max,
                                properties.IndexOf(property),
                                itemLevel
                            );

                            if (!_ignoredProperties.Contains(p.Property.Code.ToLower()))
                            {
                                result.Add(p);
                            }
                        }

                        // Skip adding the original dmg-elem
                        continue;
                    }

                    // Normal processing
                    var prop = new ItemProperty(property.Property,
                        property.Parameter,
                        property.Min,
                        property.Max,
                        properties.IndexOf(property),
                        itemLevel);

                    if (!_ignoredProperties.Contains(prop.Property.Code.ToLower()))
                    {
                        result.Add(prop);
                    }
                }
            }

            // Remove % from non-% /lvl properties, ignoring known exceptions
            var ignoredStats = new HashSet<string>
            {
                "res-cold/lvl",
                "res-fire/lvl",
                "res-ltng/lvl",
                "res-pois/lvl",
                "deadly/lvl",
                "crush/lvl",
                "wounds/lvl",
                "dmg-dem/lvl",
                "dmg-und/lvl"
            };

            foreach (var prop in result)
            {
                var stat = prop.Property.Code?.ToLower();
                if (stat != null && stat.Contains("/lvl") && !stat.Contains("%/lvl") && !ignoredStats.Contains(stat))
                {
                    if (!string.IsNullOrEmpty(prop.PropertyString))
                    {
                        prop.PropertyString = prop.PropertyString.Replace("%", "");
                    }
                }
                // Remove % from regen2 property
                if (stat != null && stat.Contains("regen2"))
                {
                    if (!string.IsNullOrEmpty(prop.PropertyString))
                    {
                        prop.PropertyString = prop.PropertyString.Replace("%", "");
                    }
                }
            }

            // Cleanup elemental damage as they are added as 2-3 different parameters
            var minDamage = result.Where(x => x.Property.Stat.Contains("mindam") && !x.Property.Stat.Contains("level"));
            var maxDamage = result.Where(x => x.Property.Stat.Contains("maxdam") && !x.Property.Stat.Contains("level"));
            var lenDamage = result.Where(x => x.Property.Stat.Contains("length"));

            if (minDamage.Count() > 0 && maxDamage.Count() > 0)
            {
                var toRemove = new List<ItemProperty>();
                var toAdd = new List<ItemProperty>();

                foreach (var minDam in minDamage)
                {
                    foreach (var maxDam in maxDamage)
                    {
                        var minDamProperty = minDam.Property.Stat.Replace("mindam", "");
                        var maxDamProperty = maxDam.Property.Stat.Replace("maxdam", "");

                        if (minDamProperty == maxDamProperty)
                        {
                            var damagePropertyName = minDamProperty;
                            if (damagePropertyName == "light")
                            {
                                damagePropertyName = "lightning";
                            }

                            damagePropertyName = damagePropertyName.First().ToString().ToUpper() + damagePropertyName.Substring(1);

                            var newProp = new ItemProperty("eledam", damagePropertyName, minDam.Min, maxDam.Max, minDam.Index);

                            toRemove.Add(minDam);
                            toRemove.Add(maxDam);

                            foreach (var lenDam in lenDamage)
                            {
                                var lenDamProperty = lenDam.Property.Stat.Replace("length", "");
                                if (minDamProperty == lenDamProperty)
                                {
                                    toRemove.Add(lenDam);
                                }
                            }

                            toAdd.Add(newProp);
                        }
                    }
                }

                toRemove.ForEach(x => result.Remove(x));
                result.AddRange(toAdd);
            }

            // Sometimes there is cold length with min/max damage
            lenDamage = result.Where(x => x.Property.Stat.Contains("length"));
            if (lenDamage.Count() > 0)
            {
                result.RemoveAll(x => lenDamage.Any(y => y == x && y.Property.Code == "cold-len"));
            }

            // Min damage sometimes contain both elements, weird.
            if (minDamage.Count() > 0 && maxDamage.Count() == 0)
            {
                foreach (var minDam in minDamage)
                {
                    if (minDam.Min != minDam.Max)
                    {
                        minDam.PropertyString = minDam.PropertyString.Replace("+", "Adds ").Replace("Minimum ", "");
                    }
                }
            }
            
            return result;
        }

        public static void CleanupDublicates(List<ItemProperty> properties)
        {
            if (properties == null || properties.Count == 0)
                return;

            var baseProps     = properties.Where(x => string.IsNullOrEmpty(x.Suffix)).ToList();
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

        public override string ToString()
        {
            return Property.ToString();
        }
    }
}
