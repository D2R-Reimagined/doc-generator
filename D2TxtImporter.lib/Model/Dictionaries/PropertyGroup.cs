using System;
using System.Collections.Generic;
using D2TxtImporter.lib.Model.Types;

namespace D2TxtImporter.lib.Model.Dictionaries
{
    public class PropertyGroup
    {
        public string Code { get; set; }
        public List<PropertyInfo> PropertyInfos { get; set; }

        public static Dictionary<string, PropertyGroup> PropertyGroups;

        public static void Import(string excelFolder)
        {
            PropertyGroups = new Dictionary<string, PropertyGroup>(StringComparer.OrdinalIgnoreCase);

            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/propertygroups.txt");
            if (table == null) return;

            foreach (var row in table)
            {
                var code = row.ContainsKey("code") ? row["code"] : null;
                if (string.IsNullOrWhiteSpace(code)) continue;

                var pg = new PropertyGroup
                {
                    Code = code,
                    PropertyInfos = new List<PropertyInfo>()
                };

                for (int i = 1; i <= 8; i++)
                {
                    var prop = row.ContainsKey($"Prop{i}") ? row[$"Prop{i}"] : null;
                    if (string.IsNullOrWhiteSpace(prop)) continue;

                    var parMin = row.ContainsKey($"ParMin{i}") ? row[$"ParMin{i}"] : null;
                    var parMax = row.ContainsKey($"ParMax{i}") ? row[$"ParMax{i}"] : null;
                    
                    var parameter = parMin;
                    if (!string.IsNullOrEmpty(parMin) && !string.IsNullOrEmpty(parMax) && parMin != parMax)
                    {
                        parameter = $"{parMin}-{parMax}";
                    }
                    else if (string.IsNullOrEmpty(parMin))
                    {
                        parameter = parMax;
                    }

                    pg.PropertyInfos.Add(new PropertyInfo(
                        prop,
                        parameter,
                        row.ContainsKey($"ModMin{i}") ? row[$"ModMin{i}"] : null,
                        row.ContainsKey($"ModMax{i}") ? row[$"ModMax{i}"] : null
                    ));
                }

                PropertyGroups[code] = pg;
            }
        }
    }
}
