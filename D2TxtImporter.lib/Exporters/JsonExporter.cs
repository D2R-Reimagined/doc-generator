using System.Collections.Generic;
using System.IO;
using System.Linq;

using D2TxtImporter.lib.Model.Items;
using D2TxtImporter.lib.Model.Equipment;
using D2TxtImporter.lib.Model.Dictionaries;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Exporters
{
    public class JsonExporter
    {
        // Shared, case-insensitive list of properties to exclude from uniques.json and sets.json
        private static readonly HashSet<string> ExcludedExportProperties =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                // Equipment common
                "NormCode",
                "UberCode",
                "UltraCode",
                "GemSockets",
                "AutoPrefix",

                // Armor / Weapon specifics
                "Block",
                "StrBonus",
                "DexBonus",
                "Speed"
            };

        // Custom resolver that filters out properties by name (case-insensitive)
        private class ExcludingPropertiesContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
        {
            private readonly ISet<string> _excluded;

            public ExcludingPropertiesContractResolver(ISet<string> excluded)
            {
                _excluded = excluded ?? new HashSet<string>();
            }

            protected override IList<Newtonsoft.Json.Serialization.JsonProperty> CreateProperties(System.Type type, MemberSerialization memberSerialization)
            {
                var props = base.CreateProperties(type, memberSerialization);
                if (_excluded == null || _excluded.Count == 0)
                {
                    return props;
                }

                // Filter by property name using case-insensitive set
                return new List<Newtonsoft.Json.Serialization.JsonProperty>(
                    Enumerable.Where(props, p => !_excluded.Contains(p.PropertyName))
                );
            }
        }

        // Helper: serialize with 4-space indentation when prettyPrint is true; otherwise compact.
        private static string SerializeWithIndent(object value, JsonSerializerSettings settings, bool prettyPrint)
        {
            if (!prettyPrint)
            {
                return JsonConvert.SerializeObject(value, Formatting.None, settings)
                    .Replace("\\ufffd", "'");
            }

            var sb = new System.Text.StringBuilder(4096);
            using (var sw = new StringWriter(sb))
            using (var writer = new JsonTextWriter(sw)
            {
                Formatting = Formatting.Indented,
                IndentChar = ' ',
                Indentation = 4
            })
            {
                var serializer = JsonSerializer.Create(settings);
                serializer.Serialize(writer, value);
            }

            return sb.ToString().Replace("\\ufffd", "'");
        }

        public static void ExportJson(string outputPath, List<Unique> uniques, List<Runeword> runewords, List<CubeRecipe> cubeRecipes, List<Set> sets, bool prettyPrint)
        {
            if (!Directory.Exists(outputPath))
            {
                throw new System.Exception("Could not find output directory");
            }

            var txtOutputDirectory = Path.Combine(outputPath, "json");

            if (!Directory.Exists(txtOutputDirectory))
            {
                Directory.CreateDirectory(txtOutputDirectory);
            }

            Uniques(Path.Combine(txtOutputDirectory, "uniques.json"), uniques, prettyPrint);
            Runewords(Path.Combine(txtOutputDirectory, "runewords.json"), runewords, prettyPrint);
            CubeRecipes(Path.Combine(txtOutputDirectory, "cube_recipes.json"), cubeRecipes, prettyPrint);
            Sets(Path.Combine(txtOutputDirectory, "sets.json"), sets, prettyPrint);
            Weapons(Path.Combine(txtOutputDirectory, "weapons.json"), prettyPrint);
            Armors(Path.Combine(txtOutputDirectory, "armors.json"), prettyPrint);
            MagicPrefixes(Path.Combine(txtOutputDirectory, "magicprefix.json"), prettyPrint);
            MagicSuffixes(Path.Combine(txtOutputDirectory, "magicsuffix.json"), prettyPrint);
            AutoMagics(Path.Combine(txtOutputDirectory, "automagic.json"), prettyPrint);
        }

        private static void Uniques(string destination, List<Unique> uniques, bool prettyPrint)
        {
            var settings = new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
                ContractResolver = new ExcludingPropertiesContractResolver(ExcludedExportProperties)
            };
            var json = SerializeWithIndent(uniques, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        private static void Runewords(string destination, List<Runeword> runewords, bool prettyPrint)
        {
            var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
            var json = SerializeWithIndent(runewords, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        private static void CubeRecipes(string destination, List<CubeRecipe> cubeRecipes, bool prettyPrint)
        {
            var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
            var json = SerializeWithIndent(cubeRecipes, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        private static void Sets(string destination, List<Set> sets, bool prettyPrint)
        {
            var settings = new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
                ContractResolver = new ExcludingPropertiesContractResolver(ExcludedExportProperties)
            };
            var json = SerializeWithIndent(sets, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        private static void MagicPrefixes(string destination, bool prettyPrint)
        {
            var list = (MagicPrefix.MagicPrefixes != null ? (IEnumerable<MagicPrefix>)MagicPrefix.MagicPrefixes.Values : Enumerable.Empty<MagicPrefix>()).ToList();
            var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
            var json = SerializeWithIndent(list, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        private static void MagicSuffixes(string destination, bool prettyPrint)
        {
            var list = (MagicSuffix.MagicSuffixes != null ? (IEnumerable<MagicSuffix>)MagicSuffix.MagicSuffixes.Values : Enumerable.Empty<MagicSuffix>()).ToList();
            var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
            var json = SerializeWithIndent(list, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        private static void AutoMagics(string destination, bool prettyPrint)
        {
            var list = (AutoMagic.AutoMagics != null ? (IEnumerable<AutoMagic>)AutoMagic.AutoMagics.Values : Enumerable.Empty<AutoMagic>()).ToList();
            var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
            var json = SerializeWithIndent(list, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        private static void Weapons(string destination, bool prettyPrint)
        {
            var list = (Weapon.Weapons != null ? (IEnumerable<Weapon>)Weapon.Weapons.Values : Enumerable.Empty<Weapon>()).ToList();
            var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
            var json = SerializeWithIndent(list, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

        private static void Armors(string destination, bool prettyPrint)
        {
            var list = (Armor.Armors != null ? (IEnumerable<Armor>)Armor.Armors.Values : Enumerable.Empty<Armor>()).ToList();
            var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
            var json = SerializeWithIndent(list, settings, prettyPrint);
            File.WriteAllText(destination, json, System.Text.Encoding.UTF8);
        }

    }
}
