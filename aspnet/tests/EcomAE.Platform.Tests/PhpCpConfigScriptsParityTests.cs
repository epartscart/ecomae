using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/CpConfigScripts/harness.php</c> ran the real PHP 8.3 CP config scripts and the multi-vendor sample CSV
/// on throwaway databases. <see cref="PhpCpConfigScripts"/> must emit the same JavaScript or CSV.
/// </summary>
public sealed class PhpCpConfigScriptsParityTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CpConfigScripts");

    private static readonly Dictionary<string, string> Variables = new(StringComparer.Ordinal)
    {
        ["storage_toggle"] = "EPC_STOREFRONT_STORAGE_TOGGLE",
        ["upload_history"] = "EPC_PRICES_UPLOAD_HISTORY",
        ["commerce"] = "EPC_COMMERCE_CP",
        ["order_item_edit"] = "EPC_OI_EDIT"
    };

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases");
        Assert.Equal(
            cases.EnumerateArray().Select(c => c.GetProperty("name").GetString()),
            golden.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task ConfigScripts_MatchPhpGolden_OnThrowawayDatabases_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var spec = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "cases.json"))).RootElement;
        var results = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("results").EnumerateArray().ToList();
        var cases = spec.GetProperty("cases").EnumerateArray().ToList();
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var name = cases[i].GetProperty("name").GetString()!;
            var script = cases[i].GetProperty("script").GetString()!;
            var expected = results[i].GetProperty("output").GetString() ?? string.Empty;
            var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
            await ExecAsync(admin, "CREATE DATABASE `" + database + "`");
            try
            {
                await using var connection = new MySqlConnection("Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";Pooling=false;");
                await connection.OpenAsync();
                foreach (var sql in spec.GetProperty("schema").EnumerateArray()
                             .Concat(spec.GetProperty("base").EnumerateArray())
                             .Concat(cases[i].GetProperty("setup").EnumerateArray()))
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = sql.GetString();
                    await command.ExecuteNonQueryAsync();
                }

                var body = script == "multivendor_sample"
                    ? PhpCpConfigScripts.MultivendorSampleCsv()
                    : await RenderAsync(connection, script, cases[i]);
                if (body != expected)
                {
                    failures.Add(name + " PHP=" + expected + " ASP.NET=" + body);
                }
            }
            finally
            {
                await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
            }
        }

        Assert.True(failures.Count == 0, "Differed on " + failures.Count + " of " + cases.Count + ":\n" + string.Join("\n", failures.Take(20)));
    }

    private static async Task<string> RenderAsync(MySqlConnection connection, string script, JsonElement c)
    {
        var cookie = c.GetProperty("cookie");
        var session = cookie.TryGetProperty("admin_session", out var s) ? s.GetString() : null;
        var user = cookie.TryGetProperty("admin_u_id", out var u) ? u.GetString() : null;
        var csrf = await PhpCpConfigScripts.AdminCsrfAsync(connection, session, user, CancellationToken.None);
        var variable = Variables[script];
        if (csrf is null)
        {
            return PhpCpConfigScripts.Empty(variable);
        }

        var query = c.GetProperty("query").GetString() ?? string.Empty;
        var itemId = QueryId(query);
        var orderId = script == "order_item_edit"
            ? await PhpCpConfigScripts.OrderIdOfItemAsync(connection, itemId, CancellationToken.None)
            : 0;
        var json = script switch
        {
            "storage_toggle" => PhpCpConfigScripts.StorageToggleConfig(csrf),
            "upload_history" => PhpCpConfigScripts.UploadHistoryConfig(csrf),
            "commerce" => PhpCpConfigScripts.CommerceConfig(csrf),
            _ => PhpCpConfigScripts.OrderItemEditConfig(itemId, orderId)
        };
        return PhpCpConfigScripts.Script(variable, json);
    }

    /// <summary>PHP <c>(int)$_GET["id"]</c>: last scalar wins; a non-empty array is 1.</summary>
    private static long QueryId(string query)
    {
        long? last = null;
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            var key = Uri.UnescapeDataString(eq < 0 ? part : part[..eq]);
            if (key.StartsWith("id[", StringComparison.Ordinal) && key.EndsWith(']'))
            {
                last = 1;
                continue;
            }

            if (!string.Equals(key, "id", StringComparison.Ordinal))
            {
                continue;
            }

            last = PhpCpConfigScripts.PhpInt(eq < 0 ? string.Empty : Uri.UnescapeDataString(part[(eq + 1)..].Replace("+", " ")));
        }

        return last ?? 0;
    }

    private static async Task ExecAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
