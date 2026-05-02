using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace D2TxtImporter.lib.Diff
{
    /// <summary>
    /// Parses a unified Git diff/patch and extracts changed keys for known tables
    /// (uniqueitems.txt, sets.txt, setitems.txt, runes.txt).
    /// The result is used to scope semantic diffs to entities actually mentioned in the PR.
    /// </summary>
    public static class DiffFromPatchService
    {
        public sealed class Targets
        {
            public HashSet<string> UniqueKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> SetKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> RunewordKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> BaseCodes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> CubeKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private enum FileKind
        {
            None,
            UniqueItems,
            Sets,
            SetItems,
            Runes,
            Weapons,
            Armors,
            CubeMain
        }

        public static Targets ParseUnifiedDiff(string patchPath)
        {
            var t = new Targets();
            if (string.IsNullOrWhiteSpace(patchPath) || !File.Exists(patchPath))
            {
                return t;
            }

            var lines = File.ReadAllLines(patchPath);
            FileKind current = FileKind.None;
            // Column map for the current file
            Dictionary<string, int> headerMap = null;

            foreach (var raw in lines)
            {
                var line = raw ?? string.Empty;

                // Start of file section
                if (line.StartsWith("diff --git ", StringComparison.Ordinal))
                {
                    current = ClassifyFile(line);
                    headerMap = null;
                    continue;
                }

                if (current == FileKind.None)
                {
                    continue;
                }

                // Skip file header markers
                if (line.StartsWith("--- ") || line.StartsWith("+++ "))
                    continue;

                // Capture header (columns are tab-separated). In unified diffs, context lines start with a space.
                var content = line.Length > 0 && (line[0] == '-' || line[0] == '+' || line[0] == ' ')
                    ? line.Substring(1)
                    : line;

                if (headerMap == null && LooksLikeHeader(content))
                {
                    headerMap = BuildHeaderMap(content);
                    continue;
                }

                // Only consider actual data lines (-/+), ignore context lines.
                if (!(line.StartsWith("-") || line.StartsWith("+")))
                    continue;

                // Ignore file markers '---' and '+++'
                if (line.StartsWith("--- ") || line.StartsWith("+++ "))
                    continue;

                // Need header to parse columns; if missing, best-effort parse by splitting and taking first column as key
                var data = content.Split('\t');
                string key = null;

                switch (current)
                {
                    case FileKind.UniqueItems:
                        key = GetCell(data, headerMap, "index", 0);
                        if (!string.IsNullOrWhiteSpace(key)) t.UniqueKeys.Add(key);
                        break;
                    case FileKind.Sets:
                        key = GetCell(data, headerMap, "index", 0);
                        if (!string.IsNullOrWhiteSpace(key)) t.SetKeys.Add(key);
                        break;
                    case FileKind.SetItems:
                        // We record the set item key too; semantic layer will associate with its parent set from JSON
                        key = GetCell(data, headerMap, "index", 0);
                        if (!string.IsNullOrWhiteSpace(key)) t.SetKeys.Add(key); // treat as relevant to sets section
                        break;
                    case FileKind.Runes:
                        key = GetCell(data, headerMap, "Name", 0);
                        if (!string.IsNullOrWhiteSpace(key)) t.RunewordKeys.Add(key);
                        break;
                    case FileKind.Weapons:
                        key = GetCell(data, headerMap, "code", 0);
                        if (!string.IsNullOrWhiteSpace(key)) t.BaseCodes.Add(key);
                        break;
                    case FileKind.Armors:
                        key = GetCell(data, headerMap, "code", 0);
                        if (!string.IsNullOrWhiteSpace(key)) t.BaseCodes.Add(key);
                        break;
                    case FileKind.CubeMain:
                        // Prefer the human Description when present; otherwise synthesize from first few columns
                        key = GetCell(data, headerMap, "description", 0) ?? GetCell(data, headerMap, "Description", 0);
                        if (string.IsNullOrWhiteSpace(key))
                        {
                            // Fallback: use first non-empty cell as key
                            key = data.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                        }
                        if (!string.IsNullOrWhiteSpace(key)) t.CubeKeys.Add(key);
                        break;
                }
            }

            return t;
        }

        private static FileKind ClassifyFile(string diffGitLine)
        {
            // Example: diff --git a/data/global/excel/uniqueitems.txt b/data/global/excel/uniqueitems.txt
            if (diffGitLine.IndexOf("uniqueitems.txt", StringComparison.OrdinalIgnoreCase) >= 0)
                return FileKind.UniqueItems;
            if (diffGitLine.IndexOf("sets.txt", StringComparison.OrdinalIgnoreCase) >= 0)
                return FileKind.Sets;
            if (diffGitLine.IndexOf("setitems.txt", StringComparison.OrdinalIgnoreCase) >= 0)
                return FileKind.SetItems;
            if (diffGitLine.IndexOf("runes.txt", StringComparison.OrdinalIgnoreCase) >= 0)
                return FileKind.Runes;
            if (diffGitLine.IndexOf("weapons.txt", StringComparison.OrdinalIgnoreCase) >= 0)
                return FileKind.Weapons;
            if (diffGitLine.IndexOf("armor.txt", StringComparison.OrdinalIgnoreCase) >= 0
                || diffGitLine.IndexOf("armors.txt", StringComparison.OrdinalIgnoreCase) >= 0)
                return FileKind.Armors;
            if (diffGitLine.IndexOf("cubemain.txt", StringComparison.OrdinalIgnoreCase) >= 0
                || diffGitLine.IndexOf("CubeMain.txt", StringComparison.OrdinalIgnoreCase) >= 0)
                return FileKind.CubeMain;
            return FileKind.None;
        }

        private static bool LooksLikeHeader(string content)
        {
            // Common headers start with 'index\t' for many tables, or include tabs with known column names
            if (string.IsNullOrWhiteSpace(content)) return false;
            if (content.IndexOf('\t') < 0) return false;
            return content.StartsWith("index\t", StringComparison.OrdinalIgnoreCase)
                   || content.IndexOf("\tName\t", StringComparison.OrdinalIgnoreCase) >= 0
                   || content.IndexOf("\t*ID\t", StringComparison.OrdinalIgnoreCase) >= 0
                   || content.IndexOf("\tcode\t", StringComparison.OrdinalIgnoreCase) >= 0
                   || content.IndexOf("\tdescription\t", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Dictionary<string, int> BuildHeaderMap(string headerLine)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var cols = headerLine.Split('\t');
            for (int i = 0; i < cols.Length; i++)
            {
                var h = cols[i]?.Trim();
                if (!string.IsNullOrEmpty(h) && !map.ContainsKey(h))
                {
                    map.Add(h, i);
                }
            }
            return map;
        }

        private static string GetCell(string[] data, Dictionary<string, int> headerMap, string preferredHeader, int fallbackIndex)
        {
            int idx = fallbackIndex;
            if (headerMap != null && headerMap.TryGetValue(preferredHeader, out var mapped))
            {
                idx = mapped;
            }
            if (idx >= 0 && idx < data.Length)
            {
                return (data[idx] ?? string.Empty).Trim();
            }
            return null;
        }
    }
}
