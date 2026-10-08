using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/FreeTools/harness.php</c> ran PHP 8.3 <c>epc_free_tools_compute()</c> (UTC, with the ERP engines loaded)
/// over 1,270 vectors; ASP.NET must answer the same JSON bytes for the same inputs and clock.
/// </summary>
public sealed class FreeToolsComputeParityTests
{
    private static readonly JsonElement Golden = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "FreeTools", "golden.json"))).RootElement;

    private static readonly Regex Uuid = new("\"uuid\":\"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\"", RegexOptions.CultureInvariant);

    private static readonly Regex RandomNumber = new("\"number\":\"(INV-[0-9]{8}-)[0-9A-F]{4}\"", RegexOptions.CultureInvariant);

    private static string Normalize(string json)
        => RandomNumber.Replace(Uuid.Replace(json, "\"uuid\":\"*\""), "\"number\":\"$1*\"");

    [Fact]
    public void Compute_MatchesPhpGolden_ForEveryVector()
    {
        if (TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow) != TimeSpan.Zero)
        {
            return;
        }

        var now = Golden.GetProperty("now").GetInt64();
        var mismatches = new List<string>();
        var count = 0;
        foreach (var c in Golden.GetProperty("cases").EnumerateArray())
        {
            count++;
            var inputs = PhpArray.JsonDecode(c.GetProperty("inputs").GetString()!) as PhpArray ?? new PhpArray();
            var actual = FreeToolsPhp.JsonEncode(FreeToolsCompute.Compute(c.GetProperty("tool").GetString()!, c.GetProperty("country").GetString()!, inputs, now));
            var expected = c.GetProperty("out").GetString()!;
            if (Normalize(actual) != Normalize(expected))
            {
                mismatches.Add(c.GetProperty("tool").GetString() + "/" + c.GetProperty("country").GetString() + " " + c.GetProperty("inputs").GetString()
                               + "\n  php: " + expected + "\n  net: " + actual);
            }
        }

        Assert.Equal(1270, count);
        Assert.True(mismatches.Count == 0, mismatches.Count + " mismatch(es):\n" + string.Join("\n", mismatches.Take(8)));
    }

    [Fact]
    public void IsEmail_MatchesPhpFilterValidateEmail()
    {
        foreach (var e in Golden.GetProperty("emails").EnumerateArray())
        {
            var email = e.GetProperty("email").GetString()!;
            Assert.True(e.GetProperty("valid").GetBoolean() == FreeToolsPhp.IsEmail(email), email);
        }
    }

    [Fact]
    public void StrToTime_MatchesPhpForTheSupportedForms()
    {
        if (TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow) != TimeSpan.Zero)
        {
            return;
        }

        var now = Golden.GetProperty("now").GetInt64();
        foreach (var d in Golden.GetProperty("dates").EnumerateArray())
        {
            var input = d.GetProperty("input").GetString()!;
            var unix = d.GetProperty("unix");
            long? expected = unix.ValueKind == JsonValueKind.Null ? null : unix.GetInt64();
            Assert.True(expected == FreeToolsPhp.StrToTime(input, now), input + ": php " + expected + " net " + FreeToolsPhp.StrToTime(input, now));
        }
    }

    [Fact]
    public void Einvoice_RandomFieldsHavePhpShape()
    {
        var input = new PhpArray { { "lines", PhpArray.List(new PhpArray { { "desc", "x" }, { "qty", 1L }, { "price", 1L } }) } };
        var result = FreeToolsCompute.Compute("einvoice", "AE", input, 1_791_467_350);
        Assert.Matches("^INV-20261008-[0-9A-F]{4}$", (string)result["number"]!);
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", (string)result["uuid"]!);
    }

    [Theory]
    [InlineData(1.0, "1")]
    [InlineData(-0.0, "-0")]
    [InlineData(0.1, "0.1")]
    [InlineData(0.0001, "0.0001")]
    [InlineData(0.00001, "1.0e-5")]
    [InlineData(1e17, "1.0e+17")]
    [InlineData(123456789012345680.0, "1.2345678901234568e+17")]
    [InlineData(1234.5, "1234.5")]
    public void JsonFloat_MatchesPhpSerializePrecision(double value, string expected)
        => Assert.Equal(expected, FreeToolsPhp.JsonFloat(value));
}
