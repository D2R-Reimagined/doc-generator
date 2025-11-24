using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Exporters
{
    public class WebExporter
    {
        public static void ExportWeb(string outputPath)
        {
            if (!Directory.Exists(outputPath))
            {
                throw new Exception("Could not find output directory");
            }

            var exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            var webPath = Path.Combine(exePath, "Exporters", "Web");
            // JSON payloads are expected under Docs\\item-jsons relative to the chosen output directory
            var jsonPath = Path.Combine(outputPath, "item-jsons");

            if (!Directory.Exists(webPath))
            {
                throw new Exception($"Could not find Web Files directory in '{webPath}'");
            }

            if (!Directory.Exists(jsonPath))
            {
                throw new Exception($"Could not find Json directory in '{jsonPath}'");
            }

            // Emit web site under Docs\\web
            var webOutputDirectory = Path.Combine(outputPath, "web");

            if (!Directory.Exists(webOutputDirectory))
            {
                Directory.CreateDirectory(webOutputDirectory);
            }

            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(webPath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(webPath, webOutputDirectory));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(webPath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(webPath, webOutputDirectory), true);
            }

            // Load JSON payloads. For cube recipes, prefer V2 file and fall back to legacy; if neither exists, use empty array.
            var uniquesPath = Path.Combine(jsonPath, "uniques.json");
            var runewordsPath = Path.Combine(jsonPath, "runewords.json");
            var cubeV2Path = Path.Combine(jsonPath, "cuberecipesv2.json");
            var cubeLegacyPath = Path.Combine(jsonPath, "cube_recipes.json");
            var setsPath = Path.Combine(jsonPath, "sets.json");

            var uniqueJson = File.ReadAllText(uniquesPath, Encoding.UTF8);
            var runewordJson = File.ReadAllText(runewordsPath, Encoding.UTF8);

            string cubeRecipeJson;
            if (File.Exists(cubeV2Path))
            {
                cubeRecipeJson = File.ReadAllText(cubeV2Path, Encoding.UTF8);
            }
            else if (File.Exists(cubeLegacyPath))
            {
                cubeRecipeJson = File.ReadAllText(cubeLegacyPath, Encoding.UTF8);
            }
            else
            {
                // Graceful fallback to an empty list when cube recipes are not exported.
                cubeRecipeJson = "[]";
            }

            var setsJson = File.ReadAllText(setsPath, Encoding.UTF8);

            var jsFile = $"{webOutputDirectory}/d2export.js";
            var js = File.ReadAllText(jsFile, Encoding.UTF8);

            js = js.Replace("\"<UNIQUES_JSON>\"", JsonConvert.ToString(uniqueJson)).Replace("\"<RUNEWORDS_JSON>\"", JsonConvert.ToString(runewordJson)).Replace("\"<CUBE_RECIPES_JSON>\"", JsonConvert.ToString(cubeRecipeJson)).Replace("\"<SETS_JSON>\"", JsonConvert.ToString(setsJson));
            File.WriteAllText(jsFile, js, Encoding.UTF8);
        }
    }
}
