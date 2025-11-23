using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace D2TxtImporter.lib.Model.Items
{
    /// <summary>
    /// Loads and validates Cube recipe qualifier tokens.
    ///
    /// Primary source is a hardcoded list based on the game’s canonical
    /// qualifiers. These are stable and mutually exclusive between Inputs and
    /// Outputs, except where the game intentionally reuses a code.
    ///
    /// As a convenience, if a local file "Cube Related/cubeinout.txt" is
    /// available near the binaries, we will parse it to enrich friendly
    /// descriptions but we will not rely on it for the token set. This avoids
    /// path sensitivity while keeping nice display names.
    /// </summary>
    internal static class CubeQualifiers
    {
        internal sealed class Maps
        {
            public ISet<string> InputTokens { get; set; }
            public ISet<string> OutputTokens { get; set; }
            public Dictionary<string, string> InputDisplay { get; set; }
            public Dictionary<string, string> OutputDisplay { get; set; }
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
                OutputDisplay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };

            // 1) Build canonical, hardcoded token sets (mutually exclusive)
            var inputTokens = new[]
            {
                "qty=#",
                "low","nor","hiq",
                "mag","set","rar","uni","crf","tmp",
                // sockets: allow both presence (sock) and exact count (sock=#) for inputs
                "nos","sock","sock=#","noe","eth","upg",
                "bas","exc","eli","nru"
            };

            var outputTokens = new[]
            {
                // Special meta outputs (kept as tokens for completeness)
                "Cow Portal","Pandemonium Portal","Pandemonium Finale Portal","Red Portal",
                // Behavioral keywords
                "usetype","useitem",
                // Generic controls
                "qty=#","pre=#","suf=#","lvl=#",
                // Quality/state
                "low","nor","hiq","mag","set","rar","uni","crf","tmp","eth",
                // Socket ops / transforms
                "sock","sock=#","mod","uns","rem","reg",
                // Tier transforms & repair/recharge
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

            // Seed friendly names (stable)
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
            Seed(maps.OutputDisplay, "reg", "Regenerate Unique (with usetype)");
            Seed(maps.OutputDisplay, "exc", "Exceptional Item");
            Seed(maps.OutputDisplay, "eli", "Elite Item");
            Seed(maps.OutputDisplay, "rep", "Repair Item");
            Seed(maps.OutputDisplay, "rch", "Recharge Charges");
            Seed(maps.OutputDisplay, "lvl=#", "Set Level (#)");

            // 2) Optionally enrich friendly strings from a nearby TSV if present
            try
            {
                var path = TryLocateReferenceFile();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
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
                }
            }
            catch { /* ignore enrichment failures */ }

            _cached = maps;
            return _cached;
        }

        private static string TryLocateReferenceFile()
        {
            try
            {
                var candidates = new List<string>();

                // 1) Executing assembly directory (DLL copied next to EXE)
                var asmDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(asmDir))
                {
                    candidates.Add(Path.Combine(asmDir, "Cube Related", "cubeinout.txt"));
                }

                // 2) App base directory (WPF/console)
                candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cube Related", "cubeinout.txt"));

                // 3) Probe the repository layout from client/bin/Debug back to root
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var up1 = Directory.GetParent(baseDir)?.FullName;            // .../D2TxtImporter.client/bin
                var up2 = Directory.GetParent(up1 ?? baseDir)?.FullName;     // .../D2TxtImporter.client
                var up3 = Directory.GetParent(up2 ?? up1 ?? baseDir)?.FullName; // .../doc-generator (repo root)
                if (!string.IsNullOrEmpty(up3))
                {
                    candidates.Add(Path.Combine(up3, "D2TxtImporter.lib", "bin", "Cube Related", "cubeinout.txt"));
                    candidates.Add(Path.Combine(up3, "D2TxtImporter.lib", "bin", "Debug", "Cube Related", "cubeinout.txt"));
                }

                return candidates.FirstOrDefault(File.Exists);
            }
            catch { return null; }
        }

        private static string ToFriendly(string token)
        {
            if (string.IsNullOrEmpty(token)) return string.Empty;
            // humanize a few common shorthands
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
