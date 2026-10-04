using System.IO.Compression;
using System.Text;
using EcomAE.Platform.Cp.PriceImport;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Supplier price-file parsing twins of pyprices (<c>price_file_handler.py</c>, <c>price_record.py</c>,
/// <c>price_parser</c>). Expected values were produced by running the Python modules on the same inputs.
/// </summary>
public sealed class CpPriceImportParsingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ecomae-price-import-tests-" + Guid.NewGuid().ToString("N"));

    public CpPriceImportParsingTests()
    {
        Directory.CreateDirectory(_dir);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Theory]
    [InlineData("12,50", "12.50")]
    [InlineData("1 234,56", "1234.56")]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("1,234", "1234")]
    [InlineData("1.234", "1234")]
    [InlineData("AED 12.50", "12.50")]
    [InlineData("$1,000", "1000")]
    [InlineData("-5", "5")]
    [InlineData("1,5", "1.5")]
    [InlineData("10.000.000", "10000000")]
    [InlineData("1e3", "1")]
    [InlineData(".5", "0.5")]
    [InlineData("0,99", "0.99")]
    [InlineData("35€99", "35.99")]
    [InlineData("35€ 99", "35.99")]
    [InlineData("1,235€99", "1235.99")]
    [InlineData("99 € 79 €", "99")]
    [InlineData("Free", "0")]
    [InlineData("12.999", "12999")]
    [InlineData("3,0000", "3.0000")]
    [InlineData("1\u00a0298,00", "1298.00")]
    [InlineData("1'234.50", "1234.50")]
    [InlineData("85.00", "85.00")]
    [InlineData("USD 20", "20")]
    [InlineData("  42.10  ", "42.10")]
    [InlineData("1.234.567,89", "1234567.89")]
    [InlineData("12,345.67", "12345.67")]
    [InlineData("€ 12,50", "12.50")]
    [InlineData("12.50 AED/pc", "12.50")]
    public void Price_parser_amounts_match_python(string input, string expected)
    {
        var amount = PyPricesPriceParser.Parse(input);
        Assert.NotNull(amount);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), amount!.Value);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("12.5.6")]
    [InlineData("50%")]
    public void Price_parser_returns_null_like_python(string input)
    {
        Assert.Null(PyPricesPriceParser.Parse(input));
        Assert.Null(PyPricesPriceParser.Parse(null));
    }

    [Theory]
    [InlineData("0986494527", "0986494527", "0986494527")]
    [InlineData("00123", "00123", "00123")]
    [InlineData("123.0", "123", "123")]
    [InlineData("W 712/1", "W7121", "W 712/1")]
    [InlineData("-5", "5", "-5")]
    [InlineData("12.50", "125", "12.5")]
    [InlineData("ABC-12.3/4", "ABC1234", "ABC-12.3/4")]
    [InlineData("Фильтр-1", "Фильтр1", "Фильтр-1")]
    [InlineData("123.5", "1235", "123.5")]
    public void Article_and_article_show_match_price_record(string raw, string article, string show)
    {
        var (a, s) = PriceRecordNormalizer.NormalizeArticle(raw);
        Assert.Equal(article, a);
        Assert.Equal(show, s);
    }

    [Theory]
    [InlineData("5", 5)]
    [InlineData("5.7", 5)]
    [InlineData("12,000", 0)]
    [InlineData(">10", 10)]
    [InlineData("1.2.3", 0)]
    [InlineData("", 0)]
    [InlineData("abc", 0)]
    [InlineData("10 pcs", 10)]
    [InlineData("0.5", 0)]
    [InlineData("100+", 100)]
    public void Exist_matches_price_record(string raw, int expected)
        => Assert.Equal(expected, PriceRecordNormalizer.ParseExist(raw));

    [Theory]
    [InlineData("5", 5)]
    [InlineData("5.0", 0)]
    [InlineData(" 7 ", 7)]
    [InlineData("+3", 3)]
    [InlineData("-2", -2)]
    [InlineData("1_0", 10)]
    [InlineData("x", 0)]
    [InlineData("", 0)]
    public void Time_to_exe_and_min_order_match_python_int(string raw, int expected)
        => Assert.Equal(expected, PriceRecordNormalizer.ParseInt(raw));

    [Fact]
    public void Article_search_matches_docpart_normalize_article_for_price()
    {
        Assert.Equal("W7121", DocpartArticle.NormalizeForPrice("w 712/1"));
        Assert.Equal("0986494527", DocpartArticle.NormalizeForPrice("0 986-494.527"));
        Assert.Equal("AB12", DocpartArticle.NormalizeForPrice("a`b'\"\\#,_1\t2\r\n"));
        Assert.Equal(string.Empty, DocpartArticle.NormalizeForPrice(null));
    }

    [Fact]
    public void Normalizer_skips_rows_the_storefront_can_never_show()
    {
        var map = new PriceListColumnMap(1, 2, 3, 4, 5, 6);
        Assert.Equal("empty_row", PriceRecordNormalizer.Normalize(["", " ", ""], map).SkipReason);
        Assert.Equal("empty_article", PriceRecordNormalizer.Normalize(["BOSCH", "--", "Pad", "5", "10"], map).SkipReason);
        Assert.Equal("invalid_price", PriceRecordNormalizer.Normalize(["BOSCH", "0986", "Pad", "5", "n/a"], map).SkipReason);
        Assert.Equal("invalid_price", PriceRecordNormalizer.Normalize(["BOSCH", "0986", "Pad", "5", "0,00"], map).SkipReason);

        var ok = PriceRecordNormalizer.Normalize([" BOSCH ", "0 986 494 527", " Brake pad ", "12 pcs", "1 234,50", "3"], map);
        Assert.True(ok.Imported);
        var row = ok.Row!;
        Assert.Equal("BOSCH", row.Manufacturer);
        Assert.Equal("0986494527", row.Article);
        Assert.Equal("0986494527", row.ArticleSearch);
        Assert.Equal("0 986 494 527", row.ArticleShow);
        Assert.Equal("Brake pad", row.Name);
        Assert.Equal(12, row.Exist);
        Assert.Equal(1234.50m, row.Price);
        Assert.Equal(3, row.TimeToExe);
    }

    [Fact]
    public void Csv_reader_matches_python_csv_module()
    {
        string Rows(string text) => string.Join("|", PyCsvReader.Read(text, ';').Select(r => "[" + string.Join(",", r) + "]"));
        Assert.Equal("[a,]", Rows("a;"));
        Assert.Equal("[a,b]", Rows("a;\"b"));
        Assert.Equal("[ax,b]", Rows("\"a\"x;b"));
        Assert.Equal("[a\"b,c]", Rows("a\"b;c"));
        Assert.Equal("[]|[]|[a,b]|[c]", Rows("\n\na;b\r\nc"));
        Assert.Equal("[x\ny,z]", Rows("\"x\r\ny\";z"));
        Assert.Equal("[,]", Rows(";"));
        Assert.Equal("[a,,]", Rows("a;;\n"));
        Assert.Equal("[q\"q,2]", Rows("\"q\"\"q\";2"));
    }

    [Theory]
    [InlineData(";")]
    [InlineData("\t")]
    [InlineData(",")]
    public void Text_reader_auto_detects_semicolon_tab_and_comma(string delimiter)
    {
        var lines = new List<string> { string.Join(delimiter, "Brand", "Article", "Name", "Qty", "Price", "Days") };
        for (var i = 0; i < 25; i++)
        {
            lines.Add(string.Join(delimiter, "BOSCH", "0986" + i, "\"Pad, front\"", "5", "12,50", "2"));
        }

        var path = Write("list.csv", string.Join("\r\n", lines), Encoding.UTF8);
        var log = new PriceFileReadLog();
        var rows = PriceFileReader.ReadRows(path, "list.csv", "auto", 1, log).ToList();

        Assert.Equal(25, rows.Count);
        Assert.All(rows, r => Assert.Null(r.SkipReason));
        Assert.Equal(delimiter == "\t" ? "\\t" : delimiter, log.Delimiter);
        Assert.Equal("Pad, front", rows[0].Cells[2]);
        Assert.Equal(2, rows[0].LineNo);
        Assert.Equal("Brand", log.HeaderRow![0]);
    }

    [Fact]
    public void Short_comma_file_falls_through_to_comma_instead_of_importing_nothing()
    {
        var path = Write("short.csv", "BOSCH,0986494527,Pad,5,85\nMANN,W7121,Filter,3,20\n", Encoding.UTF8);
        var log = new PriceFileReadLog();
        var rows = PriceFileReader.ReadRows(path, "short.csv", "auto", 0, log).ToList();
        Assert.Equal(",", log.Delimiter);
        Assert.Equal(2, rows.Count);
        Assert.Equal("W7121", rows[1].Cells[1]);
    }

    [Fact]
    public void Text_reader_detects_cp1251_and_honours_explicit_encoding()
    {
        var cp1251 = Encoding.GetEncoding(1251);
        var path = Write("ru.csv", "Бренд;Артикул;Наименование;Кол;Цена\nBOSCH;0986;Колодка тормозная;4;100\n", cp1251);
        var log = new PriceFileReadLog();
        var rows = PriceFileReader.ReadRows(path, "ru.csv", "auto", 1, log).ToList();
        Assert.Equal("cp1251", log.Encoding);
        Assert.Equal("Колодка тормозная", rows.Single().Cells[2]);

        var utf8 = Write("bom.csv", "\uFEFFBOSCH;0986;Pad;4;100\n", new UTF8Encoding(true));
        var bomRows = PriceFileReader.ReadRows(utf8, "bom.csv", "utf-8", 0, new PriceFileReadLog()).ToList();
        Assert.Equal("BOSCH", bomRows.Single().Cells[0]);
    }

    [Fact]
    public void Text_reader_reports_blank_and_short_rows_inside_the_detection_window()
    {
        var path = Write("gaps.csv", "BOSCH;0986;Pad;4;100\n\nnote only\nMANN;W7121;Filter;3;20\n", Encoding.UTF8);
        var rows = PriceFileReader.ReadRows(path, "gaps.csv", "auto", 0, new PriceFileReadLog()).ToList();
        Assert.Equal(["-", "empty_row", "too_few_columns", "-"], rows.Select(r => r.SkipReason ?? "-").ToArray());
    }

    [Fact]
    public void Xlsx_reader_reads_every_sheet_and_skips_header_rows_per_sheet()
    {
        var path = Path.Combine(_dir, "multi.xlsx");
        WriteXlsx(path,
            [["Brand", "Article", "Name", "Qty", "Price"], ["BOSCH", "0986494527", "Pad", "4", "85"]],
            [["Brand", "Article", "Name", "Qty", "Price"], ["MANN", "W 712/1", "Filter", "3", "20.5"], ["DENSO", "2343005", "Plug", "", "12"]]);
        var log = new PriceFileReadLog();
        var rows = PriceFileReader.ReadRows(path, "multi.xlsx", "auto", 1, log).ToList();

        Assert.Equal("excel", log.Format);
        Assert.Equal(3, rows.Count);
        Assert.Equal([1, 2, 2], rows.Select(r => r.Sheet).ToArray());
        Assert.Equal("W 712/1", rows[1].Cells[1]);
        var outcome = PriceRecordNormalizer.Normalize(rows[1].Cells, new PriceListColumnMap(1, 2, 3, 4, 5));
        Assert.Equal(20.5m, outcome.Row!.Price);
    }

    [Fact]
    public void Malformed_xls_is_retried_as_csv_like_pyprices()
    {
        var path = Write("fake.xls", "BOSCH;0986;Pad;4;100\n", Encoding.UTF8);
        var log = new PriceFileReadLog();
        var rows = PriceFileReader.ReadRows(path, "fake.xls", "auto", 0, log).ToList();
        Assert.Equal("text", log.Format);
        Assert.Single(rows);
        Assert.Contains(log.Messages, m => m.Contains("Trying to process it as CSV", StringComparison.Ordinal));
    }

    [Fact]
    public void Unsupported_extension_is_rejected()
    {
        var path = Write("list.pdf", "x", Encoding.UTF8);
        Assert.Throws<PriceFileFormatException>(() => PriceFileReader.ReadRows(path, "list.pdf", "auto", 0, new PriceFileReadLog()).ToList());
        Assert.Equal("noext", PriceFileReader.ExtensionOf("README"));
        Assert.Equal("xlsx", PriceFileReader.ExtensionOf("S-UAE.Stock.XLSX"));
    }

    [Fact]
    public void Archive_extractor_filters_by_type_and_substring_and_flattens_paths()
    {
        var zip = Path.Combine(_dir, "prices.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            AddEntry(archive, "nested/S-UAE-stock.csv", "BOSCH;0986;Pad;4;100\n");
            AddEntry(archive, "D-USA-stock.csv", "MANN;W7121;Filter;3;20\n");
            AddEntry(archive, "../readme.pdf", "x");
        }

        var messages = new List<string>();
        var target = Path.Combine(_dir, "out");
        var kept = PriceArchiveExtractor.Extract(zip, target, "S-UAE", messages);

        Assert.Single(kept);
        Assert.Equal(Path.Combine(target, "S-UAE-stock.csv"), kept[0]);
        Assert.Contains(messages, m => m.Contains("[readme.pdf] has wrong type", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("[D-USA-stock.csv] does not match the substring", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(_dir, "readme.pdf")));
        Assert.True(PriceFileNameFilter.Matches("anything.csv", ""));
        Assert.False(PriceFileNameFilter.Matches("s-uae.csv", "S-UAE"));
    }

    [Fact]
    public void Issue_csv_matches_php_layout()
    {
        var map = new PriceListColumnMap(1, 2, 3, 4, 5);
        var labels = PriceImportIssues.ColumnLabels(["Brand", "Article"], map);
        Assert.Equal(["Col1: Brand [manufacturer]", "Col2: Article [article]", "Col3 [name]", "Col4 [exist]", "Col5 [price]"], labels);

        var outcome = PriceRecordNormalizer.Normalize(["BOSCH", "0986", "Pad", "4", "abc"], map);
        var issue = new PriceImportIssue(7, "skipped", outcome.SkipReason!, outcome.Details, ["BOSCH", "0986", "Pad", "4", "abc"], outcome.Parsed);
        var rows = PriceImportIssues.ToCsvRows([PriceImportIssues.ToFields(issue, labels)]);

        Assert.Equal(["line_no", "issue_type", "reason_code", "why_skipped_or_error", "error_details"], rows[0].Take(5).ToArray());
        Assert.Equal("Col1: Brand [manufacturer]", rows[0][5]);
        Assert.Contains("parsed_price", rows[0]);
        Assert.Equal("7", rows[1][0]);
        Assert.Equal("invalid_price", rows[1][2]);
        Assert.StartsWith("Skipped: price column is zero or not a valid number — Price column value: \"abc\"", rows[1][3], StringComparison.Ordinal);
        Assert.Equal("a,\"b c\",\"d\"\"e\"\n", PriceImportIssues.CsvLine(["a", "b c", "d\"e"]));
    }

    private string Write(string name, string content, Encoding encoding)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, encoding.GetBytes(content));
        return path;
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(content);
    }

    /// <summary>Minimal SpreadsheetML workbook (inline strings) so the reader is exercised without binary fixtures.</summary>
    private static void WriteXlsx(string path, params string[][][] sheets)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var overrides = new StringBuilder();
        var sheetRefs = new StringBuilder();
        var rels = new StringBuilder();
        for (var s = 1; s <= sheets.Length; s++)
        {
            overrides.Append("<Override PartName=\"/xl/worksheets/sheet").Append(s)
                .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            sheetRefs.Append("<sheet name=\"Sheet").Append(s).Append("\" sheetId=\"").Append(s).Append("\" r:id=\"rId").Append(s).Append("\"/>");
            rels.Append("<Relationship Id=\"rId").Append(s)
                .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet").Append(s).Append(".xml\"/>");
        }

        AddEntry(archive, "[Content_Types].xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
            + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
            + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
            + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
            + overrides + "</Types>");
        AddEntry(archive, "_rels/.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
            + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        AddEntry(archive, "xl/workbook.xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" "
            + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" + sheetRefs + "</sheets></workbook>");
        AddEntry(archive, "xl/_rels/workbook.xml.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + rels + "</Relationships>");

        for (var s = 0; s < sheets.Length; s++)
        {
            var data = new StringBuilder();
            for (var r = 0; r < sheets[s].Length; r++)
            {
                data.Append("<row r=\"").Append(r + 1).Append("\">");
                for (var c = 0; c < sheets[s][r].Length; c++)
                {
                    var value = sheets[s][r][c];
                    if (value.Length == 0)
                    {
                        continue;
                    }

                    var cellRef = (char)('A' + c) + (r + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) && !value.StartsWith('0'))
                    {
                        data.Append("<c r=\"").Append(cellRef).Append("\"><v>").Append(value).Append("</v></c>");
                    }
                    else
                    {
                        data.Append("<c r=\"").Append(cellRef).Append("\" t=\"inlineStr\"><is><t>")
                            .Append(System.Security.SecurityElement.Escape(value)).Append("</t></is></c>");
                    }
                }

                data.Append("</row>");
            }

            AddEntry(archive, "xl/worksheets/sheet" + (s + 1) + ".xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>"
                + data + "</sheetData></worksheet>");
        }
    }
}
