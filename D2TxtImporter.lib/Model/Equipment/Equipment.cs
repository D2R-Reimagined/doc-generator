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
    }

    public enum EquipmentType
    {
        Armor,
        Weapon,
        Jewelery
    }
}
