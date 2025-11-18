using System.Collections.Generic;
using System.IO;
using D2TxtImporter.lib.Exceptions;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Dictionaries 
{

    public class Table 
    {

        [JsonIgnore]
        public static Dictionary<string, string> Tables;

        // Toggle to enable/disable duplicate key tracking and report writing
        [JsonIgnore]
        public static bool EnableDuplicateReport = true;

        // Tracks the origin (id, file) for each first-seen key
        [JsonIgnore]
        private static Dictionary<string, OriginInfo> _keyOrigins;

        // Accumulates duplicate key occurrences for later reporting
        [JsonIgnore]
        private static readonly List<DuplicateInfo> _duplicates = new List<DuplicateInfo>();

        public static void ImportFromTxt(string tableFolder) {
            // Case-sensitive keys by default; do not trim or normalize casing
            Tables = new Dictionary<string, string>();
            if (EnableDuplicateReport)
            {
                _keyOrigins = new Dictionary<string, OriginInfo>();
                _duplicates.Clear();
            }

            var files = Directory.GetFiles(tableFolder, "*.txt");
            foreach (var file in files)
            {
                var lines = Importer.ReadTxtFileToList(file);

                foreach (var line in lines)
                {
                    var values = line.Split('\t');

                    // Keep key casing and whitespace as-is (only strip wrapping quotes from format)
                    var key = values[0].Trim('"');
                    var value = values[1].Trim('"');

                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    if (EnableDuplicateReport && Tables.ContainsKey(key))
                    {
                        var currentFile = Path.GetFileName(file);
                        if (_keyOrigins != null && _keyOrigins.TryGetValue(key, out var prev))
                        {
                            _duplicates.Add(new DuplicateInfo
                            {
                                Key = key,
                                FirstId = prev.Id,
                                FirstFile = prev.FileName,
                                NewId = 0,
                                NewFile = currentFile
                            });
                        }
                    }

                    Tables[key] = value;
                    if (EnableDuplicateReport)
                    {
                        _keyOrigins[key] = new OriginInfo { Id = 0, FileName = Path.GetFileName(file) };
                    }
                }
            }
        }

        public static void ImportFromTbl(string tableFolder) 
        {
            // Prefer JSON if present, to avoid changing callers.
            var jsonFiles = Directory.GetFiles(tableFolder, "*.json");
            if (jsonFiles.Length > 0)
            {
                ImportFromJson(tableFolder);
                return;
            }

            // Case-sensitive keys by default; do not trim or normalize casing
            Tables = new Dictionary<string, string>();
            if (EnableDuplicateReport)
            {
                _keyOrigins = new Dictionary<string, OriginInfo>();
                _duplicates.Clear();
            }

            var files = Directory.GetFiles(tableFolder, "*.tbl");

            if (files.Length == 0) 
            {
                ExceptionHandler.LogException(
                    new System.Exception($"Could not find any .json or .tbl files in '{tableFolder}'"));
            }

            foreach (var file in files) 
            {

                var hashTable = TableProcessor.ReadTablesFile(file);

                foreach (var tableEntry in hashTable)
                {
                    var value = tableEntry.Value;

                    // Strip Diablo color codes like "ÿcX" (always 3 characters: 'ÿ', 'c', code)
                    if (!string.IsNullOrEmpty(value))
                    {
                        value = RemoveColorCodes(value);
                    }

                    // Keep key as-is (no trimming or normalization)
                    var key = tableEntry.Key;
                    if (string.IsNullOrEmpty(key)) 
                    {
                        continue;
                    }

                    if (EnableDuplicateReport && Tables.ContainsKey(key))
                    {
                        var currentFile = Path.GetFileName(file);
                        if (_keyOrigins != null && _keyOrigins.TryGetValue(key, out var prev))
                        {
                            _duplicates.Add(new DuplicateInfo
                            {
                                Key = key,
                                FirstId = prev.Id,
                                FirstFile = prev.FileName,
                                NewId = 0,
                                NewFile = currentFile
                            });
                        }
                    }

                    Tables[key] = value;
                    if (EnableDuplicateReport)
                    {
                        _keyOrigins[key] = new OriginInfo { Id = 0, FileName = Path.GetFileName(file) };
                    }
                }
            }
        }

        private static string RemoveColorCodes(string input)
        {
            if (string.IsNullOrEmpty(input)) 
            {
                return input;
            }

            var result = new System.Text.StringBuilder(input.Length);
            int i = 0;

            while (i < input.Length) {
                // Look for 'ÿ', 'c', and a third character and skip it if found
                if (input[i] == 'ÿ' && i + 2 < input.Length && input[i + 1] == 'c') {
                    i += 3;
                    continue;
                }
                // Also remove any '*' characters per requirement
                if (input[i] == '*') 
                {
                    i++;
                    continue;
                }
                result.Append(input[i]);
                i++;
            }
            return result.ToString();
        }

        // Expects each .json file to contain an array of objects with at least:
        // { "Key": "Betsy", "enUS": "Mistress of the Pasture" }
        public static void ImportFromJson(string tableFolder) 
        {
            // Case-sensitive keys by default; do not trim or normalize casing
            Tables = new Dictionary<string, string>();
            if (EnableDuplicateReport)
            {
                _keyOrigins = new Dictionary<string, OriginInfo>();
                _duplicates.Clear();
            }

            var files = Directory.GetFiles(tableFolder, "*.json");

            if (files.Length == 0) 
            {
                ExceptionHandler.LogException(
                    new System.Exception($"Could not find any .json files in '{tableFolder}'"));
            }

            foreach (var file in files)
            {
                try {
                    var json = File.ReadAllText(file);

                    var entries = JsonConvert.DeserializeObject<List<JsonTableEntry>>(json);

                    if (entries == null) 
                    {
                        ExceptionHandler.LogException(
                            new System.Exception($"JSON file '{file}' did not contain a valid entries array"));
                        continue;
                    }

                    foreach (var entry in entries) 
                    {
                        if (string.IsNullOrEmpty(entry.Key)) {
                            continue;
                        }

                        // Use Key for lookup, enUS as the text value.
                        var value = entry.EnUs ?? string.Empty;

                        // S color codes
                        if (!string.IsNullOrEmpty(value))
                        {
                            value = RemoveColorCodes(value);
                        }
                        // Keep key as-is (no trimming)
                        var key = entry.Key;
                        if (string.IsNullOrEmpty(key)) 
                        {
                            continue;
                        }

                        // Collect report info if a duplicate key is encountered (will overwrite previous value)
                        if (EnableDuplicateReport && Tables.ContainsKey(key)) 
                        {
                            var currentFile = Path.GetFileName(file);

                            if (_keyOrigins != null && _keyOrigins.TryGetValue(key, out var prev))
                            {
                                _duplicates.Add(new DuplicateInfo
                                {
                                    Key = key,
                                    FirstId = prev.Id,
                                    FirstFile = prev.FileName,
                                    NewId = entry.Id,
                                    NewFile = currentFile
                                });
                            }
                        }

                        Tables[key] = value;
                        // Track the latest origin of this key for potential future duplicates
                        if (EnableDuplicateReport)
                        {
                            _keyOrigins[key] = new OriginInfo { Id = entry.Id, FileName = Path.GetFileName(file) };
                        }
                    }
                }

                catch (System.Exception ex) 
                {
                    ExceptionHandler.LogException(
                        new System.Exception($"Failed to read JSON table file '{file}'", ex));
                }
            }
        }

        public static string GetValue(string key) 
        {
            if (string.IsNullOrEmpty(key)) 
            {
                return null;
            }

            // Try exact lookup first (case-sensitive)
            if (Tables.ContainsKey(key)) 
            {
                var value = Tables[key];

                // Fix class skills
                if (key == "ModStr3a") 
                {
                    value = value.Replace("Amazon", "%s");
                }
                return value;
            }

            // If not found, check if a case-insensitive match exists to signal casing issue
            string caseMismatchKey = null;
            foreach (var existing in Tables.Keys)
            {
                if (string.Equals(existing, key, System.StringComparison.OrdinalIgnoreCase) && existing != key)
                {
                    caseMismatchKey = existing;
                    break;
                }
            }

            if (caseMismatchKey != null)
            {
                // Try to include file information if we have it
                var file = _keyOrigins != null && _keyOrigins.TryGetValue(caseMismatchKey, out var origin)
                    ? origin.FileName
                    : "tables";
                // Throw to allow user to fix input and rerun
                throw new System.Exception($"Key not found for \"{key}\" in \"{file}\"");
            }

            // Otherwise, report generic missing key
            ExceptionHandler.LogException(
                new System.Exception($"Could not find key '{key}' in any table file (.json/.tbl)."));
            return null;
        }

        private class JsonTableEntry {

            public int Id { get; set; }
            public string Key { get; set; }
            public string EnUs { get; set; }

        }

        private class OriginInfo
        {
            public int Id { get; set; }
            public string FileName { get; set; }
        }

        private class DuplicateInfo
        {
            public string Key { get; set; }
            public int FirstId { get; set; }
            public string FirstFile { get; set; }
            public int NewId { get; set; }
            public string NewFile { get; set; }
        }

        // Write duplicate report into the same output directory used by JsonExporter ("<output>/json")
        public static void WriteDuplicateReport(string outputRootPath)
        {
            try
            {
                if (!EnableDuplicateReport)
                {
                    return; // Disabled
                }

                if (_duplicates == null || _duplicates.Count == 0)
                {
                    return; // Nothing to write
                }

                var jsonDir = Path.Combine(outputRootPath, "json");
                if (!Directory.Exists(jsonDir))
                {
                    Directory.CreateDirectory(jsonDir);
                }

                // Write as a simple .txt file with tab-separated values and no BOM to avoid odd characters in some spreadsheet programs
                var reportPath = Path.Combine(jsonDir, "jason key duplicates.txt");

                using (var sw = new StreamWriter(reportPath, false, new System.Text.UTF8Encoding(false)))
                {
                    // TSV header
                    sw.WriteLine("Key\tFirstID\tFirstFile\tNewID\tNewFile");
                    // TSV rows
                    foreach (var d in _duplicates)
                    {
                        sw.WriteLine($"{d.Key}\t{d.FirstId}\t{d.FirstFile}\t{d.NewId}\t{d.NewFile}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                ExceptionHandler.LogException(new System.Exception("Failed to write duplicate table keys report", ex));
            }
        }
    }
}
