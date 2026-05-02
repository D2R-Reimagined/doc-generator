using System;
using System.Collections.Generic;
using System.Globalization;
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
        [JsonIgnore]
        public string StrNameKey { get; set; }
        public string LocalizedName { get; set; }
        public int RequiredLevel { get; set; }
        [JsonIgnore]
        private static Dictionary<int?, Skill> _idSkillDictionary;
        [JsonIgnore]
        private static Dictionary<string, Skill> _nameSkillDictionary;
        [JsonIgnore]
        private static Dictionary<string, Skill> _descSkillDictionary;

        // Maps skilldesc.txt skilldesc value -> str name key
        [JsonIgnore]
        private static Dictionary<string, string> _skillDescToStrName;

        public static void Import(string excelFolder)
        {
            _idSkillDictionary = new Dictionary<int?, Skill>();
            _nameSkillDictionary = new Dictionary<string, Skill>();
            _descSkillDictionary = new Dictionary<string, Skill>();

            // Load skilldesc.txt to build skilldesc -> str name mapping
            _skillDescToStrName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var skillDescTable = Importer.ReadTxtFileToDictionaryList(excelFolder + "/skilldesc.txt");
            foreach (var descRow in skillDescTable)
            {
                var skilldesc = descRow["skilldesc"];
                var strName = descRow["str name"];
                if (!string.IsNullOrEmpty(skilldesc) && !string.IsNullOrEmpty(strName))
                {
                    _skillDescToStrName[skilldesc] = strName;
                }
            }

            var table = Importer.ReadTxtFileToDictionaryList(excelFolder + "/Skills.txt");

            foreach (var row in table)
            {
                var reqLevel = Utility.ToNullableInt(row["reqlevel"]);
                if (!reqLevel.HasValue || reqLevel.Value < 1)
                {
                    reqLevel = 1;
                }

                var skilldescValue = row["skilldesc"];
                string strNameKey = null;
                string localizedName = null;

                if (!string.IsNullOrEmpty(skilldescValue) && _skillDescToStrName.TryGetValue(skilldescValue, out strNameKey))
                {
                    if (!string.IsNullOrEmpty(strNameKey) && Table.Tables.ContainsKey(strNameKey))
                    {
                        localizedName = Table.GetValue(strNameKey);
                    }
                }

                if (string.IsNullOrEmpty(localizedName))
                {
                    localizedName = row["skill"];
                }

                var skill = new Skill
                {
                    Name = row["skill"],
                    Id = Utility.ToNullableInt(row["*Id"]),
                    CharClass = row["charclass"],
                    SkillDesc = skilldescValue,
                    StrNameKey = strNameKey,
                    LocalizedName = localizedName,
                    RequiredLevel = reqLevel.Value
                };

                _idSkillDictionary[skill.Id] = skill;
                _nameSkillDictionary[skill.Name] = skill;
                if (!string.IsNullOrEmpty(skill.SkillDesc))
                {
                    _descSkillDictionary[skill.SkillDesc] = skill;
                }
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
            return LocalizedName ?? Name;
        }
    }
}
