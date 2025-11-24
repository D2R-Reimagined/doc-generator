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
        [JsonIgnore]
        public static bool EnableDuplicateReport = true;
        [JsonIgnore]
        private static Dictionary<string, OriginInfo> _keyOrigins;
        [JsonIgnore]
        private static readonly List<DuplicateInfo> Duplicates = new List<DuplicateInfo>();
        [JsonIgnore]
        private static Dictionary<int, string> _idFirstFiles;

        public static void ImportFromTxt(string tableFolder) {
            
            Tables = new Dictionary<string, string>();
            
            if (EnableDuplicateReport)
            {
                _keyOrigins = new Dictionary<string, OriginInfo>();
                Duplicates.Clear();
                _idFirstFiles = new Dictionary<int, string>();
            }

            var files = Directory.GetFiles(tableFolder, "*.txt");
            foreach (var file in files)
            {
                var lines = Importer.ReadTxtFileToList(file);

                foreach (var line in lines)
                {
                    var values = line.Split('\t');
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
                            Duplicates.Add(new DuplicateInfo
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

            Tables = new Dictionary<string, string>();
            
            if (EnableDuplicateReport)
            {
                _keyOrigins = new Dictionary<string, OriginInfo>();
                Duplicates.Clear();
                _idFirstFiles = new Dictionary<int, string>();
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

                    if (!string.IsNullOrEmpty(value))
                    {
                        value = RemoveColorCodes(value);
                    }

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
                            Duplicates.Add(new DuplicateInfo
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
                if (input[i] == 'ÿ' && i + 2 < input.Length && input[i + 1] == 'c') {
                    i += 3;
                    continue;
                }
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

        // { "Key": "Betsy", "enUS": "Mistress of the Pasture" }
        public static void ImportFromJson(string tableFolder) 
        {
            Tables = new Dictionary<string, string>();
            if (EnableDuplicateReport)
            {
                _keyOrigins = new Dictionary<string, OriginInfo>();
                Duplicates.Clear();
                _idFirstFiles = new Dictionary<int, string>();
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

                        var value = entry.EnUs ?? string.Empty;

                        if (!string.IsNullOrEmpty(value))
                        {
                            value = RemoveColorCodes(value);
                        }
                        
                        var key = entry.Key;
                        
                        if (string.IsNullOrEmpty(key)) 
                        {
                            continue;
                        }

                        // Record duplicate numeric ID usages (JSON only), regardless of key equality
                        if (EnableDuplicateReport && _idFirstFiles != null)
                        {
                            var currentFileName = Path.GetFileName(file);
                            if (_idFirstFiles.TryGetValue(entry.Id, out var firstFileForId))
                            {
                                Duplicates.Add(new DuplicateInfo
                                {
                                    // Interleave into the same report: write the ID in the Key column
                                    Key = "ID:" + entry.Id.ToString(),
                                    FirstId = entry.Id,
                                    FirstFile = firstFileForId,
                                    NewId = entry.Id,
                                    NewFile = currentFileName
                                });
                            }
                            else
                            {
                                _idFirstFiles[entry.Id] = currentFileName;
                            }
                        }

                        // Collect report info if a duplicate key is encountered (will overwrite previous value)
                        if (EnableDuplicateReport && Tables.ContainsKey(key)) 
                        {
                            var currentFile = Path.GetFileName(file);

                            if (_keyOrigins != null && _keyOrigins.TryGetValue(key, out var prev))
                            {
                                Duplicates.Add(new DuplicateInfo
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

        private class JsonTableEntry 
        {
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

                if (Duplicates == null || Duplicates.Count == 0)
                {
                    return; // Nothing to write
                }

                var extrasDir = Path.Combine(outputRootPath, "extras");
                if (!Directory.Exists(extrasDir))
                {
                    Directory.CreateDirectory(extrasDir);
                }

                // Write as a simple .txt file with tab-separated values and no BOM to avoid odd characters in some spreadsheet programs
                var reportPath = Path.Combine(extrasDir, "jason key duplicates.txt");

                using (var sw = new StreamWriter(reportPath, false, new System.Text.UTF8Encoding(false)))
                {
                    // TSV header
                    sw.WriteLine("Key\tFirstID\tFirstFile\tNewID\tNewFile");
                    // TSV rows
                    foreach (var d in Duplicates)
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
