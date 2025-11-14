using System.Collections.Generic;
using System.IO;
using D2TxtImporter.lib.Exceptions;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Dictionaries {
    
    public class Table {
        
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
            Tables = new Dictionary<string, string>();

            var files = Directory.GetFiles(tableFolder, "*.txt");
            foreach (var file in files) {
                var lines = Importer.ReadTxtFileToList(file);

                foreach (var line in lines) {
                    var values = line.Split('\t');

                    var key = values[0].Trim('"');
                    var value = values[1].Trim('"');

                    if (string.IsNullOrEmpty(key)) {
                        continue;
                    }

                    Tables[key] = value;
                }
            }
        }
        
        public static void ImportFromTbl(string tableFolder) {
            // Prefer JSON if present, to avoid changing callers.
            var jsonFiles = Directory.GetFiles(tableFolder, "*.json");
            if (jsonFiles.Length > 0)
            {
                ImportFromJson(tableFolder);
                return;
            }

            Tables = new Dictionary<string, string>();

            var files = Directory.GetFiles(tableFolder, "*.tbl");

            if (files.Length == 0) {
                ExceptionHandler.LogException(
                    new System.Exception($"Could not find any .json or .tbl files in '{tableFolder}'"));
            }

            foreach (var file in files) {
                
                var hashTable = TableProcessor.ReadTablesFile(file);

                foreach (var tableEntry in hashTable) {
                    var value = tableEntry.Value;

                    // Strip Diablo color codes like "ÿcX" (always 3 characters: 'ÿ', 'c', code)
                    if (!string.IsNullOrEmpty(value)) {
                        value = RemoveColorCodes(value);
                    }

                    Tables[tableEntry.Key] = value;
                }
            }
        }

        private static string RemoveColorCodes(string input) {
            if (string.IsNullOrEmpty(input)) {
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
                result.Append(input[i]);
                i++;
            }
            return result.ToString();
        }
        
        // Expects each .json file to contain an array of objects with at least:
        // { "Key": "Betsy", "enUS": "Mistress of the Pasture" }
        public static void ImportFromJson(string tableFolder) {
            Tables = new Dictionary<string, string>();
            if (EnableDuplicateReport)
            {
                _keyOrigins = new Dictionary<string, OriginInfo>();
                _duplicates.Clear();
            }

            var files = Directory.GetFiles(tableFolder, "*.json");

            if (files.Length == 0) {
                ExceptionHandler.LogException(
                    new System.Exception($"Could not find any .json files in '{tableFolder}'"));
            }

            foreach (var file in files) {
                try {
                    var json = File.ReadAllText(file);

                    // File format: [ { "id": 3333, "Key": "priceless", "enUS": "Item cannot be traded here.", ... }, ... ]
                    var entries = JsonConvert.DeserializeObject<List<JsonTableEntry>>(json);

                    if (entries == null) {
                        ExceptionHandler.LogException(
                            new System.Exception($"JSON file '{file}' did not contain a valid entries array"));
                        continue;
                    }

                    foreach (var entry in entries) {
                        if (string.IsNullOrEmpty(entry.Key)) {
                            continue;
                        }

                        // Use Key for lookup, enUS as the text value.
                        var value = entry.enUS ?? string.Empty;

                        // S color codes
                        if (!string.IsNullOrEmpty(value)) {
                            value = RemoveColorCodes(value);
                        }
                        // Collect report info if a duplicate key is encountered (will overwrite previous value)
                        if (EnableDuplicateReport && Tables.ContainsKey(entry.Key)) {
                            var currentFile = System.IO.Path.GetFileName(file);

                            if (_keyOrigins != null && _keyOrigins.TryGetValue(entry.Key, out var prev))
                            {
                                _duplicates.Add(new DuplicateInfo
                                {
                                    Key = entry.Key,
                                    FirstId = prev.Id,
                                    FirstFile = prev.FileName,
                                    NewId = entry.ID,
                                    NewFile = currentFile
                                });
                            }
                        }

                        Tables[entry.Key] = value;
                        // Track the latest origin of this key for potential future duplicates
                        if (EnableDuplicateReport)
                        {
                            _keyOrigins[entry.Key] = new OriginInfo { Id = entry.ID, FileName = System.IO.Path.GetFileName(file) };
                        }
                    }
                }
                
                catch (System.Exception ex) {
                    ExceptionHandler.LogException(
                        new System.Exception($"Failed to read JSON table file '{file}'", ex));
                }
            }
        }

        public static string GetValue(string key) {
            if (Tables.ContainsKey(key)) {
                var value = Tables[key];

                // Fix class skills
                if (key == "ModStr3a") {
                    value = value.Replace("Amazon", "%s");
                }
                return value;
            }

            if (!string.IsNullOrEmpty(key)) {
                ExceptionHandler.LogException(
                    new System.Exception($"Could not find key '{key}' in any table file (.json/.tbl)"));
            }
            return null;
        }

        private class JsonTableEntry {
            
            public int ID { get; set; }
            public string Key { get; set; }
            public string enUS { get; set; }

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

                var jsonDir = System.IO.Path.Combine(outputRootPath, "json");
                if (!Directory.Exists(jsonDir))
                {
                    Directory.CreateDirectory(jsonDir);
                }

                // Write as a simple .txt file with tab-separated values and no BOM to avoid odd characters in some spreadsheet programs
                var reportPath = System.IO.Path.Combine(jsonDir, "duplicate_table_keys.txt");

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
