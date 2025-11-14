using System.Collections.Generic;
using System.IO;
using D2TxtImporter.lib.Exceptions;
using Newtonsoft.Json;

namespace D2TxtImporter.lib.Model.Dictionaries {
    
    public class Table {
        
        [JsonIgnore]
        public static Dictionary<string, string> Tables;

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
                        
                        Tables[entry.Key] = value;
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
    }
}
