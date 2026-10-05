using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>cs_import_declaration_pdf</c> / <c>epc_cs_pdf_import_from_upload</c> twin:
/// uploads a declaration PDF (≤15 MB, .pdf extension), extracts text via pdftotext when
/// available with the PHP stream/operator-string fallback, maps the UAE declaration boxes
/// and line items, stages the upload under content/files/epc_custom_shipping_pdfs/staging,
/// and refuses a duplicate declaration_number (verbatim PHP exception messages).
/// Returns the full parsed payload so the caller can review auto-filled fields before saving.
/// </summary>
public interface IErpCsPdfImportService
{
    Task<ErpCsPdfImportResult> ImportAsync(byte[] binary, string originalName, string typeHint, long excludeId, CancellationToken cancellationToken = default);
}

public sealed record ErpCsPdfImportResult(
    Dictionary<string, string> Boxes,
    Dictionary<string, string> Box45,
    List<string> Box45Lines,
    List<string> Box54Lines,
    List<Dictionary<string, string>> LineItems,
    Dictionary<string, string> Core,
    List<string> AutofillKeys,
    string DeclarationType,
    string Category,
    string TextPreview,
    int BoxesMapped,
    bool TextValid,
    string ParseWarning,
    bool Partial,
    string DeclarationNumber,
    string PdfToken,
    string PdfPreviewUrl,
    string PdfFileName,
    bool PdftotextAvailable,
    string PdftotextPath);

public sealed class ErpCsPdfImportService : IErpCsPdfImportService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IWebHostEnvironment _env;

    public ErpCsPdfImportService(IErpWriteConnectionFactory connections, IWebHostEnvironment env)
    {
        _connections = connections;
        _env = env;
    }

    public async Task<ErpCsPdfImportResult> ImportAsync(byte[] binary, string originalName, string typeHint, long excludeId, CancellationToken cancellationToken = default)
    {
        var name = originalName ?? string.Empty;
        if (!Regex.IsMatch(name, @"\.pdf$", RegexOptions.IgnoreCase))
        {
            throw new ErpWriteException("Upload must be a PDF file");
        }
        if (binary.Length > 15 * 1024 * 1024)
        {
            throw new ErpWriteException("PDF exceeds 15 MB limit");
        }
        if (binary.Length == 0)
        {
            throw new ErpWriteException("Empty PDF file");
        }

        var (text, pdftotextAvailable, pdftotextPath) = ExtractText(binary);
        var parsed = ParseDeclarationText(text, typeHint, allowPartial: true, pdftotextAvailable: pdftotextAvailable);

        var declNo = DeclarationNumberFromParsed(parsed);
        if (declNo.Length > 0)
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpCsDeclarationWriteService.AssertUniqueDeclarationNumberAsync(connection, declNo, excludeId, cancellationToken).ConfigureAwait(false);
        }

        var staged = StagePdfBinary(binary, name);
        return parsed with
        {
            DeclarationNumber = declNo,
            PdfToken = staged.Token,
            PdfPreviewUrl = staged.PreviewUrl,
            PdfFileName = staged.FileName,
            PdftotextAvailable = pdftotextAvailable,
            PdftotextPath = pdftotextPath,
        };
    }

    private static string DeclarationNumberFromParsed(ErpCsPdfImportResult p)
    {
        var direct = p.Core.TryGetValue("declaration_number", out var dn) ? dn.Trim() : string.Empty;
        if (direct.Length > 0) return direct;
        // epc_cs_declaration_number_from_data — box_01 fallback.
        return p.Boxes.TryGetValue("box_01", out var b1) ? b1.Trim() : string.Empty;
    }

    // ---------------- epc_cs_stage_pdf_binary / epc_cs_pdf_storage_root ----------------

    private string StorageRoot()
    {
        var root = Path.Combine(Presentation.PhpLegacyAssetBridge.FindRepoRoot(_env), "content", "files", "epc_custom_shipping_pdfs");
        Directory.CreateDirectory(Path.Combine(root, "staging"));
        return root;
    }

    private (string Token, string PreviewUrl, string FileName) StagePdfBinary(byte[] binary, string originalName)
    {
        if (binary.Length == 0 || binary.Length < 4 || Encoding.ASCII.GetString(binary, 0, 4) != "%PDF")
        {
            throw new ErpWriteException("Invalid PDF file");
        }
        var token = Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        var dir = Path.Combine(StorageRoot(), "staging");
        try
        {
            File.WriteAllBytes(Path.Combine(dir, token + ".pdf"), binary);
        }
        catch (IOException)
        {
            throw new ErpWriteException("Could not store PDF on server");
        }
        var fileName = !string.IsNullOrWhiteSpace(originalName)
            ? Path.GetFileName(originalName)
            : "declaration_" + token + ".pdf";
        return (token, "/content/files/epc_custom_shipping_pdfs/staging/" + token + ".pdf", fileName);
    }

    // ---------------- text extraction ----------------

    private static (bool Available, string Path) PdftotextDiagnostics()
    {
        foreach (var cmd in new[] { "bash", "-c" }) { _ = cmd; }
        var env = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in env.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var p = Path.Combine(dir, "pdftotext");
            if (File.Exists(p)) return (true, p);
        }
        foreach (var p in new[] { "/usr/bin/pdftotext", "/usr/local/bin/pdftotext" })
        {
            if (File.Exists(p)) return (true, p);
        }
        return (false, string.Empty);
    }

    /// <summary>PHP epc_cs_pdf_extract_text: pdftotext subprocess first, stream fallback.</summary>
    private static (string Text, bool PdftotextAvailable, string PdftotextPath) ExtractText(byte[] binary)
    {
        var (available, path) = PdftotextDiagnostics();
        if (binary.Length >= 4 && Encoding.ASCII.GetString(binary, 0, 4) == "%PDF")
        {
            if (available)
            {
                var viaTool = RunPdftotext(binary, path);
                if (TextLooksValid(viaTool))
                {
                    return (NormalizeText(viaTool), available, path);
                }
            }
            return (ExtractTextFromStreams(binary), available, path);
        }
        return (string.Empty, available, path);
    }

    private static string RunPdftotext(byte[] binary, string path)
    {
        var tmpIn = Path.Combine(Path.GetTempPath(), "epc_cs_pdf_" + Guid.NewGuid().ToString("N"));
        var tmpOut = tmpIn + ".txt";
        try
        {
            File.WriteAllBytes(tmpIn, binary);
            var psi = new System.Diagnostics.ProcessStartInfo(path, "-layout -q " + Quote(tmpIn) + " " + Quote(tmpOut))
            {
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(30000);
            if (File.Exists(tmpOut))
            {
                return File.ReadAllText(tmpOut);
            }
        }
        catch
        {
            // fall through to stream extraction
        }
        finally
        {
            TryDelete(tmpIn);
            TryDelete(tmpOut);
        }
        return string.Empty;
    }

    private static string Quote(string p) => "\"" + p.Replace("\"", "\\\"") + "\"";
    private static void TryDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }

    private static bool TextLooksValid(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var len = text.Length;
        if (len < 20) return false;
        var good = Regex.Matches(text, @"[\x20-\x7E\n\r]").Count;
        return (double)good / Math.Max(1, len) >= 0.55;
    }

    /// <summary>PHP epc_cs_pdf_decode_flate_stream (zlib, raw deflate, gzip).</summary>
    private static string DecodeFlateStream(byte[] stream)
    {
        foreach (var candidate in new[] { stream, stream.Length > 2 ? stream[2..] : Array.Empty<byte>() })
        {
            if (candidate.Length == 0) continue;
            try
            {
                using var ms = new MemoryStream(candidate);
                using var zs = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                zs.CopyTo(outMs);
                var s = Encoding.Latin1.GetString(outMs.ToArray());
                if (s.Length > 0) return s;
            }
            catch { }
            try
            {
                using var ms = new MemoryStream(candidate);
                using var ds = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                ds.CopyTo(outMs);
                var s = Encoding.Latin1.GetString(outMs.ToArray());
                if (s.Length > 0) return s;
            }
            catch { }
        }
        try
        {
            using var ms = new MemoryStream(stream);
            using var gs = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gs.CopyTo(outMs);
            var s = Encoding.Latin1.GetString(outMs.ToArray());
            if (s.Length > 0) return s;
        }
        catch { }
        return string.Empty;
    }

    /// <summary>PHP epc_uae_fta_pdf_unescape_string.</summary>
    private static string PdfUnescapeString(string s)
    {
        var outSb = new StringBuilder();
        var len = s.Length;
        for (var i = 0; i < len; i++)
        {
            var c = s[i];
            if (c != '\\') { outSb.Append(c); continue; }
            if (++i >= len) break;
            var esc = s[i];
            if (esc == 'n') outSb.Append('\n');
            else if (esc == 'r') outSb.Append('\r');
            else if (esc == 't') outSb.Append('\t');
            else if (esc == 'b') outSb.Append('\b');
            else if (esc == 'f') outSb.Append('\f');
            else if (esc == '(' || esc == ')' || esc == '\\') outSb.Append(esc);
            else if (esc >= '0' && esc <= '7')
            {
                var oct = esc.ToString();
                for (var j = 0; j < 2 && (i + 1) < len && s[i + 1] >= '0' && s[i + 1] <= '7'; j++)
                {
                    oct += s[++i];
                }
                outSb.Append((char)Convert.ToInt32(oct, 8));
            }
            else outSb.Append(esc);
        }
        return outSb.ToString();
    }

    /// <summary>PHP epc_cs_pdf_collect_operator_strings.</summary>
    private static void CollectOperatorStrings(string content, List<string> lines)
    {
        foreach (Match m in Regex.Matches(content, @"\((?:[^\\()]|\\.)*\)\s*(?:Tj|'|"")", RegexOptions.Singleline))
        {
            var sm = Regex.Match(m.Value, @"\((.*)\)\s*(?:Tj|'|"")", RegexOptions.Singleline);
            if (sm.Success)
            {
                var chunk = PdfUnescapeString(sm.Groups[1].Value);
                if (chunk.Trim().Length > 0) lines.Add(chunk);
            }
        }
        foreach (Match m in Regex.Matches(content, @"<([0-9A-Fa-f\s]+)>\s*(?:Tj|'|"")", RegexOptions.Singleline))
        {
            var h = Regex.Replace(m.Groups[1].Value, @"\s+", "");
            if (h.Length == 0 || (h.Length % 2) != 0) continue;
            var sb = new StringBuilder();
            for (var i = 0; i < h.Length; i += 2)
            {
                sb.Append((char)Convert.ToByte(h.Substring(i, 2), 16));
            }
            var chunk = sb.ToString();
            if (chunk.Trim().Length > 0) lines.Add(chunk);
        }
        foreach (Match m in Regex.Matches(content, @"\[(.*?)\]\s*TJ", RegexOptions.Singleline))
        {
            var sb = new StringBuilder();
            foreach (Match lit in Regex.Matches(m.Groups[1].Value, @"\((?:[^\\()]|\\.)*\)", RegexOptions.Singleline))
            {
                var sm = Regex.Match(lit.Value, @"\((.*)\)", RegexOptions.Singleline);
                if (sm.Success) sb.Append(PdfUnescapeString(sm.Groups[1].Value));
            }
            var chunk = sb.ToString();
            if (chunk.Trim().Length > 0) lines.Add(chunk);
        }
    }

    /// <summary>PHP epc_cs_pdf_extract_text_from_streams.</summary>
    private static string ExtractTextFromStreams(byte[] binary)
    {
        var raw = Encoding.Latin1.GetString(binary);
        var lines = new List<string>();
        CollectOperatorStrings(raw, lines);
        foreach (Match m in Regex.Matches(raw, @"\bBT\b(.*?)ET", RegexOptions.Singleline))
        {
            CollectOperatorStrings(m.Groups[1].Value, lines);
        }
        foreach (Match m in Regex.Matches(raw, @"stream\r?\n(.*?)\r?\nendstream", RegexOptions.Singleline))
        {
            var decoded = DecodeFlateStream(Encoding.Latin1.GetBytes(m.Groups[1].Value));
            if (decoded.Length == 0) continue;
            CollectOperatorStrings(decoded, lines);
            foreach (Match inner in Regex.Matches(decoded, @"\bBT\b(.*?)ET", RegexOptions.Singleline))
            {
                CollectOperatorStrings(inner.Groups[1].Value, lines);
            }
        }
        var text = NormalizeText(string.Join("\n", lines));
        if (text.Length > 0 && TextLooksValid(text)) return text;
        var flat = NormalizeText(string.Join(' ', lines));
        if (flat.Length > 0 && flat.Length > text.Length) return flat;
        return text;
    }

    private static string NormalizeText(string text)
    {
        var t = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        t = Regex.Replace(t, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
        return t.Trim();
    }

    // ---------------- declaration-type detection / helpers ----------------

    private static List<string> AllDeclarationTypesFlat()
    {
        var outList = new List<string>();
        foreach (var types in ErpCsDeclarationWriteService.DeclarationTypes.Values)
        {
            outList.AddRange(types);
        }
        outList.Sort((a, b) => b.Length.CompareTo(a.Length));
        return outList;
    }

    private static string DetectDeclarationType(string text, string hint)
    {
        hint = hint.Trim();
        if (hint.Length > 0)
        {
            foreach (var t in AllDeclarationTypesFlat())
            {
                if (string.Equals(t, hint, StringComparison.OrdinalIgnoreCase)) return t;
            }
        }
        foreach (var t in AllDeclarationTypesFlat())
        {
            if (text.Contains(t, StringComparison.OrdinalIgnoreCase)) return t;
        }
        var m = Regex.Match(text, @"\b(IMPORT|EXPORT|TRANSIT|COURIER)\b", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : string.Empty;
    }

    private static string CategoryForType(string declarationType)
    {
        declarationType = declarationType.Trim();
        foreach (var kv in ErpCsDeclarationWriteService.DeclarationTypes)
        {
            if (kv.Value.Contains(declarationType)) return kv.Key;
        }
        return "import";
    }

    private static string ParseDate(string raw)
    {
        raw = raw.Trim();
        var m = Regex.Match(raw, @"^(\d{2})\/(\d{2})\/(\d{4})$");
        if (m.Success) return m.Groups[3].Value + "-" + m.Groups[2].Value + "-" + m.Groups[1].Value;
        if (Regex.IsMatch(raw, @"^\d{4}-\d{2}-\d{2}$")) return raw;
        if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var num) && num > 40000 && num < 60000)
        {
            var ts = ((long)(int)num - 25569) * 86400;
            return DateTimeOffset.FromUnixTimeSeconds(ts).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        return raw;
    }

    private static List<string> CollectLines(string text)
    {
        var lines = new List<string>();
        foreach (var rawLine in Regex.Split(text, @"\n+"))
        {
            var line = Regex.Replace(rawLine, @"\s+", " ").Trim();
            if (line.Length == 0 || Regex.IsMatch(line, @"^Page \d+ of \d+$", RegexOptions.IgnoreCase)) continue;
            lines.Add(line);
        }
        return lines;
    }

    private static readonly string[] CountrySkip = { "AE", "IN", "INS", "FOB", "FRT", "LOC", "KG", "AED", "USD", "EUR", "GBP" };

    private static bool IsCountryCode(string s)
    {
        var v = s.Trim().ToUpperInvariant();
        if (v.Length != 2 || !v.All(char.IsLetter)) return false;
        return !CountrySkip.Contains(v);
    }

    private static bool IsHsCode(string s) => Regex.IsMatch(s.Trim(), @"^\d{8}$");

    private static bool IsPaymentLine(string s) => Regex.IsMatch(s.Trim(), @"^[A-Z]{2,5}\s+[\d.]+\s+\[\d+\]");

    private static List<string> DedupeStrings(IEnumerable<string> lines)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var outList = new List<string>();
        foreach (var line in lines)
        {
            var key = line.Trim();
            if (key.Length == 0 || !seen.Add(key)) continue;
            outList.Add(line);
        }
        return outList;
    }

    private static List<T> DedupeRepeatedBlock<T>(List<T> items, Func<T, T, bool>? equals = null)
    {
        var n = items.Count;
        if (n < 2 || n % 2 != 0) return items;
        var half = n / 2;
        for (var i = 0; i < half; i++)
        {
            var a = items[i];
            var b = items[i + half];
            var same = equals != null ? equals(a, b) : EqualityComparer<T>.Default.Equals(a, b);
            if (!same) return items;
        }
        return items.GetRange(0, half);
    }

    private static string LineItemHash(Dictionary<string, string> item)
    {
        static string Get(Dictionary<string, string> d, string k) => d.TryGetValue(k, out var v) ? v.Trim() : string.Empty;
        static string Num(Dictionary<string, string> d, string k1, string k2)
        {
            var raw = Get(d, k1);
            if (raw.Length == 0 && k2.Length > 0) raw = Get(d, k2);
            var v = ParseDecimal(raw);
            return Math.Round(v, 4).ToString(CultureInfo.InvariantCulture);
        }
        return string.Join('|', new[]
        {
            Get(item, "hs_code"), Get(item, "country_of_origin"), Get(item, "description"),
            Num(item, "weight_gross", "weight"), Num(item, "foreign_value", ""), Num(item, "cif_local_value", "amount"),
            Num(item, "quantity", ""),
        });
    }

    private static bool LineItemsEqual(Dictionary<string, string> a, Dictionary<string, string> b) => LineItemHash(a) == LineItemHash(b);

    private static string ClassifyLineItemLine(string line)
    {
        line = line.Trim();
        if (line.Length == 0) return "empty";
        if (IsHsCode(line)) return "hs_code";
        if (IsCountryCode(line)) return "origin";
        if (Regex.IsMatch(line, @"^[\d.]+\s*kg$", RegexOptions.IgnoreCase)) return "weight_gross";
        if (Regex.IsMatch(line, @"^(kg|pcs)$", RegexOptions.IgnoreCase)) return "unit";
        if (line is "AED" or "USD" or "EUR" or "GBP") return "currency";
        if (Regex.IsMatch(line, @"^1\.0+$")) return "currency_rate";
        if (Regex.IsMatch(line, @"^(INS|DTY)$", RegexOptions.IgnoreCase)) return "income_type";
        if (Regex.IsMatch(line, @"^\d+\.\d+$"))
        {
            if (decimal.TryParse(line, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v <= 100 && Regex.IsMatch(line, @"\.0+$")) return "duty_rate";
            return "number";
        }
        if (IsPaymentLine(line) || Regex.IsMatch(line, @"^\[")) return "stop";
        if (Regex.IsMatch(line, @"^(Total Value|LOC:|Page \d+)", RegexOptions.IgnoreCase)) return "stop";
        if (Regex.IsMatch(line, @"^[A-Z][A-Z0-9\s\-\/\.]{4,}$")
            && !Regex.IsMatch(line, @"^(LAND|SEA|AIR|IMPORT|EXPORT|AED|INS)$", RegexOptions.IgnoreCase)
            && !line.Contains("Total Value", StringComparison.OrdinalIgnoreCase)
            && !Regex.IsMatch(line, @"^AE-\d+", RegexOptions.IgnoreCase)
            && !Regex.IsMatch(line, @"^\d"))
        {
            return "description";
        }
        if (Regex.IsMatch(line, @"^\d+(\.\d+)?$")) return "number";
        return "other";
    }

    private static (int? Start, int? End, List<string> Codes) FindHsCodeBlock(List<string> lines)
    {
        var count = lines.Count;
        int? bestStart = null, bestEnd = null;
        var bestLen = 0;
        for (var i = 0; i < count; i++)
        {
            if (ClassifyLineItemLine(lines[i]) != "hs_code") continue;
            var start = i;
            while (i < count && ClassifyLineItemLine(lines[i]) == "hs_code") i++;
            var len = i - start;
            if (len >= bestLen)
            {
                bestStart = start;
                bestEnd = i - 1;
                bestLen = len;
            }
        }
        if (bestStart is null) return (null, null, new List<string>());
        var codes = DedupeRepeatedBlock(lines.GetRange(bestStart.Value, bestLen));
        bestEnd = bestStart + codes.Count - 1;
        return (bestStart, bestEnd, codes);
    }

    private static (Dictionary<string, List<string>> Out, List<List<string>> NumberRuns) CollectRunsBackward(List<string> lines, int startIdx, string[] expectedTypes)
    {
        var outDict = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var numberRuns = new List<List<string>>();
        var i = startIdx - 1;
        foreach (var type in expectedTypes)
        {
            var values = new List<string>();
            if (i < 0)
            {
                if (type == "number") numberRuns.Add(new List<string>());
                else outDict[type] = new List<string>();
                continue;
            }
            while (i >= 0)
            {
                var cls = ClassifyLineItemLine(lines[i]);
                if (cls is "stop" or "other" or "empty") break;
                if (type == "number")
                {
                    if (cls != "number") break;
                }
                else if (cls != type) break;
                values.Add(lines[i]);
                i--;
            }
            values.Reverse();
            if (type == "number") numberRuns.Add(values);
            else outDict[type] = values;
        }
        return (outDict, numberRuns);
    }

    private static string NormalizeWeightValue(string raw) => DecimalString(Regex.Replace(raw.Trim(), @"\s*kg", "", RegexOptions.IgnoreCase));

    private static List<string> TakeN(List<string> values, int n, bool fromEnd = false)
    {
        if (n <= 0 || values.Count == 0) return new List<string>();
        if (values.Count <= n) return new List<string>(values);
        return fromEnd ? values.GetRange(values.Count - n, n) : values.GetRange(0, n);
    }

    private static (List<string> WeightsGross, List<string> Units, List<string> Quantities) ExtractLowerWeightTable(List<string> lines, int hsEnd, int zoneEnd, int n)
    {
        var slice = lines.GetRange(hsEnd + 1, Math.Max(0, zoneEnd - hsEnd - 1));
        var weightsGross = new List<string>();
        var units = new List<string>();
        var quantities = new List<string>();
        var count = slice.Count;
        var i = 0;
        while (i < count && weightsGross.Count < n)
        {
            var line = slice[i];
            var m = Regex.Match(line, @"^([\d.]+)\s*kg$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                weightsGross.Add(NormalizeWeightValue(m.Groups[1].Value));
                i++;
                continue;
            }
            var m2 = Regex.Match(line, @"^([\d.]+)$");
            if (m2.Success && i + 1 < count && Regex.IsMatch(slice[i + 1], @"^kg$", RegexOptions.IgnoreCase))
            {
                weightsGross.Add(NormalizeWeightValue(m2.Groups[1].Value));
                i += 2;
                continue;
            }
            i++;
        }
        while (i < count && units.Count < n)
        {
            if (Regex.IsMatch(slice[i], @"^(kg|pcs)$", RegexOptions.IgnoreCase)) units.Add(slice[i].ToUpperInvariant());
            i++;
        }
        while (i < count && quantities.Count < n)
        {
            var line = slice[i];
            if (Regex.IsMatch(line, @"^\d+(\.\d+)?$")) quantities.Add(line);
            else if (IsPaymentLine(line) || Regex.IsMatch(line, @"^\d{5,}$")) break;
            i++;
        }
        return (weightsGross, units, quantities);
    }

    private static bool IsDescriptionLine(string line)
    {
        line = line.Trim();
        return Regex.IsMatch(line, @"^[A-Z][A-Z0-9\s\-\/\.]{4,}$")
            && !Regex.IsMatch(line, @"^(LAND|SEA|AIR|IMPORT|EXPORT|AED|INS)$", RegexOptions.IgnoreCase)
            && !IsPaymentLine(line)
            && !line.Contains("Total Value", StringComparison.OrdinalIgnoreCase)
            && !line.Contains("LOC:", StringComparison.OrdinalIgnoreCase)
            && !Regex.IsMatch(line, @"^AE-\d+", RegexOptions.IgnoreCase)
            && !Regex.IsMatch(line, @"^\[")
            && !Regex.IsMatch(line, @"^\d");
    }

    private static (List<string> Descriptions, List<string> Origins) FillUpperIdentityFields(List<string> lines, int hsStart, int n, List<string> descriptions, List<string> origins)
    {
        if (descriptions.Count >= n && origins.Count >= n)
        {
            return (TakeN(descriptions, n), TakeN(origins, n));
        }
        var desc = new List<string>();
        var orig = new List<string>();
        foreach (var line in lines.GetRange(0, Math.Max(0, hsStart)))
        {
            if (IsCountryCode(line)) orig.Add(line.ToUpperInvariant());
            else if (IsDescriptionLine(line)) desc.Add(line);
        }
        desc = TakeN(DedupeRepeatedBlock(desc), n, true);
        orig = TakeN(DedupeRepeatedBlock(orig), n, true);
        if (descriptions.Count < n) descriptions = desc;
        if (origins.Count < n) origins = orig;
        return (TakeN(descriptions, n), TakeN(origins, n));
    }

    private static int LineItemsZoneEnd(List<string> lines)
    {
        var n = lines.Count;
        for (var i = 0; i < n; i++)
        {
            var line = lines[i];
            if (IsPaymentLine(line)) return i;
            if (Regex.IsMatch(line, @"^\[FOB\]", RegexOptions.IgnoreCase)
                || Regex.IsMatch(line, @"^(?:Total\s+Value|Invoice\s+Value|Inv\.?\s*Value)\s*:", RegexOptions.IgnoreCase))
            {
                return i;
            }
        }
        return n;
    }

    private static List<Dictionary<string, string>> ParseLineItemBlocks(List<string> allLines, Dictionary<string, string> core)
    {
        var zoneEnd = LineItemsZoneEnd(allLines);
        var lines = allLines.GetRange(0, zoneEnd);
        var (hsStart, hsEnd, hsCodes) = FindHsCodeBlock(lines);
        if (hsCodes.Count == 0 || hsStart is null) return new List<Dictionary<string, string>>();
        var n = hsCodes.Count;
        var end = hsEnd ?? hsStart.Value;

        var upperTypes = new[] { "description", "origin", "number", "currency", "currency_rate", "number", "duty_rate", "income_type", "number" };
        var (upper, numberRuns) = CollectRunsBackward(lines, hsStart.Value, upperTypes);

        var descriptions = DedupeRepeatedBlock(TakeN(upper.GetValueOrDefault("description", new List<string>()), n));
        var origins = DedupeRepeatedBlock(TakeN(upper.GetValueOrDefault("origin", new List<string>()), n));

        var cifValues = new List<string>();
        var foreignValues = new List<string>();
        var totalDuties = new List<string>();
        if (numberRuns.Count >= 3)
        {
            cifValues = DedupeRepeatedBlock(TakeN(numberRuns[0], n));
            foreignValues = DedupeRepeatedBlock(TakeN(numberRuns[1], n));
            totalDuties = DedupeRepeatedBlock(TakeN(numberRuns[2], n));
        }
        else if (numberRuns.Count == 2)
        {
            foreignValues = DedupeRepeatedBlock(TakeN(numberRuns[0], n));
            cifValues = DedupeRepeatedBlock(TakeN(numberRuns[1], n));
        }
        else if (numberRuns.Count == 1)
        {
            cifValues = DedupeRepeatedBlock(TakeN(numberRuns[0], n));
        }

        var currencies = DedupeRepeatedBlock(TakeN(upper.GetValueOrDefault("currency", new List<string>()), n));
        var currencyRates = DedupeRepeatedBlock(TakeN(upper.GetValueOrDefault("currency_rate", new List<string>()), n));
        var incomeTypes = DedupeRepeatedBlock(TakeN(upper.GetValueOrDefault("income_type", new List<string>()), n));
        var dutyRates = TakeN(DedupeRepeatedBlock(TakeN(upper.GetValueOrDefault("duty_rate", new List<string>()), n)), n);

        var (weightsGross, units, quantities) = ExtractLowerWeightTable(lines, end, zoneEnd, n);
        weightsGross = TakeN(DedupeRepeatedBlock(weightsGross), n);
        units = DedupeRepeatedBlock(TakeN(units, n));
        quantities = DedupeRepeatedBlock(TakeN(quantities, n));

        (descriptions, origins) = FillUpperIdentityFields(lines, hsStart.Value, n, descriptions, origins);

        var pkgQtys = new List<string>();
        var upperRows = new List<Dictionary<string, string>>();
        var lowerRows = new List<Dictionary<string, string>>();
        for (var i = 0; i < n; i++)
        {
            upperRows.Add(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["hs_code"] = i < hsCodes.Count ? hsCodes[i] : "",
                ["description"] = i < descriptions.Count ? descriptions[i] : "",
                ["country_of_origin"] = i < origins.Count ? origins[i].ToUpperInvariant() : "",
                ["foreign_value"] = i < foreignValues.Count ? DecimalString(foreignValues[i]) : "0",
                ["currency"] = i < currencies.Count ? currencies[i] : "AED",
                ["currency_rate"] = i < currencyRates.Count ? DecimalString(currencyRates[i]) : "1",
                ["cif_local_value"] = i < cifValues.Count ? DecimalString(cifValues[i]) : "0",
                ["duty_rate"] = i < dutyRates.Count ? DecimalString(dutyRates[i]) : "0",
                ["income_type"] = i < incomeTypes.Count ? incomeTypes[i] : "",
                ["total_duty_aed"] = i < totalDuties.Count ? DecimalString(totalDuties[i]) : "0",
            });
            var gross = i < weightsGross.Count ? weightsGross[i] : "0";
            lowerRows.Add(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["packages_qty"] = i < pkgQtys.Count ? DecimalString(pkgQtys[i]) : "0",
                ["packages_type"] = core.GetValueOrDefault("package_type", ""),
                ["quantity"] = i < quantities.Count ? DecimalString(quantities[i]) : "0",
                ["unit"] = i < units.Count ? units[i].ToUpperInvariant() : "KG",
                ["weight_net"] = gross != "0" ? gross : "0",
                ["weight_gross"] = gross != "0" ? gross : "0",
            });
        }

        var items = new List<Dictionary<string, string>>();
        for (var i = 0; i < n; i++)
        {
            var upperRow = upperRows[i];
            var lowerRow = lowerRows[i];
            if (upperRow["hs_code"].Trim().Length == 0
                && upperRow["description"].Trim().Length == 0
                && upperRow["country_of_origin"].Trim().Length == 0)
            {
                continue;
            }
            var item = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["line_number"] = (i + 1).ToString(CultureInfo.InvariantCulture),
                ["weight"] = lowerRow["weight_gross"],
                ["amount"] = upperRow["cif_local_value"],
                ["volume"] = "0",
                ["volume_unit"] = "CBM",
                ["aip_no"] = "",
                ["aip_duty"] = "",
            };
            foreach (var kv in upperRow) item[kv.Key] = kv.Value;
            foreach (var kv in lowerRow) item[kv.Key] = kv.Value;
            if (ParseDecimal(item["quantity"]) <= 0 && ParseDecimal(item["weight_gross"]) > 0)
            {
                item["quantity"] = item["weight_gross"];
                item["unit"] = "KG";
            }
            if (ParseDecimal(item["quantity"]) <= 0) item["quantity"] = "1";
            item["weight"] = item["weight_gross"];
            item["amount"] = item["cif_local_value"];
            items.Add(item);
        }
        return items;
    }

    private static List<Dictionary<string, string>> DedupeLineItems(List<Dictionary<string, string>> items)
    {
        if (items.Count == 0) return items;
        items = DedupeRepeatedBlock(items, LineItemsEqual);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var outList = new List<Dictionary<string, string>>();
        foreach (var item in items)
        {
            var h = LineItemHash(item);
            if (h == "|||||0|0|0" || Regex.IsMatch(h, @"^\|+0(\|0)*$")) continue;
            if (!seen.Add(h)) continue;
            outList.Add(item);
        }
        for (var i = 0; i < outList.Count; i++)
        {
            outList[i]["line_number"] = (i + 1).ToString(CultureInfo.InvariantCulture);
        }
        return outList;
    }

    private static decimal ParseDecimal(string? val, decimal fallback = 0m)
    {
        if (val is null) return fallback;
        var s = val.Trim().Replace(",", "").Replace(" ", "");
        if (s.Length == 0) return fallback;
        return decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

    private static string DecimalString(string? val, string fallback = "")
    {
        var s = (val ?? string.Empty).Trim().Replace(",", "").Replace(" ", "");
        if (s.Length == 0) return fallback;
        return decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? s : fallback;
    }

    private static string NormalizeYesNo(string raw)
    {
        var v = raw.Trim().ToUpperInvariant();
        if (v is "Y" or "YES" or "1" or "TRUE") return "YES";
        if (v is "N" or "NO" or "0" or "FALSE") return "NO";
        return string.Empty;
    }

    private static Dictionary<string, string> ParseBox45Fields(string flat, List<string> lines, List<string> box45Lines)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["invoice_term"] = "",
            ["invoice_value"] = "",
            ["customs_inspection_required"] = "",
        };
        foreach (var line in box45Lines.Concat(lines))
        {
            Match m;
            if (result["invoice_term"].Length == 0 && (m = Regex.Match(line, @"\[(FOB|CIF|CFR|EXW|DAP|DDP|FCA|CPT|CIP|FAS|DAT|DPU)\]", RegexOptions.IgnoreCase)).Success)
            {
                result["invoice_term"] = m.Groups[1].Value.ToUpperInvariant();
            }
            if (result["invoice_term"].Length == 0
                && (m = Regex.Match(line, @"\b(FOB|CIF|CFR|EXW|DAP|DDP|FCA|CPT|CIP|FAS|DAT|DPU)\b", RegexOptions.IgnoreCase)).Success
                && !Regex.IsMatch(line, @"FRT|FREIGHT|INS\s*:", RegexOptions.IgnoreCase))
            {
                result["invoice_term"] = m.Groups[1].Value.ToUpperInvariant();
            }
            if (result["invoice_value"].Length == 0
                && (m = Regex.Match(line, @"^(?:Total\s+Value|Invoice\s+Value|Inv\.?\s*Value)\s*:\s*([\d,\.]+)", RegexOptions.IgnoreCase)).Success)
            {
                result["invoice_value"] = DecimalString(m.Groups[1].Value.Replace(",", ""));
            }
            if (result["customs_inspection_required"].Length == 0
                && (m = Regex.Match(line, @"(?:Custom\s*Inspection|Inspection\s+Required|Physical\s+Inspection|CUST\s*INSP)\s*:?\s*(YES|NO|Y|N)", RegexOptions.IgnoreCase)).Success)
            {
                result["customs_inspection_required"] = NormalizeYesNo(m.Groups[1].Value);
            }
        }
        Match fm;
        if (result["invoice_term"].Length == 0
            && (fm = Regex.Match(flat, @"\[(FOB|CIF|CFR|EXW|DAP|DDP|FCA|CPT|CIP|FAS|DAT|DPU)\]", RegexOptions.IgnoreCase)).Success)
        {
            result["invoice_term"] = fm.Groups[1].Value.ToUpperInvariant();
        }
        if (result["invoice_value"].Length == 0
            && (fm = Regex.Match(flat, @"(?:Total\s+Value|Invoice\s+Value|Inv\.?\s*Value)\s*:\s*([\d,\.]+)", RegexOptions.IgnoreCase)).Success)
        {
            result["invoice_value"] = DecimalString(fm.Groups[1].Value.Replace(",", ""));
        }
        if (result["customs_inspection_required"].Length == 0)
        {
            foreach (var pat in new[]
            {
                @"(?:Custom\s*Inspection|Inspection\s+Required|Physical\s+Inspection|CUST\s*INSP)\s*:?\s*(YES|NO|Y|N)",
                @"\bInspection\s+(YES|NO|Y|N)\b",
            })
            {
                if ((fm = Regex.Match(flat, pat, RegexOptions.IgnoreCase)).Success)
                {
                    result["customs_inspection_required"] = NormalizeYesNo(fm.Groups[1].Value);
                    break;
                }
            }
        }
        return result;
    }

    /// <summary>PHP epc_cs_pdf_parse_declaration_text — verbatim box mapping.</summary>
    private static ErpCsPdfImportResult ParseDeclarationText(string rawText, string typeHint, bool allowPartial, bool pdftotextAvailable)
    {
        var text = NormalizeText(rawText);
        var textValid = TextLooksValid(text);
        var pdftotextMissing = !pdftotextAvailable;

        if (text.Length == 0)
        {
            var msg = "Could not extract any text from the PDF.";
            if (pdftotextMissing)
            {
                msg += " Server: pdftotext (poppler-utils) is not installed — ask your administrator to run the poppler install script, or fill the form manually.";
            }
            else
            {
                msg += " Use a text-based declaration copy (not a scanned image), or fill the form manually.";
            }
            throw new ErpWriteException(msg);
        }

        var parseWarning = "";
        if (!textValid)
        {
            parseWarning = "PDF text quality is low — some fields may be missing.";
            if (pdftotextMissing) parseWarning += " Server: pdftotext missing (poppler-utils not installed).";
            if (!allowPartial)
            {
                var msg = "Could not extract readable text from PDF. Use a text-based declaration copy (not a scan)";
                if (pdftotextMissing) msg += ", or ensure pdftotext is installed on the server (poppler-utils)";
                msg += ". You can continue filling the form manually.";
                throw new ErpWriteException(msg);
            }
        }
        var lines = CollectLines(text);
        var flat = string.Join("\n", lines);

        var boxes = new Dictionary<string, string>(StringComparer.Ordinal);
        var autofill = new HashSet<string>(StringComparer.Ordinal);
        var core = new Dictionary<string, string>(StringComparer.Ordinal);
        var box45Lines = new List<string>();
        var box54Lines = new List<string>();

        void SetBox(string key, string? val)
        {
            var v = val?.Trim();
            if (string.IsNullOrEmpty(v)) return;
            boxes[key] = v;
            autofill.Add(key);
        }

        var declType = DetectDeclarationType(flat, typeHint);
        if (declType.Length > 0)
        {
            SetBox("box_03", declType);
            core["declaration_type"] = declType;
            core["category"] = CategoryForType(declType);
        }

        Match m;
        if ((m = Regex.Match(flat, @"\b(\d{3}-\d{8}-\d{2})\b")).Success)
        {
            SetBox("box_01", m.Groups[1].Value);
            core["declaration_number"] = m.Groups[1].Value;
        }

        var dates = new List<string>();
        foreach (Match dm in Regex.Matches(flat, @"\b(\d{2}\/\d{2}\/\d{4})\b"))
        {
            dates.Add(ParseDate(dm.Groups[1].Value));
        }
        if (dates.Count > 0)
        {
            SetBox("box_02", dates[0]);
            core["declaration_date"] = dates[0];
            core["entry_date"] = dates[0];
            if (dates.Count > 1) SetBox("box_55", dates[1]);
        }

        foreach (var line in lines)
        {
            if ((m = Regex.Match(line, @"^(LAND|SEA|AIR)$", RegexOptions.IgnoreCase)).Success)
            {
                SetBox("box_04", m.Groups[1].Value.ToUpperInvariant());
                break;
            }
        }

        if ((m = Regex.Match(flat, @"\b(AE-\d+\s*-\s*.+?)(?:\s*\(|$)", RegexOptions.IgnoreCase)).Success)
        {
            var imp = m.Groups[1].Value.Trim();
            SetBox("box_06", imp);
            core["company"] = imp;
            var cm = Regex.Match(imp, @"AE-(\d+)");
            if (cm.Success) SetBox("box_43", "AE-" + cm.Groups[1].Value);
        }

        if ((m = Regex.Match(flat, @"\b(\d+(?:\.\d+)?)\s*\(\s*kg\s*\)", RegexOptions.IgnoreCase)).Success)
        {
            SetBox("box_10", m.Groups[1].Value + " kg");
            core["gross_weight"] = ParseDecimal(m.Groups[1].Value).ToString(CultureInfo.InvariantCulture);
        }

        if ((m = Regex.Match(flat, @"\b(\d+(?:\.\d+)?)\s+(CARTONS?|PALLETS?|PACKAGES?|BOXES?)\b", RegexOptions.IgnoreCase)).Success)
        {
            SetBox("box_16", m.Groups[1].Value + " " + m.Groups[2].Value.ToUpperInvariant());
            core["package_detail"] = m.Groups[1].Value + " " + m.Groups[2].Value.ToUpperInvariant();
            core["package_type"] = m.Groups[2].Value.ToUpperInvariant();
        }

        if ((m = Regex.Match(flat, @"\b(100\d{12})\b")).Success)
        {
            SetBox("box_12a", m.Groups[1].Value);
        }

        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"^\d{5,8}$") && !boxes.ContainsKey("box_12"))
            {
                SetBox("box_12", line);
                break;
            }
        }

        if ((m = Regex.Match(flat, @"\b(\d{10,15})\b")).Success)
        {
            foreach (var line in lines)
            {
                if (line == m.Groups[1].Value && line.Length >= 10 && !IsHsCode(line))
                {
                    SetBox("box_17", line);
                    core["bl_number"] = line;
                    break;
                }
            }
        }
        if (!boxes.ContainsKey("box_17") && (m = Regex.Match(flat, @"\b(\d{12})\b")).Success && !IsHsCode(m.Groups[1].Value))
        {
            SetBox("box_17", m.Groups[1].Value);
            core["bl_number"] = m.Groups[1].Value;
        }

        foreach (var line in lines)
        {
            if ((m = Regex.Match(line, @"^LOC:\s*(.+)$", RegexOptions.IgnoreCase)).Success)
            {
                SetBox("box_20", m.Groups[1].Value.Trim());
                core["port_of_exit"] = m.Groups[1].Value.Trim();
            }
        }

        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"^\[FOB\]", RegexOptions.IgnoreCase) || Regex.IsMatch(line, @"\[(FOB|CIF|CFR|EXW|DAP|DDP|FCA)\]", RegexOptions.IgnoreCase))
            {
                box45Lines.Add(line);
            }
            else if (Regex.IsMatch(line, @"^(?:Total\s+Value|Invoice\s+Value|Inv\.?\s*Value)\s*:", RegexOptions.IgnoreCase))
            {
                box45Lines.Add(line);
            }
            else if (Regex.IsMatch(line, @"(?:Custom\s*Inspection|Inspection\s+Required|Physical\s+Inspection|CUST\s*INSP)\s*:?\s*(YES|NO|Y|N)", RegexOptions.IgnoreCase))
            {
                box45Lines.Add(line);
            }
            else if (IsPaymentLine(line))
            {
                box54Lines.Add(line);
            }
        }
        box45Lines = DedupeStrings(box45Lines);
        box54Lines = DedupeStrings(box54Lines);
        if (box45Lines.Count > 0) SetBox("box_45", string.Join("\n", box45Lines));
        if (box54Lines.Count > 0) SetBox("box_54", string.Join(" | ", box54Lines));

        var box45Parsed = ParseBox45Fields(flat, lines, box45Lines);
        if (box45Parsed["invoice_term"].Length > 0)
        {
            core["invoice_term"] = box45Parsed["invoice_term"];
            core["shipping_terms_inco"] = box45Parsed["invoice_term"];
            autofill.Add("invoice_term");
        }
        if (box45Parsed["invoice_value"].Length > 0)
        {
            core["invoice_value"] = box45Parsed["invoice_value"];
            core["invoice_amount_aed"] = ParseDecimal(box45Parsed["invoice_value"]).ToString(CultureInfo.InvariantCulture);
            core["total_cost_aed"] = core["invoice_amount_aed"];
            autofill.Add("invoice_value");
        }
        if (box45Parsed["customs_inspection_required"].Length > 0)
        {
            core["customs_inspection_required"] = box45Parsed["customs_inspection_required"];
            core["custom_inspection"] = box45Parsed["customs_inspection_required"];
            autofill.Add("customs_inspection_required");
            autofill.Add("custom_inspection");
        }

        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, @"^[a-z][a-z0-9._-]{4,}$", RegexOptions.IgnoreCase)
                && !IsHsCode(line)
                && !IsCountryCode(line)
                && !line.Contains("IMPORT", StringComparison.OrdinalIgnoreCase)
                && !line.Contains("EXPORT", StringComparison.OrdinalIgnoreCase)
                && !Regex.IsMatch(line, @"^\d+(\.\d+)?$"))
            {
                SetBox("box_38", line);
                break;
            }
        }

        var lineItems = DedupeLineItems(ParseLineItemBlocks(lines, core));

        if (lineItems.Count > 0)
        {
            decimal netSum = 0;
            foreach (var li in lineItems)
            {
                var w = li.GetValueOrDefault("weight_net", "");
                if (w.Length == 0) w = li.GetValueOrDefault("weight_gross", "0");
                netSum += ParseDecimal(w);
            }
            if (netSum > 0)
            {
                SetBox("box_07", netSum.ToString(CultureInfo.InvariantCulture));
                core["net_weight"] = netSum.ToString(CultureInfo.InvariantCulture);
            }
        }

        core["customs_emirate"] = "DUBAI";
        core["currency"] = "AED";

        var hasUsefulData = boxes.Count > 0
            || core.GetValueOrDefault("declaration_number", "").Length > 0
            || lineItems.Count > 0
            || declType.Length > 0;

        if (!textValid && !hasUsefulData)
        {
            var msg = "Could not map any declaration fields from this PDF.";
            if (pdftotextMissing) msg += " Server: pdftotext missing — install poppler-utils on the server for best results.";
            msg += " Fill the form manually or try a text-based declaration copy.";
            throw new ErpWriteException(msg);
        }

        var result = new ErpCsPdfImportResult(
            boxes, box45Parsed, box45Lines, box54Lines, lineItems, core,
            autofill.ToList(), declType, CategoryForType(declType),
            flat.Length > 1200 ? flat[..1200] : flat,
            boxes.Count, textValid, parseWarning,
            !textValid && boxes.Count > 0,
            core.GetValueOrDefault("declaration_number", ""),
            "", "", "", pdftotextAvailable, "");

        if (!textValid && hasUsefulData && parseWarning.Length == 0)
        {
            result = result with { ParseWarning = "Partial import — verify all fields before saving.", Partial = true };
        }
        return result;
    }
}
