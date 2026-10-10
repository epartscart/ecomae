using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1HalyardParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Halyard");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PlanQ1Halyard_MatchPhpGolden()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var name = cases[i].GetProperty("name").GetString()!;
            var expected = results[i].GetProperty("result");
            var actual = Render(name);
            if (!JsonEquivalent(JsonDocument.Parse(Json(actual)).RootElement, expected))
            {
                failures.Add(name + " exp=" + expected.GetRawText() + " got=" + Json(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("cp/content/shop/prices_upload/for_pyprices/del_tmp_folder.php", PhpPlanQ1Halyard.DelTmpFolderPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Halyard.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Halyard.DelTmpFolderPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", Json(PhpPlanQ1Halyard.EpcDelTmpFolderRun("bad.name")), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Halyard.Reset();
        return name switch
        {
            "name" => new object[] { Run("bad.name", true, false), Run("ABC", true, false), Run("ok_name 1", true, false) },
            "missing" => Run("gone_dir", true, false),
            "delete" => Run("ok_dir", true, true),
            "nodb" => Run("ok_dir", false, true),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object[] Run(string posted, bool db, bool makeDir)
    {
        var doc = Path.Combine(Path.GetTempPath(), "ecomae_halyard_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(doc, "cp", "tmp_prices", "keep"));
        PhpPlanQ1Halyard.DocumentRoot = doc;
        PhpPlanQ1Halyard.HasDb = () => db;
        if (makeDir)
        {
            var dir = Path.Combine(doc, "cp", "tmp_prices", posted);
            Directory.CreateDirectory(Path.Combine(dir, "nested"));
            File.WriteAllText(Path.Combine(dir, "index.html"), "keep");
            File.WriteAllText(Path.Combine(dir, "price.csv"), "x");
            File.WriteAllText(Path.Combine(dir, "nested", "x.txt"), "y");
        }

        var json = PhpPlanQ1Halyard.EpcDelTmpFolderRun(posted);
        if (json.TryGetValue("tmp_folder_name", out var path) && path is string full)
        {
            json["tmp_folder_name"] = full.Replace(doc, "{doc}", StringComparison.Ordinal);
        }

        var folderLeft = Directory.Exists(Path.Combine(doc, "cp", "tmp_prices", posted)) ? 1 : 0;
        var indexLeft = File.Exists(Path.Combine(doc, "cp", "tmp_prices", posted, "index.html")) ? 1 : 0;
        try
        {
            if (Directory.Exists(doc))
            {
                Directory.Delete(doc, recursive: true);
            }
        }
        catch
        {
        }

        return [json, folderLeft, indexLeft];
    }

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number && left.GetDouble() == right.GetDouble();
        }

        return left.ValueKind switch
        {
            JsonValueKind.Object => left.EnumerateObject().All(p => right.TryGetProperty(p.Name, out var o) && JsonEquivalent(p.Value, o))
                && left.EnumerateObject().Count() == right.EnumerateObject().Count(),
            JsonValueKind.Array => left.EnumerateArray().ToList().Zip(right.EnumerateArray().ToList(), JsonEquivalent).All(x => x)
                && left.GetArrayLength() == right.GetArrayLength(),
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.Number => left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble(),
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            _ => true
        };
    }
}
