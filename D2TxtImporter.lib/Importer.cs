using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using D2TxtImporter.lib.Exceptions;
using D2TxtImporter.lib.Exporters;
using D2TxtImporter.lib.Model.Dictionaries;
using D2TxtImporter.lib.Model.Equipment;
using D2TxtImporter.lib.Model.Items;

namespace D2TxtImporter.lib
{
    public class Importer
    {
        private string _outputPath;
        private string _excelPath;
        private string _tablePath;

        public List<Unique> Uniques { get; set; }
        public List<Runeword> Runewords { get; set; }
        public List<CubeRecipe> CubeRecipes { get; set; }
        public List<Set> Sets { get; set; }

        public Importer(string excelPath, string tablePath, string outputDir)
        {
            ExceptionHandler.Initialize();

            if (!Directory.Exists(outputDir))
            {
                throw new Exception($"Could not find output directory at '{outputDir}'");
            }

            if (!Directory.Exists(excelPath))
            {
                throw new Exception($"Could not find excel directory at '{_excelPath}'");
            }

            if (!Directory.Exists(tablePath))
            {
                throw new Exception($"Could not find table directory at '{_tablePath}'");
            }

            _outputPath = outputDir.Trim('/', '\\');
            _excelPath = excelPath.Trim('/', '\\');
            _tablePath = tablePath.Trim('/', '\\');
        }

        public void LoadData()
        {
            try
            {
                Table.ImportFromTbl(_tablePath);
                MagicPrefix.Import(_excelPath);
                MagicSuffix.Import(_excelPath);
                ItemStatCost.Import(_excelPath);
                EffectProperty.Import(_excelPath);
                ItemType.Import(_excelPath);
                Armor.Import(_excelPath);
                Weapon.Import(_excelPath);
                Skill.Import(_excelPath);
                CharStat.Import(_excelPath);
                MonStat.Import(_excelPath);
                Misc.Import(_excelPath);
                Gem.Import(_excelPath);
                SetItem.Import(_excelPath);
            }
            catch (Exception e)
            {
                ExceptionHandler.WriteException(e);
            }
        }

        public void ImportModel()
        {
            try
            {
                Uniques = Unique.Import(_excelPath);
                Runewords = Runeword.Import(_excelPath);
                CubeRecipes = CubeRecipe.Import(_excelPath);
                Sets = Set.Import(_excelPath);
            }
            catch (Exception e)
            {
                ExceptionHandler.WriteException(e);
            }
        }

        public void Export()
        {
            try
            {
                //TxtExporter.ExportTxt(_outputPath, Uniques, Runewords, CubeRecipes, Sets); // Out of date
                JsonExporter.ExportJson(_outputPath, Uniques, Runewords, CubeRecipes, Sets);
                WebExporter.ExportWeb(_outputPath);
            }
            catch (Exception e)
            {
                ExceptionHandler.WriteException(e);
            }
        }

        public static List<string> ReadTxtFileToList(string path)
        {
            return File.ReadAllLines(path).ToList();
        }

        public static List<Dictionary<string, string>> ReadTxtFileToDictionaryList(string path)
        {
            try
            {
                var table = new List<Dictionary<string, string>>();

                var fileArray = File.ReadAllLines(path);
                var headerArray = fileArray.Take(1).First().Split('\t');

                var header = new List<string>();

                foreach (var column in headerArray)
                {
                    header.Add(column);
                }

                var dataArray = fileArray.Skip(1);
                foreach (var valueLine in dataArray)
                {
                    var values = valueLine.Split('\t');
                    if (string.IsNullOrEmpty(values[1]))
                    {
                        continue;
                    }

                    var row = new Dictionary<string, string>();

                    for (var i = 0; i < values.Length; i++)
                    {
                        row[headerArray[i]] = values[i];
                    }

                    table.Add(row);
                }

                return table;   
            } catch (Exception e)
            {
                ExceptionHandler.WriteException(e);
                return null;
            }
        }
    }
}
