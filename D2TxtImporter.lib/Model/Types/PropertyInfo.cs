﻿using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Types
{
    public class PropertyInfo
    {
        [JsonIgnore] public string Property { get; set; }
        [JsonIgnore] public string Parameter { get; set; }
        [JsonIgnore] public int? Min { get; set; }
        [JsonIgnore] public int? Max { get; set; }

        public PropertyInfo(string property, string parameter, int? min, int? max)
        {
            Property = property;
            Parameter = parameter;
            Min = min;
            Max = max;
        }

        public PropertyInfo(string property, string parameter, string min, string max)
        {
            Property = property;
            Parameter = parameter;
            Min = Utility.ToNullableInt(min);
            Max = Utility.ToNullableInt(max);
        }

        public override string ToString()
        {
            return Property;
        }
    }
}