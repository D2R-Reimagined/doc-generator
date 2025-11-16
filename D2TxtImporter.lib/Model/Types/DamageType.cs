using System;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Types
{
    public class DamageType : ICloneable
    {
        public DamageTypeEnum Type { get; set; }

        [JsonIgnore]
        public int MinDamage { get; set; }

        [JsonIgnore]
        public int MaxDamage { get; set; }
        public string DamageString { get; set; }

        public object Clone()
        {
            return new DamageType
            {
                DamageString = DamageString,
                Type = Type,
                MaxDamage = MaxDamage,
                MinDamage = MinDamage
            };
        }

        public override string ToString()
        {
            return Type.ToString();
        }
    }

    public enum DamageTypeEnum
    {
        OneHanded,
        TwoHanded,
        Thrown,
        Normal
    }
}
