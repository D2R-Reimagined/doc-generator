using System;
using System.IO;

using CommandLine;
using D2TxtImporter.lib.Exceptions;
using D2TxtImporter.lib.Model.Items;
using D2TxtImporter.lib.Diff;

namespace D2TxtImporter_console
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Parser.Default
                    .ParseArguments<ExportOptions, DiffPatchOptions>(args)
                    .MapResult(
                        (ExportOptions o) =>
                        {
                            var importer = new D2TxtImporter.lib.Importer(o.ExcelPath, o.TablePath, o.OutputPath);

                            CubeRecipeV2.UseDescription = o.CubeRecipeDescription;
                            ExceptionHandler.ContinueOnException = o.ContinueOnException;

                            importer.LoadData();
                            importer.ImportModel();
                            importer.Export();
                            return 0;
                        },
                        (DiffPatchOptions o) =>
                        {
                            // Validate input
                            if (string.IsNullOrWhiteSpace(o.Patch) || string.IsNullOrWhiteSpace(o.JsonOld) || string.IsNullOrWhiteSpace(o.JsonNew) || string.IsNullOrWhiteSpace(o.OutDir))
                            {
                                throw new Exception("Missing required arguments. Usage: diff-patch --patch <file> --json-old <dir> --json-new <dir> --out <dir> [--include-unmentioned] [--md]");
                            }

                            if (!File.Exists(o.Patch))
                            {
                                throw new Exception($"Patch file not found: {o.Patch}");
                            }
                            if (!Directory.Exists(o.JsonOld))
                            {
                                throw new Exception($"Old JSON directory not found: {o.JsonOld}");
                            }
                            if (!Directory.Exists(o.JsonNew))
                            {
                                throw new Exception($"New JSON directory not found: {o.JsonNew}");
                            }
                            if (!Directory.Exists(o.OutDir))
                            {
                                Directory.CreateDirectory(o.OutDir);
                            }

                            // Pipeline: parse diff -> load JSONs -> compute semantic diff -> export Markdown
                            var targets = DiffFromPatchService.ParseUnifiedDiff(o.Patch);
                            var models = JsonModelLoader.Load(o.JsonOld, o.JsonNew);
                            var semantic = SemanticDiffService.Compute(models, targets, o.IncludeUnmentioned);

                            var md = MarkdownPatchNotesExporter.Render(semantic);
                            var outFile = Path.Combine(o.OutDir, "patch-notes.md");
                            File.WriteAllText(outFile, md);
                            Console.WriteLine($"Patch notes written to: {outFile}");
                            return 0;
                        },
                        errs => 1);
            }
            catch (Exception e)
            {
                // Print full exception with stack trace and inner exceptions
                Console.WriteLine("Unhandled error during diff-patch:\n");
                Console.WriteLine(e.ToString());
                Console.ReadLine();
            }
            var debugFile = $"{Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location)}/debuglog.txt";
            if (File.Exists(debugFile))
            {
                var errorLines = File.ReadAllText(debugFile);
                Console.WriteLine(errorLines);
                Console.ReadLine();
            }
        }
    }

    [Verb("export", HelpText = "Import TXT and export JSON/TXT/Web outputs.")]
    public class ExportOptions
    {
        [Option('e', "excelPath", Required = true, HelpText = "Path to the .txt files")]
        public string ExcelPath { get; set; }

        [Option('t', "tablePath", Required = true, HelpText = "Path to the .tbl or .json files")]
        public string TablePath { get; set; }

        [Option('o', "outputPath", Required = true, HelpText = "Path the output is genereted at (must exist)")]
        public string OutputPath { get; set; }

        [Option("cubeRecipeDescription", Required = false, HelpText = "Use the description from CubeRecipes.txt instead of generating it")]
        public bool CubeRecipeDescription { get; set; }

        [Option("continueOnException", Required = false, HelpText = "If an exception occures, log it and continue. Check debuglog.txt and errorlog.txt for info")]
        public bool ContinueOnException { get; set; }
    }

    [Verb("diff-patch", HelpText = "Generate Markdown patch notes by reconciling a GitHub .diff with old/new JSON exports.")]
    public class DiffPatchOptions
    {
        [Option("patch", Required = true, HelpText = "Path to the .diff or .patch file")]
        public string Patch { get; set; }

        [Option("json-old", Required = true, HelpText = "Path to the OLD export json directory (contains uniques.json, sets.json, runewords.json)")]
        public string JsonOld { get; set; }

        [Option("json-new", Required = true, HelpText = "Path to the NEW export json directory (contains uniques.json, sets.json, runewords.json)")]
        public string JsonNew { get; set; }

        [Option("out", Required = true, HelpText = "Directory where patch-notes.md will be written")]
        public string OutDir { get; set; }

        [Option("include-unmentioned", Required = false, HelpText = "Also include changes present in JSONs that are not referenced by the .diff")]
        public bool IncludeUnmentioned { get; set; }

        [Option("md", Required = false, HelpText = "Generate Markdown output (default true)")]
        public bool Md { get; set; } = true;
    }
}
