using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace D2TxtImporter.lib.Model.Items
{
    // Loads qualifier token maps and friendly names for Cube recipes.
    internal static class CubeQualifiers
    {
        internal sealed class Maps
        {
            public ISet<string> InputTokens { get; set; }
            public ISet<string> OutputTokens { get; set; }
            public Dictionary<string, string> InputDisplay { get; set; }
            public Dictionary<string, string> OutputDisplay { get; set; }
            public Dictionary<string, string> CombinedDisplay { get; set; }
        }

        private static Maps _cached;

        public static Maps Load()
        {
            if (_cached != null) return _cached;

            var maps = new Maps
            {
                InputTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                OutputTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                InputDisplay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                OutputDisplay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                CombinedDisplay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };

            // Canonical token sets
            var inputTokens = new[]
            {
                "qty=#",
                "low","nor","hiq",
                "mag","set","rar","uni","crf","tmp",
                "nos","sock","sock=#","noe","eth","upg",
                "bas","exc","eli","nru"
            };

            var outputTokens = new[]
            {
                "Cow Portal","Pandemonium Portal","Pandemonium Finale Portal","Red Portal",
                "usetype","useitem",
                "qty=#","pre=#","suf=#","lvl=#",
                "low","nor","hiq","mag","set","rar","uni","crf","tmp","eth",
                "sock","sock=#","mod","uns","rem","reg",
                "exc","eli","rep","rch"
            };

            foreach (var t in inputTokens)
            {
                maps.InputTokens.Add(t);
            }
            foreach (var t in outputTokens)
            {
                maps.OutputTokens.Add(t);
            }

            // Seed friendly names
            void Seed(Dictionary<string, string> dict, string token, string display)
            {
                if (!dict.ContainsKey(token)) dict[token] = display;
            }

            // Input friendly
            Seed(maps.InputDisplay, "qty=#", "Quantity");
            Seed(maps.InputDisplay, "low", "Low Quality");
            Seed(maps.InputDisplay, "nor", "Normal Quality");
            Seed(maps.InputDisplay, "hiq", "High Quality (Superior)");
            Seed(maps.InputDisplay, "mag", "Magic Item");
            Seed(maps.InputDisplay, "set", "Set Item");
            Seed(maps.InputDisplay, "rar", "Rare Item");
            Seed(maps.InputDisplay, "uni", "Unique Item");
            Seed(maps.InputDisplay, "crf", "Crafted Item");
            Seed(maps.InputDisplay, "tmp", "Tempered Item");
            Seed(maps.InputDisplay, "nos", "No Sockets");
            Seed(maps.InputDisplay, "sock=#", "Item with Sockets (#)");
            Seed(maps.InputDisplay, "sock", "Item with Sockets");
            Seed(maps.InputDisplay, "noe", "Not Ethereal");
            Seed(maps.InputDisplay, "eth", "Ethereal");
            Seed(maps.InputDisplay, "upg", "Upgradeable");
            Seed(maps.InputDisplay, "bas", "Basic Item");
            Seed(maps.InputDisplay, "exc", "Exceptional Item");
            Seed(maps.InputDisplay, "eli", "Elite Item");
            Seed(maps.InputDisplay, "nru", "Not a Runeword");

            // Output friendly
            Seed(maps.OutputDisplay, "Cow Portal", "Cow Portal");
            Seed(maps.OutputDisplay, "Pandemonium Portal", "Pandemonium Portal");
            Seed(maps.OutputDisplay, "Pandemonium Finale Portal", "Pandemonium Finale Portal");
            Seed(maps.OutputDisplay, "Red Portal", "Red Portal");
            Seed(maps.OutputDisplay, "usetype", "Use Type of Input 1");
            Seed(maps.OutputDisplay, "useitem", "Use Item from Input 1");
            Seed(maps.OutputDisplay, "qty=#", "Quantity");
            Seed(maps.OutputDisplay, "pre=#", "Force Prefix (#)");
            Seed(maps.OutputDisplay, "suf=#", "Force Suffix (#)");
            Seed(maps.OutputDisplay, "low", "Low Quality Item");
            Seed(maps.OutputDisplay, "nor", "Normal Item");
            Seed(maps.OutputDisplay, "hiq", "High Quality Item (Superior)");
            Seed(maps.OutputDisplay, "mag", "Magic Item");
            Seed(maps.OutputDisplay, "set", "Set Item");
            Seed(maps.OutputDisplay, "rar", "Rare Item");
            Seed(maps.OutputDisplay, "uni", "Unique Item");
            Seed(maps.OutputDisplay, "crf", "Crafted Item");
            Seed(maps.OutputDisplay, "tmp", "Tempered Item");
            Seed(maps.OutputDisplay, "eth", "Ethereal Item");
            Seed(maps.OutputDisplay, "sock", "Item with Sockets");
            Seed(maps.OutputDisplay, "sock=#", "Item with Sockets (#)");
            Seed(maps.OutputDisplay, "mod", "Keep Modifiers");
            Seed(maps.OutputDisplay, "uns", "Unsocket (Destroy Socketed)");
            Seed(maps.OutputDisplay, "rem", "Remove Socketed (Return)");
            Seed(maps.OutputDisplay, "reg", "Regenerate Unique (Reroll if Base Upgraded)");
            Seed(maps.OutputDisplay, "exc", "Exceptional Item");
            Seed(maps.OutputDisplay, "eli", "Elite Item");
            Seed(maps.OutputDisplay, "rep", "Repair Item");
            Seed(maps.OutputDisplay, "rch", "Recharge Charges");
            Seed(maps.OutputDisplay, "lvl=#", "Set Level (#)");

            // Enrich friendly strings from required TSV. If missing, throw.
            var path = TryLocateReferenceFile();
            var lines = File.ReadAllLines(path);
            bool inOutput = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (i < 2) continue; // skip headers
                var parts = lines[i].Split('\t');
                if (parts.Length == 0) continue;
                var key = (parts[0] ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(key)) continue;

                if (key.Equals("Outputs", StringComparison.OrdinalIgnoreCase)) { inOutput = true; continue; }
                if (key.Equals("Inputs", StringComparison.OrdinalIgnoreCase)) { inOutput = false; continue; }

                var friendly = parts.Length > 1 ? (parts[1] ?? string.Empty).Trim() : string.Empty;
                if (!string.IsNullOrEmpty(friendly) && friendly != "--")
                {
                    if (!inOutput)
                    {
                        if (maps.InputTokens.Contains(key)) maps.InputDisplay[key] = friendly;
                    }
                    else
                    {
                        if (maps.OutputTokens.Contains(key)) maps.OutputDisplay[key] = friendly;
                    }
                }
            }

            // Build merged display once to avoid repeated dual lookups elsewhere
            foreach (var kv in maps.InputDisplay)
            {
                maps.CombinedDisplay[kv.Key] = kv.Value;
            }
            foreach (var kv in maps.OutputDisplay)
            {
                if (!maps.CombinedDisplay.ContainsKey(kv.Key))
                    maps.CombinedDisplay[kv.Key] = kv.Value;
            }

            _cached = maps;
            return _cached;
        }

        private static string TryLocateReferenceFile()
        {
            // Expect: <projectRoot>\utilities\constants\cubeinout.txt
            var path = ResolveConstantsFilePath("cubeinout.txt");
            if (File.Exists(path)) return path;
            throw new FileNotFoundException($"Expected dependency file no located: \"{path}\"");
        }

        private static string ResolveConstantsFilePath(string fileName)
        {
            // Probe from a few likely bases up the directory tree to find project root
            var bases = new List<string>();
            try { bases.Add(AppDomain.CurrentDomain.BaseDirectory); } catch { }
            try { bases.Add(Directory.GetCurrentDirectory()); } catch { }
            try { bases.Add(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)); } catch { }

            foreach (var b in bases.Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var dir = new DirectoryInfo(b);
                for (int i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
                {
                    // Preferred: detect project root by solution file
                    var sln = Path.Combine(dir.FullName, "D2TxtImporter.sln");
                    if (File.Exists(sln))
                    {
                        var expect = Path.Combine(dir.FullName, "utilities", "constants", fileName);
                        if (File.Exists(expect)) return expect;
                        return expect; // Give the expected path in error even if missing
                    }

                    // Otherwise, if utilities\constants folder exists, use it
                    var constantsDir = Path.Combine(dir.FullName, "utilities", "constants");
                    if (Directory.Exists(constantsDir))
                    {
                        var expect = Path.Combine(constantsDir, fileName);
                        if (File.Exists(expect)) return expect;
                        return expect; // Return expected path for error message
                    }
                }
            }

            // Last resort: assume under current base directory
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "utilities", "constants", fileName);
        }

        private static string ToFriendly(string token)
        {
            if (string.IsNullOrEmpty(token)) return string.Empty;
            // Humanize a few common shorthands
            switch (token)
            {
                case "noe": return "Not Ethereal";
                case "nos": return "No Sockets";
                case "eth": return "Ethereal";
                case "mag": return "Magic";
                case "rar": return "Rare";
                case "uni": return "Unique";
                case "set": return "Set";
                case "crf": return "Crafted";
                case "tmp": return "Tempered";
                default:
                    // Title case fallback
                    try
                    {
                        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(token.Replace('_', ' '));
                    }
                    catch
                    {
                        return token;
                    }
            }
        }
    }
}
