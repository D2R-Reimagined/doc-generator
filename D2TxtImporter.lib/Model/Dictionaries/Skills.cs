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
        private static Dictionary<int?, Skill> _idSkillDictionary;

        [JsonIgnore]
        private static Dictionary<string, Skill> _nameSkillDictionary;

        [JsonIgnore]
        private static Dictionary<string, Skill> _descSkillDictionary;

        public static void Import(string excelFolder)
        {
            _idSkillDictionary = new Dictionary<int?, Skill>();
            _nameSkillDictionary = new Dictionary<string, Skill>();
            _descSkillDictionary = new Dictionary<string, Skill>();

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

                _idSkillDictionary[skill.Id] = skill;
                _nameSkillDictionary[skill.Name] = skill;
                _descSkillDictionary[skill.SkillDesc] = skill;
            }
        }

        public static Skill GetSkill(string skill)
        {
            if (Utility.ToNullableInt(skill).HasValue)
            {
                return _idSkillDictionary[Utility.ToNullableInt(skill)];
            }

            if (_nameSkillDictionary.ContainsKey(skill))
            {
                return _nameSkillDictionary[skill];
            }

            if (_descSkillDictionary.ContainsKey(skill))
            {
                return _descSkillDictionary[skill];
            }

            throw new Exception($"Could not find skill with id, name, or description '{skill}' in Skills.txt");
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
