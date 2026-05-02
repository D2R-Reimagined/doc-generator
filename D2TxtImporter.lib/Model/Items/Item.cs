using System;
using System.Collections.Generic;
using System.Linq;
using D2TxtImporter.lib.Exceptions;
using D2TxtImporter.lib.Model.Dictionaries;
using D2TxtImporter.lib.Model.Types;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Items
{
    public class Item
    {
        [JsonIgnore]
        public static Item CurrentItem { get; set; }

        public string Name
        {
            get
            {
                if (!Table.Tables.ContainsKey(Index))
                {
                    ExceptionHandler.LogException(new Exception($"Could not find translation for '{Index}' in any .tbl or .json files"));
                }

                return Table.Tables[Index];
            }
        }
        
        public string Index { get; set; }
        public bool Enabled { get; set; }
        public int Rarity { get; set; }
        public int ItemLevel { get; set; }
        public int RequiredLevel { get; set; }
        public string Code { get; set; }
        public List<ItemProperty> Properties { get; set; }
        public bool DamageArmorEnhanced { get; set; }
        public Equipment.Equipment Equipment { get; set; }

        public Item()
        {
            CurrentItem = this;
        }

        public void AdjustRequirements()
        {
            if (Equipment == null || Properties == null) return;

            var easeProps = Properties.Where(p => p.Property != null && string.Equals(p.Property.Code, "ease", StringComparison.OrdinalIgnoreCase)).ToList();
            if (!easeProps.Any()) return;

            int minEase = easeProps.Sum(p => p.Min ?? 0);
            int maxEase = easeProps.Sum(p => p.Max ?? 0);

            if (minEase == 0 && maxEase == 0) return;

            Equipment.RequiredStrength = AdjustStat(Equipment.RequiredStrength, minEase, maxEase);
            Equipment.RequiredDexterity = AdjustStat(Equipment.RequiredDexterity, minEase, maxEase);
        }

        private string AdjustStat(string baseStatStr, int minEase, int maxEase)
        {
            if (string.IsNullOrEmpty(baseStatStr) || baseStatStr == "0") return baseStatStr;
            if (!int.TryParse(baseStatStr, out int baseStat)) return baseStatStr;

            int val1 = (int)Math.Floor(baseStat * (100.0 + minEase) / 100.0);
            int val2 = (int)Math.Floor(baseStat * (100.0 + maxEase) / 100.0);

            int finalMin = Math.Min(val1, val2);
            int finalMax = Math.Max(val1, val2);

            if (finalMin == finalMax) return finalMin.ToString();
            return $"{finalMin}-{finalMax}";
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
