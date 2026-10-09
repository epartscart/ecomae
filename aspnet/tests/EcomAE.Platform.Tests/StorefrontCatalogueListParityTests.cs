using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Storefront;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/CatalogueList/harness.php</c> ran the real PHP 8.3 <c>ajax_get_products_page.php</c> (which includes the real
/// <c>ajax_get_products_list.php</c>, <c>query_products_all.php</c>, <c>query_products_show.php</c> and
/// <c>generate_products_objects_by_sql.php</c>) once per case on a throwaway MariaDB database and recorded the ordered keys of
/// <c>$products_objects</c> (the product ids the page renders) plus everything both scripts print.
/// <see cref="StorefrontPhpAjax.CatalogueProductIdsAsync"/>, <see cref="StorefrontPhpAjax.CatalogueListAsync"/> and
/// <see cref="StorefrontPhpAjax.CataloguePageAsync"/> must select the same ids in the same order for the same request JSON,
/// <c>my_city</c> cookie, user, language and data. The product block markup of <c>helper.php::printProductBlock</c> is not part of this
/// golden (the harness stubs it to <c>[id]</c>); the page test reads the ASP.NET block ids instead.
/// </summary>
public sealed class StorefrontCatalogueListParityTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CatalogueList");

    private const string EmptyPage = "    <div style=\"text-center\">4078</div>\n    ";

    /// <summary>
    /// PHP aborts with an uncaught TypeError / PDOException for these requests (500, nothing rendered). ASP.NET must not 500: it selects
    /// no product (<c>PhpFatal</c>, page shows the "nothing found" block) or, for the two inputs it rejects up front, answers
    /// "Product request is empty." exactly like the count endpoint. Every deviation is pinned here with its reason.
    /// </summary>
    private static readonly Dictionary<string, string> PhpErrorDeviations = new(StringComparer.Ordinal)
    {
        // strtolower(array) TypeError.
        ["all_cookie1_guest_sort_direction_array"] = Fatal,
        // `startFrom * productsPerPage` and `needPagesCount * productsPerPage` are PHP 8 arithmetic: "abc", "" and arrays are TypeErrors.
        ["limit_perpage_string_abc_pages1_start0"] = Fatal,
        ["limit_perpage_string_abc_pages2_start1"] = Fatal,
        ["limit_perpage_empty_string_pages1_start0"] = Fatal,
        ["limit_perpage_empty_string_pages2_start1"] = Fatal,
        ["limit_perpage_array_pages1_start0"] = Fatal,
        ["limit_perpage_array_pages2_start1"] = Fatal,
        ["limit_pages_string_abc_perpage3_start0"] = Fatal,
        ["limit_pages_string_abc_perpage3_start1"] = Fatal,
        ["limit_start_string_abc_perpage3_pages1"] = Fatal,
        ["limit_start_array_perpage3_pages1"] = Fatal,
        ["limit_start_string_abc_perpage_zero"] = Fatal,
        // A negative or fractional product is interpolated into `LIMIT a, b` and MariaDB answers with a syntax error.
        ["limit_perpage_negative_pages1_start0"] = Fatal,
        ["limit_perpage_negative_pages2_start1"] = Fatal,
        ["limit_perpage_float_fraction_pages1_start0"] = Fatal,
        ["limit_perpage_float_fraction_pages2_start1"] = Fatal,
        ["limit_pages_negative_perpage3_start0"] = Fatal,
        ["limit_pages_negative_perpage3_start1"] = Fatal,
        ["limit_pages_float_fraction_perpage3_start0"] = Fatal,
        ["limit_pages_float_fraction_perpage3_start1"] = Fatal,
        ["limit_start_negative_perpage3_pages1"] = Fatal,
        ["limit_start_float_fraction_perpage3_pages1"] = Fatal,
        ["limit_negative_perpage_negative_start"] = Fatal,
        ["limit_pages_negative_perpage_negative"] = Fatal,
        // Only one-letter tokens make text_search_algorithm.php emit "WHERE ()" (PDOException). ASP.NET searches without a token filter like the
        // count endpoint: every translated published product, in the usual price order.
        ["search_only_short_tokens"] = "ids:21,4,1,22,2,23,11,12,13,15,16,14,6,7,10,19,3,9,18,20",
        // A storage whose currency has no shop_currencies row concatenates a NULL rate into the CASE expression: syntax error in PHP.
        // ASP.NET keeps rate 1 for that storage (same deviation as the count endpoint), so the USD products are priced unconverted.
        ["currency_rate_missing_for_storage_currency"] = "ids:4,22,2",
        // query_products_all.php builds an empty UNION when the shop has no office at all.
        ["no_offices_at_all"] = Fatal,
        // The caption_translation sub-select returns two rows: MariaDB error 1242 in PHP; ASP.NET runs the same sub-select and the error surfaces.
        ["duplicate_translation_for_lang"] = "throws",
        // `"abc"["category_id"]` is a TypeError for a JSON string scalar.
        ["request_json_scalar_string"] = Fatal,
        // category_id is interpolated raw by PHP ("abc" is an unknown column); ASP.NET rejects non-numeric category ids.
        ["request_category_non_numeric"] = "Product request is empty.",
        // count("abc") TypeError in PHP; ASP.NET rejects a properties_list that is not a JSON array.
        ["request_properties_list_string"] = "Product request is empty."
    };

    private const string Fatal = "fatal";

    private static JsonElement Golden() => JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;

    private static JsonElement Spec() => JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement;

    [Fact]
    public void Golden_IsPhp83RuntimeOutputOfTheListAndPageScripts()
    {
        var golden = Golden();
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
        Assert.Equal("content/shop/catalogue/ajax_get_products_list.php", golden.GetProperty("source").GetString());
        var cases = Spec().GetProperty("cases").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        Assert.Equal(cases, results.Select(r => r.GetProperty("name").GetString()).ToList());
        Assert.Equal(cases.Count, cases.Distinct(StringComparer.Ordinal).Count());
        foreach (var result in results)
        {
            var name = result.GetProperty("name").GetString()!;
            if (result.TryGetProperty("php_error", out var error))
            {
                Assert.Contains(name, PhpErrorDeviations.Keys);
                Assert.Matches("^(TypeError|PDOException)$", error.GetString()!);
                continue;
            }

            Assert.Equal(string.Empty, result.GetProperty("list_output").GetString());
            var ids = result.GetProperty("ids").EnumerateArray().Select(i => i.GetInt32()).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            if (!result.TryGetProperty("unordered", out _))
            {
                var expectedPage = ids.Count == 0 ? EmptyPage : string.Concat(ids.Select(i => "[" + i + "]"));
                Assert.Equal(expectedPage, result.GetProperty("page_output").GetString());
            }
        }

        var phpErrors = results.Where(r => r.TryGetProperty("php_error", out _)).Select(r => r.GetProperty("name").GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (var name in PhpErrorDeviations.Keys)
        {
            Assert.True(phpErrors.Contains(name), name + " is pinned as a PHP error but the golden has PHP output for it.");
        }

        Assert.Equal(PhpErrorDeviations.Keys.OrderBy(k => k, StringComparer.Ordinal), phpErrors.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void Golden_CoversEverySortModeDirectionPaginationAndBlockType()
    {
        var names = Spec().GetProperty("cases").EnumerateArray().Select(c => c.GetProperty("name").GetString()!).ToList();
        foreach (var fragment in new[]
                 {
                     "sort_price_asc", "sort_price_desc", "sort_name_asc", "sort_name_desc", "sort_random", "category40_ties_",
                     "union_duplicates_", "page_size5_start", "pages2_size4_start", "limit_perpage_zero", "limit_perpage_negative",
                     "limit_perpage_string_abc", "search_brake_", "block_1_", "block_2_", "block_3_", "block_4_", "pricefilter_100_600_",
                     "unpriced_ids_", "page_start_out_of_range"
                 })
        {
            Assert.Contains(names, n => n.Contains(fragment, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task CatalogueListAndPage_MatchPhpGolden_OnThrowawayDatabases_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var spec = Spec();
        var results = Golden().GetProperty("results").EnumerateArray().ToList();
        var cases = spec.GetProperty("cases").EnumerateArray().ToList();
        Assert.Equal(cases.Count, results.Count);
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var failures = new List<string>();
        var shared = new SharedDatabase(admin, password, spec);
        try
        {
            for (var i = 0; i < cases.Count; i++)
            {
                var c = cases[i];
                var name = c.GetProperty("name").GetString()!;
                Assert.Equal(name, results[i].GetProperty("name").GetString());
                var setup = c.GetProperty("setup").EnumerateArray().Select(s => s.GetString()!).ToList();
                var requestJson = c.TryGetProperty("request_raw", out var raw) ? raw.GetString() : c.GetProperty("request").GetRawText();
                var cookie = c.GetProperty("cookie").ValueKind == JsonValueKind.Null ? null : c.GetProperty("cookie").GetString();
                var lang = c.TryGetProperty("lang", out var langElement) ? langElement.GetString() : "en";
                var userId = c.GetProperty("user_id").GetInt64();

                await using var own = setup.Count == 0 ? null : await shared.CreateAsync(setup);
                var connection = own?.Connection ?? await shared.BaseAsync();
                try
                {
                    var failure = await CompareAsync(connection, name, results[i], requestJson, cookie, userId, lang);
                    if (failure is not null)
                    {
                        failures.Add(failure);
                    }
                }
                catch (Exception ex) when (name == "duplicate_translation_for_lang" && ex is MySqlException)
                {
                    Assert.Equal("throws", PhpErrorDeviations[name]);
                }
            }
        }
        finally
        {
            await shared.DisposeAsync();
        }

        Assert.True(failures.Count == 0, "List/page differ from the PHP golden for " + failures.Count + " of " + cases.Count + " cases:\n" + string.Join("\n", failures));
    }

    private static async Task<string?> CompareAsync(
        MySqlConnection connection,
        string name,
        JsonElement golden,
        string? requestJson,
        string? cookie,
        long userId,
        string? lang)
    {
        var idsResult = await StorefrontPhpAjax.CatalogueProductIdsAsync(connection, requestJson, CancellationToken.None, cookie, userId, lang);
        var list = Assert.IsType<StorefrontPhpAjax.RawHttp>(await StorefrontPhpAjax.CatalogueListAsync(connection, requestJson, CancellationToken.None, cookie, userId, lang));
        var page = Assert.IsType<StorefrontPhpAjax.RawHttp>(await StorefrontPhpAjax.CataloguePageAsync(connection, requestJson, true, CancellationToken.None, cookie, userId, lang));
        var pageIds = BlockIds(page.Body);

        if (golden.TryGetProperty("php_error", out _))
        {
            if (!PhpErrorDeviations.TryGetValue(name, out var expected))
            {
                return name + ": PHP raised " + golden.GetProperty("php_error").GetString() + " but the case is not pinned; ASP.NET ids=[" + string.Join(",", idsResult.Ids) + "] fatal=" + idsResult.PhpFatal + " message=" + idsResult.Message;
            }

            if (expected == Fatal)
            {
                if (!idsResult.PhpFatal || idsResult.Ids.Count != 0 || idsResult.Message is not null)
                {
                    return name + ": PHP raised " + golden.GetProperty("php_error").GetString() + ", ASP.NET expected PhpFatal with no ids but got fatal=" + idsResult.PhpFatal + " ids=[" + string.Join(",", idsResult.Ids) + "] message=" + idsResult.Message;
                }

                return page.Body == EmptyPage ? null : name + ": PHP raised, ASP.NET page expected the empty block but got " + page.Body;
            }

            if (expected.StartsWith("ids:", StringComparison.Ordinal))
            {
                var expectedIds = expected[4..].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
                return !idsResult.PhpFatal && idsResult.Message is null && expectedIds.SequenceEqual(idsResult.Ids) && expectedIds.SequenceEqual(pageIds)
                    ? null
                    : name + ": PHP raised, ASP.NET expected ids [" + string.Join(",", expectedIds) + "] but got [" + string.Join(",", idsResult.Ids) + "] page=[" + string.Join(",", pageIds) + "]";
            }

            if (expected == "throws")
            {
                return "duplicate translation must surface as a MariaDB error but the selection succeeded";
            }

            return idsResult.Message == expected && idsResult.Ids.Count == 0 && page.Body == expected
                ? null
                : name + ": PHP raised, ASP.NET expected '" + expected + "' but got message='" + idsResult.Message + "' page='" + page.Body + "'";
        }

        var phpIds = golden.GetProperty("ids").EnumerateArray().Select(i => i.GetInt32()).ToList();
        var unordered = golden.TryGetProperty("unordered", out _);
        var actual = idsResult.Ids.ToList();
        if (idsResult.PhpFatal || idsResult.Message is not null)
        {
            return name + ": ASP.NET rejected the request (fatal=" + idsResult.PhpFatal + " message=" + idsResult.Message + "); PHP=[" + string.Join(",", phpIds) + "]";
        }

        if (unordered)
        {
            actual.Sort();
            pageIds.Sort();
        }

        var problems = new List<string>();
        if (!phpIds.SequenceEqual(actual))
        {
            problems.Add("ids PHP=[" + string.Join(",", phpIds) + "] ASP.NET=[" + string.Join(",", actual) + "]");
        }

        if (!phpIds.SequenceEqual(pageIds))
        {
            problems.Add("page blocks PHP=[" + string.Join(",", phpIds) + "] ASP.NET=[" + string.Join(",", pageIds) + "]");
        }

        if (phpIds.Count == 0 && page.Body != golden.GetProperty("page_output").GetString())
        {
            problems.Add("empty page PHP=" + golden.GetProperty("page_output").GetString() + " ASP.NET=" + page.Body);
        }

        if (list.Body != golden.GetProperty("list_output").GetString())
        {
            problems.Add("list body PHP='" + golden.GetProperty("list_output").GetString() + "' ASP.NET='" + list.Body + "'");
        }

        return problems.Count == 0 ? null : name + ": " + string.Join("; ", problems);
    }

    private static List<int> BlockIds(string html)
        => Regex.Matches(html, @"onclick=""addToBookmarks\((\d+), this\);""", RegexOptions.CultureInvariant)
            .Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

    /// <summary>
    /// PHP is intolerant of raw SQL in these request values only through category_id and products_ids_str (the count slice pins those);
    /// the list and page scripts additionally interpolate the sort direction/field (whitelisted by a switch), the paging operands
    /// (arithmetic) and the language. ASP.NET never lets request text reach the statements.
    /// </summary>
    [Fact]
    public async Task CatalogueList_RequestValuesNeverReachSql_OnThrowawayDatabase_ThenDropped()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var spec = Spec();
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        await using var shared = new SharedDatabase(admin, password, spec);
        var connection = await shared.BaseAsync();

        async Task<StorefrontPhpAjax.CatalogueIds> Ids(string json)
            => await StorefrontPhpAjax.CatalogueProductIdsAsync(connection, json, CancellationToken.None, "1", 0, "en");

        var baseline = (await Ids("""{"category_id":40,"product_block_type":1,"products_sort_mode":{"field":"price","asc_desc":"asc"}}""")).Ids;
        Assert.NotEmpty(baseline);

        foreach (var field in new[] { "price`; DROP TABLE shop_catalogue_products; -- ", "name` DESC, (SELECT SLEEP(5)) -- ", "price) OR (1=1", "RAND()" })
        {
            var result = await Ids("{\"category_id\":40,\"product_block_type\":1,\"products_sort_mode\":{\"field\":" + JsonSerializer.Serialize(field) + ",\"asc_desc\":\"asc\"}}");
            Assert.Equal(baseline, result.Ids);
        }

        foreach (var direction in new[] { "asc; DROP TABLE shop_catalogue_products", "desc, SLEEP(5)", "DESC) UNION SELECT 1 -- " })
        {
            var result = await Ids("{\"category_id\":40,\"product_block_type\":1,\"products_sort_mode\":{\"field\":\"price\",\"asc_desc\":" + JsonSerializer.Serialize(direction) + "}}");
            Assert.Equal(baseline, result.Ids);
        }

        foreach (var number in new[] { "1; DROP TABLE shop_catalogue_products", "1 OR 1=1", "0x10", "1) UNION SELECT 1 -- " })
        {
            var json = "{\"category_id\":40,\"product_block_type\":1,\"productsPerPage\":" + JsonSerializer.Serialize(number) + ",\"needPagesCount\":1,\"startFrom\":0}";
            var result = await Ids(json);
            Assert.True(result.PhpFatal || result.Ids.All(baseline.Contains), number + " widened the selection beyond category 40");
        }

        Assert.Equal("Product request is empty.", (await Ids("""{"category_id":"40 OR 1=1","product_block_type":1}""")).Message);
        Assert.Empty((await Ids("""{"category_id":0,"product_block_type":3,"products_ids_str":"1) OR (1=1"}""")).Ids);
        Assert.Empty((await Ids("""{"category_id":0,"product_block_type":1,"search_string":"zz') OR 1=1 -- "}""")).Ids);

        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM `shop_catalogue_products`";
        Assert.Equal(23L, Convert.ToInt64(await check.ExecuteScalarAsync()));

        var injectedLang = await StorefrontPhpAjax.CatalogueProductIdsAsync(
            connection,
            """{"category_id":40,"product_block_type":1,"products_sort_mode":{"field":"name","asc_desc":"asc"}}""",
            CancellationToken.None,
            "1",
            0,
            "en' OR '1'='1");
        Assert.Equal(23L, Convert.ToInt64(await check.ExecuteScalarAsync()));
        Assert.NotEmpty(injectedLang.Ids);
    }

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>One throwaway <c>ecomae_cpw_*</c> database for all cases without setup, plus one per case that needs extra rows.</summary>
    private sealed class SharedDatabase : IAsyncDisposable
    {
        private readonly string admin;
        private readonly string password;
        private readonly JsonElement spec;
        private readonly List<string> databases = [];
        private MySqlConnection? baseConnection;

        public SharedDatabase(string admin, string password, JsonElement spec)
        {
            this.admin = admin;
            this.password = password;
            this.spec = spec;
        }

        public async Task<MySqlConnection> BaseAsync()
            => baseConnection ??= (await CreateAsync([])).Connection;

        public async Task<Owned> CreateAsync(IReadOnlyList<string> setup)
        {
            var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
            await ExecAsync(admin, "CREATE DATABASE `" + database + "`");
            databases.Add(database);
            var connection = new MySqlConnection("Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";Pooling=false;");
            await connection.OpenAsync();
            foreach (var sql in spec.GetProperty("schema").EnumerateArray().Concat(spec.GetProperty("base").EnumerateArray()).Select(s => s.GetString()!).Concat(setup))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }

            return new Owned(connection, database, this);
        }

        public async ValueTask DisposeAsync()
        {
            if (baseConnection is not null)
            {
                await baseConnection.DisposeAsync();
            }

            foreach (var database in databases.ToList())
            {
                await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
            }

            databases.Clear();
        }

        public sealed class Owned : IAsyncDisposable
        {
            private readonly string database;
            private readonly SharedDatabase owner;

            public Owned(MySqlConnection connection, string database, SharedDatabase owner)
            {
                Connection = connection;
                this.database = database;
                this.owner = owner;
            }

            public MySqlConnection Connection { get; }

            public async ValueTask DisposeAsync()
            {
                if (ReferenceEquals(owner.baseConnection, Connection))
                {
                    return;
                }

                await Connection.DisposeAsync();
                await ExecAsync(owner.admin, "DROP DATABASE IF EXISTS `" + database + "`");
                owner.databases.Remove(database);
            }
        }
    }
}
