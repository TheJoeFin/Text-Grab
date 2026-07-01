using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Text_Grab.Models;

public enum EtwEditorMode
{
    Text,
    Markdown,
    Spreadsheet
}

public enum EtwStructuredTextFormat
{
    PlainText,
    DelimitedText,
    Csv,
    Tsv,
    Xml
}

public sealed record EditTextTableWrappedCell(int RowIndex, int ColumnIndex);

public sealed class EditTextTableDocument
{
    public const int DefaultMinimumRowCount = 25;
    public const int DefaultMinimumColumnCount = 8;

    public EtwStructuredTextFormat Format { get; set; } = EtwStructuredTextFormat.PlainText;

    public string NewLineSequence { get; set; } = Environment.NewLine;

    public string Delimiter { get; set; } = "\t";

    public string XmlRootElementName { get; set; } = "rows";

    public string? XmlContainerElementName { get; set; }

    public string XmlRowElementName { get; set; } = "row";

    public List<string> ColumnNames { get; set; } = [];

    public List<List<string>> Rows { get; set; } = [];

    public int RowCount { get; set; }

    public int ColumnCount { get; set; }

    public int MinimumRowCount { get; set; } = DefaultMinimumRowCount;

    public int MinimumColumnCount { get; set; } = DefaultMinimumColumnCount;

    public List<double?> ColumnWidths { get; set; } = [];

    public List<double?> RowHeights { get; set; } = [];

    public List<EditTextTableWrappedCell> WrappedCells { get; set; } = [];

    public static EditTextTableDocument CreateFromText(
        string? text,
        int minimumRowCount = DefaultMinimumRowCount,
        int minimumColumnCount = DefaultMinimumColumnCount)
    {
        string safeText = text ?? string.Empty;
        string newlineSequence = DetectNewLineSequence(safeText);

        EditTextTableDocument document =
            TryCreateDelimitedDocument(safeText, '\t', EtwStructuredTextFormat.Tsv, newlineSequence, minimumRowCount, minimumColumnCount)
            ?? TryCreateDelimitedDocument(safeText, ',', EtwStructuredTextFormat.Csv, newlineSequence, minimumRowCount, minimumColumnCount)
            ?? TryCreateXmlDocument(safeText, newlineSequence, minimumRowCount, minimumColumnCount)
            ?? TryCreateHeuristicDelimitedDocument(safeText, newlineSequence, minimumRowCount, minimumColumnCount)
            ?? CreatePlainTextDocument(safeText, newlineSequence, minimumRowCount, minimumColumnCount);

        document.EnsureMinimumSize();
        return document;
    }

    public static EditTextTableDocument? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            EditTextTableDocument? document = JsonSerializer.Deserialize<EditTextTableDocument>(json);
            if (document is null)
                return null;

            document.EnsureMinimumSize();
            return document;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string SerializeToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    public string SerializeToText()
    {
        EnsureMinimumSize();

        return Format switch
        {
            EtwStructuredTextFormat.Xml => SerializeToXml(),
            EtwStructuredTextFormat.Csv => SerializeDelimitedText(','),
            EtwStructuredTextFormat.Tsv => SerializeDelimitedText('\t'),
            EtwStructuredTextFormat.DelimitedText => SerializeDelimitedText(GetDelimiterCharacter()),
            _ => SerializePlainText(),
        };
    }

    public void InsertRow(int rowIndex)
    {
        EnsureMinimumSize();

        int insertIndex = Math.Clamp(rowIndex, 0, RowCount);
        WrappedCells = [.. WrappedCells
            .Select(cell => cell.RowIndex >= insertIndex
                ? cell with { RowIndex = cell.RowIndex + 1 }
                : cell)];
        Rows.Insert(insertIndex, Enumerable.Repeat(string.Empty, ColumnNames.Count).ToList());
        RowHeights.Insert(insertIndex, null);
        RowCount++;
        MinimumRowCount = Math.Max(MinimumRowCount, RowCount);
    }

    public void InsertColumn(int columnIndex, string? columnName = null)
    {
        EnsureMinimumSize();

        int insertIndex = Math.Clamp(columnIndex, 0, ColumnCount);
        string nameToInsert = EnsureUniqueColumnName(columnName ?? GetDefaultColumnName(insertIndex), ColumnNames);

        WrappedCells = [.. WrappedCells
            .Select(cell => cell.ColumnIndex >= insertIndex
                ? cell with { ColumnIndex = cell.ColumnIndex + 1 }
                : cell)];
        ColumnNames.Insert(insertIndex, nameToInsert);
        ColumnWidths.Insert(insertIndex, null);
        foreach (List<string> row in Rows)
            row.Insert(insertIndex, string.Empty);

        ColumnCount++;
        MinimumColumnCount = Math.Max(MinimumColumnCount, ColumnCount);
    }

    public void DeleteRow(int rowIndex)
    {
        EnsureMinimumSize();

        if (rowIndex < 0 || rowIndex >= RowCount)
            return;

        WrappedCells = [.. WrappedCells
            .Where(cell => cell.RowIndex != rowIndex)
            .Select(cell => cell.RowIndex > rowIndex
                ? cell with { RowIndex = cell.RowIndex - 1 }
                : cell)];
        Rows.RemoveAt(rowIndex);
        if (rowIndex < RowHeights.Count)
            RowHeights.RemoveAt(rowIndex);
        RowCount = Math.Max(1, RowCount - 1);
    }

    public void DeleteColumn(int columnIndex)
    {
        EnsureMinimumSize();

        if (columnIndex < 0 || columnIndex >= ColumnCount)
            return;

        WrappedCells = [.. WrappedCells
            .Where(cell => cell.ColumnIndex != columnIndex)
            .Select(cell => cell.ColumnIndex > columnIndex
                ? cell with { ColumnIndex = cell.ColumnIndex - 1 }
                : cell)];
        ColumnNames.RemoveAt(columnIndex);
        if (columnIndex < ColumnWidths.Count)
            ColumnWidths.RemoveAt(columnIndex);
        foreach (List<string> row in Rows)
        {
            if (columnIndex < row.Count)
                row.RemoveAt(columnIndex);
        }

        ColumnCount = Math.Max(1, ColumnCount - 1);
    }

    public void MoveRow(int fromIndex, int toIndex)
    {
        EnsureMinimumSize();

        if (fromIndex < 0 || fromIndex >= RowCount || toIndex < 0 || toIndex >= RowCount || fromIndex == toIndex)
            return;

        WrappedCells = [.. WrappedCells
            .Select(cell => cell with { RowIndex = TranslateMovedIndex(cell.RowIndex, fromIndex, toIndex) })];
        List<string> row = Rows[fromIndex];
        Rows.RemoveAt(fromIndex);
        Rows.Insert(toIndex, row);
        MoveListItem(RowHeights, fromIndex, toIndex);
    }

    public void MoveColumn(int fromIndex, int toIndex)
    {
        EnsureMinimumSize();

        if (fromIndex < 0 || fromIndex >= ColumnCount || toIndex < 0 || toIndex >= ColumnCount || fromIndex == toIndex)
            return;

        WrappedCells = [.. WrappedCells
            .Select(cell => cell with { ColumnIndex = TranslateMovedIndex(cell.ColumnIndex, fromIndex, toIndex) })];
        string columnName = ColumnNames[fromIndex];
        ColumnNames.RemoveAt(fromIndex);
        ColumnNames.Insert(toIndex, columnName);
        MoveListItem(ColumnWidths, fromIndex, toIndex);

        foreach (List<string> row in Rows)
        {
            string value = row[fromIndex];
            row.RemoveAt(fromIndex);
            row.Insert(toIndex, value);
        }
    }

    public void Transpose()
    {
        EnsureMinimumSize();

        int sourceRowCount = Math.Max(1, RowCount);
        int sourceColumnCount = Math.Max(1, ColumnCount);
        int originalMinimumRowCount = MinimumRowCount;
        int originalMinimumColumnCount = MinimumColumnCount;

        List<List<string>> transposedRows = [];
        for (int columnIndex = 0; columnIndex < sourceColumnCount; columnIndex++)
        {
            List<string> transposedRow = [];
            for (int rowIndex = 0; rowIndex < sourceRowCount; rowIndex++)
            {
                string value = rowIndex < Rows.Count && columnIndex < Rows[rowIndex].Count
                    ? Rows[rowIndex][columnIndex] ?? string.Empty
                    : string.Empty;
                transposedRow.Add(value);
            }

            transposedRows.Add(transposedRow);
        }

        Rows = transposedRows;
        RowCount = sourceColumnCount;
        ColumnCount = sourceRowCount;
        MinimumRowCount = Math.Max(1, originalMinimumColumnCount);
        MinimumColumnCount = Math.Max(1, originalMinimumRowCount);
        ColumnNames = BuildGenericColumnNames(Math.Max(1, ColumnCount));
        ColumnWidths = [];
        RowHeights = [];
        WrappedCells = [.. WrappedCells
            .Select(cell => new EditTextTableWrappedCell(cell.ColumnIndex, cell.RowIndex))];
        EnsureMinimumSize();
    }

    public void EnsureMinimumSize()
    {
        if (MinimumRowCount < 1)
            MinimumRowCount = DefaultMinimumRowCount;

        if (MinimumColumnCount < 1)
            MinimumColumnCount = DefaultMinimumColumnCount;

        int maxRowWidth = Rows.Count == 0 ? 0 : Rows.Max(row => row.Count);

        if (ColumnCount < 0)
            ColumnCount = 0;

        if (RowCount < 0)
            RowCount = 0;

        if (ColumnCount == 0)
            ColumnCount = InferLogicalColumnCount();

        if (RowCount == 0 && Rows.Any(row => row.Any(value => !string.IsNullOrEmpty(value))))
            RowCount = Rows.Count;

        int requiredColumns = Math.Max(Math.Max(ColumnCount, maxRowWidth), MinimumColumnCount);

        while (ColumnNames.Count < requiredColumns)
            ColumnNames.Add(EnsureUniqueColumnName(GetDefaultColumnName(ColumnNames.Count), ColumnNames));

        while (ColumnWidths.Count < requiredColumns)
            ColumnWidths.Add(null);

        while (ColumnWidths.Count > requiredColumns)
            ColumnWidths.RemoveAt(ColumnWidths.Count - 1);

        foreach (List<string> row in Rows)
            while (row.Count < requiredColumns)
                row.Add(string.Empty);

        int requiredRows = Math.Max(RowCount, MinimumRowCount);
        while (Rows.Count < requiredRows)
            Rows.Add(Enumerable.Repeat(string.Empty, requiredColumns).ToList());

        while (RowHeights.Count < requiredRows)
            RowHeights.Add(null);

        while (RowHeights.Count > requiredRows)
            RowHeights.RemoveAt(RowHeights.Count - 1);

        NormalizeWrappedCells();
    }

    public void ApplyViewMetricsFrom(EditTextTableDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);

        EnsureMinimumSize();
        source.EnsureMinimumSize();

        for (int columnIndex = 0; columnIndex < Math.Min(ColumnWidths.Count, source.ColumnWidths.Count); columnIndex++)
            ColumnWidths[columnIndex] = source.ColumnWidths[columnIndex];

        for (int rowIndex = 0; rowIndex < Math.Min(RowHeights.Count, source.RowHeights.Count); rowIndex++)
            RowHeights[rowIndex] = source.RowHeights[rowIndex];

        WrappedCells = [.. source.WrappedCells];
        NormalizeWrappedCells();
    }

    public void SetColumnWidth(int columnIndex, double? width)
    {
        EnsureMinimumSize();
        if (columnIndex < 0 || columnIndex >= ColumnWidths.Count)
            return;

        ColumnWidths[columnIndex] = NormalizeViewMetric(width);
    }

    public void SetRowHeight(int rowIndex, double? height)
    {
        EnsureMinimumSize();
        if (rowIndex < 0 || rowIndex >= RowHeights.Count)
            return;

        RowHeights[rowIndex] = NormalizeViewMetric(height);
    }

    public bool IsCellWrapped(int rowIndex, int columnIndex)
    {
        EnsureMinimumSize();
        return WrappedCells.Contains(new EditTextTableWrappedCell(rowIndex, columnIndex));
    }

    public void SetCellWrap(int rowIndex, int columnIndex, bool shouldWrap)
    {
        EnsureMinimumSize();

        if (rowIndex < 0
            || rowIndex >= Rows.Count
            || columnIndex < 0
            || columnIndex >= ColumnNames.Count)
        {
            return;
        }

        EditTextTableWrappedCell wrappedCell = new(rowIndex, columnIndex);
        if (shouldWrap)
        {
            if (!WrappedCells.Contains(wrappedCell))
                WrappedCells.Add(wrappedCell);
        }
        else
        {
            WrappedCells.RemoveAll(cell => cell == wrappedCell);
        }

        NormalizeWrappedCells();
    }

    private string SerializePlainText()
    {
        if (ColumnCount <= 1)
            return string.Join(NewLineSequence, Rows.Take(RowCount).Select(row => row.FirstOrDefault() ?? string.Empty));

        return SerializeDelimitedText(GetDelimiterCharacter());
    }

    private static void MoveListItem<T>(List<T> items, int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= items.Count || toIndex < 0 || toIndex >= items.Count || fromIndex == toIndex)
            return;

        T item = items[fromIndex];
        items.RemoveAt(fromIndex);
        items.Insert(toIndex, item);
    }

    private static int TranslateMovedIndex(int currentIndex, int fromIndex, int toIndex)
    {
        if (currentIndex == fromIndex)
            return toIndex;

        if (fromIndex < toIndex && currentIndex > fromIndex && currentIndex <= toIndex)
            return currentIndex - 1;

        if (toIndex < fromIndex && currentIndex >= toIndex && currentIndex < fromIndex)
            return currentIndex + 1;

        return currentIndex;
    }

    private static double? NormalizeViewMetric(double? value)
    {
        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value <= 0)
            return null;

        return value.Value;
    }

    private void NormalizeWrappedCells()
    {
        int maxRowCount = Rows.Count;
        int maxColumnCount = ColumnNames.Count;

        WrappedCells = [.. WrappedCells
            .Where(cell => cell.RowIndex >= 0
                && cell.RowIndex < maxRowCount
                && cell.ColumnIndex >= 0
                && cell.ColumnIndex < maxColumnCount)
            .Distinct()
            .OrderBy(cell => cell.RowIndex)
            .ThenBy(cell => cell.ColumnIndex)];
    }

    private string SerializeDelimitedText(char delimiter)
    {
        StringBuilder builder = new();

        for (int rowIndex = 0; rowIndex < RowCount; rowIndex++)
        {
            if (rowIndex > 0)
                builder.Append(NewLineSequence);

            List<string> row = Rows[rowIndex];
            for (int columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
            {
                if (columnIndex > 0)
                    builder.Append(delimiter);

                string cellValue = columnIndex < row.Count ? row[columnIndex] ?? string.Empty : string.Empty;
                builder.Append(EscapeDelimitedValue(cellValue, delimiter));
            }
        }

        return builder.ToString();
    }

    private string SerializeToXml()
    {
        XElement root = new(CreateXmlName(XmlRootElementName, "rows", 0));
        XContainer rowContainer = root;

        if (!string.IsNullOrWhiteSpace(XmlContainerElementName))
        {
            XElement container = new(CreateXmlName(XmlContainerElementName, "items", 0));
            root.Add(container);
            rowContainer = container;
        }

        for (int rowIndex = 0; rowIndex < RowCount; rowIndex++)
        {
            XElement rowElement = new(CreateXmlName(XmlRowElementName, "row", rowIndex));
            List<string> row = Rows[rowIndex];

            for (int columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
            {
                string columnName = ColumnNames[columnIndex];
                string value = columnIndex < row.Count ? row[columnIndex] ?? string.Empty : string.Empty;

                if (columnName.StartsWith('@'))
                {
                    rowElement.SetAttributeValue(CreateXmlName(columnName[1..], "attribute", columnIndex), value);
                    continue;
                }

                rowElement.Add(new XElement(CreateXmlName(columnName, "column", columnIndex), value));
            }

            rowContainer.Add(rowElement);
        }

        XDocument document = new(root);
        return NormalizeLineEndings(document.ToString(), NewLineSequence);
    }

    private char GetDelimiterCharacter()
    {
        return string.IsNullOrEmpty(Delimiter) ? '\t' : Delimiter[0];
    }

    private static EditTextTableDocument? TryCreateDelimitedDocument(
        string text,
        char delimiter,
        EtwStructuredTextFormat format,
        string newlineSequence,
        int minimumRowCount,
        int minimumColumnCount)
    {
        List<List<string>> rows = ParseDelimitedText(text, delimiter);
        if (!LooksStructured(rows))
            return null;

        TrimParserAddedTerminalRow(text, rows);

        return new EditTextTableDocument
        {
            Format = format,
            Delimiter = delimiter.ToString(),
            NewLineSequence = newlineSequence,
            ColumnNames = BuildGenericColumnNames(rows.Max(row => row.Count)),
            Rows = rows,
            RowCount = rows.Count,
            ColumnCount = rows.Max(row => row.Count),
            MinimumRowCount = minimumRowCount,
            MinimumColumnCount = minimumColumnCount,
        };
    }

    private static EditTextTableDocument? TryCreateHeuristicDelimitedDocument(
        string text,
        string newlineSequence,
        int minimumRowCount,
        int minimumColumnCount)
    {
        char[] heuristicDelimiters = ['|', ';', ':'];

        foreach (char delimiter in heuristicDelimiters)
        {
            List<List<string>> rows = ParseDelimitedText(text, delimiter);
            if (!LooksStructured(rows))
                continue;

            TrimParserAddedTerminalRow(text, rows);

            return new EditTextTableDocument
            {
                Format = EtwStructuredTextFormat.DelimitedText,
                Delimiter = delimiter.ToString(),
                NewLineSequence = newlineSequence,
                ColumnNames = BuildGenericColumnNames(rows.Max(row => row.Count)),
                Rows = rows,
                RowCount = rows.Count,
                ColumnCount = rows.Max(row => row.Count),
                MinimumRowCount = minimumRowCount,
                MinimumColumnCount = minimumColumnCount,
            };
        }

        return null;
    }

    private static EditTextTableDocument? TryCreateXmlDocument(
        string text,
        string newlineSequence,
        int minimumRowCount,
        int minimumColumnCount)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith('<'))
            return null;

        try
        {
            XDocument xDocument = XDocument.Parse(text, LoadOptions.None);
            XElement? root = xDocument.Root;
            if (root is null)
                return null;

            XElement? rowParent = null;
            List<XElement>? rowElements = null;

            foreach (XElement candidateParent in root.DescendantsAndSelf())
            {
                IGrouping<string, XElement>? repeatedGroup = candidateParent.Elements()
                    .GroupBy(element => element.Name.LocalName)
                    .OrderByDescending(group => group.Count())
                    .FirstOrDefault(group => group.Count() > 1);

                if (repeatedGroup is null)
                    continue;

                if (rowElements is null || repeatedGroup.Count() > rowElements.Count)
                {
                    rowParent = candidateParent;
                    rowElements = repeatedGroup.ToList();
                }
            }

            if (rowParent is null || rowElements is null || rowElements.Count == 0)
                return null;

            List<string> columnNames = [];
            foreach (XElement rowElement in rowElements)
            {
                foreach (XAttribute attribute in rowElement.Attributes())
                    AddUnique(columnNames, $"@{attribute.Name.LocalName}");

                foreach (XElement child in rowElement.Elements())
                    AddUnique(columnNames, child.Name.LocalName);
            }

            if (columnNames.Count == 0)
                columnNames.Add("Value");

            List<List<string>> rows = [];
            foreach (XElement rowElement in rowElements)
            {
                List<string> row = [];
                foreach (string columnName in columnNames)
                {
                    if (columnName.StartsWith('@'))
                    {
                        row.Add(rowElement.Attribute(columnName[1..])?.Value ?? string.Empty);
                        continue;
                    }

                    row.Add(rowElement.Element(columnName)?.Value ?? string.Empty);
                }

                rows.Add(row);
            }

            string? containerElementName = rowParent == root ? null : rowParent.Name.LocalName;

            return new EditTextTableDocument
            {
                Format = EtwStructuredTextFormat.Xml,
                NewLineSequence = newlineSequence,
                XmlRootElementName = root.Name.LocalName,
                XmlContainerElementName = containerElementName,
                XmlRowElementName = rowElements[0].Name.LocalName,
                ColumnNames = columnNames,
                Rows = rows,
                RowCount = rows.Count,
                ColumnCount = columnNames.Count,
                MinimumRowCount = minimumRowCount,
                MinimumColumnCount = minimumColumnCount,
            };
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static EditTextTableDocument CreatePlainTextDocument(
        string text,
        string newlineSequence,
        int minimumRowCount,
        int minimumColumnCount)
    {
        List<List<string>> rows = SplitPlainTextRows(text);

        return new EditTextTableDocument
        {
            Format = EtwStructuredTextFormat.PlainText,
            NewLineSequence = newlineSequence,
            Delimiter = "\t",
            ColumnNames = ["Column A"],
            Rows = rows,
            RowCount = rows.Count,
            ColumnCount = 1,
            MinimumRowCount = minimumRowCount,
            MinimumColumnCount = minimumColumnCount,
        };
    }

    private static List<List<string>> SplitPlainTextRows(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        string normalized = NormalizeLineEndings(text, "\n");
        string[] lines = normalized.Split('\n', StringSplitOptions.None);
        return lines.Select(line => new List<string> { line }).ToList();
    }

    private static List<List<string>> ParseDelimitedText(string text, char delimiter)
    {
        List<List<string>> rows = [];
        List<string> currentRow = [];
        StringBuilder currentField = new();
        bool insideQuotes = false;

        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];

            if (insideQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        currentField.Append('"');
                        index++;
                    }
                    else
                    {
                        insideQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(character);
                }

                continue;
            }

            if (character == '"')
            {
                insideQuotes = true;
                continue;
            }

            if (character == delimiter)
            {
                currentRow.Add(currentField.ToString());
                currentField.Clear();
                continue;
            }

            if (character is '\r' or '\n')
            {
                currentRow.Add(currentField.ToString());
                currentField.Clear();
                rows.Add(currentRow);
                currentRow = [];

                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                    index++;

                continue;
            }

            currentField.Append(character);
        }

        currentRow.Add(currentField.ToString());
        rows.Add(currentRow);

        return rows;
    }

    private static bool LooksStructured(List<List<string>> rows)
    {
        if (rows.Count == 0)
            return false;

        List<List<string>> nonEmptyRows = rows.Where(row => row.Any(value => !string.IsNullOrWhiteSpace(value))).ToList();
        if (nonEmptyRows.Count == 0)
            return false;

        int maxColumns = nonEmptyRows.Max(row => row.Count);
        if (maxColumns < 2)
            return false;

        int matchingStructuredRows = nonEmptyRows.Count(row => row.Count == maxColumns && maxColumns > 1);
        if (matchingStructuredRows >= 2)
            return true;

        return nonEmptyRows.Count == 1 && maxColumns >= 2;
    }

    private int InferLogicalColumnCount()
    {
        for (int columnIndex = ColumnNames.Count - 1; columnIndex >= 0; columnIndex--)
        {
            foreach (List<string> row in Rows)
            {
                if (columnIndex < row.Count && !string.IsNullOrEmpty(row[columnIndex]))
                    return columnIndex + 1;
            }
        }

        return ColumnNames.Count > 0 ? 1 : 0;
    }

    private static void TrimParserAddedTerminalRow(string originalText, List<List<string>> rows)
    {
        if (rows.Count < 2 || string.IsNullOrEmpty(originalText))
            return;

        bool endsWithNewLine = originalText.EndsWith("\r", StringComparison.Ordinal)
            || originalText.EndsWith("\n", StringComparison.Ordinal);

        if (!endsWithNewLine)
            return;

        List<string> lastRow = rows[^1];
        if (lastRow.All(string.IsNullOrEmpty))
            rows.RemoveAt(rows.Count - 1);
    }

    private static string EscapeDelimitedValue(string value, char delimiter)
    {
        bool needsQuotes =
            value.Contains(delimiter)
            || value.Contains('"')
            || value.Contains('\r')
            || value.Contains('\n');

        if (!needsQuotes)
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string DetectNewLineSequence(string text)
    {
        int carriageReturnLineFeedIndex = text.IndexOf("\r\n", StringComparison.Ordinal);
        if (carriageReturnLineFeedIndex >= 0)
            return "\r\n";

        if (text.Contains('\n'))
            return "\n";

        if (text.Contains('\r'))
            return "\r";

        return Environment.NewLine;
    }

    private static string NormalizeLineEndings(string text, string newLineSequence)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", newLineSequence, StringComparison.Ordinal);
    }

    private static List<string> BuildGenericColumnNames(int count)
    {
        List<string> columnNames = [];
        for (int index = 0; index < count; index++)
            columnNames.Add(GetDefaultColumnName(index));

        return columnNames;
    }

    public static string GetSpreadsheetColumnLabel(int index)
    {
        return ToSpreadsheetColumnName(index);
    }

    private static string GetDefaultColumnName(int index)
    {
        return $"Column {GetSpreadsheetColumnLabel(index)}";
    }

    private static string ToSpreadsheetColumnName(int index)
    {
        int workingIndex = index + 1;
        StringBuilder builder = new();

        while (workingIndex > 0)
        {
            workingIndex--;
            builder.Insert(0, (char)('A' + (workingIndex % 26)));
            workingIndex /= 26;
        }

        return builder.ToString();
    }

    private static string EnsureUniqueColumnName(string desiredName, IEnumerable<string> existingNames)
    {
        string baseName = string.IsNullOrWhiteSpace(desiredName) ? "Column" : desiredName.Trim();
        HashSet<string> existingNameSet = new(existingNames, StringComparer.OrdinalIgnoreCase);

        if (!existingNameSet.Contains(baseName))
            return baseName;

        int suffix = 2;
        while (existingNameSet.Contains($"{baseName} {suffix}"))
            suffix++;

        return $"{baseName} {suffix}";
    }

    private static void AddUnique(ICollection<string> names, string name)
    {
        if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
            names.Add(name);
    }

    private static XName CreateXmlName(string? rawName, string fallbackPrefix, int index)
    {
        string safeName = string.IsNullOrWhiteSpace(rawName)
            ? $"{fallbackPrefix}{index + 1}"
            : rawName.Trim();

        safeName = safeName.Replace(' ', '_');
        safeName = XmlConvert.EncodeLocalName(safeName);

        if (char.IsDigit(safeName[0]))
            safeName = $"_{safeName}";

        return XName.Get(safeName);
    }
}
