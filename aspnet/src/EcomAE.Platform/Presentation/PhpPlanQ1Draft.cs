using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-draft bidirectional cross-reference helpers. PHP identifiers kept for the inventory:
/// <c>docpart_analogs_host_load1</c>, <c>docpart_load_interchange_partners</c>,
/// <c>docpart_price_name_cluster_core</c>, <c>docpart_cross_refs_from_price_name_cluster</c>,
/// <c>docpart_cross_refs_from_stock_oem_mention</c>, <c>docpart_cross_prepare_brand_name</c>,
/// <c>docpart_cross_infer_brand_from_article_norm</c>,
/// <c>docpart_cross_resolve_brand_for_article</c>,
/// <c>docpart_cross_repair_empty_manufacturers</c>,
/// <c>docpart_cross_pair_exists_with_brands</c>,
/// <c>docpart_cross_persist_interchange_pair</c>,
/// <c>docpart_cross_persist_interchange_pair_bidirectional</c>.
/// Path: <c>content/shop/docpart/docpart_cross_interchange.php</c>.
/// GET never mints a session cookie. Article-match is the Beam twin; host load stays injected.
/// </summary>
public static class PhpPlanQ1Draft
{
    public const string CrossInterchangePath = "content/shop/docpart/docpart_cross_interchange.php";

    private static readonly string[] BrandSweep = ["#", "`", "\r\n", "\r", "\n", "\t", "'", "\"", "\\"];
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex TrailingCode = new(@"\s+\d{5,}[A-Z0-9\-]*$", RegexOptions.Compiled);
    private static readonly Regex FilterPrefix = new(@"^(OIL FILTER|FILTER|AIR FILTER|FUEL FILTER|CABIN FILTER|SPARK PLUG)", RegexOptions.Compiled);

    public static Func<double?>? HostLoad { get; set; }

    public static void Reset()
    {
        HostLoad = null;
        PhpPlanQ1Beam.Reset();
    }

    public static double? DocpartAnalogsHostLoad1() => HostLoad?.Invoke();

    public static List<Dictionary<string, object?>> DocpartLoadInterchangePartners(MySqlConnection? db, string articleNorm, int maxRounds = 6, int rowLimit = 5000)
    {
        articleNorm = PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(articleNorm);
        if (articleNorm == "" || db is null)
        {
            return [];
        }

        maxRounds = Math.Max(0, Math.Min(3, maxRounds));
        rowLimit = Math.Max(0, Math.Min(200, rowLimit));
        var load1 = DocpartAnalogsHostLoad1();
        if (load1 is >= 6.0)
        {
            return [];
        }

        if (load1 is >= 4.0)
        {
            maxRounds = Math.Min(maxRounds, 1);
            rowLimit = Math.Min(rowLimit, 30);
        }
        else if (load1 is >= 2.5)
        {
            maxRounds = Math.Min(maxRounds, 1);
            rowLimit = Math.Min(rowLimit, 60);
        }

        if (maxRounds <= 0 || rowLimit <= 0)
        {
            return [];
        }

        var partners = new List<Dictionary<string, object?>>();
        var seen = new Dictionary<string, bool>(StringComparer.Ordinal);
        var knownNorms = new Dictionary<string, bool>(StringComparer.Ordinal) { [articleNorm] = true };
        var (artExpr, analogExpr) = PhpPlanQ1Beam.DocpartAnalogsMatchExprs(db);
        for (var round = 0; round < maxRounds; round++)
        {
            var normBatch = knownNorms.Keys.ToList();
            if (normBatch.Count == 0)
            {
                break;
            }

            var discovered = new Dictionary<string, bool>(StringComparer.Ordinal);
            var stopAll = false;
            foreach (var chunk in Chunk(normBatch, 150))
            {
                if (stopAll || partners.Count >= rowLimit)
                {
                    stopAll = true;
                    break;
                }

                try
                {
                    TrySetTimeout(db, 3, 3000);
                    using var cmd = db.CreateCommand();
                    var ph = string.Join(",", chunk.Select((_, i) => "@c" + i));
                    cmd.CommandText = "SELECT `article`, `manufacturer_article`, `analog`, `manufacturer_analog` FROM `shop_docpart_articles_analogs_list` WHERE "
                        + artExpr + " IN (" + ph + ") OR " + analogExpr + " IN (" + ph + ") LIMIT " + rowLimit;
                    for (var i = 0; i < chunk.Count; i++)
                    {
                        cmd.Parameters.AddWithValue("@c" + i, chunk[i]);
                    }

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var rowArticleNorm = PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(reader["article"]);
                        var rowAnalogNorm = PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(reader["analog"]);
                        if (knownNorms.ContainsKey(rowArticleNorm))
                        {
                            var brand = (Convert.ToString(reader["manufacturer_analog"]) ?? "").Trim();
                            var article = (Convert.ToString(reader["analog"]) ?? "").Trim();
                            if (article != "" && rowAnalogNorm != "")
                            {
                                var key = (brand + "|" + rowAnalogNorm).ToUpperInvariant();
                                if (!seen.ContainsKey(key))
                                {
                                    seen[key] = true;
                                    partners.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                                    {
                                        ["brand"] = brand,
                                        ["article"] = article,
                                        ["article_norm"] = rowAnalogNorm
                                    });
                                }

                                discovered[rowAnalogNorm] = true;
                            }
                        }

                        if (knownNorms.ContainsKey(rowAnalogNorm))
                        {
                            var brand = (Convert.ToString(reader["manufacturer_article"]) ?? "").Trim();
                            var article = (Convert.ToString(reader["article"]) ?? "").Trim();
                            if (article != "" && rowArticleNorm != "")
                            {
                                var key = (brand + "|" + rowArticleNorm).ToUpperInvariant();
                                if (!seen.ContainsKey(key))
                                {
                                    seen[key] = true;
                                    partners.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                                    {
                                        ["brand"] = brand,
                                        ["article"] = article,
                                        ["article_norm"] = rowArticleNorm
                                    });
                                }

                                discovered[rowArticleNorm] = true;
                            }
                        }

                        if (partners.Count >= rowLimit)
                        {
                            stopAll = true;
                            break;
                        }
                    }
                }
                catch
                {
                    stopAll = true;
                    break;
                }
            }

            if (stopAll)
            {
                break;
            }

            var added = false;
            foreach (var norm in discovered.Keys)
            {
                if (!knownNorms.ContainsKey(norm))
                {
                    knownNorms[norm] = true;
                    added = true;
                }
            }

            if (!added)
            {
                break;
            }
        }

        return partners;
    }

    public static string DocpartPriceNameClusterCore(string? name)
    {
        var text = (name ?? "").Trim().ToUpperInvariant();
        text = Spaces.Replace(text, " ");
        text = TrailingCode.Replace(text, "");
        text = text.Trim();
        if (text == "" || Encoding.UTF8.GetByteCount(text) < 6)
        {
            return "";
        }

        return FilterPrefix.IsMatch(text) ? text : "";
    }

    public static List<Dictionary<string, object?>> DocpartCrossRefsFromPriceNameCluster(MySqlConnection db, string articleNorm, int limit = 100)
    {
        articleNorm = PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(articleNorm);
        if (articleNorm == "")
        {
            return [];
        }

        var refs = new List<Dictionary<string, object?>>();
        var seen = new Dictionary<string, bool>(StringComparer.Ordinal);
        var artExpr = ArtExpr(db);
        var cores = new Dictionary<string, bool>(StringComparer.Ordinal);
        try
        {
            using var names = db.CreateCommand();
            names.CommandText = "SELECT DISTINCT TRIM(`name`) AS `name` FROM `shop_docpart_prices_data` WHERE "
                + artExpr + " = @a AND IFNULL(`exist`, 0) > 0 AND IFNULL(`price`, 0) > 0 AND TRIM(`name`) <> '' LIMIT 15";
            names.Parameters.AddWithValue("@a", articleNorm);
            using var reader = names.ExecuteReader();
            while (reader.Read())
            {
                var core = DocpartPriceNameClusterCore(Convert.ToString(reader["name"]));
                if (core != "")
                {
                    cores[core] = true;
                }
            }
        }
        catch
        {
            return refs;
        }

        if (cores.Count == 0)
        {
            return refs;
        }

        foreach (var core in cores.Keys)
        {
            if (refs.Count >= limit)
            {
                break;
            }

            try
            {
                using var sib = db.CreateCommand();
                sib.CommandText = "SELECT `manufacturer`, `article`, MAX(`article_show`) AS `article_show`, MAX(`name`) AS `name` FROM `shop_docpart_prices_data` WHERE UPPER(TRIM(`name`)) LIKE @like AND "
                    + artExpr + " <> @a AND IFNULL(`exist`, 0) > 0 AND IFNULL(`price`, 0) > 0 GROUP BY `manufacturer`, `article` ORDER BY `manufacturer`, `article` LIMIT "
                    + Math.Min(40, limit);
                sib.Parameters.AddWithValue("@like", core + "%");
                sib.Parameters.AddWithValue("@a", articleNorm);
                using var reader = sib.ExecuteReader();
                while (reader.Read())
                {
                    var productNorm = PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(reader["article"]);
                    if (productNorm == "" || productNorm == articleNorm)
                    {
                        continue;
                    }

                    var brand = (Convert.ToString(reader["manufacturer"]) ?? "").Trim();
                    var show = Convert.ToString(reader["article_show"]) ?? "";
                    var article = (!PhpEmpty(show) ? show : Convert.ToString(reader["article"]) ?? "").Trim();
                    var key = (brand + "|" + productNorm).ToUpperInvariant();
                    if (seen.ContainsKey(key))
                    {
                        continue;
                    }

                    seen[key] = true;
                    refs.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["brand"] = brand,
                        ["article"] = article,
                        ["article_norm"] = productNorm
                    });
                    if (refs.Count >= limit)
                    {
                        break;
                    }
                }
            }
            catch
            {
            }
        }

        return refs;
    }

    public static List<Dictionary<string, object?>> DocpartCrossRefsFromStockOemMention(MySqlConnection db, string articleNorm, int limit = 30)
    {
        articleNorm = PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(articleNorm);
        if (articleNorm == "" || Encoding.UTF8.GetByteCount(articleNorm) < 5)
        {
            return [];
        }

        var refs = new List<Dictionary<string, object?>>();
        var seen = new Dictionary<string, bool>(StringComparer.Ordinal);
        var artExpr = ArtExpr(db);
        try
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT `manufacturer`, `article`, MAX(`article_show`) AS `article_show` FROM `shop_docpart_prices_data` WHERE UPPER(TRIM(`name`)) LIKE @like AND "
                + artExpr + " <> @a AND IFNULL(`exist`, 0) > 0 AND IFNULL(`price`, 0) > 0 GROUP BY `manufacturer`, `article` ORDER BY `manufacturer`, `article` LIMIT "
                + Math.Max(5, Math.Min(25, limit));
            cmd.Parameters.AddWithValue("@like", "%" + articleNorm + "%");
            cmd.Parameters.AddWithValue("@a", articleNorm);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var productNorm = PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(reader["article"]);
                if (productNorm == "" || productNorm == articleNorm)
                {
                    continue;
                }

                var brand = (Convert.ToString(reader["manufacturer"]) ?? "").Trim();
                var show = Convert.ToString(reader["article_show"]) ?? "";
                var article = (!PhpEmpty(show) ? show : Convert.ToString(reader["article"]) ?? "").Trim();
                var key = (brand + "|" + productNorm).ToUpperInvariant();
                if (seen.ContainsKey(key))
                {
                    continue;
                }

                seen[key] = true;
                refs.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["brand"] = brand,
                    ["article"] = article,
                    ["article_norm"] = productNorm
                });
            }
        }
        catch
        {
            return refs;
        }

        return refs;
    }

    public static string DocpartCrossPrepareBrandName(object? brand)
    {
        var text = (Convert.ToString(brand, CultureInfo.InvariantCulture) ?? "").Trim().ToUpperInvariant();
        foreach (var token in BrandSweep)
        {
            text = text.Replace(token, "", StringComparison.Ordinal);
        }

        return text.Trim();
    }

    public static string DocpartCrossInferBrandFromArticleNorm(string articleNorm)
    {
        articleNorm = PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(articleNorm);
        if (articleNorm == "")
        {
            return "";
        }

        if (Regex.IsMatch(articleNorm, "^90915", RegexOptions.IgnoreCase))
        {
            return "TOYOTA";
        }

        if (Regex.IsMatch(articleNorm, "^15400", RegexOptions.IgnoreCase))
        {
            return "HONDA";
        }

        if (Regex.IsMatch(articleNorm, "^15208|^22040", RegexOptions.IgnoreCase))
        {
            return "NISSAN";
        }

        if (Regex.IsMatch(articleNorm, "^26300|^28113", RegexOptions.IgnoreCase))
        {
            return "HYUNDAI";
        }

        if (Regex.IsMatch(articleNorm, "^06[A-Z0-9]|^1K0|^5W0|^8E0", RegexOptions.IgnoreCase))
        {
            return "VAG";
        }

        if (Regex.IsMatch(articleNorm, "^A000|^A[0-9]{9,10}$", RegexOptions.IgnoreCase))
        {
            return "MERCEDES-BENZ";
        }

        if (Regex.IsMatch(articleNorm, "^B6Y1|^PE01|^LF05", RegexOptions.IgnoreCase))
        {
            return "MAZDA";
        }

        if (Regex.IsMatch(articleNorm, "^12279|^12280|^12281|^13101|^13102|^13103|^13104|^13105|^13106", RegexOptions.IgnoreCase))
        {
            return "TOYOTA";
        }

        if (Regex.IsMatch(articleNorm, "^46256|^45114", RegexOptions.IgnoreCase))
        {
            return "TEIKIN";
        }

        return "";
    }

    public static string DocpartCrossResolveBrandForArticle(MySqlConnection db, string article, Dictionary<string, string>? hints = null)
    {
        var fallback = DocpartCrossPrepareBrandName(hints is not null && hints.TryGetValue("fallback_brand", out var fb) ? fb : "");
        if (fallback != "")
        {
            return fallback;
        }

        var articleNorm = PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(article);
        if (articleNorm == "")
        {
            return "";
        }

        var artExpr = ArtExpr(db);
        var (analogArtExpr, analogExpr) = PhpPlanQ1Beam.DocpartAnalogsMatchExprs(db);
        try
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT UPPER(TRIM(`manufacturer`)) AS `mfr`, COUNT(*) AS `cnt` FROM `shop_docpart_prices_data` WHERE "
                + artExpr + " = @a AND TRIM(`manufacturer`) <> '' GROUP BY UPPER(TRIM(`manufacturer`)) ORDER BY `cnt` DESC LIMIT 1";
            cmd.Parameters.AddWithValue("@a", articleNorm);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var mfr = Convert.ToString(reader["mfr"]) ?? "";
                if (!PhpEmpty(mfr))
                {
                    return DocpartCrossPrepareBrandName(mfr);
                }
            }
        }
        catch
        {
        }

        try
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT `manufacturer_article`, `manufacturer_analog`, `article`, `analog` FROM `shop_docpart_articles_analogs_list` WHERE "
                + analogArtExpr + " = @a OR " + analogExpr + " = @b ORDER BY `id` DESC LIMIT 40";
            cmd.Parameters.AddWithValue("@a", articleNorm);
            cmd.Parameters.AddWithValue("@b", articleNorm);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var rowArticleNorm = PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(reader["article"]);
                var rowAnalogNorm = PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(reader["analog"]);
                var mfr = "";
                if (rowArticleNorm == articleNorm)
                {
                    mfr = DocpartCrossPrepareBrandName(reader["manufacturer_article"]);
                }
                else if (rowAnalogNorm == articleNorm)
                {
                    mfr = DocpartCrossPrepareBrandName(reader["manufacturer_analog"]);
                }

                if (mfr != "")
                {
                    counts[mfr] = counts.TryGetValue(mfr, out var n) ? n + 1 : 1;
                }
            }

            if (counts.Count > 0)
            {
                return counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key;
            }
        }
        catch
        {
        }

        var partner = DocpartCrossPrepareBrandName(hints is not null && hints.TryGetValue("partner_brand", out var pb) ? pb : "");
        if (partner != "")
        {
            return partner;
        }

        return DocpartCrossInferBrandFromArticleNorm(articleNorm);
    }

    public static Dictionary<string, object?> DocpartCrossRepairEmptyManufacturers(MySqlConnection db, string articleFilter = "", int limit = 500)
    {
        limit = Math.Max(1, Math.Min(5000, limit));
        var where = "(`manufacturer_article` = '' OR `manufacturer_analog` = '')";
        var paramsList = new List<string>();
        var filter = (articleFilter ?? "").Trim();
        if (filter != "")
        {
            var norm = PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(filter);
            if (norm != "")
            {
                var (artExpr, analogExpr) = PhpPlanQ1Beam.DocpartAnalogsMatchExprs(db);
                where += " AND (" + artExpr + " = @n1 OR " + analogExpr + " = @n2)";
                paramsList.Add(norm);
            }
        }

        var updated = 0;
        var skipped = 0;
        try
        {
            using var select = db.CreateCommand();
            select.CommandText = "SELECT `id`, `article`, `manufacturer_article`, `analog`, `manufacturer_analog` FROM `shop_docpart_articles_analogs_list` WHERE "
                + where + " ORDER BY `id` DESC LIMIT " + limit;
            if (paramsList.Count > 0)
            {
                select.Parameters.AddWithValue("@n1", paramsList[0]);
                select.Parameters.AddWithValue("@n2", paramsList[0]);
            }

            var rows = new List<Dictionary<string, object?>>();
            using (var reader = select.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["id"] = Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture),
                        ["article"] = Convert.ToString(reader["article"]) ?? "",
                        ["manufacturer_article"] = Convert.ToString(reader["manufacturer_article"]) ?? "",
                        ["analog"] = Convert.ToString(reader["analog"]) ?? "",
                        ["manufacturer_analog"] = Convert.ToString(reader["manufacturer_analog"]) ?? ""
                    });
                }
            }

            foreach (var row in rows)
            {
                var mfrArticle = DocpartCrossPrepareBrandName(row["manufacturer_article"]);
                var mfrAnalog = DocpartCrossPrepareBrandName(row["manufacturer_analog"]);
                if (mfrArticle == "")
                {
                    mfrArticle = DocpartCrossResolveBrandForArticle(db, Convert.ToString(row["article"]) ?? "", new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["partner_brand"] = mfrAnalog
                    });
                }

                if (mfrAnalog == "")
                {
                    mfrAnalog = DocpartCrossResolveBrandForArticle(db, Convert.ToString(row["analog"]) ?? "", new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["partner_brand"] = mfrArticle
                    });
                }

                if (mfrArticle == "" || mfrAnalog == "")
                {
                    skipped++;
                    continue;
                }

                using var upd = db.CreateCommand();
                upd.CommandText = "UPDATE `shop_docpart_articles_analogs_list` SET `manufacturer_article` = @a, `manufacturer_analog` = @b WHERE `id` = @id";
                upd.Parameters.AddWithValue("@a", mfrArticle);
                upd.Parameters.AddWithValue("@b", mfrAnalog);
                upd.Parameters.AddWithValue("@id", row["id"]);
                upd.ExecuteNonQuery();
                updated++;
            }
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["updated"] = 0,
                ["skipped"] = 0,
                ["error"] = ex.Message
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["updated"] = updated,
            ["skipped"] = skipped,
            ["error"] = ""
        };
    }

    public static Dictionary<string, object?> DocpartCrossPairExistsWithBrands(MySqlConnection db, string articleA, string brandA, string articleB, string brandB)
    {
        articleA = PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(articleA);
        articleB = PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(articleB);
        brandA = DocpartCrossPrepareBrandName(brandA);
        brandB = DocpartCrossPrepareBrandName(brandB);
        if (articleA == "" || articleB == "" || brandA == "" || brandB == "")
        {
            return Linked(false, 0);
        }

        if (articleA == articleB && brandA == brandB)
        {
            return Linked(false, 0);
        }

        var load1 = DocpartAnalogsHostLoad1();
        if (load1 is >= 10.0)
        {
            return Linked(false, 0);
        }

        var (artExpr, analogExpr) = PhpPlanQ1Beam.DocpartAnalogsMatchExprs(db);
        try
        {
            TrySetTimeout(db, 2, 2000);
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT `id` FROM `shop_docpart_articles_analogs_list` WHERE ("
                + artExpr + " = @a1 AND " + analogExpr + " = @b1 AND UPPER(TRIM(`manufacturer_article`)) = @ba AND UPPER(TRIM(`manufacturer_analog`)) = @bb"
                + ") OR ("
                + artExpr + " = @a2 AND " + analogExpr + " = @b2 AND UPPER(TRIM(`manufacturer_article`)) = @bb2 AND UPPER(TRIM(`manufacturer_analog`)) = @ba2"
                + ") LIMIT 1";
            cmd.Parameters.AddWithValue("@a1", articleA);
            cmd.Parameters.AddWithValue("@b1", articleB);
            cmd.Parameters.AddWithValue("@ba", brandA);
            cmd.Parameters.AddWithValue("@bb", brandB);
            cmd.Parameters.AddWithValue("@a2", articleB);
            cmd.Parameters.AddWithValue("@b2", articleA);
            cmd.Parameters.AddWithValue("@bb2", brandB);
            cmd.Parameters.AddWithValue("@ba2", brandA);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return Linked(true, Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture));
            }
        }
        catch
        {
            return Linked(false, 0);
        }

        return Linked(false, 0);
    }

    public static bool DocpartCrossPersistInterchangePair(MySqlConnection db, string article, string manufacturerArticle, string analog, string manufacturerAnalog)
    {
        article = (article ?? "").Trim();
        analog = (analog ?? "").Trim();
        manufacturerArticle = DocpartCrossPrepareBrandName(manufacturerArticle);
        manufacturerAnalog = DocpartCrossPrepareBrandName(manufacturerAnalog);
        if (article == "" || analog == "")
        {
            return false;
        }

        if (manufacturerArticle == "")
        {
            manufacturerArticle = DocpartCrossResolveBrandForArticle(db, article, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["partner_brand"] = manufacturerAnalog
            });
        }

        if (manufacturerAnalog == "")
        {
            manufacturerAnalog = DocpartCrossResolveBrandForArticle(db, analog, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["partner_brand"] = manufacturerArticle,
                ["fallback_brand"] = manufacturerArticle
            });
        }

        if (manufacturerArticle == "" || manufacturerAnalog == "")
        {
            return false;
        }

        var aNorm = PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(article);
        var anNorm = PhpPlanQ1Beam.DocpartNormalizeArticleForPrice(analog);
        if (aNorm == "" || anNorm == "")
        {
            return false;
        }

        if (aNorm == anNorm && manufacturerArticle == manufacturerAnalog)
        {
            return false;
        }

        var exists = DocpartCrossPairExistsWithBrands(db, article, manufacturerArticle, analog, manufacturerAnalog);
        if (!PhpEmpty(exists["linked"]))
        {
            return false;
        }

        try
        {
            using var cmd = db.CreateCommand();
            if (PhpPlanQ1Beam.DocpartAnalogsHasSearchColumns(db))
            {
                cmd.CommandText = "INSERT INTO `shop_docpart_articles_analogs_list` (`article`, `article_search`, `manufacturer_article`, `analog`, `analog_search`, `manufacturer_analog`) VALUES (@a,@as,@ma,@b,@bs,@mb)";
                cmd.Parameters.AddWithValue("@a", article);
                cmd.Parameters.AddWithValue("@as", aNorm);
                cmd.Parameters.AddWithValue("@ma", manufacturerArticle);
                cmd.Parameters.AddWithValue("@b", analog);
                cmd.Parameters.AddWithValue("@bs", anNorm);
                cmd.Parameters.AddWithValue("@mb", manufacturerAnalog);
            }
            else
            {
                cmd.CommandText = "INSERT INTO `shop_docpart_articles_analogs_list` (`article`, `manufacturer_article`, `analog`, `manufacturer_analog`) VALUES (@a,@ma,@b,@mb)";
                cmd.Parameters.AddWithValue("@a", article);
                cmd.Parameters.AddWithValue("@ma", manufacturerArticle);
                cmd.Parameters.AddWithValue("@b", analog);
                cmd.Parameters.AddWithValue("@mb", manufacturerAnalog);
            }

            cmd.ExecuteNonQuery();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static int DocpartCrossPersistInterchangePairBidirectional(MySqlConnection db, string article, string manufacturerArticle, string analog, string manufacturerAnalog)
    {
        var inserted = 0;
        if (DocpartCrossPersistInterchangePair(db, article, manufacturerArticle, analog, manufacturerAnalog))
        {
            inserted++;
        }

        if (DocpartCrossPersistInterchangePair(db, analog, manufacturerAnalog, article, manufacturerArticle))
        {
            inserted++;
        }

        return inserted;
    }

    private static string ArtExpr(MySqlConnection db)
        => PhpPlanQ1Beam.DocpartPriceDataEnsureArticleSearchColumn(db)
            ? "`article_search`"
            : PhpPlanQ1Beam.DocpartSqlArticleNormalizedExpr("`article`");

    private static Dictionary<string, object?> Linked(bool linked, int id)
        => new(StringComparer.Ordinal) { ["linked"] = linked, ["id"] = id };

    private static IEnumerable<List<T>> Chunk<T>(List<T> items, int size)
    {
        for (var i = 0; i < items.Count; i += size)
        {
            yield return items.GetRange(i, Math.Min(size, items.Count - i));
        }
    }

    private static void TrySetTimeout(MySqlConnection db, int seconds, int millis)
    {
        try
        {
            using var a = db.CreateCommand();
            a.CommandText = "SET SESSION max_statement_time = " + seconds;
            a.ExecuteNonQuery();
        }
        catch
        {
        }

        try
        {
            using var b = db.CreateCommand();
            b.CommandText = "SET SESSION MAX_EXECUTION_TIME = " + millis;
            b.ExecuteNonQuery();
        }
        catch
        {
        }
    }

    private static bool PhpEmpty(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is bool flag)
        {
            return !flag;
        }

        if (value is int n)
        {
            return n == 0;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return text == "" || text == "0";
    }
}
