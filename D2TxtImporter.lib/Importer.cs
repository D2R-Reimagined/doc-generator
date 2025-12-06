using System;
using System.Collections.Generic;
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

        public bool ExportJson { get; set; } = true;
        public bool ExportWeb { get; set; } = true;
        public bool PrettyPrintJson { get; set; } = true;
        public bool ExportExcel { get; set; } = false;
        // Toggle for ItemStatCost TSV export
        public bool ExportItemStatCostReport { get; set; } = false;
        // Toggle for Cube Recipes V2 export
        public bool ExportCubeRecipesV2 { get; set; } = false;
        // Toggle for optional Sets grouped-by-base JSON export
        public bool ExportSetsByBase { get; set; } = false;

        public List<Unique> Uniques { get; set; }
        public List<Runeword> Runewords { get; set; }
        public List<Model.Items.CubeRecipeV2> CubeRecipesV2 { get; set; }
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
                throw new Exception($"Could not find excel directory at '{excelPath}'");
            }

            if (!Directory.Exists(tablePath))
            {
                throw new Exception($"Could not find table directory at '{tablePath}'");
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
                ItemStatCost.Import(_excelPath);
                EffectProperty.Import(_excelPath);
                ItemType.Import(_excelPath);
                Skill.Import(_excelPath);
                CharStat.Import(_excelPath);
                MonStat.Import(_excelPath);
                MagicPrefix.Import(_excelPath);
                MagicSuffix.Import(_excelPath);
                AutoMagic.Import(_excelPath);
                Armor.Import(_excelPath);
                Weapon.Import(_excelPath);
                Misc.Import(_excelPath);
                Gem.Import(_excelPath);
                SetItem.Import(_excelPath);
            }
            catch (Exception e)
            {
                ExceptionHandler.WriteException(e);
                if (!ExceptionHandler.ContinueOnException)
                {
                    // Re-throw so callers receive the exception (e.g., missing key)
                    throw;
                }
            }
        }

        public void ImportModel()
        {
            try
            {
                // Start a fresh required-level report to avoid duplication across runs
                RequiredLevelReport.Clear();
                Uniques = Unique.Import(_excelPath);
                Runewords = Runeword.Import(_excelPath);
                Sets = Set.Import(_excelPath);

                // New Cube Recipes V2 (validated & structured)
                CubeRecipesV2 = Model.Items.CubeRecipeV2.Import(_excelPath, Uniques, Model.Items.SetItem.SetItems);
            }
            catch (Exception e)
            {
                ExceptionHandler.WriteException(e);
                if (!ExceptionHandler.ContinueOnException)
                {
                    throw; // propagate
                }
            }
        }

        public void Export()
        {
            try
            {
                // Compute output root (caller controls exact path)
                var docsDir = _outputPath;
                if (!Directory.Exists(docsDir))
                {
                    Directory.CreateDirectory(docsDir);
                }

                // Subfolders are created on-demand by individual exporters to avoid unused directories.

                if (ExportJson)
                {
                    JsonExporter.ExportJson(docsDir, Uniques, Runewords, Sets, PrettyPrintJson);
                    // Optional: write grouped-by-base sets file when enabled
                    if (ExportSetsByBase)
                    {
                        JsonExporter.ExportSetsByBase(docsDir, Sets, PrettyPrintJson);
                    }
                }

                // Write duplicate table key report next to JSON exports directory (guarded by toggle)
                if (Table.EnableDuplicateReport)
                {
                    Table.WriteDuplicateReport(docsDir);
                }

                // Write required-level property report if enabled
                RequiredLevelReport.WriteReport(docsDir);

                // Export ItemStatCost summary (stat, resolved descstrpos, decoded min..max, param range, per-level)
                if (ExportItemStatCostReport)
                {
                    StatsExporter.ExportItemStatCosts(docsDir);
                }

                // Export Cube Recipes V2 if enabled
                if (ExportCubeRecipesV2 && CubeRecipesV2 != null)
                {
                    CubeRecipesExporter.ExportV2(docsDir, CubeRecipesV2, PrettyPrintJson);
                }

                if (ExportWeb)
                {
                    WebExporter.ExportWeb(docsDir);
                }

                if (ExportExcel)
                {
                    ExcelExporter.ExportExcel(docsDir, Uniques, Runewords, Sets);
                }
            }
            catch (Exception e)
            {
                ExceptionHandler.WriteException(e);
                if (!ExceptionHandler.ContinueOnException)
                {
                    throw; // propagate
                }
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
                ExceptionHandler.WriteException(new Exception($"Failed to read or parse txt file '{path}'", e));
                if (!ExceptionHandler.ContinueOnException)
                {
                    // WriteException already rethrows when ContinueOnException is false, but guard here for clarity
                    throw;
                }
                return null;
            }
        }
    }
}
