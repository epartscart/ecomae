using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1TackParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Tack");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PlanQ1Tack_MatchPhpGolden()
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
        => Assert.Equal("content/cron/product_exist_limit.php", PhpPlanQ1Tack.ProductExistLimitPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Tack.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Tack.ProductExistLimitPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Tack.ProductExistLimitPath, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Tack.Reset();
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_tack_" + Guid.NewGuid().ToString("N")[..8];
        admin.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Exec(db, """
                CREATE TABLE shop_catalogue_products (
                    id INT PRIMARY KEY, category_id INT, caption VARCHAR(64),
                    min_limit INT, min_limit_enable VARCHAR(8), min_limit_status VARCHAR(8)
                )
                """);
            Exec(db, """
                CREATE TABLE shop_storages_data (
                    id INT AUTO_INCREMENT PRIMARY KEY, storage_id INT, product_id INT, category_id INT,
                    price DECIMAL(10,2), exist INT, reserved INT, issued INT
                )
                """);
            switch (name)
            {
                case "reset":
                    Seed(db, false);
                    break;
                case "limit":
                    Seed(db, true);
                    break;
                case "skip":
                    Exec(db, "INSERT INTO shop_catalogue_products VALUES (9, 20, 'X', 3, '1', '1')");
                    Exec(db, "INSERT INTO shop_storages_data (storage_id, product_id, category_id, price, exist, reserved, issued) VALUES (1, 9, 21, 1.00, 0, 0, 0)");
                    break;
                case "empty":
                    Exec(db, "INSERT INTO shop_catalogue_products VALUES (5, 1, 'Y', 1, '0', '1')");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(name));
            }

            var wrote = 0;
            PhpPlanQ1Tack.WriteLog = _ => wrote = 1;
            var outRow = PhpPlanQ1Tack.EpcProductExistLimitRun(db);
            return new object?[] { outRow["rows"], wrote };
        }
        finally
        {
            admin.Execute($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static void Seed(MySqlConnection db, bool enableLimits)
    {
        var one = enableLimits ? "1" : "0";
        Exec(db, $"INSERT INTO shop_catalogue_products VALUES (1, 10, 'Pad', 5, '{one}', '1'), (2, 10, 'Disc', 2, '1', '1'), (3, 11, 'Oil', 8, '1', '1'), (4, 12, 'Air', 1, '1', '0')");
        Exec(db, "INSERT INTO shop_storages_data (storage_id, product_id, category_id, price, exist, reserved, issued) VALUES (7, 1, 10, 1.00, 2, 0, 0), (8, 1, 10, 1.00, 1, 0, 0), (7, 2, 10, 1.00, 9, 0, 0), (7, 3, 99, 1.00, 0, 0, 0), (7, 4, 12, 1.00, 0, 0, 0)");
    }

    private static string Password()
        => Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN")
            ?? throw new InvalidOperationException("missing ECOMAE_LOCAL_MARIADB_E2E_DSN");

    private static MySqlConnection OpenAdmin()
    {
        var db = new MySqlConnection($"Server=127.0.0.1;Port=3306;User ID=ecomae;Password={Password()};AllowUserVariables=true;");
        db.Open();
        return db;
    }

    private static MySqlConnection OpenDb(string schema)
    {
        var db = new MySqlConnection($"Server=127.0.0.1;Port=3306;Database={schema};User ID=ecomae;Password={Password()};AllowUserVariables=true;");
        db.Open();
        return db;
    }

    private static void Exec(MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
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
            _ => true
        };
    }
}

file static class TackMysql
{
    public static void Execute(this MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
