using System.Globalization;
using System.Text;
using ExcelDataReader;

namespace EcomAE.Platform.Cp.PriceImport;

/// <summary>One physical row from a supplier price file (1-based <see cref="LineNo"/> per sheet).</summary>
public sealed record PriceSourceRow(int Sheet, long LineNo, IReadOnlyList<string> Cells, string? SkipReason = null, string SkipDetails = "");

/// <summary>Progress notes and the detected text layout, mirroring pyprices <c>task.other_messages</c>.</summary>
public sealed class PriceFileReadLog
{
    public List<string> Messages { get; } = [];

    public string Format { get; set; } = "";

    public string Encoding { get; set; } = "";

    public string Delimiter { get; set; } = "";

    /// <summary>First skipped row (header) of the first sheet when <c>strings_to_left &gt; 0</c>; used for issue column labels.</summary>
    public IReadOnlyList<string>? HeaderRow { get; set; }
}

public sealed class PriceFileFormatException(string message) : Exception(message);

/// <summary>
/// pyprices <c>PriceFileHandler</c>: TXT/CSV via the auto-detected delimiter loop (<c>;</c>, tab, <c>,</c>) with the
/// "more than 20 consecutive rows with &lt; 3 columns in the first 100 lines" detector, encoding <c>auto</c>
/// (UTF-8 if the head decodes, otherwise Windows-1251) or the explicit list encoding with invalid bytes dropped,
/// XLSX/XLS over every sheet with <c>strings_to_left</c> applied per sheet, and a malformed XLS retried as CSV.
/// </summary>
public static class PriceFileReader
{
    public static readonly IReadOnlyList<string> TextExtensions = ["txt", "csv"];
    public static readonly IReadOnlyList<string> ExcelExtensions = ["xlsx", "xls"];
    public static readonly IReadOnlyList<string> ArchiveExtensions = ["zip", "rar", "7z", "tar"];

    /// <summary>pyprices <c>file_receiver.extensions_suitable</c>.</summary>
    public static readonly IReadOnlyList<string> SuitableExtensions = ["txt", "csv", "xls", "xlsx"];

    public static readonly IReadOnlyList<char> TextDelimiters = [';', '\t', ','];

    private const int ShortRowLimit = 20;
    private const int ShortRowWindow = 100;
    private const int AutoDetectProbeBytes = 16384;

    static PriceFileReader()
    {
        System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>pyprices <c>file_receiver.get_file_extension</c>: lower-cased text after the last dot, <c>noext</c> without one.</summary>
    public static string ExtensionOf(string fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        var dot = name.LastIndexOf('.');
        return dot < 0 ? "noext" : name[(dot + 1)..].ToLowerInvariant();
    }

    public static bool IsSuitable(string fileName) => SuitableExtensions.Contains(ExtensionOf(fileName));

    public static bool IsArchive(string fileName) => ArchiveExtensions.Contains(ExtensionOf(fileName));

    public static IEnumerable<PriceSourceRow> ReadRows(string path, string fileName, string? encodingSetting, int stringsToLeft, PriceFileReadLog log)
    {
        var extension = ExtensionOf(fileName);
        stringsToLeft = Math.Max(0, stringsToLeft);
        if (TextExtensions.Contains(extension))
        {
            return ReadText(path, encodingSetting, stringsToLeft, log);
        }

        if (ExcelExtensions.Contains(extension))
        {
            return ReadExcelOrText(path, fileName, encodingSetting, stringsToLeft, log);
        }

        throw new PriceFileFormatException("The file has unsupported type " + extension);
    }

    private static IEnumerable<PriceSourceRow> ReadExcelOrText(string path, string fileName, string? encodingSetting, int stringsToLeft, PriceFileReadLog log)
    {
        if (IsReadableWorkbook(path))
        {
            return ReadExcel(path, encodingSetting, stringsToLeft, log);
        }

        log.Messages.Add("File " + fileName + " has an incorrect structure. It may not be " + ExtensionOf(fileName).ToUpperInvariant()
                         + ". Trying to process it as CSV.");
        return ReadText(path, encodingSetting, stringsToLeft, log);
    }

    public static bool IsReadableWorkbook(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            reader.Read();
            return true;
        }
        catch (Exception ex) when (ex is ExcelDataReader.Exceptions.ExcelReaderException or InvalidDataException or NotSupportedException
                                       or IOException or ArgumentException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private static IEnumerable<PriceSourceRow> ReadExcel(string path, string? encodingSetting, int stringsToLeft, PriceFileReadLog log)
    {
        log.Format = "excel";
        log.Messages.Add("Processing via Excel function. File named: " + Path.GetFileName(path));
        using var stream = File.OpenRead(path);
        var configuration = new ExcelReaderConfiguration();
        if (!IsAuto(encodingSetting) && TryEncoding(encodingSetting, out var fallback))
        {
            configuration.FallbackEncoding = fallback;
        }

        using var reader = ExcelReaderFactory.CreateReader(stream, configuration);
        var sheet = 0;
        do
        {
            sheet++;
            long lineNo = 0;
            while (reader.Read())
            {
                lineNo++;
                var cells = new string[reader.FieldCount];
                for (var i = 0; i < cells.Length; i++)
                {
                    cells[i] = CellText(reader.GetValue(i));
                }

                if (lineNo <= stringsToLeft)
                {
                    if (lineNo == 1 && sheet == 1)
                    {
                        log.HeaderRow = cells;
                    }

                    continue;
                }

                yield return new PriceSourceRow(sheet, lineNo, TrimTrailingEmpty(cells));
            }
        }
        while (reader.NextResult());
    }

    private static IEnumerable<PriceSourceRow> ReadText(string path, string? encodingSetting, int stringsToLeft, PriceFileReadLog log)
    {
        log.Format = "text";
        log.Messages.Add("Reading via CSV function");
        log.Messages.Add("Using auto-detect separator mode");
        var encoding = ResolveTextEncoding(path, encodingSetting, log);

        for (var index = 0; index < TextDelimiters.Count; index++)
        {
            var delimiter = TextDelimiters[index];
            log.Messages.Add("Separator: " + (delimiter == '\t' ? "Tabulation" : delimiter.ToString()));
            if (DelimiterFits(path, encoding, delimiter, stringsToLeft))
            {
                log.Delimiter = delimiter == '\t' ? "\\t" : delimiter.ToString();
                return StreamText(path, encoding, delimiter, stringsToLeft, log);
            }

            if (index < TextDelimiters.Count - 1)
            {
                log.Messages.Add("The current separator did not fit. Trying another one.");
            }
        }

        throw new PriceFileFormatException(
            "This file is not a text file (CSV or TXT) due to the fact that it contains a number of incorrect lines in a row. None of the possible separators worked");
    }

    /// <summary>
    /// The pyprices detector: a delimiter is rejected once more than 20 consecutive rows inside the first 100 lines
    /// have fewer than 3 columns. A file that ends before that point without a single usable row is rejected too,
    /// so short comma files fall through to <c>,</c> instead of importing nothing.
    /// </summary>
    private static bool DelimiterFits(string path, Encoding encoding, char delimiter, int stringsToLeft)
    {
        using var reader = OpenText(path, encoding);
        var i = 0;
        var shortRun = 0;
        var usable = 0;
        var shortRows = 0;
        foreach (var record in PyCsvReader.Read(reader, delimiter))
        {
            if (stringsToLeft > i)
            {
                i++;
                continue;
            }

            i++;
            if (i >= ShortRowWindow)
            {
                return true;
            }

            if (record.Count < 3)
            {
                shortRows++;
                shortRun++;
                if (shortRun > ShortRowLimit)
                {
                    return false;
                }

                continue;
            }

            shortRun = 0;
            usable++;
        }

        return usable > 0 || shortRows == 0;
    }

    private static IEnumerable<PriceSourceRow> StreamText(string path, Encoding encoding, char delimiter, int stringsToLeft, PriceFileReadLog log)
    {
        using var reader = OpenText(path, encoding);
        var i = 0;
        foreach (var record in PyCsvReader.Read(reader, delimiter))
        {
            if (stringsToLeft > i)
            {
                if (i == 0)
                {
                    log.HeaderRow = record;
                }

                i++;
                continue;
            }

            i++;
            if (record.Count < 3 && i < ShortRowWindow)
            {
                var blank = record.All(string.IsNullOrWhiteSpace);
                yield return new PriceSourceRow(1, i, record, blank ? "empty_row" : "too_few_columns",
                    blank ? "" : "Row has " + record.Count.ToString(CultureInfo.InvariantCulture) + " column(s) with separator " + log.Delimiter);
                continue;
            }

            yield return new PriceSourceRow(1, i, record);
        }
    }

    private static StreamReader OpenText(string path, Encoding encoding)
    {
        var reader = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 65536);
        if (reader.Peek() == '\uFEFF')
        {
            reader.Read();
        }

        return reader;
    }

    public static Encoding ResolveTextEncoding(string path, string? encodingSetting, PriceFileReadLog log)
    {
        if (IsAuto(encodingSetting))
        {
            var detected = DetectEncoding(path);
            log.Encoding = detected;
            log.Messages.Add("Encoding auto-detected: " + detected);
            return IgnoringInvalid(detected);
        }

        if (TryEncoding(encodingSetting, out _))
        {
            log.Encoding = encodingSetting!.Trim().ToLowerInvariant();
            return IgnoringInvalid(encodingSetting!.Trim());
        }

        log.Messages.Add("Unknown encoding '" + encodingSetting + "'. Using the default encoding ANSI. It is recommended to set the encoding explicitly in the price list settings");
        log.Encoding = "cp1251";
        return IgnoringInvalid("cp1251");
    }

    /// <summary>pyprices <c>detect_encoding</c>: UTF-8 when the head of the file decodes strictly, otherwise cp1251.</summary>
    public static string DetectEncoding(string path)
    {
        var buffer = new byte[AutoDetectProbeBytes];
        int length;
        using (var stream = File.OpenRead(path))
        {
            length = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        }

        return IsValidUtf8Head(buffer.AsSpan(0, length), length == buffer.Length) ? "utf-8" : "cp1251";
    }

    public static bool IsValidUtf8Head(ReadOnlySpan<byte> bytes, bool truncated)
    {
        if (truncated)
        {
            var cut = bytes.Length;
            var back = 0;
            while (cut > 0 && back < 4 && (bytes[cut - 1] & 0xC0) == 0x80)
            {
                cut--;
                back++;
            }

            if (cut > 0 && bytes[cut - 1] >= 0xC0)
            {
                var lead = bytes[cut - 1];
                var need = lead >= 0xF0 ? 3 : lead >= 0xE0 ? 2 : 1;
                if (back < need)
                {
                    bytes = bytes[..(cut - 1)];
                }
            }
        }

        try
        {
            new UTF8Encoding(false, true).GetCharCount(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool IsAuto(string? setting) => string.IsNullOrWhiteSpace(setting) || string.Equals(setting.Trim(), "auto", StringComparison.OrdinalIgnoreCase);

    private static bool TryEncoding(string? name, out Encoding encoding)
    {
        encoding = System.Text.Encoding.UTF8;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        try
        {
            encoding = System.Text.Encoding.GetEncoding(NormalizeEncodingName(name));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string NormalizeEncodingName(string name)
    {
        var n = name.Trim().ToLowerInvariant();
        return n switch
        {
            "cp1251" or "ansi" => "windows-1251",
            "utf8" => "utf-8",
            _ => n,
        };
    }

    private static Encoding IgnoringInvalid(string name)
        => System.Text.Encoding.GetEncoding(NormalizeEncodingName(name), EncoderFallback.ReplacementFallback, new DecoderReplacementFallback(string.Empty));

    /// <summary>Cell text as openpyxl/xlrd <c>str(cell.value)</c> would give for the column readers, with empty cells as "".</summary>
    public static string CellText(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        double d when Math.Abs(d) < 1e15 && d == Math.Floor(d) => ((long)d).ToString(CultureInfo.InvariantCulture),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        bool b => b ? "True" : "False",
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static string[] TrimTrailingEmpty(string[] cells)
    {
        var end = cells.Length;
        while (end > 0 && cells[end - 1].Length == 0)
        {
            end--;
        }

        return end == cells.Length ? cells : cells[..end];
    }
}
