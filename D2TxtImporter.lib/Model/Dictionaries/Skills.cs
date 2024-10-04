using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Dictionaries
{
    public class Skill
    {
        public string Name { get; set; }

        [JsonIgnore]
        public int? Id { get; set; }
        
        public string CharClass { get; set; }

        [JsonIgnore]
        public string SkillDesc { get; set; }
        
        public int RequiredLevel { get; set; }

        [JsonIgnore]
        private static Dictionary<int?, Skill> IdSkillDictionary;
        
        [JsonIgnore]
        private static Dictionary<string, Skill> NameSkillDictionary;

        [JsonIgnore]
        private static Dictionary<string, Skill> DescSkillDictionary;

        public static void Import(string excelFolder)
        {
            IdSkillDictionary = new Dictionary<int?, Skill>();
            NameSkillDictionary = new Dictionary<string, Skill>();
            DescSkillDictionary = new Dictionary<string, Skill>();

            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/Skills.txt");

            foreach (var row in table)
            {
                var reqLevel = Utility.ToNullableInt(row["reqlevel"]);
                if (!reqLevel.HasValue || reqLevel.Value < 1)
                {
                    reqLevel = 1;
                    //ExceptionHandler.LogException(new Exception($"Invalid required level for skill '{row["reqlevel"]}' in Skills.txt, should be an integer value 1 or above"));
                }

                var skill = new Skill
                {
                    Name = row["skill"],
                    Id = Utility.ToNullableInt(row["*Id"]),
                    CharClass = row["charclass"],
                    SkillDesc = row["skilldesc"],
                    RequiredLevel = reqLevel.Value
                };

                IdSkillDictionary[skill.Id] = skill;
                NameSkillDictionary[skill.Name] = skill;
                DescSkillDictionary[skill.SkillDesc] = skill;
            }
        }

        public static Skill GetSkill(string skill)
        {
            if (Utility.ToNullableInt(skill).HasValue)
            {
                return IdSkillDictionary[Utility.ToNullableInt(skill)];
            }

            if (NameSkillDictionary.ContainsKey(skill))
            {
                return NameSkillDictionary[skill];
            }

            if (DescSkillDictionary.ContainsKey(skill))
            {
                return DescSkillDictionary[skill];
            }

            throw new Exception($"Could not find skill with id, name, or description '{skill}' in Skills.txt");
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
