﻿using System.Collections.Generic;
using System.IO;
using D2TxtImporter.lib.Model.Items;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Exporters
{
    public class JsonExporter
    {
        public static void ExportJson(string outputPath, List<Unique> uniques, List<Runeword> runewords, List<CubeRecipe> cubeRecipes, List<Set> sets)
        {
            if (!Directory.Exists(outputPath))
            {
                throw new System.Exception("Could not find output directory");
            }

            var txtOutputDirectory = outputPath + "/json";

            if (!Directory.Exists(txtOutputDirectory))
            {
                Directory.CreateDirectory(txtOutputDirectory);
            }

            Uniques(txtOutputDirectory + "/uniques.json", uniques);
            Runewords(txtOutputDirectory + "/runewords.json", runewords);
            CubeRecipes(txtOutputDirectory + "/cube_recipes.json", cubeRecipes);
            Sets(txtOutputDirectory + "/sets.json", sets);
        }

        private static void Uniques(string destination, List<Unique> uniques)
        {
            var json = JsonConvert.SerializeObject(uniques, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii }).Replace("\\ufffd", "'");
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        private static void Runewords(string destination, List<Runeword> runewords)
        {
            var json = JsonConvert.SerializeObject(runewords, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii }).Replace("\\ufffd", "'");
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        private static void CubeRecipes(string destination, List<CubeRecipe> cubeRecipes)
        {
            var json = JsonConvert.SerializeObject(cubeRecipes, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii }).Replace("\\ufffd", "'");
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        private static void Sets(string destination, List<Set> sets)
        {
            var json = JsonConvert.SerializeObject(sets, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii }).Replace("\\ufffd", "'");
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }
    }
}
