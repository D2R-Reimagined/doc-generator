﻿using System.Collections.Generic;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Dictionaries
{
    public class MonStat
    {
        [JsonIgnore]
        public string Id { get; set; }
        
        [JsonIgnore]
        public string Hcldx { get; set; }
        
        [JsonIgnore]
        public string NameStr { get; set; }

        [JsonIgnore]
        public static Dictionary<string, MonStat> MonStats;

        public static void Import(string excelFolder)
        {
            MonStats = new Dictionary<string, MonStat>();

            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/MonStats.txt");

            foreach (var row in table)
            {
                var monStat = new MonStat
                {
                    Id = row["Id"],
                    Hcldx = row["*hcIdx"],
                    NameStr = row["NameStr"]
                };

                MonStats[monStat.Hcldx] = monStat;
            }
        }

        public override string ToString()
        {
            return NameStr;
        }
    }
}
