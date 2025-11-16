using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using D2TxtImporter.lib.Exceptions;
using D2TxtImporter.lib.Model.Types;

namespace D2TxtImporter.lib.Model.Dictionaries
{
    public static class RequiredLevelReport
    {
        [JsonIgnore]
        public static bool Enabled = false;

        private class Entry
        {
            public string ItemType { get; set; }
            public string ItemName { get; set; }
            public string PropertyName { get; set; }
            public int PropertyReqLevel { get; set; }
            public int ItemReqBefore { get; set; }
        }

        private static readonly List<Entry> _entries = new List<Entry>();
        // Track unique records to avoid duplication across multiple passes/runs
        private static readonly HashSet<string> _entryKeys = new HashSet<string>(StringComparer.Ordinal);

        // Clears accumulated entries and de-dup keys; call at the start of a run
        public static void Clear()
        {
            _entries.Clear();
            _entryKeys.Clear();
        }

        public static void Record(string itemType, string itemName, string propertyName, int propertyReqLevel, int itemReqBefore)
        {
            if (!Enabled)
            {
                return;
            }

            var key = string.Concat(itemType, "|", itemName, "|", propertyName, "|", propertyReqLevel.ToString(), "|", itemReqBefore.ToString());
            if (_entryKeys.Contains(key))
            {
                return; // already recorded
            }

            _entryKeys.Add(key);

            _entries.Add(new Entry
            {
                ItemType = itemType,
                ItemName = itemName,
                PropertyName = propertyName,
                PropertyReqLevel = propertyReqLevel,
                ItemReqBefore = itemReqBefore
            });
        }

        public static void WriteReport(string outputRootPath)
        {
            try
            {
                if (!Enabled || _entries.Count == 0)
                {
                    return;
                }

                var jsonDir = Path.Combine(outputRootPath, "json");
                if (!Directory.Exists(jsonDir))
                {
                    Directory.CreateDirectory(jsonDir);
                }

                var reportPath = Path.Combine(jsonDir, "required level properties.txt");

                using (var sw = new StreamWriter(reportPath, false, new System.Text.UTF8Encoding(false)))
                {
                    sw.WriteLine("ItemType\tItemName\tPropertyName\tPropertyReqLevel\tItemReqBefore");
                    foreach (var e in _entries)
                    {
                        sw.WriteLine($"{e.ItemType}\t{e.ItemName}\t{e.PropertyName}\t{e.PropertyReqLevel}\t{e.ItemReqBefore}");
                    }
                }

                // After writing a report, reset accumulators to avoid carry-over
                Clear();
            }
            catch (Exception ex)
            {
                ExceptionHandler.LogException(new Exception("Failed to write required level properties report", ex));
            }
        }
        
        // compute levelreq skill and oskill impact on item required level 
        public static int ComputeAdjustedRequiredLevel(string itemType, string itemName, int baseReqLevel, IEnumerable<ItemProperty> properties)
        {
            if (properties == null)
            {
                return baseReqLevel;
            }

            int explicitIncrease = 0;
            int maxImplied = 0;

            // For reporting, keep the implied levels per property together with a friendly name
            var impliedPerProp = new List<Tuple<string, int>>();

            foreach (var prop in properties)
            {
                // Sum explicit increases: item_levelreq
                var stat = prop?.ItemStatCost?.Stat;
                if (string.Equals(stat, "item_levelreq", StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Max.HasValue && prop.Max.Value > 0)
                    {
                        explicitIncrease += prop.Max.Value;
                    }
                    else if (prop.Min.HasValue && prop.Min.Value > 0)
                    {
                        explicitIncrease += prop.Min.Value;
                    }
                    // continue to next property; explicit modifiers do not imply separate requirements
                    continue;
                }

                // Consider implied requirement only for skill/oskill
                try
                {
                    var code = prop?.Property?.Code?.ToLowerInvariant();
                    if (code == "skill" || code == "oskill")
                    {
                        if (!string.IsNullOrEmpty(prop.Parameter))
                        {
                            var skill = Skill.GetSkill(prop.Parameter);
                            if (skill != null && skill.RequiredLevel > 0)
                            {
                                var implied = skill.RequiredLevel;
                                if (implied > maxImplied)
                                {
                                    maxImplied = implied;
                                }

                                var propName = !string.IsNullOrEmpty(prop.PropertyString)
                                    ? prop.PropertyString
                                    : (prop.Property?.Code ?? "");
                                impliedPerProp.Add(Tuple.Create(propName, implied));
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore resolution failures
                }
            }

            // Apply explicit increases first
            var afterExplicit = baseReqLevel + explicitIncrease;

            // If implied requirement from properties exceeds this, record each offending property and raise
            if (maxImplied > afterExplicit)
            {
                foreach (var tuple in impliedPerProp)
                {
                    var implied = tuple.Item2;
                    if (implied > afterExplicit)
                    {
                        Record(itemType, itemName, tuple.Item1, implied, afterExplicit);
                    }
                }
                return maxImplied;
            }

            return afterExplicit;
        }

        // Helper: compute explicit +Required Level increase from one or more property lists
        public static int ComputeRequiredLevelIncrease(params IEnumerable<ItemProperty>[] propertyLists)
        {
            int total = 0;
            if (propertyLists == null)
            {
                return 0;
            }

            foreach (var list in propertyLists)
            {
                if (list == null) continue;
                foreach (var prop in list)
                {
                    var stat = prop?.ItemStatCost?.Stat;
                    if (string.Equals(stat, "item_levelreq", StringComparison.OrdinalIgnoreCase))
                    {
                        if (prop.Max.HasValue && prop.Max.Value > 0) total += prop.Max.Value;
                        else if (prop.Min.HasValue && prop.Min.Value > 0) total += prop.Min.Value;
                    }
                }
            }
            return total;
        }
    }
}
