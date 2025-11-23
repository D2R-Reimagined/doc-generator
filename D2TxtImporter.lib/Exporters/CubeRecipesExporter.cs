using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using D2TxtImporter.lib.Model.Items;

namespace D2TxtImporter.lib.Exporters
{
    public static class CubeRecipesExporter
    {
        public static void ExportV2(string outputDir, List<CubeRecipeV2> recipes, bool prettyPrint)
        {
            try
            {
                if (recipes == null || recipes.Count == 0) return;

                var jsonDir = Path.Combine(outputDir, "json");
                if (!Directory.Exists(jsonDir)) Directory.CreateDirectory(jsonDir);

                // Use a clear, versioned filename for the V2 export (agreed naming)
                var path = Path.Combine(jsonDir, "cuberecipesv2.json");

                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = prettyPrint ? Formatting.Indented : Formatting.None
                };

                var json = JsonConvert.SerializeObject(recipes, settings);

                using (var sw = new StreamWriter(path, false, new System.Text.UTF8Encoding(false)))
                {
                    sw.Write(json);
                }
            }
            catch (Exception ex)
            {
                Exceptions.ExceptionHandler.LogException(new Exception("Failed to export CubeRecipesV2", ex));
                if (!Exceptions.ExceptionHandler.ContinueOnException) throw;
            }
        }
    }
}
