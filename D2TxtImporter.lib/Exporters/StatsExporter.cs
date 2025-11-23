using System;
using System.Globalization;
using System.IO;
using System.Linq;
using D2TxtImporter.lib.Model.Dictionaries;

namespace D2TxtImporter.lib.Exporters
{
    public static class StatsExporter
    {
        // Writes a TSV export of ItemStatCost with: stat, descstrpos (resolved text), range, paramRange, perLevel
        public static void ExportItemStatCosts(string outputDir)
        {
            if (ItemStatCost.ItemStatCosts == null || ItemStatCost.ItemStatCosts.Count == 0)
            {
                return; // nothing to write
            }

            // Ensure output matches the path convention used by RequiredLevelReport and duplicate report
            var jsonDir = Path.Combine(outputDir, "json");
            if (!Directory.Exists(jsonDir))
            {
                Directory.CreateDirectory(jsonDir);
            }

            var path = Path.Combine(jsonDir, "item stat ranges.txt");

            // Write UTF-8 without BOM to match other reports
            using (var w = new StreamWriter(path, false, new System.Text.UTF8Encoding(false)))
            {
                // Columns: stat, descstrpos (resolved), min-max (decoded), Save Param Bits, per-level increment
                w.WriteLine("stat\tdescstrpos\trange\tparrange\tperLevel");

                foreach (var isc in ItemStatCost.ItemStatCosts.Values.OrderBy(x => x.Id))
                {
                    // Exclude by stat code containing any of the filtered fragments
                    if (!string.IsNullOrEmpty(isc.Stat))
                    {
                        var stat = isc.Stat;
                        foreach (var frag in ExcludedFragments)
                        {
                            if (stat.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                goto SkipRow; // skip writing this row
                            }
                        }
                    }

                    // Filter: only export rows where descstrpos is present and not 'nul', and savebits has a value > 0
                    var key = isc.DescStrPosKey;
                    if (string.IsNullOrWhiteSpace(key) || key.Equals("nul", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!isc.SaveBits.HasValue || isc.SaveBits.Value <= 0)
                    {
                        continue;
                    }

                    // Compute min/max based on savebits/saveadd (ignore valshift for human readability)
                    string range = string.Empty;

                    try
                    {
                        var bits = isc.SaveBits ?? 0;
                        var add = isc.SaveAdd ?? 0;

                        if (bits > 0)
                        {
                            // use double to avoid overflow
                            var maxRaw = Math.Pow(2, bits) - 1; 
                            var min = 0 - (double)add;
                            var max = maxRaw - add;
                            var minStr = FormatNumber(min);
                            var maxStr = FormatNumber(max);
                            range = minStr == maxStr ? minStr : (minStr + " - " + maxStr);
                        }
                        // If bits are zero, filtered above; no else branch
                    }
                    catch
                    {
                        // If anything goes wrong, leave as empty to avoid breaking export
                    }

                    // Param range from saveparam bits
                    string paramRange = string.Empty;
                    try
                    {
                        var pbits = isc.SaveParam ?? 0;
                        if (pbits > 0)
                        {
                            var pMaxVal = (long)(Math.Pow(2, pbits) - 1);
                            paramRange = "0-" + pMaxVal.ToString(CultureInfo.InvariantCulture);
                        }
                    }
                    catch
                    {
                        // ignore
                    }

                    // Per level increment: export as "2^OpParam ths" (e.g., "8 ths") so users
                    // know that every N from the property equals +1 per level growth.
                    string perLevel = string.Empty;
                    if (!string.IsNullOrEmpty(isc.Stat) && isc.Stat.IndexOf("perlevel", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (isc.OpParam.HasValue)
                        {
                            var op = isc.OpParam.Value;
                            // Clamp to a reasonable range of shift amounts
                            if (op < 0) op = 0;
                            if (op > 30) op = 30;
                            var threshold = 1 << op; // 2^OpParam
                            perLevel = threshold.ToString(CultureInfo.InvariantCulture) + "ths";
                        }
                    }

                    // Resolve descstrpos to its translated value; avoid calling Table.GetValue for 'nul' (filtered above)
                    var desc = isc.DescriptonStringPositive ?? string.Empty;

                    w.WriteLine(string.Join(
                        "\t",
                        Sanitize(isc.Stat),
                        Sanitize(desc),
                        Sanitize(range),
                        Sanitize(paramRange),
                        Sanitize(perLevel)
                    ));

                    SkipRow: ;
                    continue;

                    // Sanitize for TSV (replace tabs/newlines)
                    string Sanitize(string s) => (s ?? string.Empty).Replace("\t", " ").Replace("\r", "").Replace("\n", "");
                }
            }
        }

        private static string FormatNumber(double value)
        {
            // Trim trailing zeros but keep up to 2 decimals
            var s = value.ToString("0.##", CultureInfo.InvariantCulture);
            return s;
        }

        // Substring filters for ItemStatCost export (case-insensitive)
        private static readonly System.Collections.Generic.HashSet<string> ExcludedFragments =
            new System.Collections.Generic.HashSet<string>(new[]
            {
                "bytime",
                "dye_",
                "stacked_",
                "stamina",
                "_immunity",
                "fade",
                "_control",
                "_weight",
                "splashonhit",
                "rave",
                "extra_",
                "corrupted",
                "vs_montype"
            }, StringComparer.OrdinalIgnoreCase);

    }
}
