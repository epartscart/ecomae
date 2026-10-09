using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    private static readonly JsonSerializerOptions CatalogueJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly (string Code, string Name, string Iso2)[] DemandRegistry =
    [
        ("SDN", "Sudan", "SD"),
        ("DZA", "Algeria", "DZ"),
        ("KEN", "Kenya", "KE"),
        ("ARE", "United Arab Emirates", "AE"),
        ("EGY", "Egypt", "EG"),
        ("NGA", "Nigeria", "NG"),
        ("SAU", "Saudi Arabia", "SA")
    ];

    private static readonly string[][] ShowcasePools =
    [
        ["SDN", "DZA", "KEN"],
        ["DZA", "EGY"],
        ["KEN", "NGA"],
        ["SDN", "EGY", "SAU"],
        ["DZA", "KEN"],
        ["NGA", "SAU"],
        ["SDN", "NGA"],
        ["KEN", "EGY"],
        ["DZA", "NGA", "KEN"],
        ["SDN", "DZA", "EGY", "KEN"]
    ];

    private static readonly Dictionary<string, Dictionary<string, object?>> DemandJobs = new(StringComparer.Ordinal);

    public static object DemandAuth()
        => new DemandAuthBody(false, "auth", DemandAuthMessage);

    public static object DemandDatabaseUnavailable()
        => new DemandMessageBody(false, DatabaseUnavailable);

    public static object GarageUcatsUnavailable()
        => new GarageUcatsBody(false);

    public static async Task<bool> DemandAdminAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(adminSession) || string.IsNullOrEmpty(adminUserId))
        {
            return false;
        }

        try
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ?"),
                cancellationToken,
                adminSession,
                adminUserId).ConfigureAwait(false);
            return count == 1;
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return false;
        }
    }

    public static Task<object> CatalogueCountAsync(
        DbConnection connection,
        string? requestJson,
        CancellationToken cancellationToken,
        string? cityCookie = null,
        long userId = 0,
        string? lang = null)
        => CatalogueAsync(connection, requestJson, render: false, pricesVisible: false, cancellationToken, cityCookie: cityCookie, userId: userId, lang: lang);

    public static Task<object> CatalogueListAsync(
        DbConnection connection,
        string? requestJson,
        CancellationToken cancellationToken,
        string? cityCookie = null,
        long userId = 0,
        string? lang = null)
        => CatalogueAsync(connection, requestJson, render: false, pricesVisible: false, cancellationToken, listOnly: true, cityCookie: cityCookie, userId: userId, lang: lang);

    public static Task<object> CataloguePageAsync(
        DbConnection connection,
        string? requestJson,
        bool pricesVisible,
        CancellationToken cancellationToken,
        string? cityCookie = null,
        long userId = 0,
        string? lang = null)
        => CatalogueAsync(connection, requestJson, render: true, pricesVisible, cancellationToken, cityCookie: cityCookie, userId: userId, lang: lang);

    public static async Task<object> PickupTimingAsync(
        DbConnection connection,
        SessionLook session,
        string? officeIdText,
        CancellationToken cancellationToken)
    {
        if (session.MissingTable)
        {
            return Text(SessionsMissing);
        }

        var officeId = ParseInt(officeIdText);
        Dictionary<int, int> storages;
        try
        {
            storages = await PickupOfficeStoragesAsync(connection, officeId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return Text(OfficeStoragesMissing);
        }

        var userId = session.UserId;
        var sessionId = 0;
        if (userId <= 0)
        {
            if (!session.Found)
            {
                return new SessionErrorBody(false, "incorrect_session", "Session error");
            }

            sessionId = session.SessionRecordId;
        }

        List<(int Id, int ProductType, int T2StorageId, int T2Time)> cart;
        try
        {
            cart = await PickupCartAsync(connection, userId, sessionId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return Text(CartMissing);
        }

        var canExecute = true;
        var maxTime = 0L;
        foreach (var line in cart)
        {
            if (line.ProductType == 1)
            {
                List<(int StorageId, int StorageRecordId)> details;
                try
                {
                    details = await PickupDetailsAsync(connection, line.Id, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return Text(CartDetailsMissing);
                }

                foreach (var detail in details)
                {
                    if (!storages.TryGetValue(detail.StorageId, out var additional))
                    {
                        canExecute = false;
                        break;
                    }

                    long time = additional * 3600L;
                    try
                    {
                        var stock = await PickupStockAsync(connection, detail.StorageRecordId, cancellationToken).ConfigureAwait(false);
                        if (stock is null)
                        {
                            return Text(WarehouseStockMissing);
                        }

                        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        var untilArrival = stock.Value.Arrival - now;
                        time += untilArrival > 0 ? untilArrival : stock.Value.TimeToExe * 86400L;
                    }
                    catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
                    {
                        return Text(WarehouseStockMissing);
                    }

                    if (time > maxTime)
                    {
                        maxTime = time;
                    }
                }
            }
            else if (line.ProductType == 2)
            {
                if (!storages.ContainsKey(line.T2StorageId))
                {
                    canExecute = false;
                }

                var time = line.T2Time * 86400L;
                if (time > maxTime)
                {
                    maxTime = time;
                }
            }

            if (!canExecute)
            {
                break;
            }
        }

        var css = "alert-success";
        var textInfo = "4421";
        if (!canExecute)
        {
            css = "alert-danger";
            textInfo = "4422";
        }
        else if (maxTime > 0)
        {
            var when = DateTimeOffset.UtcNow.AddSeconds(maxTime).ToLocalTime();
            var days = new[] { "4423", "4424", "4425", "4426", "4427", "4428", "4429" };
            var months = new[] { "", "4430", "4431", "4432", "4433", "4434", "4435", "4436", "4437", "4438", "4439", "4440", "4441" };
            textInfo = "4442 " + days[(int)when.DayOfWeek] + " " + when.Day.ToString(CultureInfo.InvariantCulture) + " " + months[when.Month] + ". 4443";
            css = "alert-warning";
        }

        OfficeCard? office;
        try
        {
            office = await PickupOfficeAsync(connection, officeId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return Text(OfficesMissing);
        }

        if (office is null)
        {
            return Text(OfficesMissing);
        }

        var timetable = (office.Timetable ?? string.Empty).Replace("\r\n", "<br>", StringComparison.Ordinal).Replace("\n", "<br>", StringComparison.Ordinal);
        var button = canExecute
            ? "<a href=\"javascript:void(0);\" onclick=\"nextStep();\" class=\"btn btn-ar btn-primary\">4447</a>"
            : string.Empty;
        var html = "<div class=\"execute_info\">"
            + "<p style=\"margin-bottom: 0;\" class=\"alert alert-border " + css + "\">" + textInfo + "</p>"
            + "<div style=\"padding-left: 0;\" class=\"timetable_show\" onclick=\"show_hide_timetable_map();\"><a style=\"display:inline-block; text-decoration:none;\"><table><tr><td style=\"padding-right:5px;\"><i class=\"fa fa-arrow-down\" aria-hidden=\"true\"></i></td><td>4444</td></tr></table></a></div>"
            + "<div class=\"timetable_map\" id=\"timetable_map\" style=\"display:none\" state=\"hidden\"><table class=\"table\"><tr><th>4418</th></tr><tr><td><span>3376: " + office.City + ", " + office.Address + "</span></td></tr><tr><td>4446: " + timetable + "</td></tr><tr><td>1312: " + office.Phone + "</td></tr></table></div>"
            + "<div class=\"buttons\">" + button + "</div></div>";
        return new RawHttp(html, "text/html; charset=utf-8");
    }

    public static async Task<object> DemandMetaAsync(DbConnection connection, int userId, bool admin, CancellationToken cancellationToken)
    {
        await EnsureDemandSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var access = await AccessAsync(connection, userId, admin, cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, object?>
        {
            ["status"] = true,
            ["access"] = access,
            ["countries"] = await DemandOverviewAsync(connection, cancellationToken).ConfigureAwait(false),
            ["registry"] = DemandRegistry.Select(row => new Dictionary<string, object?>
            {
                ["code"] = row.Code,
                ["name"] = row.Name,
                ["iso2"] = row.Iso2
            }).ToList()
        };
    }

    public static async Task<object> DemandShowcaseAsync(
        DbConnection connection,
        int userId,
        bool admin,
        string? limitText,
        string? reseedText,
        CancellationToken cancellationToken)
    {
        _ = userId;
        _ = admin;
        var limit = Clamp(limitText, 10, 1, 20, emptyBecomesDefault: true);
        var reseed = reseedText is null || !string.Equals(reseedText, "0", StringComparison.Ordinal);
        try
        {
            await EnsureDemandSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var stockMissing = !await TableExistsAsync(connection, "shop_docpart_prices_data", cancellationToken).ConfigureAwait(false);
            if (stockMissing)
            {
                return new Dictionary<string, object?>
                {
                    ["status"] = true,
                    ["parts"] = new List<object>(),
                    ["seeded"] = 0,
                    ["source"] = "price_list_absent",
                    ["limit"] = limit,
                    ["message"] = PriceListsMissing,
                    ["generated_at"] = Now()
                };
            }

            var rows = await ShowcaseStockAsync(connection, limit, cancellationToken).ConfigureAwait(false);
            var source = rows.Count > 0 ? "uae_engine_stock" : "uae_top_stock";
            if (rows.Count == 0)
            {
                rows.Add(new DemandPart("TOYOTA", "1310154101", "1310154101", "Piston (demo)", 0, string.Empty, string.Empty));
            }

            var seeded = 0;
            if (reseed)
            {
                seeded = await SeedShowcaseAsync(connection, rows, cancellationToken).ConfigureAwait(false);
            }

            var parts = new List<Dictionary<string, object?>>();
            foreach (var part in rows.Take(limit))
            {
                parts.Add(await ShowcaseSummaryAsync(connection, part, cancellationToken).ConfigureAwait(false));
            }

            return new Dictionary<string, object?>
            {
                ["status"] = true,
                ["parts"] = parts,
                ["seeded"] = seeded,
                ["source"] = source,
                ["limit"] = limit,
                ["generated_at"] = Now()
            };
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new DemandMessageBody(false, PriceListsMissing);
        }
    }

    public static async Task<object> DemandByCountryAsync(
        DbConnection connection,
        int userId,
        bool admin,
        string? country,
        string? limitText,
        string? seedText,
        CancellationToken cancellationToken)
    {
        if (string.Equals(seedText, "1", StringComparison.Ordinal))
        {
            await DemandShowcaseAsync(connection, userId, admin, "10", null, cancellationToken).ConfigureAwait(false);
        }

        var gate = await AssertCountryAsync(connection, userId, admin, country, cancellationToken).ConfigureAwait(false);
        if (gate.Error is not null)
        {
            return gate.Error;
        }

        var limit = Clamp(limitText, 50, 1, 100, emptyBecomesDefault: true);
        var parts = await CountryPartsAsync(connection, gate.Code, limit, cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, object?>
        {
            ["status"] = true,
            ["country"] = RegistryRow(gate.Code),
            ["country_code"] = gate.Code,
            ["parts"] = parts.Select(PartJson).ToList(),
            ["total"] = parts.Count,
            ["generated_at"] = Now(),
            ["countries_overview"] = await DemandOverviewAsync(connection, cancellationToken).ConfigureAwait(false)
        };
    }

    public static async Task<object> DemandCardAsync(
        DbConnection connection,
        int userId,
        bool admin,
        string? brandText,
        string? articleText,
        string? country,
        string? seedText,
        CancellationToken cancellationToken)
    {
        await EnsureDemandSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var brand = brandText is null ? "TOYOTA" : brandText.Trim();
        var article = articleText is null ? "1310154101" : articleText.Trim();
        var articleNorm = NormalizeArticle(article);
        if (string.Equals(seedText, "1", StringComparison.Ordinal))
        {
            await SeedDemoAsync(connection, brand, article, cancellationToken).ConfigureAwait(false);
        }

        string selected;
        if (!string.IsNullOrWhiteSpace(country))
        {
            var gate = await AssertCountryAsync(connection, userId, admin, country, cancellationToken).ConfigureAwait(false);
            if (gate.Error is not null)
            {
                return gate.Error;
            }

            selected = gate.Code;
        }
        else
        {
            var access = await AccessAsync(connection, userId, admin, cancellationToken).ConfigureAwait(false);
            selected = access["default_country"] as string ?? string.Empty;
        }

        var countries = await CountriesForPartAsync(connection, brand, articleNorm, cancellationToken).ConfigureAwait(false);
        if (countries.Count == 0
            && string.Equals(articleNorm, "1310154101", StringComparison.Ordinal)
            && string.Equals(brand, "TOYOTA", StringComparison.OrdinalIgnoreCase))
        {
            await SeedDemoAsync(connection, brand, article, cancellationToken).ConfigureAwait(false);
            countries = await CountriesForPartAsync(connection, brand, articleNorm, cancellationToken).ConfigureAwait(false);
        }

        var lines = await AnchorStockAsync(connection, brand, articleNorm, cancellationToken).ConfigureAwait(false);
        var qty = lines.Count == 0 ? 0d : lines.Max(line => line.Qty);
        var best = lines.OrderByDescending(line => line.Qty).FirstOrDefault();
        var codes = countries.Select(row => row["code"] as string ?? string.Empty).Where(code => code.Length > 0).ToList();
        return new Dictionary<string, object?>
        {
            ["status"] = true,
            ["brand"] = brand,
            ["article"] = article,
            ["article_norm"] = articleNorm,
            ["demand_countries"] = countries,
            ["stock_region"] = new Dictionary<string, object?>
            {
                ["code"] = "ARE",
                ["name"] = "United Arab Emirates",
                ["note"] = "Single UAE warehouse pool (R-UAE / S-UAE). Demand countries are planning tags only."
            },
            ["anchor"] = new Dictionary<string, object?>
            {
                ["in_stock"] = qty > 0,
                ["qty"] = qty,
                ["price"] = best?.Price,
                ["currency"] = best?.Currency ?? string.Empty,
                ["name"] = best?.Name ?? string.Empty,
                ["lines"] = lines.Select(line => line.ToDictionary()).ToList()
            },
            ["cross_summary"] = new Dictionary<string, object?>
            {
                ["total_catalog"] = 0,
                ["references_loaded"] = 0,
                ["stock_lines"] = 0,
                ["in_stock_cross_count"] = 0,
                ["source"] = string.Empty,
                ["cross_available"] = false
            },
            ["sellable_crosses"] = new List<object>(),
            ["cross_gaps"] = new List<object>(),
            ["cross_gaps_count"] = 0,
            ["cross_gaps_shown"] = 0,
            ["supply_status"] = qty > 0 ? "oe_in_stock" : "no_stock_signal",
            ["selected_country"] = NormalizeCountry(selected),
            ["in_selected_country_demand"] = selected.Length == 0 || codes.Contains(NormalizeCountry(selected), StringComparer.Ordinal),
            ["demand_statistics"] = await DemandStatisticsAsync(connection, brand, articleNorm, cancellationToken).ConfigureAwait(false),
            ["fitment"] = EmptyFitment(),
            ["part_url"] = PartUrl(brand, articleNorm),
            ["part_url_absolute"] = PartUrl(brand, articleNorm),
            ["generated_at"] = Now()
        };
    }

    public static async Task<object> DemandTagsAsync(
        DbConnection connection,
        int userId,
        bool admin,
        string? country,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureDemandSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var access = await AccessAsync(connection, userId, admin, cancellationToken).ConfigureAwait(false);
            var selected = (country ?? string.Empty).Trim();
            if (selected.Length > 0)
            {
                var gate = await AssertCountryAsync(connection, userId, admin, selected, cancellationToken).ConfigureAwait(false);
                if (gate.Error is not null)
                {
                    return gate.Error;
                }

                selected = gate.Code;
            }
            else if (access["country_locked"] is true)
            {
                selected = access["default_country"] as string ?? string.Empty;
            }

            var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            try
            {
                await using var command = connection.CreateCommand();
                if (selected.Length > 0)
                {
                    command.CommandText = ErpDb.Positional("SELECT `manufacturer`, `article_norm`, `country_code` FROM `epc_article_demand` WHERE `country_code` = ?");
                    ErpDb.AddParameters(command, selected);
                }
                else if (admin)
                {
                    command.CommandText = "SELECT `manufacturer`, `article_norm`, `country_code` FROM `epc_article_demand`";
                }
                else if (access["allowed_codes"] is List<string> allowed && allowed.Count > 0)
                {
                    command.CommandText = "SELECT `manufacturer`, `article_norm`, `country_code` FROM `epc_article_demand` WHERE `country_code` IN ("
                        + string.Join(",", allowed.Select((_, i) => "?" + (i + 1).ToString(CultureInfo.InvariantCulture))) + ")";
                    ErpDb.AddParameters(command, allowed.Cast<object>().ToArray());
                }
                else
                {
                    command.CommandText = "SELECT `manufacturer`, `article_norm`, `country_code` FROM `epc_article_demand` WHERE 1 = 0";
                }

                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var brand = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim().ToUpperInvariant();
                    var norm = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
                    var code = NormalizeCountry(reader.IsDBNull(2) ? string.Empty : reader.GetString(2));
                    if (brand.Length == 0 || norm.Length == 0 || code.Length == 0)
                    {
                        continue;
                    }

                    var key = brand + "|" + norm;
                    if (!index.TryGetValue(key, out var codes))
                    {
                        codes = [];
                        index[key] = codes;
                    }

                    if (!codes.Contains(code, StringComparer.Ordinal))
                    {
                        codes.Add(code);
                    }
                }
            }
            catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
            {
                index.Clear();
            }

            return new Dictionary<string, object?>
            {
                ["status"] = true,
                ["country"] = selected,
                ["index"] = index
            };
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new Dictionary<string, object?>
            {
                ["status"] = true,
                ["country"] = string.Empty,
                ["index"] = new Dictionary<string, List<string>>()
            };
        }
    }

    public static async Task<object> DemandVehiclesAsync(
        DbConnection connection,
        int userId,
        bool admin,
        string? actionText,
        string? country,
        string? jobId,
        string? limitText,
        string? batchText,
        string? seedText,
        string? requireStockText,
        CancellationToken cancellationToken)
    {
        var action = string.IsNullOrWhiteSpace(actionText) ? "start" : actionText.Trim().ToLowerInvariant();
        if (string.Equals(seedText, "1", StringComparison.Ordinal))
        {
            await DemandShowcaseAsync(connection, userId, admin, "10", null, cancellationToken).ConfigureAwait(false);
        }

        if (action == "start")
        {
            var gate = await AssertCountryAsync(connection, userId, admin, country, cancellationToken).ConfigureAwait(false);
            if (gate.Error is not null)
            {
                return gate.Error;
            }

            if (gate.Code.Length == 0)
            {
                return new DemandMessageBody(false, "Unknown country");
            }

            var limit = Clamp(limitText, 40, 1, 80, emptyBecomesDefault: true);
            var requireStock = requireStockText is null || !string.Equals(requireStockText, "0", StringComparison.Ordinal);
            var parts = await CountryPartsAsync(connection, gate.Code, limit, cancellationToken).ConfigureAwait(false);
            if (requireStock)
            {
                parts = parts.Where(part => part.Qty > 0).ToList();
            }

            var id = "di_" + gate.Code + "_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
            var job = new Dictionary<string, object?>
            {
                ["job_id"] = id,
                ["country_code"] = gate.Code,
                ["parts"] = parts,
                ["parts_total"] = parts.Count,
                ["cursor"] = 0,
                ["done"] = parts.Count == 0,
                ["current_part"] = string.Empty
            };
            lock (DemandJobs)
            {
                DemandJobs[id] = job;
            }

            var name = DemandRegistry.First(row => row.Code == gate.Code).Name;
            return new Dictionary<string, object?>
            {
                ["status"] = true,
                ["job_id"] = id,
                ["country_code"] = gate.Code,
                ["parts_total"] = parts.Count,
                ["parts_scanned"] = 0,
                ["progress"] = parts.Count > 0 ? 0 : 100,
                ["done"] = parts.Count == 0,
                ["vehicles"] = new List<object>(),
                ["part_lines"] = new List<object>(),
                ["products"] = new List<object>(),
                ["summary"] = new Dictionary<string, object?>
                {
                    ["country_code"] = gate.Code,
                    ["country_name"] = name,
                    ["parts_count"] = parts.Count,
                    ["vehicles_count"] = 0,
                    ["product_groups_count"] = 0,
                    ["makes_count"] = 0,
                    ["total_stock_qty"] = 0
                },
                ["message"] = parts.Count > 0
                    ? "Found " + parts.Count.ToString(CultureInfo.InvariantCulture) + " price-list parts for this country. Loading products & fitment…"
                    : "No in-stock price-list parts with demand tag for this country."
            };
        }

        if (action == "step")
        {
            Dictionary<string, object?>? job;
            lock (DemandJobs)
            {
                DemandJobs.TryGetValue(jobId ?? string.Empty, out job);
            }

            if (job is null || job["parts"] is not List<DemandPartView> parts)
            {
                return new DemandMessageBody(false, "Job not found or expired");
            }

            var batch = Clamp(batchText, 2, 1, 5, emptyBecomesDefault: true);
            var cursor = job["cursor"] is int current ? current : 0;
            var end = Math.Min(cursor + batch, parts.Count);
            var lines = new List<Dictionary<string, object?>>();
            var groups = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
            var label = string.Empty;
            for (var i = cursor; i < end; i++)
            {
                var part = parts[i];
                label = (part.Brand + " " + part.Article).Trim();
                var group = InferGroup(part.Name);
                lines.Add(new Dictionary<string, object?>
                {
                    ["brand"] = part.Brand,
                    ["article"] = part.Article,
                    ["article_norm"] = part.ArticleNorm,
                    ["name"] = part.Name,
                    ["product_group"] = group,
                    ["qty"] = part.Qty
                });
                if (!groups.TryGetValue(group, out var bucket))
                {
                    bucket = new Dictionary<string, object?>
                    {
                        ["label"] = group,
                        ["parts_count"] = 0,
                        ["total_qty"] = 0d
                    };
                    groups[group] = bucket;
                }

                bucket["parts_count"] = (int)bucket["parts_count"]! + 1;
                bucket["total_qty"] = (double)bucket["total_qty"]! + part.Qty;
            }

            var done = end >= parts.Count;
            job["cursor"] = end;
            job["done"] = done;
            job["current_part"] = label;
            var total = parts.Count;
            var progress = total > 0 ? (int)Math.Round(end * 100d / total) : 100;
            var code = job["country_code"] as string ?? string.Empty;
            var countryName = DemandRegistry.FirstOrDefault(row => row.Code == code).Name;
            if (string.IsNullOrEmpty(countryName))
            {
                countryName = code;
            }

            return new Dictionary<string, object?>
            {
                ["status"] = true,
                ["job_id"] = jobId,
                ["parts_total"] = total,
                ["parts_scanned"] = end,
                ["progress"] = progress,
                ["done"] = done,
                ["current_part"] = label,
                ["vehicles"] = new List<object>(),
                ["vehicle_count"] = 0,
                ["part_lines"] = lines,
                ["products"] = groups.Values.ToList(),
                ["summary"] = new Dictionary<string, object?>
                {
                    ["country_code"] = code,
                    ["country_name"] = countryName,
                    ["parts_count"] = lines.Count,
                    ["vehicles_count"] = 0,
                    ["product_groups_count"] = groups.Count,
                    ["makes_count"] = 0,
                    ["total_stock_qty"] = lines.Sum(line => (double)line["qty"]!)
                },
                ["fitment_source"] = string.Empty,
                ["message"] = done
                    ? "Scan complete — products and vehicles ready."
                    : "Checking part " + end.ToString(CultureInfo.InvariantCulture) + " / " + total.ToString(CultureInfo.InvariantCulture) + "…"
            };
        }

        return new DemandMessageBody(false, "Unknown action");
    }

    private static async Task<object> CatalogueAsync(
        DbConnection connection,
        string? requestJson,
        bool render,
        bool pricesVisible,
        CancellationToken cancellationToken,
        bool listOnly = false,
        string? cityCookie = null,
        long userId = 0,
        string? lang = null)
    {
        CatalogueRequest? request;
        try
        {
            request = ParseCatalogueRequest(requestJson, countOnly: !render && !listOnly);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is null)
        {
            return Text("Product request is empty.");
        }

        var filter = BuildPropertyFilter(request.Properties);
        List<long>? searchIds = null;
        var search = SearchText(request.SearchString);
        if (search.Length > 0)
        {
            try
            {
                lang ??= await WorkLangAsync(connection, cancellationToken).ConfigureAwait(false);
                searchIds = await SearchIdsAsync(connection, search, lang, cancellationToken).ConfigureAwait(false);
            }
            catch (CatalogueFail ex)
            {
                return Text(ex.Message);
            }
        }

        var blockType = request.ProductBlockType;
        var publishedOnly = blockType is 1 or 4;
        var adminPrices = blockType == 2;
        var productIds = RequestProductIds(request.ProductsIdsStr) ?? searchIds;
        var searchApplied = searchIds is not null && !(searchIds.Count == 0 || (searchIds.Count == 1 && searchIds[0] == 0));
        var categoryId = searchApplied ? 0 : request.CategoryId;
        try
        {
            if (!await TableExistsAsync(connection, "shop_catalogue_products", cancellationToken).ConfigureAwait(false))
            {
                return Text(CatalogueProductsMissing);
            }

            if (!await TableExistsAsync(connection, "shop_offices", cancellationToken).ConfigureAwait(false))
            {
                return Text(OfficesMissing);
            }
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return Text(CatalogueProductsMissing);
        }

        foreach (var table in filter.Tables.Distinct(StringComparer.Ordinal))
        {
            try
            {
                if (!await TableExistsAsync(connection, table, cancellationToken).ConfigureAwait(false))
                {
                    return Text(CataloguePropertyFiltersMissing);
                }
            }
            catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
            {
                return Text(CataloguePropertyFiltersMissing);
            }
        }

        if (listOnly)
        {
            return new RawHttp(string.Empty, "text/html; charset=utf-8");
        }

        if (!render)
        {
            try
            {
                var count = await CountProductsAsync(connection, categoryId, publishedOnly, productIds, filter, cityCookie, userId, adminPrices, cancellationToken).ConfigureAwait(false);
                return new RawHttp(count.ToString(CultureInfo.InvariantCulture), "text/plain; charset=utf-8");
            }
            catch (CatalogueFail ex)
            {
                return Text(ex.Message);
            }
            catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
            {
                return Text(filter.ExtraWhere.Length > 0 || filter.Having.Length > 0
                    ? CataloguePropertyFiltersMissing
                    : CatalogueProductsMissing);
            }
        }

        var perPage = request.ProductsPerPage <= 0 ? 100 : request.ProductsPerPage;
        var pages = request.NeedPagesCount <= 0 ? 1 : request.NeedPagesCount;
        var start = Math.Max(0, request.StartFrom);
        var from = start * perPage;
        var take = pages * perPage;
        List<CatalogueRow> rows;
        try
        {
            rows = await PageProductsAsync(connection, categoryId, publishedOnly, productIds, filter, from, take, cityCookie, userId, adminPrices, cancellationToken).ConfigureAwait(false);
        }
        catch (CatalogueFail ex)
        {
            return Text(ex.Message);
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return Text(filter.ExtraWhere.Length > 0 || filter.Having.Length > 0
                ? CataloguePropertyFiltersMissing
                : CatalogueProductsMissing);
        }

        if (rows.Count == 0)
        {
            return new RawHttp("<div style=\"text-center\">4078</div>", "text/html; charset=utf-8");
        }

        var css = request.PageStyle switch
        {
            2 => "product_div_list_photo col-lg-12",
            3 => "product_div_list col-lg-12",
            _ => "product_div_tile col-xs-12 col-sm-4 col-md-4 col-lg-3"
        };
        var html = new StringBuilder();
        foreach (var row in rows)
        {
            html.Append(ProductBlock(row, css, blockType, pricesVisible));
        }

        return new RawHttp(html.ToString(), "text/html; charset=utf-8");
    }

    private static string ProductBlock(CatalogueRow row, string css, int blockType, bool pricesVisible)
    {
        var showImage = css.Contains("product_div_tile", StringComparison.Ordinal) || css.Contains("product_div_list_photo", StringComparison.Ordinal);
        var url = "/" + row.CategoryUrl + "/" + row.Alias;
        var image = showImage
            ? "<div class=\"product_div_image_wrap\"><a title=\"" + row.Caption + "\" href=\"" + url + "\" target=\"_blank\"><img src=\"/content/files/images/no_image.png\" alt=\"" + row.Caption + "\" border=\"0\" /></a></div>"
            : string.Empty;
        var stars = new StringBuilder();
        for (var i = 0; i < 5; i++)
        {
            stars.Append((i + 1) <= row.Mark
                ? "<i class=\"fa fa-star em-primary\"></i>"
                : "<i class=\"fa fa-star-o em-primary\"></i>");
        }

        var price = string.Empty;
        var exist = string.Empty;
        var button = string.Empty;
        if (blockType is 1 or 4 or 5 or 6 or 7)
        {
            exist = "<div class=\"product_div_exist_info\"><span class=\"red\">4099</span></div>";
            if (!pricesVisible)
            {
                price = "<div class=\"product_div_price\">**</div>";
            }

            button = "<div class=\"btn-ar btn-primary\"><table><tr><td><a href=\"" + url + "\" target=\"_blank\">3608</a></td></tr></table></div>";
        }
        else if (blockType == 2)
        {
            button = "<a class=\" btn btn-ar btn-primary\" href=\"/cp/shop/catalogue/products/product?category_id=" + row.CategoryId.ToString(CultureInfo.InvariantCulture) + "&product_id=" + row.Id.ToString(CultureInfo.InvariantCulture) + "\">2270</a>";
        }

        var articleButton = string.Empty;
        if (row.Article.Length > 0)
        {
            var href = row.Manufacturer.Length > 0
                ? "/parts/" + Uri.EscapeDataString(row.Manufacturer) + "/" + Uri.EscapeDataString(row.Article)
                : "/parts/brands/" + Uri.EscapeDataString(row.Article);
            articleButton = "<a target=\"_blank\" title=\"4092\" href=\"" + href + "\">4093 <i class=\"fa fa-search\"></i></a>";
        }

        return "<div class=\"" + css + "\">"
            + image
            + "<div class=\"product_div_name\"><a title=\"" + row.Caption + "\" href=\"" + url + "\" target=\"_blank\"><div class=\"product_div_manufacturer\"><span>" + row.Manufacturer + "</span> <span>" + row.Article + "</span></div><div class=\"product_div_caption\">" + row.Caption + "</div></a></div>"
            + "<div class=\"stickers\"></div>"
            + "<div title=\"4101: " + row.MarksCount.ToString(CultureInfo.InvariantCulture) + "\" class=\"product_div_marks\">" + stars + "<span class=\"product_div_marks_count hidden\">" + row.MarksCount.ToString(CultureInfo.InvariantCulture) + "</span></div>"
            + "<div class=\"product_div_bookmark\"><a href=\"javascript:void(0);\" onclick=\"addToBookmarks(" + row.Id.ToString(CultureInfo.InvariantCulture) + ", this);\" title=\"4105\"><i class=\"fa fa-bookmark-o\"></i><span>4106</span></a></div>"
            + "<div class=\"product_div_compare\"><a href=\"javascript:void(0);\" onclick=\"addToCompare(" + row.Id.ToString(CultureInfo.InvariantCulture) + ", this);\" title=\"4110\"><i class=\"fa fa-copy fa-flip-horizontal\"></i><span>4111</span></a></div>"
            + exist
            + price
            + "<div class=\"article_button\">" + articleButton + "</div>"
            + "<div class=\"main_action_div\">" + button + "</div></div>";
    }

    private static async Task<int> CountProductsAsync(
        DbConnection connection,
        int categoryId,
        bool publishedOnly,
        List<long>? productIds,
        CatalogueFilter filter,
        string? cityCookie,
        long userId,
        bool adminPrices,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var where = ProductWhere(categoryId, publishedOnly, productIds, filter, out var args);
        if (filter.Having.Length > 0)
        {
            var priced = await PriceSelectAsync(connection, where, filter.Having, args, cityCookie, userId, adminPrices, cancellationToken).ConfigureAwait(false);
            command.CommandText = "SELECT COUNT(DISTINCT `id`) FROM (" + priced.Sql + ") AS `all`";
            args = priced.Args;
        }
        else
        {
            command.CommandText = "SELECT COUNT(DISTINCT `shop_catalogue_products`.`id`) FROM `shop_catalogue_products` " + where;
        }

        if (args.Count > 0)
        {
            ErpDb.AddParameters(command, args.ToArray());
        }

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static async Task<List<CatalogueRow>> PageProductsAsync(
        DbConnection connection,
        int categoryId,
        bool publishedOnly,
        List<long>? productIds,
        CatalogueFilter filter,
        int from,
        int take,
        string? cityCookie,
        long userId,
        bool adminPrices,
        CancellationToken cancellationToken)
    {
        var rows = new List<CatalogueRow>();
        await using var command = connection.CreateCommand();
        var where = ProductWhere(categoryId, publishedOnly, productIds, filter, out var args);
        var limit = " ORDER BY `id` ASC LIMIT " + from.ToString(CultureInfo.InvariantCulture) + ", " + take.ToString(CultureInfo.InvariantCulture);
        if (filter.Having.Length > 0)
        {
            var priced = await PriceSelectAsync(connection, where, filter.Having, args, cityCookie, userId, adminPrices, cancellationToken).ConfigureAwait(false);
            command.CommandText = "SELECT p.`id`, p.`caption`, p.`alias`, p.`category_id` FROM `shop_catalogue_products` p INNER JOIN (SELECT DISTINCT `id` FROM ("
                + priced.Sql
                + ") AS `priced`) keep ON keep.`id` = p.`id` ORDER BY p.`id` ASC LIMIT "
                + from.ToString(CultureInfo.InvariantCulture) + ", " + take.ToString(CultureInfo.InvariantCulture);
            args = priced.Args;
        }
        else
        {
            command.CommandText = "SELECT `id`, `caption`, `alias`, `category_id` FROM `shop_catalogue_products` " + where + limit;
        }

        if (args.Count > 0)
        {
            ErpDb.AddParameters(command, args.ToArray());
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
            var captionKey = reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty;
            var alias = reader.IsDBNull(2) ? string.Empty : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty;
            var category = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture);
            rows.Add(new CatalogueRow(id, captionKey, alias, category, string.Empty, string.Empty, string.Empty, 0, 0));
        }

        await reader.DisposeAsync().ConfigureAwait(false);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var caption = await TranslateAsync(connection, row.Caption, cancellationToken).ConfigureAwait(false);
            var categoryUrl = await CategoryUrlAsync(connection, row.CategoryId, cancellationToken).ConfigureAwait(false);
            var article = await ArticleAsync(connection, row.Id, row.CategoryId, cancellationToken).ConfigureAwait(false);
            rows[i] = row with { Caption = caption, CategoryUrl = categoryUrl, Article = article.Article, Manufacturer = article.Manufacturer };
        }

        return rows;
    }

    private static CatalogueRequest? ParseCatalogueRequest(string? requestJson, bool countOnly)
    {
        // PHP json_decode() turns bad JSON or a non-object into null and every `$propucts_request[...]` read into null,
        // so the count endpoint answers with the unfiltered total instead of an error.
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(requestJson) ? "null" : requestJson);
            root = document.RootElement.Clone();
        }
        catch (JsonException) when (countOnly)
        {
            root = default;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return countOnly ? JsonSerializer.Deserialize<CatalogueRequest>("{}", CatalogueJson) : null;
        }

        return root.Deserialize<CatalogueRequest>(CatalogueJson);
    }

    /// <summary>
    /// PHP interpolates <c>products_ids_str</c> raw into <c>IN(...)</c>. ASP.NET only accepts a comma separated list of
    /// integers (SQL-injection hardening); anything else matches no product instead of reaching SQL.
    /// </summary>
    private static List<long>? RequestProductIds(JsonElement? value)
    {
        if (StorefrontPhpLoose.IsNull(value))
        {
            return null;
        }

        var element = value!.Value;
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var text = element.GetString() ?? string.Empty;
                return text.Length == 0 ? null : ParseIdList(text);
            case JsonValueKind.Number:
                return element.TryGetInt64(out var single) ? [single] : [];
            case JsonValueKind.True:
                return [1];
            default:
                return [];
        }
    }

    private static List<long> ParseIdList(string text)
    {
        var ids = new List<long>();
        foreach (var token in text.Split(','))
        {
            var trimmed = token.Trim(' ', '\t', '\n', '\r', '\v', '\f');
            if (trimmed.Length == 0
                || !Regex.IsMatch(trimmed, "^[+-]?[0-9]+$", RegexOptions.CultureInvariant)
                || !long.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var id))
            {
                return [];
            }

            ids.Add(id);
        }

        return ids;
    }

    private static string ProductWhere(int categoryId, bool publishedOnly, List<long>? productIds, CatalogueFilter filter, out List<object> args)
    {
        var parts = new List<string>();
        args = [];
        if (categoryId != 0)
        {
            parts.Add("`shop_catalogue_products`.`category_id` = ?");
            args.Add(categoryId);
        }

        if (publishedOnly)
        {
            parts.Add("`shop_catalogue_products`.`published_flag` = 1");
        }

        if (productIds is not null)
        {
            parts.Add(productIds.Count == 0
                ? "`shop_catalogue_products`.`id` IN (0)"
                : "`shop_catalogue_products`.`id` IN (" + string.Join(",", productIds.Select(id => id.ToString(CultureInfo.InvariantCulture))) + ")");
        }

        if (filter.ExtraWhere.Length > 0)
        {
            parts.Add(filter.ExtraWhere);
            args.AddRange(filter.Args);
        }

        return parts.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", parts);
    }

    private static CatalogueFilter BuildPropertyFilter(List<JsonElement>? properties)
    {
        if (properties is null || properties.Count == 0)
        {
            return CatalogueFilter.None;
        }

        var clauses = new List<string>();
        var args = new List<object>();
        var tables = new List<string>();
        var having = new StringBuilder();
        string? listJoiner = null;
        foreach (var property in properties)
        {
            if (property.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var typeId = StorefrontPhpLoose.Get(property, "property_type_id");
            var propertyId = StorefrontPhpLoose.Get(property, "property_id");
            var minNeed = StorefrontPhpLoose.Get(property, "min_need");
            var minValue = StorefrontPhpLoose.Get(property, "min_value");
            var maxNeed = StorefrontPhpLoose.Get(property, "max_need");
            var maxValue = StorefrontPhpLoose.Get(property, "max_value");

            if (StorefrontPhpLoose.EqualsString(propertyId, "price"))
            {
                if (StorefrontPhpLoose.Compare(minNeed, minValue) > 0 || StorefrontPhpLoose.Compare(maxNeed, maxValue) < 0)
                {
                    if (having.Length > 0)
                    {
                        having.Append(" AND ");
                    }

                    having.Append("(`customer_price` >= ");
                    having.Append(StorefrontPhpLoose.FloatSql(StorefrontPhpLoose.Floatval(minNeed)));
                    having.Append(" AND `customer_price` < ");
                    having.Append(StorefrontPhpLoose.FloatSql(StorefrontPhpLoose.Floatval(maxNeed) + 1d));
                    having.Append(')');
                    tables.Add("shop_storages_data");
                    tables.Add("shop_offices_storages_map");
                    tables.Add("shop_storages");
                    tables.Add("shop_currencies");
                }

                continue;
            }

            var isInt = StorefrontPhpLoose.Equal(typeId, 1d);
            var isFloat = !isInt && StorefrontPhpLoose.Equal(typeId, 2d);
            if ((isInt || isFloat)
                && StorefrontPhpLoose.Equal(minNeed, minValue)
                && StorefrontPhpLoose.Equal(maxNeed, maxValue))
            {
                continue;
            }

            if (isInt || isFloat)
            {
                var table = isInt ? "shop_properties_values_int" : "shop_properties_values_float";
                clauses.Add("( (SELECT `value` FROM " + table + " WHERE product_id = `shop_catalogue_products`.id AND `property_id` = ?) >= ? AND (SELECT `value` FROM " + table + " WHERE product_id = `shop_catalogue_products`.id AND `property_id` = ?) <= ? )");
                args.Add(JsonArg(propertyId));
                args.Add(JsonArg(minNeed));
                args.Add(JsonArg(propertyId));
                args.Add(JsonArg(maxNeed));
                tables.Add(table);
            }
            else if (StorefrontPhpLoose.Equal(typeId, 4d))
            {
                var trueChecked = StorefrontPhpLoose.Truthy(StorefrontPhpLoose.Get(property, "true_checked"));
                var falseChecked = StorefrontPhpLoose.Truthy(StorefrontPhpLoose.Get(property, "false_checked"));
                if ((!trueChecked && !falseChecked) || (trueChecked && falseChecked))
                {
                    continue;
                }

                clauses.Add("(SELECT `value` FROM shop_properties_values_bool WHERE product_id = `shop_catalogue_products`.id AND `property_id` = ?) = ?");
                args.Add(JsonArg(propertyId));
                args.Add(trueChecked ? 1 : 0);
                tables.Add("shop_properties_values_bool");
            }
            else if (StorefrontPhpLoose.Equal(typeId, 5d))
            {
                AddListClause(clauses, args, tables, property, propertyId, ref listJoiner);
            }
            else if (StorefrontPhpLoose.Equal(typeId, 6d))
            {
                var currentValue = StorefrontPhpLoose.Get(property, "current_value");
                if (StorefrontPhpLoose.Equal(StorefrontPhpLoose.Get(property, "current_level"), 1d) && StorefrontPhpLoose.Equal(currentValue, 0d))
                {
                    continue;
                }

                clauses.Add("( SELECT `value` FROM `shop_properties_values_tree_list` WHERE product_id = `shop_catalogue_products`.id AND `property_id` = ? AND value = ? LIMIT 1) ");
                args.Add(JsonArg(propertyId));
                args.Add(JsonArg(currentValue));
                tables.Add("shop_properties_values_tree_list");
            }
        }

        return new CatalogueFilter
        {
            ExtraWhere = string.Join(" AND ", clauses),
            Args = args,
            Having = having.ToString(),
            Tables = tables
        };
    }

    /// <summary>
    /// PHP keeps <c>$OR_AND</c> across properties and only reassigns it for list_type 1 or 2. With no joiner yet, two checked
    /// options make PHP emit invalid SQL (PDOException); ASP.NET ignores the filter in that one case instead of failing.
    /// A missing <c>list_options</c> is a PHP TypeError in <c>count()</c> and is ignored the same way.
    /// </summary>
    private static void AddListClause(
        List<string> clauses,
        List<object> args,
        List<string> tables,
        JsonElement property,
        JsonElement? propertyId,
        ref string? joiner)
    {
        var listType = StorefrontPhpLoose.Get(property, "list_type");
        if (StorefrontPhpLoose.Equal(listType, 1d))
        {
            joiner = "OR";
        }
        else if (StorefrontPhpLoose.Equal(listType, 2d))
        {
            joiner = "AND";
        }

        var options = StorefrontPhpLoose.Get(property, "list_options");
        if (options is null || options.Value.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var parts = new List<string>();
        var optionArgs = new List<object>();
        foreach (var option in options.Value.EnumerateArray())
        {
            var flag = StorefrontPhpLoose.Get(option, "value");
            if (option.ValueKind != JsonValueKind.Object || flag is null || !StorefrontPhpLoose.Truthy(flag))
            {
                continue;
            }

            parts.Add("(`shop_catalogue_products`.id IN (SELECT `product_id` FROM shop_properties_values_list WHERE product_id = `shop_catalogue_products`.id AND `property_id` = ? AND value = ?))");
            optionArgs.Add(JsonArg(propertyId));
            optionArgs.Add(JsonArg(StorefrontPhpLoose.Get(option, "id")));
        }

        if (parts.Count == 0 || (parts.Count > 1 && joiner is null))
        {
            return;
        }

        clauses.Add("(" + string.Join(" " + joiner + " ", parts) + ")");
        args.AddRange(optionArgs);
        tables.Add("shop_properties_values_list");
    }

    private static async Task<(string Sql, List<object> Args)> PriceSelectAsync(
        DbConnection connection,
        string where,
        string having,
        List<object> whereArgs,
        string? cityCookie,
        long userId,
        bool adminPrices,
        CancellationToken cancellationToken)
    {
        var offices = await StorefrontCustomerOffices.LoadAsync(connection, cityCookie, cancellationToken, missingGeoTablesAsEmpty: true).ConfigureAwait(false);
        if (offices.Count == 0)
        {
            throw new CatalogueFail(CataloguePropertyFiltersMissing);
        }

        var arms = new List<string>();
        var args = new List<object>();
        object groupId;
        try
        {
            groupId = (object?)await CatalogueGroupIdAsync(connection, userId, cancellationToken).ConfigureAwait(false) ?? DBNull.Value;
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            groupId = DBNull.Value;
        }

        foreach (var officeId in offices)
        {
            string customerPrice;
            string inList;
            try
            {
                var storages = await PriceStorageIdsAsync(connection, officeId, cancellationToken).ConfigureAwait(false);
                inList = storages.Count == 0
                    ? "0"
                    : string.Join(",", storages.Select(id => id.ToString(CultureInfo.InvariantCulture)));
                customerPrice = adminPrices
                    ? "`price` AS `customer_price`"
                    : await CustomerPriceSqlAsync(connection, officeId, groupId, inList, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
            {
                throw new CatalogueFail(CataloguePropertyFiltersMissing);
            }

            args.AddRange(whereArgs);
            arms.Add(
                "SELECT `shop_catalogue_products`.`id`, "
                + customerPrice
                + " FROM `shop_catalogue_products` LEFT OUTER JOIN `shop_storages_data` ON `shop_catalogue_products`.`id` = `shop_storages_data`.`product_id` AND `shop_storages_data`.`storage_id` IN ("
                + inList
                + ") AND `exist` > 0 AND `price` > 0 "
                + where
                + " HAVING "
                + having);

            // The catalogue administrator view (block type 2) prices only the first office.
            if (adminPrices)
            {
                break;
            }
        }

        return (string.Join(" UNION ", arms), args);
    }

    /// <summary>The sell price of PHP query_products_all.php: currency rate of the storage and the office/storage/group markup band.</summary>
    private static async Task<string> CustomerPriceSqlAsync(
        DbConnection connection,
        int officeId,
        object groupId,
        string storagesInOffice,
        CancellationToken cancellationToken)
    {
        var currencyCases = new StringBuilder();
        await using (var currency = connection.CreateCommand())
        {
            currency.CommandText = "SELECT `id`, (SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = `currency`) AS `rate` FROM `shop_storages` WHERE `id` IN (" + storagesInOffice + ")";
            await using var reader = await currency.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // PHP concatenates the NULL rate as an empty string and fails with a SQL syntax error; the storage keeps rate 1 here.
                if (reader.IsDBNull(1))
                {
                    continue;
                }

                currencyCases.Append("WHEN `shop_storages_data`.`storage_id` = ")
                    .Append(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture))
                    .Append(" THEN ")
                    .Append(Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture))
                    .Append(' ');
            }
        }

        var rate = currencyCases.Length > 0 ? "(CASE " + currencyCases + "ELSE 1 END)" : "1";
        var bands = new StringBuilder();
        await using (var markup = connection.CreateCommand())
        {
            markup.CommandText = ErpDb.Positional("SELECT `storage_id`, `min_point`, `max_point`, `markup` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `group_id` = ? AND `storage_id` IN (" + storagesInOffice + ")");
            ErpDb.AddParameters(markup, officeId, groupId);
            await using var reader = await markup.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var storageId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                var min = StorefrontPhpLoose.FloatSql(Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture));
                var max = StorefrontPhpLoose.FloatSql(Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture));
                var percent = StorefrontPhpLoose.FloatSql(Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture));
                bands.Append(" WHEN `shop_storages_data`.`storage_id` = ").Append(storageId)
                    .Append(" AND `shop_storages_data`.`price` * ").Append(rate).Append(" >= ").Append(min)
                    .Append(" AND `shop_storages_data`.`price` * ").Append(rate).Append(" < ").Append(max)
                    .Append(" THEN `price` * ").Append(rate).Append(" + `price` * ").Append(rate).Append(" * (").Append(percent).Append(" / 100)");
            }
        }

        return bands.Length > 0
            ? "CASE" + bands + " ELSE `price` * " + rate + " + `price` * " + rate + " * (0 / 100) END AS `customer_price`"
            : "`price` * " + rate + " AS `customer_price`";
    }

    /// <summary>First group of PHP <c>DP_User::getUserProfile()</c>; <see langword="null"/> when the profile has no group.</summary>
    private static async Task<long?> CatalogueGroupIdAsync(DbConnection connection, long userId, CancellationToken cancellationToken)
    {
        async Task<long?> FirstAsync(string sql, params object?[] parameters)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(sql);
            ErpDb.AddParameters(command, parameters);
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null or DBNull ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        if (userId == 0)
        {
            return await FirstAsync("SELECT `id` FROM `groups` WHERE `for_guests` = 1").ConfigureAwait(false);
        }

        return await FirstAsync("SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ?", userId).ConfigureAwait(false)
            ?? await FirstAsync("SELECT `id` FROM `groups` WHERE `for_registrated` = 1 ORDER BY `id` ASC LIMIT 1").ConfigureAwait(false);
    }

    private static async Task<List<int>> PriceStorageIdsAsync(DbConnection connection, int officeId, CancellationToken cancellationToken)
    {
        var ids = new List<int>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT DISTINCT `storage_id` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` IN (SELECT `id` FROM `shop_storages` WHERE `interface_type` = 1)");
        ErpDb.AddParameters(command, officeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));
        }

        return ids;
    }

    private static object JsonArg(JsonElement? value)
    {
        if (StorefrontPhpLoose.IsNull(value))
        {
            return DBNull.Value;
        }

        var element = value!.Value;
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt64(out var number) => number,
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => DBNull.Value
        };
    }

    /// <summary>
    /// PHP <c>text_search_algorithm.php</c>. With only one-letter tokens PHP builds <c>WHERE ()</c> and the PDO exception
    /// ends the request; ASP.NET matches every translated product there instead.
    /// </summary>
    private static async Task<List<long>> SearchIdsAsync(DbConnection connection, string search, string lang, CancellationToken cancellationToken)
    {
        var tokens = search.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim(' ', '\t', '\n', '\r', '\0', '\v'))
            .Where(token => token.EnumerateRunes().Count() >= 2)
            .ToList();
        var ids = new List<long>();
        await using (var command = connection.CreateCommand())
        {
            if (tokens.Count == 0)
            {
                command.CommandText = "SELECT `id` FROM `shop_catalogue_products` WHERE `caption` IN (SELECT `str_id` FROM `lang_text_strings_translation`)";
            }
            else
            {
                var likes = string.Join(" AND ", tokens.Select(_ => "`value` LIKE ?"));
                command.CommandText = "SELECT `id` FROM `shop_catalogue_products` WHERE `caption` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE " + likes + ")";
                ErpDb.AddParameters(command, tokens.Select(token => (object)("%" + token + "%")).ToArray());
            }

            try
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
                }
            }
            catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
            {
                throw new CatalogueFail(CatalogueTextSearchMissing);
            }
        }

        await using (var text = connection.CreateCommand())
        {
            if (tokens.Count == 0)
            {
                text.CommandText = "SELECT `product_id` FROM `shop_products_text` WHERE `content` IN (SELECT `str_id` FROM `lang_text_strings_translation`)";
            }
            else
            {
                var likes = string.Join(" AND ", tokens.Select(_ => "`value` LIKE ?"));
                text.CommandText = "SELECT `product_id` FROM `shop_products_text` WHERE `content` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE " + likes + ")";
                ErpDb.AddParameters(text, tokens.Select(token => (object)("%" + token + "%")).ToArray());
            }

            try
            {
                await using var reader = await text.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
                }
            }
            catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
            {
                throw new CatalogueFail(ProductDescriptionsMissing);
            }
        }

        var articleNorm = SearchArticleNorm(search);
        try
        {
            await using var article = connection.CreateCommand();
            article.CommandText = ErpDb.Positional(
                "SELECT `product_id` FROM `shop_properties_values_text` WHERE `property_id` IN (SELECT `id` FROM `shop_categories_properties_map` WHERE `value` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` IN ('Артикул', 'Article')) AND `property_type_id` = 3) AND `value` IN (SELECT `str_id` FROM `lang_text_strings_translation` WHERE `lang_code` = ? AND `value` = ?)");
            ErpDb.AddParameters(article, lang, articleNorm);
            await using var reader = await article.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            // Installs without the article property tables cannot run this lookup (PHP would fail); name, description and alias search still apply.
        }

        if (articleNorm.Length > 0)
        {
            await using var alias = connection.CreateCommand();
            alias.CommandText = ErpDb.Positional(
                "SELECT `id` FROM `shop_catalogue_products` WHERE `published_flag` = 1 AND (`alias` LIKE ? OR `alias` LIKE ? OR `alias` = ?)");
            ErpDb.AddParameters(alias, "%/" + articleNorm, articleNorm + "%", AsciiLower(articleNorm));
            try
            {
                await using var reader = await alias.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
                }
            }
            catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
            {
                throw new CatalogueFail(CatalogueProductsMissing);
            }
        }

        var searchLower = AsciiLower(search.Trim(' ', '\t', '\n', '\r', '\0', '\v'));
        if (searchLower.Length > 0)
        {
            try
            {
                await using var discovery = connection.CreateCommand();
                discovery.CommandText = ErpDb.Positional(
                    "SELECT DISTINCT q.`product_id` FROM `epc_product_discovery_queue` q WHERE q.`status` = 'imported' AND q.`product_id` > 0 AND (q.`brand_article_key` LIKE ? OR q.`meta_json` LIKE ?)");
                ErpDb.AddParameters(discovery, "%" + searchLower + "%", "%\"brand_article_key\":\"%" + searchLower + "%");
                await using var reader = await discovery.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
                }
            }
            catch (DbException)
            {
                // PHP wraps this optional lookup in try/catch (Throwable) as well.
            }
        }

        return ids.Distinct().ToList();
    }

    private static string SearchArticleNorm(string search)
        => Regex.Replace(search, "[^a-zA-Z0-9А-Яа-яёЁ]+", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .ToUpperInvariant();

    private static async Task<string> TranslateAsync(DbConnection connection, string key, CancellationToken cancellationToken)
    {
        if (key.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? LIMIT 1");
            ErpDb.AddParameters(command, key);
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (value is null or DBNull)
            {
                return key;
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(text) ? key : text;
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return key;
        }
    }

    private static async Task<string> CategoryUrlAsync(DbConnection connection, int categoryId, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `url` FROM `shop_catalogue_categories` WHERE `id` = ? LIMIT 1");
            ErpDb.AddParameters(command, categoryId);
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null or DBNull ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return string.Empty;
        }
    }

    private static async Task<(string Article, string Manufacturer)> ArticleAsync(
        DbConnection connection,
        int productId,
        int categoryId,
        CancellationToken cancellationToken)
    {
        _ = categoryId;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                """
                SELECT
                (SELECT `value` FROM `shop_properties_values_text` WHERE `product_id` = ? AND `property_id` IN (SELECT `id` FROM `shop_categories_properties_map` WHERE `property_type_id` = 3) LIMIT 1),
                (SELECT `value` FROM `shop_line_lists_items` WHERE `id` = (SELECT `value` FROM `shop_properties_values_list` WHERE `product_id` = ? LIMIT 1) LIMIT 1)
                """);
            ErpDb.AddParameters(command, productId, productId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return (string.Empty, string.Empty);
            }

            var article = reader.IsDBNull(0) ? string.Empty : NormalizeArticle(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture));
            var manufacturer = reader.IsDBNull(1) ? string.Empty : (Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty).Trim().ToUpperInvariant();
            return (article, manufacturer);
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return (string.Empty, string.Empty);
        }
    }

    private static async Task<Dictionary<int, int>> PickupOfficeStoragesAsync(DbConnection connection, int officeId, CancellationToken cancellationToken)
    {
        var map = new Dictionary<int, int>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT DISTINCT `storage_id`, `additional_time` FROM `shop_offices_storages_map` WHERE `office_id` = ?");
        ErpDb.AddParameters(command, officeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var storage = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
            var extra = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
            map[storage] = extra;
        }

        return map;
    }

    private static async Task<List<(int Id, int ProductType, int T2StorageId, int T2Time)>> PickupCartAsync(
        DbConnection connection,
        int userId,
        int sessionId,
        CancellationToken cancellationToken)
    {
        var rows = new List<(int, int, int, int)>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id`, `product_type`, `t2_storage_id`, `t2_time_to_exe` FROM `shop_carts` WHERE `user_id` = ? AND `session_id` = ? AND `checked_for_order` = 1");
        ErpDb.AddParameters(command, userId, sessionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add((
                Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<List<(int StorageId, int StorageRecordId)>> PickupDetailsAsync(
        DbConnection connection,
        int cartId,
        CancellationToken cancellationToken)
    {
        var rows = new List<(int, int)>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `storage_id`, `storage_record_id` FROM `shop_carts_details` WHERE `cart_record_id` = ?");
        ErpDb.AddParameters(command, cartId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add((
                Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<(long Arrival, int TimeToExe)?> PickupStockAsync(
        DbConnection connection,
        int storageRecordId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `arrival_time`, `time_to_exe` FROM `shop_storages_data` WHERE `id` = ?");
        ErpDb.AddParameters(command, storageRecordId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var arrival = reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
        var time = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
        return (arrival, time);
    }

    private static async Task<OfficeCard?> PickupOfficeAsync(DbConnection connection, int officeId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `city`, `address`, `timetable`, `phone` FROM `shop_offices` WHERE `id` = ?");
        ErpDb.AddParameters(command, officeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new OfficeCard(TextOf(reader, 0), TextOf(reader, 1), TextOf(reader, 2), TextOf(reader, 3));
    }

    private static async Task<Dictionary<string, object?>> AccessAsync(
        DbConnection connection,
        int userId,
        bool admin,
        CancellationToken cancellationToken)
    {
        var userCode = admin ? string.Empty : await UserCountryAsync(connection, userId, cancellationToken).ConfigureAwait(false);
        var allowed = admin
            ? DemandRegistry.Where(row => row.Code != "ARE").Select(row => row.Code).ToList()
            : userCode.Length == 0 ? [] : new List<string> { userCode };
        var names = allowed.Select(code => new Dictionary<string, object?>
        {
            ["code"] = code,
            ["name"] = DemandRegistry.First(row => row.Code == code).Name
        }).ToList();
        var fallback = string.Empty;
        if (allowed.Count == 1)
        {
            fallback = allowed[0];
        }
        else if (admin && allowed.Contains("SDN", StringComparer.Ordinal))
        {
            fallback = "SDN";
        }
        else if (allowed.Count > 0)
        {
            fallback = allowed[0];
        }

        var userName = userCode.Length == 0 ? string.Empty : DemandRegistry.First(row => row.Code == userCode).Name;
        return new Dictionary<string, object?>
        {
            ["is_admin"] = admin,
            ["country_locked"] = !admin && allowed.Count == 1,
            ["user_country"] = userCode,
            ["user_country_name"] = userName,
            ["allowed_codes"] = allowed,
            ["allowed_countries"] = names,
            ["default_country"] = fallback
        };
    }

    private static async Task<(string Code, object? Error)> AssertCountryAsync(
        DbConnection connection,
        int userId,
        bool admin,
        string? country,
        CancellationToken cancellationToken)
    {
        await EnsureDemandSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var access = await AccessAsync(connection, userId, admin, cancellationToken).ConfigureAwait(false);
        var code = NormalizeCountry(country ?? string.Empty);
        var allowed = access["allowed_codes"] as List<string> ?? [];
        if (code.Length > 0 && allowed.Contains(code, StringComparer.Ordinal))
        {
            return (code, null);
        }

        string message;
        if (admin)
        {
            message = UnknownCountryCode;
        }
        else if ((access["user_country"] as string ?? string.Empty).Length > 0)
        {
            var name = access["user_country_name"] as string ?? access["user_country"] as string ?? string.Empty;
            message = DemandCountryOnlyPrefix + name + ".";
        }
        else
        {
            message = NoDemandCountry;
        }

        return (string.Empty, new Dictionary<string, object?>
        {
            ["status"] = false,
            ["code"] = "forbidden",
            ["message"] = message,
            ["access"] = access
        });
    }

    private static async Task<string> UserCountryAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return string.Empty;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `country_code` FROM `epc_user_demand_country` WHERE `user_id` = ? LIMIT 1");
            ErpDb.AddParameters(command, userId);
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null or DBNull ? string.Empty : NormalizeCountry(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return string.Empty;
        }
    }

    private static async Task EnsureDemandSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        foreach (var sql in new[]
        {
            """
            CREATE TABLE IF NOT EXISTS `epc_demand_country` (
              `code` CHAR(3) NOT NULL,
              `name` VARCHAR(128) NOT NULL,
              `sort_order` INT NOT NULL DEFAULT 0,
              PRIMARY KEY (`code`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """,
            """
            CREATE TABLE IF NOT EXISTS `epc_article_demand` (
              `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
              `manufacturer` VARCHAR(128) NOT NULL,
              `article_norm` VARCHAR(64) NOT NULL,
              `country_code` CHAR(3) NOT NULL,
              `source` VARCHAR(64) NOT NULL DEFAULT 'manual',
              `notes` VARCHAR(255) NOT NULL DEFAULT '',
              `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`),
              UNIQUE KEY `uq_part_country` (`manufacturer`, `article_norm`, `country_code`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """,
            """
            CREATE TABLE IF NOT EXISTS `epc_price_list_demand` (
              `price_id` INT UNSIGNED NOT NULL,
              `country_code` CHAR(3) NOT NULL,
              PRIMARY KEY (`price_id`, `country_code`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """,
            """
            CREATE TABLE IF NOT EXISTS `epc_user_demand_country` (
              `user_id` INT UNSIGNED NOT NULL,
              `country_code` CHAR(3) NOT NULL,
              `updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
              PRIMARY KEY (`user_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """
        })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var order = 0;
        foreach (var row in DemandRegistry)
        {
            order += 10;
            await using var insert = connection.CreateCommand();
            insert.CommandText = ErpDb.Positional(
                """
                INSERT INTO `epc_demand_country` (`code`, `name`, `sort_order`) VALUES (?, ?, ?)
                ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `sort_order` = VALUES(`sort_order`)
                """);
            ErpDb.AddParameters(insert, row.Code, row.Name, order);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<List<Dictionary<string, object?>>> DemandOverviewAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `country_code`, COUNT(DISTINCT CONCAT(UPPER(`manufacturer`), '|', `article_norm`)) FROM `epc_article_demand` GROUP BY `country_code`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var code = NormalizeCountry(reader.IsDBNull(0) ? string.Empty : reader.GetString(0));
                if (code.Length > 0)
                {
                    counts[code] = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
                }
            }
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            counts.Clear();
        }

        return DemandRegistry.Where(row => row.Code != "ARE").Select(row => new Dictionary<string, object?>
        {
            ["code"] = row.Code,
            ["name"] = row.Name,
            ["parts_count"] = counts.TryGetValue(row.Code, out var count) ? count : 0
        }).ToList();
    }

    private static async Task<List<DemandPart>> ShowcaseStockAsync(DbConnection connection, int limit, CancellationToken cancellationToken)
    {
        var rows = new List<DemandPart>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `manufacturer`, `article`, `name`, `exist`, `price`, `storage` FROM `shop_docpart_prices_data` WHERE IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0 ORDER BY `exist` DESC LIMIT "
            + Math.Max(limit, 1).ToString(CultureInfo.InvariantCulture);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var brand = TextOf(reader, 0);
            var article = TextOf(reader, 1);
            var name = TextOf(reader, 2).ToLowerInvariant();
            if (!name.Contains("piston", StringComparison.Ordinal) && !name.Contains("filter", StringComparison.Ordinal) && !name.Contains("gasket", StringComparison.Ordinal))
            {
                continue;
            }

            rows.Add(new DemandPart(brand, article, NormalizeArticle(article), TextOf(reader, 2), ReadDouble(reader, 3), TextOf(reader, 4), TextOf(reader, 5)));
        }

        return rows;
    }

    private static async Task<int> SeedShowcaseAsync(DbConnection connection, List<DemandPart> parts, CancellationToken cancellationToken)
    {
        var seeded = 0;
        for (var i = 0; i < parts.Count; i++)
        {
            seeded += await SeedCountriesAsync(connection, parts[i].Brand, parts[i].Article, ShowcasePools[i % ShowcasePools.Length], "showcase_seed", "Engine showcase demand tags", cancellationToken).ConfigureAwait(false);
        }

        return seeded;
    }

    private static Task<int> SeedDemoAsync(DbConnection connection, string brand, string article, CancellationToken cancellationToken)
        => SeedCountriesAsync(connection, brand, article, ["SDN", "DZA", "KEN"], "demo_seed", "Planning demand tag (Sudan, Algeria, Kenya)", cancellationToken);

    private static async Task<int> SeedCountriesAsync(
        DbConnection connection,
        string brand,
        string article,
        IReadOnlyList<string> codes,
        string source,
        string notes,
        CancellationToken cancellationToken)
    {
        await EnsureDemandSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var norm = NormalizeArticle(article);
        brand = brand.Trim();
        if (norm.Length == 0 || brand.Length == 0)
        {
            return 0;
        }

        var seeded = 0;
        var now = Now();
        foreach (var raw in codes)
        {
            var code = NormalizeCountry(raw);
            if (code.Length == 0 || code == "ARE")
            {
                continue;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                """
                INSERT INTO `epc_article_demand` (`manufacturer`, `article_norm`, `country_code`, `source`, `notes`, `created_at`)
                VALUES (?, ?, ?, ?, ?, ?)
                ON DUPLICATE KEY UPDATE `source` = VALUES(`source`), `notes` = VALUES(`notes`)
                """);
            ErpDb.AddParameters(command, brand, norm, code, source, notes, now);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            seeded++;
        }

        return seeded;
    }

    private static async Task<Dictionary<string, object?>> ShowcaseSummaryAsync(DbConnection connection, DemandPart part, CancellationToken cancellationToken)
    {
        var norm = NormalizeArticle(part.Article);
        var lines = await AnchorStockAsync(connection, part.Brand, norm, cancellationToken).ConfigureAwait(false);
        var qty = part.Qty;
        if (lines.Count > 0)
        {
            qty = Math.Max(qty, lines.Max(line => line.Qty));
        }

        var countries = await CountriesForPartAsync(connection, part.Brand, norm, cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, object?>
        {
            ["brand"] = part.Brand,
            ["article"] = part.Article,
            ["article_norm"] = norm,
            ["name"] = part.Name,
            ["qty"] = qty,
            ["price"] = part.Price,
            ["warehouse"] = part.Warehouse,
            ["demand_countries"] = countries,
            ["demand_codes"] = countries.Select(row => row["code"]).ToList(),
            ["anchor_in_stock"] = qty > 0,
            ["part_url"] = PartUrl(part.Brand, norm)
        };
    }

    private static async Task<List<DemandPartView>> CountryPartsAsync(
        DbConnection connection,
        string country,
        int limit,
        CancellationToken cancellationToken)
    {
        var parts = new List<DemandPartView>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `manufacturer`, `article_norm` FROM `epc_article_demand` WHERE `country_code` = ? GROUP BY `manufacturer`, `article_norm` ORDER BY `manufacturer`, `article_norm` LIMIT "
            + limit.ToString(CultureInfo.InvariantCulture));
        ErpDb.AddParameters(command, country);
        var raw = new List<(string Brand, string Norm)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                raw.Add((TextOf(reader, 0), TextOf(reader, 1)));
            }
        }

        foreach (var row in raw)
        {
            var lines = await AnchorStockAsync(connection, row.Brand, row.Norm, cancellationToken).ConfigureAwait(false);
            var best = lines.OrderByDescending(line => line.Qty).FirstOrDefault();
            var qty = best?.Qty ?? 0;
            var codes = (await CountriesForPartAsync(connection, row.Brand, row.Norm, cancellationToken).ConfigureAwait(false))
                .Select(item => item["code"] as string ?? string.Empty)
                .Where(code => code.Length > 0)
                .ToList();
            parts.Add(new DemandPartView(
                row.Brand,
                best?.Article ?? row.Norm,
                row.Norm,
                best?.Name ?? string.Empty,
                qty,
                best?.Price ?? string.Empty,
                best?.Warehouse ?? string.Empty,
                codes,
                PartUrl(row.Brand, best?.Article ?? row.Norm)));
        }

        return parts;
    }

    private static async Task<List<Dictionary<string, object?>>> CountriesForPartAsync(
        DbConnection connection,
        string brand,
        string articleNorm,
        CancellationToken cancellationToken)
    {
        var rows = new List<Dictionary<string, object?>>();
        if (articleNorm.Length == 0 || brand.Trim().Length == 0)
        {
            return rows;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `country_code` FROM `epc_article_demand` WHERE `article_norm` = ? AND UPPER(`manufacturer`) = UPPER(?) ORDER BY `country_code`");
            ErpDb.AddParameters(command, articleNorm, brand.Trim());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var code = NormalizeCountry(TextOf(reader, 0));
                var known = DemandRegistry.FirstOrDefault(row => row.Code == code);
                if (known.Code is null)
                {
                    continue;
                }

                rows.Add(new Dictionary<string, object?>
                {
                    ["code"] = known.Code,
                    ["name"] = known.Name,
                    ["iso2"] = known.Iso2
                });
            }
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return [];
        }

        return rows;
    }

    private static async Task<List<StockLine>> AnchorStockAsync(
        DbConnection connection,
        string brand,
        string articleNorm,
        CancellationToken cancellationToken)
    {
        var lines = new List<StockLine>();
        if (articleNorm.Length == 0 || !await TableExistsAsync(connection, "shop_docpart_prices_data", cancellationToken).ConfigureAwait(false))
        {
            return lines;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                """
                SELECT `manufacturer`, `article`, `article_show`, `name`, `price`, `exist`, `storage`, `price_id`
                FROM `shop_docpart_prices_data`
                WHERE UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`article`, ' ', ''), '-', ''), '_', ''), '/', ''), '.', '')) = ?
                AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0
                AND `price_id` IN (SELECT `id` FROM `shop_docpart_prices` WHERE IFNULL(`storefront_temp_disabled`, 0) = 0)
                """);
            ErpDb.AddParameters(command, articleNorm);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var wanted = brand.Trim().ToUpperInvariant();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var productBrand = TextOf(reader, 0);
                if (wanted.Length > 0 && !string.Equals(productBrand, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var article = TextOf(reader, 2);
                if (article.Length == 0)
                {
                    article = TextOf(reader, 1);
                }

                lines.Add(new StockLine(
                    productBrand,
                    article,
                    NormalizeArticle(TextOf(reader, 1)),
                    TextOf(reader, 3),
                    TextOf(reader, 4),
                    string.Empty,
                    ReadDouble(reader, 5),
                    TextOf(reader, 6),
                    reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture)));
            }
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return [];
        }

        return lines;
    }

    private static async Task<Dictionary<string, object?>> DemandStatisticsAsync(
        DbConnection connection,
        string brand,
        string articleNorm,
        CancellationToken cancellationToken)
    {
        var byBrand = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `manufacturer`, `country_code` FROM `epc_article_demand` WHERE `article_norm` = ? ORDER BY `manufacturer`, `country_code`");
            ErpDb.AddParameters(command, articleNorm);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = TextOf(reader, 0);
                var code = NormalizeCountry(TextOf(reader, 1));
                if (name.Length == 0 || code.Length == 0 || DemandRegistry.All(row => row.Code != code))
                {
                    continue;
                }

                if (!byBrand.TryGetValue(name, out var codes))
                {
                    codes = [];
                    byBrand[name] = codes;
                }

                if (!codes.Contains(code, StringComparer.Ordinal))
                {
                    codes.Add(code);
                }
            }
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            byBrand.Clear();
        }

        var brands = byBrand.Select(pair => new Dictionary<string, object?>
        {
            ["brand"] = pair.Key,
            ["country_codes"] = pair.Value,
            ["is_anchor"] = string.Equals(pair.Key, brand, StringComparison.OrdinalIgnoreCase)
        }).ToList();
        return new Dictionary<string, object?>
        {
            ["brands"] = brands,
            ["anchor_brand"] = brand
        };
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = ?");
        ErpDb.AddParameters(command, table);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is not null and not DBNull && Convert.ToInt32(value, CultureInfo.InvariantCulture) > 0;
    }

    private static Dictionary<string, object?> PartJson(DemandPartView part)
        => new()
        {
            ["brand"] = part.Brand,
            ["article"] = part.Article,
            ["article_norm"] = part.ArticleNorm,
            ["name"] = part.Name,
            ["qty"] = part.Qty,
            ["anchor_in_stock"] = part.Qty > 0,
            ["price"] = part.Price,
            ["warehouse"] = part.Warehouse,
            ["demand_codes"] = part.DemandCodes,
            ["selected_country"] = string.Empty,
            ["in_selected_country"] = true,
            ["supply_status"] = part.Qty > 0 ? "oe_in_stock" : "needs_cross_or_gap",
            ["part_url"] = part.PartUrl
        };

    private static Dictionary<string, object?> RegistryRow(string code)
    {
        var row = DemandRegistry.First(item => item.Code == code);
        return new Dictionary<string, object?>
        {
            ["code"] = row.Code,
            ["name"] = row.Name,
            ["iso2"] = row.Iso2
        };
    }

    private static Dictionary<string, object?> EmptyFitment()
        => new()
        {
            ["part_name"] = string.Empty,
            ["product_group"] = string.Empty,
            ["vehicle_count"] = 0,
            ["vehicles_sample"] = new List<object>(),
            ["fitment_source"] = string.Empty
        };

    private static string InferGroup(string name)
    {
        var hay = name.ToLowerInvariant();
        foreach (var pair in new (string Label, string Needle)[]
        {
            ("Piston", "piston"),
            ("Gasket", "gasket"),
            ("Oil filter", "oil filter"),
            ("Filter", "filter"),
            ("Brake", "brake")
        })
        {
            if (hay.Contains(pair.Needle, StringComparison.Ordinal))
            {
                return pair.Label;
            }
        }

        return name.Length == 0 ? "Uncategorized" : "Other parts";
    }

    private static string PartUrl(string brand, string article)
    {
        var norm = NormalizeArticle(article);
        var alias = brand.Trim();
        if (alias.Length == 0)
        {
            return "/en/parts/brands/" + Uri.EscapeDataString(norm);
        }

        return "/en/parts/" + Uri.EscapeDataString(alias) + "/" + Uri.EscapeDataString(norm);
    }

    private static string NormalizeCountry(string code)
    {
        var letters = new string(code.Trim().ToUpperInvariant().Where(char.IsLetter).ToArray());
        if (letters.Length == 0)
        {
            return string.Empty;
        }

        if (DemandRegistry.Any(row => row.Code == letters))
        {
            return letters;
        }

        if (letters.Length == 2)
        {
            var match = DemandRegistry.FirstOrDefault(row => row.Iso2 == letters);
            return match.Code ?? string.Empty;
        }

        return string.Empty;
    }

    private static string NormalizeArticle(string? article)
    {
        if (string.IsNullOrEmpty(article))
        {
            return string.Empty;
        }

        var stripped = Regex.Replace(article, "[\\s\\-_/`'\"\\\\.,#\\r\\n\\t]", string.Empty);
        return stripped.ToUpperInvariant();
    }

    /// <summary>PHP <c>trim(htmlspecialchars(strip_tags($search_string)))</c> (ENT_QUOTES | ENT_SUBSTITUTE | ENT_HTML401).</summary>
    private static string SearchText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var stripped = Regex.Replace(value, "<(?![ \\t\\n\\v\\f\\r])[^>]*(?:>|\\z)", string.Empty, RegexOptions.CultureInvariant);
        return stripped.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Trim(' ', '\t', '\n', '\r', '\0', '\v');
    }

    private static int Clamp(string? text, int fallback, int min, int max, bool emptyBecomesDefault)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return fallback;
        }

        if (value < min)
        {
            return emptyBecomesDefault ? fallback : min;
        }

        return Math.Min(value, max);
    }

    private static int ParseInt(string? text)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static long Now()
        => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string TextOf(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;

    private static double ReadDouble(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0 : Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static RawHttp Text(string body)
        => new(body, "text/plain; charset=utf-8");

    private sealed class CatalogueFail : Exception
    {
        public CatalogueFail(string message) : base(message)
        {
        }
    }

    private sealed class CatalogueFilter
    {
        public static CatalogueFilter None { get; } = new();

        public string ExtraWhere { get; init; } = string.Empty;

        public List<object> Args { get; init; } = [];

        public string Having { get; init; } = string.Empty;

        public List<string> Tables { get; init; } = [];
    }

    private sealed record CatalogueRequest(
        [property: JsonPropertyName("category_id"), JsonConverter(typeof(CategoryIdConverter))] int CategoryId,
        [property: JsonPropertyName("properties_list")] List<JsonElement>? Properties,
        [property: JsonPropertyName("product_block_type"), JsonConverter(typeof(BlockTypeConverter))] int ProductBlockType,
        [property: JsonPropertyName("productsPerPage")] int ProductsPerPage,
        [property: JsonPropertyName("needPagesCount")] int NeedPagesCount,
        [property: JsonPropertyName("startFrom")] int StartFrom,
        [property: JsonPropertyName("page_style")] int PageStyle,
        [property: JsonPropertyName("search_string"), JsonConverter(typeof(PhpStringConverter))] string? SearchString,
        [property: JsonPropertyName("products_ids_str")] JsonElement? ProductsIdsStr);

    /// <summary>
    /// PHP interpolates <c>category_id</c> raw into SQL. JSON numbers, numeric strings, booleans and null behave like PHP;
    /// any other string or structure is rejected (SQL-injection hardening) instead of reaching the query.
    /// </summary>
    private sealed class CategoryIdConverter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => PhpInt(ref reader, strict: true);

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
            => writer.WriteNumberValue(value);
    }

    /// <summary>PHP only compares <c>product_block_type == 1</c> etc., so any value that is not numeric simply matches no block type.</summary>
    private sealed class BlockTypeConverter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => PhpInt(ref reader, strict: false);

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
            => writer.WriteNumberValue(value);
    }

    private static int PhpInt(ref Utf8JsonReader reader, bool strict)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var whole))
                {
                    return whole;
                }

                return (int)Math.Clamp(Math.Truncate(reader.GetDouble()), int.MinValue, int.MaxValue);
            case JsonTokenType.String:
                var text = reader.GetString() ?? string.Empty;
                if (StorefrontPhpLoose.TryNumeric(text, out var number))
                {
                    return (int)Math.Clamp(Math.Truncate(number), int.MinValue, int.MaxValue);
                }

                return strict ? throw new JsonException("category_id is not numeric.") : 0;
            case JsonTokenType.True:
                return 1;
            case JsonTokenType.False:
            case JsonTokenType.Null:
                return 0;
            default:
                reader.Skip();
                return strict ? throw new JsonException("category_id is not a scalar.") : 0;
        }
    }

    private sealed class PhpStringConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                    return reader.TryGetInt64(out var whole)
                        ? whole.ToString(CultureInfo.InvariantCulture)
                        : reader.GetDouble().ToString("G14", CultureInfo.InvariantCulture);
                case JsonTokenType.True:
                    return "1";
                case JsonTokenType.False:
                    return string.Empty;
                case JsonTokenType.Null:
                    return null;
                default:
                    reader.Skip();
                    throw new JsonException("search_string is not a scalar.");
            }
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }

    private sealed record CatalogueRow(int Id, string Caption, string Alias, int CategoryId, string CategoryUrl, string Article, string Manufacturer, int Mark, int MarksCount);

    private sealed record OfficeCard(string City, string Address, string Timetable, string Phone);

    private sealed record DemandPart(string Brand, string Article, string ArticleNorm, string Name, double Qty, string Price, string Warehouse);

    private sealed record DemandPartView(
        string Brand,
        string Article,
        string ArticleNorm,
        string Name,
        double Qty,
        string Price,
        string Warehouse,
        List<string> DemandCodes,
        string PartUrl);

    private sealed record StockLine(string Brand, string Article, string ArticleNorm, string Name, string Price, string Currency, double Qty, string Warehouse, int PriceId)
    {
        public Dictionary<string, object?> ToDictionary()
            => new()
            {
                ["brand"] = Brand,
                ["article"] = Article,
                ["article_norm"] = ArticleNorm,
                ["name"] = Name,
                ["price"] = Price,
                ["currency"] = Currency,
                ["qty"] = Qty,
                ["warehouse"] = Warehouse,
                ["price_id"] = PriceId
            };
    }

    public sealed record DemandAuthBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message);

    public sealed record DemandMessageBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message);

    public sealed record GarageUcatsBody(
        [property: JsonPropertyName("status")] bool Status);

    public sealed record SessionErrorBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message);
}
