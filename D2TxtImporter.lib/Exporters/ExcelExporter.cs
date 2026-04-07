using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using D2TxtImporter.lib.Model.Dictionaries;
using D2TxtImporter.lib.Model.Items;

namespace D2TxtImporter.lib.Exporters
{
    public static class ExcelExporter
    {
        private static string QualifyRangeWithSheet(IXLWorksheet ws, IXLRangeBase range)
        {
            // Wrap sheet name in single quotes to be safe with spaces
            var sheetName = ws.Name.Replace("'", "''");
            // RangeAddress.ToStringFixed() returns e.g. $A$2:$A$100
            return $"'{sheetName}'!{range.RangeAddress.ToStringFixed()}";
        }

        public static void ExportExcel(string outputPath, List<Unique> uniques, List<Runeword> runewords, List<Set> sets)
        {
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Ensure Docs\\extras exists (we receive Docs as outputPath from Importer)
            var extrasDir = Path.Combine(outputPath, "extras");
            if (!Directory.Exists(extrasDir))
            {
                Directory.CreateDirectory(extrasDir);
            }

            var wb = new XLWorkbook();

            // Sheets
            var home = wb.AddWorksheet("Home");
            var wsUniques = wb.AddWorksheet("Uniques");
            var wsSets = wb.AddWorksheet("Sets");
            var wsRunewords = wb.AddWorksheet("Runewords");

            // Build Uniques sheet
            BuildUniquesSheet(wsUniques, uniques);
            // Build Sets sheet
            var setNames = BuildSetsSheet(wsSets, sets);
            // Build Runewords sheet
            BuildRunewordsSheet(wsRunewords, runewords);
            // Build Home (summary only)
            BuildHomeSheet(home, setNames.Count);

            // Save into extras
            var path = Path.Combine(extrasDir, "D2RR_Holy_Grail.xlsx");
            wb.SaveAs(path);

            // Remove the calcChain from the saved xlsx to prevent Excel repair errors.
            // ClosedXML 0.97 generates a calcChain with incorrect dependency ordering
            // which causes "Repaired Records: Cell information" errors when opened in Excel.
            // Excel safely rebuilds the calcChain on open.
            RemoveCalcChain(path);
        }

        private static void BuildUniquesSheet(IXLWorksheet ws, List<Unique> uniques)
        {
            // Columns: Found, Name, Base, BaseType, RequiredLevel, Rarity
            var headers = new[] { "Found", "Name", "Base", "Base Type", "Required Level", "Rarity" };
            for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;

            var rows = uniques
                .OrderBy(u => u?.Equipment?.Type?.Name)
                .ThenBy(u => u.RequiredLevel)
                .ThenBy(u => u.Name)
                .Select(u => new
                {
                    // Default to blank; any non-blank value counts as found
                    Found = "",
                    u.Name,
                    Base = u?.Equipment?.Name,
                    BaseType = u?.Equipment?.Type?.Name,
                    RequiredLevel = u.RequiredLevel,
                    u.Rarity
                }).ToList();

            var rowIndex = 2;
            foreach (var row in rows)
            {
                ws.Cell(rowIndex, 1).Value = row.Found;
                ws.Cell(rowIndex, 2).Value = row.Name;
                ws.Cell(rowIndex, 3).Value = row.Base;
                ws.Cell(rowIndex, 4).Value = row.BaseType;
                ws.Cell(rowIndex, 5).Value = row.RequiredLevel;
                ws.Cell(rowIndex, 6).Value = row.Rarity;
                rowIndex++;
            }

            var lastRow = Math.Max(rowIndex - 1, 1);
            var tblRange = ws.Range(1, 1, lastRow, headers.Length);
            var table = tblRange.CreateTable("UniquesTable");
            table.ShowAutoFilter = true;
            // Apply table theme: Light 21 for Uniques
            table.Theme = XLTableTheme.TableStyleLight21;
            // Move totals to header area instead of table totals row
            table.ShowTotalsRow = false;

            // Header styling: black background, white text (only table header to avoid interfering with extra header cells)
            headerRange.Style.Fill.BackgroundColor = XLColor.Black;
            headerRange.Style.Font.FontColor = XLColor.White;

            // Header sticky total on the right (immediately after headers)
            int totalHeaderCol = headers.Length + 1;
            ws.Cell(1, totalHeaderCol).Value = "Total Found:";
            ws.Cell(1, totalHeaderCol).Style.Font.Bold = true;
            // Count any non-blank cell in the Found column
            ws.Cell(1, totalHeaderCol + 1).FormulaA1 = "=COUNTA(UniquesTable[Found])";
            // Style the Total Found label and count as black background with white text
            ws.Cell(1, totalHeaderCol).Style.Fill.BackgroundColor = XLColor.Black;
            ws.Cell(1, totalHeaderCol).Style.Font.FontColor = XLColor.White;
            var uniquesTotalCountCell = ws.Cell(1, totalHeaderCol + 1);
            uniquesTotalCountCell.Style.Fill.BackgroundColor = XLColor.Black;
            uniquesTotalCountCell.Style.Font.FontColor = XLColor.White;

            // No dropdown/data validation for Found column

            // No conditional formatting or banding
            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();
        }

        private static HashSet<string> BuildSetsSheet(IXLWorksheet ws, List<Set> sets)
        {
            // Columns: Found, Item Name, Base, BaseType, Item Lvl Req, Rarity, Set Name, Set Max Lvl Req
            var headers = new[] { "Found", "Item Name", "Base", "Base Type", "Item Lvl Req", "Rarity", "Set Name", "Set Max Lvl Req" };
            for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;

            // Pre-compute set max required level
            var setMaxReq = sets.ToDictionary(s => s.Name, s => (s.SetItems != null && s.SetItems.Count > 0) ? s.SetItems.Max(si => si.RequiredLevel) : s.Level);

            var items = new List<(string SetName, SetItem Item)>();
            foreach (var s in sets)
            {
                if (s.SetItems == null) continue;
                foreach (var si in s.SetItems)
                {
                    items.Add((s.Name, si));
                }
            }

            var rows = items
                .OrderBy(x => x.SetName)
                .ThenByDescending(x => setMaxReq.ContainsKey(x.SetName) ? setMaxReq[x.SetName] : 0)
                .ThenBy(x => x.Item?.Equipment?.Name)
                .ThenBy(x => x.Item?.Name)
                .Select(x => new
                {
                    // Default to blank; any non-blank value counts as found
                    Found = "",
                    ItemName = x.Item?.Name,
                    Base = x.Item?.Equipment?.Name,
                    BaseType = x.Item?.Equipment?.Type?.Name,
                    ItemRequiredLevel = x.Item?.RequiredLevel ?? 0,
                    Rarity = x.Item?.Rarity ?? 0,
                    SetName = x.SetName,
                    SetMaxRequiredLevel = setMaxReq.ContainsKey(x.SetName) ? setMaxReq[x.SetName] : 0
                }).ToList();

            var rowIndex = 2;
            foreach (var row in rows)
            {
                ws.Cell(rowIndex, 1).Value = row.Found;
                ws.Cell(rowIndex, 2).Value = row.ItemName;
                ws.Cell(rowIndex, 3).Value = row.Base;
                ws.Cell(rowIndex, 4).Value = row.BaseType;
                ws.Cell(rowIndex, 5).Value = row.ItemRequiredLevel;
                ws.Cell(rowIndex, 6).Value = row.Rarity;
                ws.Cell(rowIndex, 7).Value = row.SetName;
                ws.Cell(rowIndex, 8).Value = row.SetMaxRequiredLevel;
                rowIndex++;
            }

            var lastRow = Math.Max(rowIndex - 1, 1);
            var tblRange = ws.Range(1, 1, lastRow, headers.Length);
            var table = tblRange.CreateTable("SetsTable");
            table.ShowAutoFilter = true;
            // Apply table theme: Light 18 for Sets
            table.Theme = XLTableTheme.TableStyleLight18;
            table.ShowTotalsRow = false;

            // Header styling: black background, white text for table header range
            headerRange.Style.Fill.BackgroundColor = XLColor.Black;
            headerRange.Style.Font.FontColor = XLColor.White;

            // Header sticky total on the right (immediately after headers)
            int totalHeaderCol = headers.Length + 1;
            ws.Cell(1, totalHeaderCol).Value = "Total Found:";
            ws.Cell(1, totalHeaderCol).Style.Font.Bold = true;
            // Count any non-blank cell in the Found column
            ws.Cell(1, totalHeaderCol + 1).FormulaA1 = "=COUNTA(SetsTable[Found])";
            // Style the Total Found label and count as black background with white text
            ws.Cell(1, totalHeaderCol).Style.Fill.BackgroundColor = XLColor.Black;
            ws.Cell(1, totalHeaderCol).Style.Font.FontColor = XLColor.White;
            var setsTotalCountCell = ws.Cell(1, totalHeaderCol + 1);
            setsTotalCountCell.Style.Fill.BackgroundColor = XLColor.Black;
            setsTotalCountCell.Style.Font.FontColor = XLColor.White;

            // No dropdown/data validation for Found column

            // No conditional formatting
            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();

            // Helper area to compute completed sets
            // We list distinct Set Names in a hidden area and compute if all its items are found
            var distinctSets = items.Select(x => x.SetName).Distinct().OrderBy(x => x).ToList();
            int helperColStart = headers.Length + 3; // leave some gap
            int helperRowStart = 1;
            ws.Cell(helperRowStart, helperColStart).Value = "Helper_SetName";
            ws.Cell(helperRowStart, helperColStart + 1).Value = "Helper_Completed";
            ws.Range(helperRowStart, helperColStart, helperRowStart, helperColStart + 1).Style.Font.Bold = true;
            for (int i = 0; i < distinctSets.Count; i++)
            {
                var row = helperRowStart + 1 + i;
                var setNameCell = ws.Cell(row, helperColStart);
                setNameCell.Value = distinctSets[i];
                var completedCell = ws.Cell(row, helperColStart + 1);
                // Completed if every item in the set has a non-blank Found value
                // COUNTIFS counts rows where Set Name matches AND Found is blank; if zero, the set is complete
                completedCell.FormulaA1 = $"=IF(COUNTIFS(SetsTable[Set Name],{setNameCell.Address.ToStringRelative()},SetsTable[Found],\"\")=0,1,0)";
            }
            // Hide helper columns
            ws.Column(helperColStart).Hide();
            ws.Column(helperColStart + 1).Hide();
            // Name the completed flags range so Home can sum it robustly
            if (distinctSets.Count > 0)
            {
                var flagsRange = ws.Range(helperRowStart + 1, helperColStart + 1, helperRowStart + distinctSets.Count, helperColStart + 1);
                flagsRange.AddToNamed("CompletedSetsFlags", XLScope.Workbook);
            }

            return new HashSet<string>(distinctSets);
        }

        private static void BuildRunewordsSheet(IXLWorksheet ws, List<Runeword> runewords)
        {
            // Columns: Found, Name, Required Level, Runes, Types
            var headers = new[] { "Found", "Name", "Required Level", "Runes", "Types" };
            for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;

            string JoinRunes(Runeword rw)
            {
                if (rw?.Runes == null) return null;
                return string.Join(" + ", rw.Runes.Select(r => r?.Name));
            }
            string JoinTypes(Runeword rw)
            {
                if (rw?.Types == null) return null;
                return string.Join(", ", rw.Types.Select(t => t?.Name));
            }

            var rows = runewords
                .OrderBy(rw => rw.RequiredLevel)
                .ThenBy(rw => rw.Name)
                .Select(rw => new
                {
                    // Default to blank; any non-blank value counts as found
                    Found = "",
                    rw.Name,
                    RequiredLevel = rw.RequiredLevel,
                    Runes = JoinRunes(rw),
                    Types = JoinTypes(rw)
                }).ToList();

            var rowNum = 2;
            foreach (var row in rows)
            {
                ws.Cell(rowNum, 1).Value = row.Found;
                ws.Cell(rowNum, 2).Value = row.Name;
                ws.Cell(rowNum, 3).Value = row.RequiredLevel;
                ws.Cell(rowNum, 4).Value = row.Runes;
                ws.Cell(rowNum, 5).Value = row.Types;
                rowNum++;
            }

            var lastRow = Math.Max(rowNum - 1, 1);
            var tblRange = ws.Range(1, 1, lastRow, headers.Length);
            var table = tblRange.CreateTable("RunewordsTable");
            table.ShowAutoFilter = true;
            // Apply requested table theme: Light 20 for Runewords
            table.Theme = XLTableTheme.TableStyleLight20;
            table.ShowTotalsRow = false;

            // Header styling: black background, white text for table header range
            headerRange.Style.Fill.BackgroundColor = XLColor.Black;
            headerRange.Style.Font.FontColor = XLColor.White;

            // Header sticky total on the right (immediately after headers)
            int totalHeaderCol = headers.Length + 1;
            ws.Cell(1, totalHeaderCol).Value = "Total Found:";
            ws.Cell(1, totalHeaderCol).Style.Font.Bold = true;
            // Count any non-blank cell in the Found column
            ws.Cell(1, totalHeaderCol + 1).FormulaA1 = "=COUNTA(RunewordsTable[Found])";
            // Style the Total Found label and count as black background with white text
            ws.Cell(1, totalHeaderCol).Style.Fill.BackgroundColor = XLColor.Black;
            ws.Cell(1, totalHeaderCol).Style.Font.FontColor = XLColor.White;
            var runewordsTotalCountCell = ws.Cell(1, totalHeaderCol + 1);
            runewordsTotalCountCell.Style.Fill.BackgroundColor = XLColor.Black;
            runewordsTotalCountCell.Style.Font.FontColor = XLColor.White;

            // Per request: no dropdown/data validation for Found column

            // No conditional formatting by request
            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();
            // No banding or colors by request
        }

        private static void BuildHomeSheet(IXLWorksheet ws, int totalDistinctSets)
        {
            // Leave row 1 and column A empty as spacers
            // Content starts at row 2, column B
            int colStart = 2; // Column B
            int summaryCols = 4; // B..E

            // Top banner merged across columns B..E (row 2)
            var banner = ws.Range(2, colStart, 2, colStart + summaryCols - 1);
            banner.Merge();
            banner.Value = "D2R Reimagined Holy Grail Checklist";
            banner.Style.Font.Bold = true;
            banner.Style.Font.FontSize = 16;
            banner.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            // Banner formatting: black background with white text
            banner.Style.Fill.BackgroundColor = XLColor.Black;
            banner.Style.Font.FontColor = XLColor.White;

            // Links row using HYPERLINK formulas on row 3 (B..E)
            ws.Cell(3, colStart + 0).FormulaA1 = "=HYPERLINK(\"https://www.d2r-reimagined.com/\",\"Web\")";
            ws.Cell(3, colStart + 1).FormulaA1 = "=HYPERLINK(\"https://wiki.d2r-reimagined.com/\",\"Wiki\")";
            ws.Cell(3, colStart + 2).FormulaA1 = "=HYPERLINK(\"https://www.nexusmods.com/diablo2resurrected/mods/503\",\"Nexus\")";
            ws.Cell(3, colStart + 3).FormulaA1 = "=HYPERLINK(\"https://discord.gg/4QENnUfqPd\",\"Discord\")";
            var linksRow = ws.Range(3, colStart, 3, colStart + summaryCols - 1);
            linksRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            linksRow.Style.Font.Bold = true;

            // Summary header at row 5 (B..E)
            int rowNum = 5;
            int colCategory = colStart + 0; // B
            int colFound = colStart + 1;    // C
            int colTotal = colStart + 2;    // D
            int colNotes = colStart + 3;    // E

            ws.Cell(rowNum, colCategory).Value = "Category";
            ws.Cell(rowNum, colFound).Value = "Found";
            ws.Cell(rowNum, colTotal).Value = "Total";
            ws.Cell(rowNum, colNotes).Value = "Notes";
            var headerRow = ws.Range(rowNum, colStart, rowNum, colStart + summaryCols - 1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.Black;
            headerRow.Style.Font.FontColor = XLColor.White;
            rowNum++;

            // Define associated background colors for categories
            var uniquesBg = XLColor.FromArgb(221, 235, 247);   // light blue
            var setsBg = XLColor.FromArgb(226, 239, 218);      // light green
            var compSetsBg = XLColor.FromArgb(198, 224, 180);  // green tint
            var runewordsBg = XLColor.FromArgb(255, 229, 204); // light orange
            // Previous design used light red for totals; per latest request, totals should be purple numeric

            // Helper local function to format a category row
            void FormatCategoryRow(int r, string label, string foundFormula, string totalFormulaOrValue, string notes, XLColor bodyBg, bool totalIsFormula)
            {
                ws.Cell(r, colCategory).Value = label;
                ws.Cell(r, colCategory).Style.Font.Bold = true;
                // Category column cell black/white
                ws.Cell(r, colCategory).Style.Fill.BackgroundColor = XLColor.Black;
                ws.Cell(r, colCategory).Style.Font.FontColor = XLColor.White;

                if (!string.IsNullOrEmpty(foundFormula))
                    ws.Cell(r, colFound).FormulaA1 = foundFormula;
                // Total column should be numeric and purple text
                if (totalIsFormula)
                {
                    ws.Cell(r, colTotal).FormulaA1 = totalFormulaOrValue;
                }
                else
                {
                    // Ensure numeric when a constant was provided (e.g., totalDistinctSets)
                    if (int.TryParse(totalFormulaOrValue, out var parsed))
                        ws.Cell(r, colTotal).Value = parsed;
                    else
                        ws.Cell(r, colTotal).Value = totalFormulaOrValue;
                }
                if (!string.IsNullOrEmpty(notes))
                    ws.Cell(r, colNotes).Value = notes;

                // Background color for remainder populated cells
                ws.Range(r, colFound, r, colNotes).Style.Fill.BackgroundColor = bodyBg;
                // Style Total column: number format and purple text
                ws.Cell(r, colTotal).Style.NumberFormat.Format = "0";
                ws.Cell(r, colTotal).Style.Font.FontColor = XLColor.Purple;
                ws.Cell(r, colFound).Style.Font.Bold = true;
                ws.Cell(r, colTotal).Style.Font.Bold = true;
            }

            // Uniques summary
            FormatCategoryRow(
                rowNum++,
                "Uniques",
                "=COUNTA(UniquesTable[Found])",
                "=ROWS(UniquesTable[Found])",
                null,
                uniquesBg,
                true);

            // Sets summary
            FormatCategoryRow(
                rowNum++,
                "Set Items",
                "=COUNTA(SetsTable[Found])",
                "=ROWS(SetsTable[Found])",
                null,
                setsBg,
                true);

            // Completed Sets summary (uses named range from Sets sheet)
            FormatCategoryRow(
                rowNum++,
                "Completed Sets",
                "=SUM(CompletedSetsFlags)",
                totalDistinctSets.ToString(),
                "Distinct set names",
                compSetsBg,
                false);

            // Runewords summary
            FormatCategoryRow(
                rowNum++,
                "Runewords",
                "=COUNTA(RunewordsTable[Found])",
                "=ROWS(RunewordsTable[Found])",
                null,
                runewordsBg,
                true);

            // Spacer row
            rowNum++;

            // Grand Total row
            ws.Cell(rowNum, colCategory).Value = "Grand Total";
            ws.Cell(rowNum, colCategory).Style.Font.Bold = true;
            ws.Cell(rowNum, colCategory).Style.Fill.BackgroundColor = XLColor.Black;
            ws.Cell(rowNum, colCategory).Style.Font.FontColor = XLColor.White;
            ws.Cell(rowNum, colFound).FormulaA1 = "=COUNTA(UniquesTable[Found])+COUNTA(SetsTable[Found])+COUNTA(RunewordsTable[Found])";
            ws.Cell(rowNum, colTotal).FormulaA1 = "=ROWS(UniquesTable[Found])+ROWS(SetsTable[Found])+ROWS(RunewordsTable[Found])";
            ws.Range(rowNum, colFound, rowNum, colNotes).Style.Fill.BackgroundColor = XLColor.FromArgb(242, 242, 242); // light gray backdrop
            // Style Total (grand) as numeric and purple text
            ws.Cell(rowNum, colTotal).Style.NumberFormat.Format = "0";
            ws.Cell(rowNum, colTotal).Style.Font.FontColor = XLColor.Purple;
            ws.Cell(rowNum, colFound).Style.Font.Bold = true;
            ws.Cell(rowNum, colTotal).Style.Font.Bold = true;

            ws.Columns().AdjustToContents();
        }

        /// <summary>
        /// Removes the calcChain part from a saved xlsx file.
        /// ClosedXML 0.97 can generate a calcChain with incorrect dependency ordering,
        /// which causes Excel to report "Repaired Records: Cell information" errors.
        /// Excel safely rebuilds the calcChain when the workbook is opened.
        /// </summary>
        private static void RemoveCalcChain(string xlsxPath)
        {
            using (var zip = ZipFile.Open(xlsxPath, ZipArchiveMode.Update))
            {
                // Remove the calcChain part
                var calcChainEntry = zip.GetEntry("xl/calcChain.xml");
                if (calcChainEntry != null)
                {
                    calcChainEntry.Delete();
                }

                // Remove the calcChain reference from [Content_Types].xml
                var contentTypesEntry = zip.GetEntry("[Content_Types].xml");
                if (contentTypesEntry != null)
                {
                    string contentTypesXml;
                    using (Stream stream = contentTypesEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        contentTypesXml = reader.ReadToEnd();
                    }

                    var cleaned = Regex.Replace(
                        contentTypesXml,
                        @"<Override[^>]*PartName\s*=\s*""/xl/calcChain\.xml""[^>]*/?>",
                        string.Empty);

                    if (cleaned != contentTypesXml)
                    {
                        contentTypesEntry.Delete();
                        var newEntry = zip.CreateEntry("[Content_Types].xml", CompressionLevel.Optimal);
                        using (Stream stream = newEntry.Open())
                        using (var writer = new StreamWriter(stream))
                        {
                            writer.Write(cleaned);
                        }
                    }
                }

                // Remove the calcChain relationship from xl/_rels/workbook.xml.rels
                var relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
                if (relsEntry != null)
                {
                    string relsXml;
                    using (Stream stream = relsEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        relsXml = reader.ReadToEnd();
                    }

                    var cleaned = Regex.Replace(
                        relsXml,
                        @"<Relationship[^>]*Target\s*=\s*""calcChain\.xml""[^>]*/?>",
                        string.Empty);

                    if (cleaned != relsXml)
                    {
                        relsEntry.Delete();
                        var newEntry = zip.CreateEntry("xl/_rels/workbook.xml.rels", CompressionLevel.Optimal);
                        using (Stream stream = newEntry.Open())
                        using (var writer = new StreamWriter(stream))
                        {
                            writer.Write(cleaned);
                        }
                    }
                }
            }
        }
    }
}
