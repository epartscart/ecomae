using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-sheet CP cross helpers. PHP identifiers kept for the inventory:
/// <c>epc_cp_cross_normalize_article</c>, <c>epc_cp_cross_prepare_brand</c>,
/// <c>epc_cp_cross_pair_status</c>, <c>epc_cp_cross_count_links_for_anchor</c>,
/// <c>epc_cp_cross_fetch_search</c>, <c>epc_cp_cross_enrich_reference_brand</c>,
/// <c>epc_cp_cross_annotate_references</c>, <c>epc_cp_cross_add_link</c>,
/// <c>epc_cp_cross_import_references</c>.
/// GET never mints a session cookie. Leftover docpart parents stay injected.
/// </summary>
public static class PhpPlanQ1Sheet
{
    public const string CpCrossHelpersPath = "cp/content/shop/crosses/epc_cp_cross_helpers.php";

    private static readonly string[] ArticleSweep = [" ", "-", "_", "`", "/", "'", "\"", "\\", ".", ",", "#", "\r\n", "\r", "\n", "\t"];
    private static readonly string[] BrandSweep = ["#", "`", "\r\n", "\r", "\n", "\t", "'", "\"", "\\"];

    public static Func<string, string, string, string, Dictionary<string, object?>>? PairExists { get; set; }
    public static Func<string, string>? ArticleExpr { get; set; }
    public static Func<string, Dictionary<string, string>, string>? ResolveBrand { get; set; }
    public static Func<string, string, string, string, int>? PersistPair { get; set; }
    public static Func<string, string, bool, string, string, Dictionary<string, object?>?>? FetchSearch { get; set; }

    public static void Reset()
    {
        PairExists = (_, _, _, _) => new Dictionary<string, object?>(StringComparer.Ordinal) { ["linked"] = false, ["id"] = 0 };
        ArticleExpr = column => column;
        ResolveBrand = (_, opts) => opts.TryGetValue("fallback_brand", out var fb) ? fb.Trim() : "";
        PersistPair = (_, _, _, _) => 0;
        FetchSearch = (_, _, _, _, _) => null;
    }

    public static string EpcCpCrossNormalizeArticle(object? article)
    {
        var value = MbUpper(Trim(article));
        foreach (var sweep in ArticleSweep)
        {
            value = value.Replace(sweep, "", StringComparison.Ordinal);
        }

        return value;
    }

    public static string EpcCpCrossPrepareBrand(object? brand)
    {
        var value = MbUpper(Trim(brand));
        foreach (var sweep in BrandSweep)
        {
            value = value.Replace(sweep, "", StringComparison.Ordinal);
        }

        return value.Trim();
    }

    public static Dictionary<string, object?> EpcCpCrossPairStatus(
        MySqlConnection? db, object? anchorArticle, object? anchorBrand, object? refArticle, object? refBrand)
    {
        var a = EpcCpCrossNormalizeArticle(anchorArticle);
        var r = EpcCpCrossNormalizeArticle(refArticle);
        var ab = EpcCpCrossPrepareBrand(anchorBrand);
        var rb = EpcCpCrossPrepareBrand(refBrand);
        if (a == "" || r == "" || ab == "" || rb == "")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["linked"] = false, ["id"] = 0 };
        }

        if (a == r && ab == rb)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["linked"] = false, ["id"] = 0 };
        }

        return PairExists != null
            ? PairExists(a, ab, r, rb)
            : new Dictionary<string, object?>(StringComparer.Ordinal) { ["linked"] = false, ["id"] = 0 };
    }

    public static int EpcCpCrossCountLinksForAnchor(MySqlConnection db, object? anchorArticle, object? anchorBrand = null)
    {
        var article = EpcCpCrossNormalizeArticle(anchorArticle);
        if (article == "")
        {
            return 0;
        }

        var artExpr = ArticleExpr != null ? ArticleExpr("`article`") : "`article`";
        var analogExpr = ArticleExpr != null ? ArticleExpr("`analog`") : "`analog`";
        try
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM `shop_docpart_articles_analogs_list` WHERE " + artExpr + " = @a OR " + analogExpr + " = @b";
            cmd.Parameters.AddWithValue("@a", article);
            cmd.Parameters.AddWithValue("@b", article);
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }
        catch
        {
            return 0;
        }
    }

    public static Dictionary<string, object?>? EpcCpCrossFetchSearch(SheetConfig cfg, object? articleInput, object? anchorBrand, bool cpBulk = false)
    {
        var article = Trim(articleInput);
        if (article == "")
        {
            return null;
        }

        return FetchSearch != null
            ? FetchSearch(article, Trim(anchorBrand), cpBulk, cfg.TechKey, cfg.DomainPath.TrimEnd('/'))
            : null;
    }

    public static Dictionary<string, object?> EpcCpCrossEnrichReferenceBrand(
        MySqlConnection? db, Dictionary<string, object?> reference, object? anchorBrand = null)
    {
        var refBrand = reference.TryGetValue("brand", out var rawBrand) ? Trim(rawBrand) : "";
        if (refBrand != "")
        {
            reference["brand"] = EpcCpCrossPrepareBrand(refBrand);
            reference["brand_complete"] = true;
            return reference;
        }

        var refArticle = reference.TryGetValue("article", out var rawArticle) ? Convert.ToString(rawArticle) ?? "" : "";
        if (ResolveBrand != null)
        {
            var resolved = ResolveBrand(refArticle, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["fallback_brand"] = Trim(anchorBrand)
            });
            if (resolved != "")
            {
                reference["brand"] = resolved;
                reference["brand_inferred"] = true;
                reference["brand_complete"] = true;
                return reference;
            }
        }

        reference["brand_complete"] = false;
        return reference;
    }

    public static List<Dictionary<string, object?>> EpcCpCrossAnnotateReferences(
        MySqlConnection? db, object? anchorArticle, object? anchorBrand, IEnumerable<object?> references)
    {
        var brand = EpcCpCrossPrepareBrand(anchorBrand);
        if (brand == "" && ResolveBrand != null)
        {
            brand = ResolveBrand(Convert.ToString(anchorArticle) ?? "", new Dictionary<string, string>(StringComparer.Ordinal));
        }

        var outRows = new List<Dictionary<string, object?>>();
        foreach (var raw in references)
        {
            if (raw is not Dictionary<string, object?> reference)
            {
                continue;
            }

            EpcCpCrossEnrichReferenceBrand(db, reference, brand);
            var refArticle = reference.TryGetValue("article", out var a) ? Convert.ToString(a) ?? "" : "";
            var refBrand = reference.TryGetValue("brand", out var b) ? Convert.ToString(b) ?? "" : "";
            var status = EpcCpCrossPairStatus(db, anchorArticle, brand, refArticle, refBrand);
            reference["cp_linked"] = status.TryGetValue("linked", out var linked) && Truthy(linked);
            reference["cp_link_id"] = status.TryGetValue("id", out var id) ? Convert.ToInt32(id) : 0;
            outRows.Add(reference);
        }

        return outRows;
    }

    public static Dictionary<string, object?> EpcCpCrossAddLink(
        MySqlConnection? db, object? anchorArticle, object? anchorBrand, object? refArticle, object? refBrand)
    {
        var anchorShow = Trim(anchorArticle);
        var refShow = Trim(refArticle);
        var anchorDb = EpcCpCrossNormalizeArticle(anchorShow);
        var refDb = EpcCpCrossNormalizeArticle(refShow);
        if (anchorDb == "" || refDb == "")
        {
            return Result(0, 1, 0, "invalid_article");
        }

        if (anchorDb == refDb && Convert.ToString(anchorBrand) == Convert.ToString(refBrand))
        {
            return Result(0, 1, 0, "same_part_same_brand");
        }

        string ab;
        string rb;
        if (ResolveBrand != null)
        {
            ab = ResolveBrand(anchorDb, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["fallback_brand"] = EpcCpCrossPrepareBrand(anchorBrand)
            });
            rb = ResolveBrand(refDb, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["fallback_brand"] = EpcCpCrossPrepareBrand(refBrand),
                ["partner_brand"] = ab
            });
        }
        else
        {
            ab = EpcCpCrossPrepareBrand(anchorBrand);
            rb = EpcCpCrossPrepareBrand(refBrand);
        }

        if (ab == "" || rb == "")
        {
            return Result(0, 1, 0, "missing_brand");
        }

        var status = EpcCpCrossPairStatus(db, anchorDb, ab, refDb, rb);
        if (status.TryGetValue("linked", out var linked) && Truthy(linked))
        {
            return Result(0, 0, 1, "already_linked");
        }

        if (PersistPair is null)
        {
            return Result(0, 1, 0, null);
        }

        var inserted = PersistPair(anchorDb, ab, refDb, rb);
        return inserted > 0 ? Result(1, 0, 0, "") : Result(0, 1, 0, "insert_failed");
    }

    public static Dictionary<string, object?> EpcCpCrossImportReferences(
        MySqlConnection? db, object? anchorArticle, object? anchorBrand, IEnumerable<object?> references, bool onlyMissing = true, string sourceFilter = "")
    {
        var inserted = 0;
        var already = 0;
        var skipped = 0;
        var processed = 0;
        var prepared = EpcCpCrossPrepareBrand(anchorBrand);
        if (prepared == "" && ResolveBrand != null)
        {
            prepared = ResolveBrand(Convert.ToString(anchorArticle) ?? "", new Dictionary<string, string>(StringComparer.Ordinal));
        }

        foreach (var raw in references)
        {
            if (raw is not Dictionary<string, object?> reference)
            {
                skipped++;
                continue;
            }

            if (sourceFilter != "")
            {
                var src = reference.TryGetValue("source", out var s) ? Convert.ToString(s) ?? "" : "";
                if (src != sourceFilter && !src.Contains(sourceFilter, StringComparison.Ordinal))
                {
                    continue;
                }
            }

            var refArticle = reference.TryGetValue("article", out var a) ? Convert.ToString(a) ?? "" : "";
            EpcCpCrossEnrichReferenceBrand(db, reference, prepared);
            var refBrand = reference.TryGetValue("brand", out var b) ? Convert.ToString(b) ?? "" : "";
            if (refBrand == "" || refArticle == "")
            {
                skipped++;
                continue;
            }

            processed++;
            if (onlyMissing)
            {
                var st = EpcCpCrossPairStatus(db, anchorArticle, prepared, refArticle, refBrand);
                if (st.TryGetValue("linked", out var linked) && Truthy(linked))
                {
                    already++;
                    continue;
                }
            }

            var res = EpcCpCrossAddLink(db, anchorArticle, prepared, refArticle, refBrand);
            inserted += Convert.ToInt32(res["inserted"]);
            already += Convert.ToInt32(res["already"]);
            skipped += Convert.ToInt32(res["skipped"]);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["inserted"] = inserted,
            ["already"] = already,
            ["skipped"] = skipped,
            ["processed"] = processed
        };
    }

    public sealed class SheetConfig
    {
        public string DomainPath { get; set; } = "";
        public string TechKey { get; set; } = "";
    }

    private static Dictionary<string, object?> Result(int inserted, int skipped, int already, string? reason)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["inserted"] = inserted,
            ["skipped"] = skipped,
            ["already"] = already
        };
        if (reason is not null)
        {
            row["reason"] = reason;
        }

        return row;
    }

    private static string Trim(object? value) => (Convert.ToString(value) ?? "").Trim();

    private static string MbUpper(string value) => value.ToUpperInvariant();

    private static bool Truthy(object? value)
        => value is true or int and not 0 or long and not 0L
            || (value is string s && s != "" && s != "0");
}
