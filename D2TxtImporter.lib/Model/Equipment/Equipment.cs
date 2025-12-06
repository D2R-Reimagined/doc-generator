using System;
using D2TxtImporter.lib.Model.Dictionaries;
using System.Linq;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Equipment
{
    public class Equipment : ICloneable
    {
        public EquipmentType EquipmentType { get; set; }
        public string Name { get { return Table.GetValue(Code); } }
        public string Code { get; set; }
        public int? BaseRequiredLevel { get; set; }
        public int RequiredStrength { get; set; }
        public int RequiredDexterity { get; set; }
        public int Durability { get; set; }
        public int ItemLevel { get; set; }
        public ItemType Type { get; set; }
        public string NormCode { get; set; }
        public string UberCode { get; set; }
        public string UltraCode { get; set; }
        [JsonIgnore]
        public int GemSockets { get; set; }
        public string AutoPrefix { get; set; }
        public string RequiredClass
        {
            get
            {
                // Resolve Equiv2 to an ItemType and return only if it maps to a character class
                var equiv2 = Type?.Equiv2;
                if (string.IsNullOrEmpty(equiv2) || !ItemType.ItemTypes.ContainsKey(equiv2))
                {
                    return "";
                }

                var resolved = ItemType.ItemTypes[equiv2].Name.Replace(" Item", "");
                var classes = new[] { "Sorceress", "Barbarian", "Druid", "Assassin", "Necromancer", "Paladin", "Amazon" };
                return classes.Contains(resolved) ? resolved : "";
            }
        }

        // Shared helper to determine damage string prefixes across equipment/uniques
        public static string GetDamagePrefix(ItemType type)
        {
            if (type == null)
            {
                return null;
            }

            // Shields and Auric Shields use Smite Damage
            if (string.Equals(type.Code, "shld", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type.Equiv1, "shld", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type.Code, "ashd", StringComparison.OrdinalIgnoreCase))
            {
                return "Smite Damage";
            }

            // Boots use Kick Damage
            if (string.Equals(type.Code, "boot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type.Equiv1, "boot", StringComparison.OrdinalIgnoreCase))
            {
                return "Kick Damage";
            }

            return null;
        }

        // Resolve a code using the JSON/TBL table keys to its display string.
        public static string ResolveItemTypeName(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var value = Table.GetValue(code);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            // Fallback to the original code if the key doesn't exist in the tables
            return code;
        }

        public object Clone()
        {
            return new Equipment
            {
                EquipmentType = EquipmentType,
                Code = Code,
                BaseRequiredLevel = BaseRequiredLevel,
                RequiredStrength = RequiredStrength,
                RequiredDexterity = RequiredDexterity,
                Durability = Durability,
                ItemLevel = ItemLevel,
                Type = Type,
                NormCode = NormCode,
                UberCode = UberCode,
                UltraCode = UltraCode,
                GemSockets = GemSockets,
                AutoPrefix = AutoPrefix
            };
        }

        // Compute per-level socket limits string using ItemTypes thresholds and item gem socket cap
        // Example output: "1-25:3, 26-40:4, 41+:6"
        public static string BuildSocketRangeString(ItemType type, int gemSockets)
        {
            if (gemSockets <= 0 || type == null)
            {
                return (gemSockets > 0) ? gemSockets.ToString() : "0";
            }

            var s1 = type.MaxSockets1 ?? 0;
            var t1 = type.MaxSocketsLevelThreshold1 ?? 0;
            var s2 = type.MaxSockets2 ?? 0;
            var t2 = type.MaxSocketsLevelThreshold2 ?? 0;
            var s3 = type.MaxSockets3 ?? 0;

            // If no thresholds provided at all, just return the item cap as a single value
            if (s1 == 0 && s2 == 0 && s3 == 0)
            {
                return gemSockets.ToString();
            }

            // Build raw bands
            var bands = new System.Collections.Generic.List<(int Start, int? End, int Sockets)>();

            // Band 1: 1..t1
            if (t1 > 0)
            {
                var b1 = System.Math.Min(gemSockets, s1 > 0 ? s1 : gemSockets);
                bands.Add((1, t1, b1));
            }

            // Band 2: (t1+1 or 1) .. t2
            if (t2 > 0)
            {
                var start2 = t1 > 0 ? t1 + 1 : 1;
                var cap2 = s2 > 0 ? s2 : (s1 > 0 ? s1 : gemSockets);
                var b2 = System.Math.Min(gemSockets, cap2);
                bands.Add((start2, t2, b2));
            }

            // Band 3: (t2+1 or t1+1 or 1)..+
            var start3 = t2 > 0 ? t2 + 1 : (t1 > 0 ? t1 + 1 : 1);
            var prevCap = s2 > 0 ? s2 : (s1 > 0 ? s1 : gemSockets);
            var cap3 = s3 > 0 ? s3 : prevCap;
            var b3 = System.Math.Min(gemSockets, cap3);
            bands.Add((start3, (int?)null, b3));

            // Merge adjacent bands with the same socket value
            var merged = new System.Collections.Generic.List<(int Start, int? End, int Sockets)>();
            foreach (var band in bands)
            {
                if (merged.Count == 0)
                {
                    merged.Add(band);
                }
                else
                {
                    var last = merged[merged.Count - 1];
                    if (last.Sockets == band.Sockets)
                    {
                        // Extend the last band to cover this band
                        merged[merged.Count - 1] = (last.Start, band.End, last.Sockets);
                    }
                    else
                    {
                        merged.Add(band);
                    }
                }
            }

            // If after merge there is a single band from 1 to +, show "1+:X"
            string FormatBand((int Start, int? End, int Sockets) b)
                => b.End.HasValue ? $"{b.Start}-{b.End}:{b.Sockets}" : $"{b.Start}+:{b.Sockets}";

            return string.Join(", ", merged.ConvertAll(FormatBand));
        }
    }

    public enum EquipmentType
    {
        Armor,
        Weapon,
        Jewelery
    }
}
