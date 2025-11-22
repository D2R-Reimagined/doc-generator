using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using D2TxtImporter.lib.Model.Dictionaries;
using D2TxtImporter.lib.Model.Equipment;
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

            // Save
            var path = Path.Combine(outputPath, "D2RR_Holy_Grail.xlsx");
            wb.SaveAs(path);
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
                    Found = false,
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
                ws.Cell(rowIndex, 1).DataType = XLDataType.Boolean;
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
            table.Theme = XLTableTheme.None;
            // Move totals to header area instead of table totals row
            table.ShowTotalsRow = false;
            var foundColRange = table.DataRange.Column(1); // Found column data only

            // Header sticky total on the right (immediately after headers)
            int totalHeaderCol = headers.Length + 1;
            ws.Cell(1, totalHeaderCol).Value = "Total Found:";
            ws.Cell(1, totalHeaderCol).Style.Font.Bold = true;
            ws.Cell(1, totalHeaderCol + 1).FormulaA1 = "=COUNTIF(A:A,TRUE)+COUNTIF(A:A,\"Y\")";

            // Data validation for Found column: allow TRUE/FALSE (booleans)
            var foundValidationRange = ws.Range(2, 1, lastRow, 1);
            var dv = foundValidationRange.CreateDataValidation();
            dv.AllowedValues = XLAllowedValues.List;
            dv.InCellDropdown = true;
            
            // Robust dropdown source via hidden helper range (handles locales and Excel quirks)
            int listCol = headers.Length + 5;
            ws.Cell(1, listCol).Value = true; ws.Cell(1, listCol).DataType = XLDataType.Boolean;
            ws.Cell(2, listCol).Value = false; ws.Cell(2, listCol).DataType = XLDataType.Boolean;
            ws.Column(listCol).Hide();
            var yesNoRange = ws.Range(1, listCol, 2, listCol);
            dv.List(yesNoRange);
            
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
                    Found = false,
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
                ws.Cell(rowIndex, 1).DataType = XLDataType.Boolean;
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
            table.Theme = XLTableTheme.None;
            table.ShowTotalsRow = false;
            var foundColRange = table.DataRange.Column(1);

            // Header sticky total on the right (immediately after headers)
            int totalHeaderCol = headers.Length + 1;
            ws.Cell(1, totalHeaderCol).Value = "Total Found:";
            ws.Cell(1, totalHeaderCol).Style.Font.Bold = true;
            ws.Cell(1, totalHeaderCol + 1).FormulaA1 = "=COUNTIF(A:A,TRUE)+COUNTIF(A:A,\"Y\")";

            // Data validation for Found column: allow TRUE/FALSE
            var foundValidationRange = ws.Range(2, 1, lastRow, 1);
            var dv = foundValidationRange.CreateDataValidation();
            dv.AllowedValues = XLAllowedValues.List;
            dv.InCellDropdown = true;
            int listCol = headers.Length + 5;
            ws.Cell(1, listCol).Value = true; ws.Cell(1, listCol).DataType = XLDataType.Boolean;
            ws.Cell(2, listCol).Value = false; ws.Cell(2, listCol).DataType = XLDataType.Boolean;
            ws.Column(listCol).Hide();
            var yesNoRange = ws.Range(1, listCol, 2, listCol);
            dv.List(yesNoRange);
            
            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();

            // Helper area to compute completed sets
            // We list distinct Set Names in a hidden area and compute if all its items are Found == "Y"
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
                // Completed if there are zero rows for the set where Found is neither TRUE nor "Y"
                // =IF(COUNTIFS(SetsTable[Set Name], L2, SetsTable[Found], "<>TRUE", SetsTable[Found], "<>\"Y\"")=0,1,0)
                completedCell.FormulaA1 = $"=IF(COUNTIFS(SetsTable[Set Name],{setNameCell.Address.ToStringRelative()},SetsTable[Found],\"<>TRUE\",SetsTable[Found],\"<>\"\"Y\"\"\")=0,1,0)";
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
                    Found = false,
                    rw.Name,
                    RequiredLevel = rw.RequiredLevel,
                    Runes = JoinRunes(rw),
                    Types = JoinTypes(rw)
                }).ToList();

            var rowNum = 2;
            foreach (var row in rows)
            {
                ws.Cell(rowNum, 1).Value = row.Found;
                ws.Cell(rowNum, 1).DataType = XLDataType.Boolean;
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
            table.Theme = XLTableTheme.None;
            table.ShowTotalsRow = false;
            var foundColRange = table.DataRange.Column(1);

            // Header sticky total on the right (immediately after headers)
            int totalHeaderCol = headers.Length + 1;
            ws.Cell(1, totalHeaderCol).Value = "Total Found:";
            ws.Cell(1, totalHeaderCol).Style.Font.Bold = true;
            ws.Cell(1, totalHeaderCol + 1).FormulaA1 = "=COUNTIF(A:A,TRUE)+COUNTIF(A:A,\"Y\")";

            // Data validation for Found column: allow TRUE/FALSE
            var foundValidationRange = ws.Range(2, 1, lastRow, 1);
            var dv = foundValidationRange.CreateDataValidation();
            dv.AllowedValues = XLAllowedValues.List;
            dv.InCellDropdown = true;
            int listCol = headers.Length + 5;
            ws.Cell(1, listCol).Value = true; ws.Cell(1, listCol).DataType = XLDataType.Boolean;
            ws.Cell(2, listCol).Value = false; ws.Cell(2, listCol).DataType = XLDataType.Boolean;
            ws.Column(listCol).Hide();
            var yesNoRange = ws.Range(1, listCol, 2, listCol);
            dv.List(yesNoRange);
            
            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();
        }

        private static void BuildHomeSheet(IXLWorksheet ws, int totalDistinctSets)
        {
            // Determine span for header rows: use 4 columns so banner spans summary table width
            int summaryCols = 4;

            // Links row using HYPERLINK formulas
            ws.Cell(2, 1).FormulaA1 = "=HYPERLINK(\"https://www.d2r-reimagined.com/\",\"Web\")";
            ws.Cell(2, 2).FormulaA1 = "=HYPERLINK(\"https://wiki.d2r-reimagined.com/\",\"Wiki\")";
            ws.Cell(2, 3).FormulaA1 = "=HYPERLINK(\"https://www.nexusmods.com/diablo2resurrected/mods/503\",\"Nexus\")";
            ws.Cell(2, 4).FormulaA1 = "=HYPERLINK(\"https://discord.gg/4QENnUfqPd\",\"Discord\")";
            var linksRow = ws.Range(2, 1, 2, summaryCols);
            // Center link cells only (no colors)
            linksRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Merge banner across summary columns and center
            var banner = ws.Range(1, 1, 1, summaryCols);
            banner.Merge();
            banner.Value = "D2R Reimagined Holy Grail Checklist";
            banner.Style.Font.Bold = true;
            banner.Style.Font.FontSize = 16;
            banner.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int rowNum = 4;
            ws.Cell(rowNum, 1).Value = "Category"; ws.Cell(rowNum, 2).Value = "Found"; ws.Cell(rowNum, 3).Value = "Total"; ws.Cell(rowNum, 4).Value = "Notes";
            var header = ws.Range(rowNum, 1, rowNum, 4);
            header.Style.Font.Bold = true;
            rowNum++;

            // Uniques
            ws.Cell(rowNum, 1).Value = "Uniques";
            ws.Cell(rowNum, 2).FormulaA1 = "=COUNTIF(UniquesTable[Found],TRUE)+COUNTIF(UniquesTable[Found],\"Y\")";
            ws.Cell(rowNum, 3).FormulaA1 = "=ROWS(UniquesTable[Found])";
            rowNum++;

            // Set Items
            ws.Cell(rowNum, 1).Value = "Set Items";
            ws.Cell(rowNum, 2).FormulaA1 = "=COUNTIF(SetsTable[Found],TRUE)+COUNTIF(SetsTable[Found],\"Y\")";
            ws.Cell(rowNum, 3).FormulaA1 = "=ROWS(SetsTable[Found])";
            rowNum++;

            // Completed Sets
            ws.Cell(rowNum, 1).Value = "Completed Sets";
            // Sum of helper completed flags using a named range created on Sets sheet
            ws.Cell(rowNum, 2).FormulaA1 = "=SUM(CompletedSetsFlags)";
            ws.Cell(rowNum, 3).Value = totalDistinctSets; // total sets
            ws.Cell(rowNum, 4).Value = "Counts sets with all items marked Y";
            rowNum++;

            // Runewords
            ws.Cell(rowNum, 1).Value = "Runewords";
            ws.Cell(rowNum, 2).FormulaA1 = "=COUNTIF(RunewordsTable[Found],TRUE)+COUNTIF(RunewordsTable[Found],\"Y\")";
            ws.Cell(rowNum, 3).FormulaA1 = "=ROWS(RunewordsTable[Found])";
            rowNum++;

            // Totals row (Grand Total)
            rowNum++;
            ws.Cell(rowNum, 1).Value = "Grand Total"; ws.Cell(rowNum, 1).Style.Font.Bold = true;
            ws.Cell(rowNum, 2).FormulaA1 = "=COUNTIF(UniquesTable[Found],TRUE)+COUNTIF(UniquesTable[Found],\"Y\")+COUNTIF(SetsTable[Found],TRUE)+COUNTIF(SetsTable[Found],\"Y\")+COUNTIF(RunewordsTable[Found],TRUE)+COUNTIF(RunewordsTable[Found],\"Y\")";
            ws.Cell(rowNum, 3).FormulaA1 = "=ROWS(UniquesTable[Found])+ROWS(SetsTable[Found])+ROWS(RunewordsTable[Found])";
            
            ws.Columns().AdjustToContents();
        }
    }
}
