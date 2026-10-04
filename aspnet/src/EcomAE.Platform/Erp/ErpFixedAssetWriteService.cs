using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_fa_create_asset</c> / <c>epc_erp_fa_run_depreciation</c> twins
/// (<c>content/shop/finance/epc_erp_fixed_assets.php</c>). Schema ensure mirrors
/// <c>epc_erp_fa_ensure_schema</c> (the PHP page and depreciation run both call it);
/// depreciation posts Dr 5100 / Cr 1550 through the shared GL after the sub-ledger commit.
/// </summary>
public interface IErpFixedAssetWriteService
{
    Task<long> CreateAssetAsync(ErpFaCreateAssetInput input, int adminId, CancellationToken cancellationToken = default);

    Task<ErpFaDepreciationRunResult> RunDepreciationAsync(string? periodMonth, string? note, int adminId, CancellationToken cancellationToken = default);
}

public sealed class ErpFaCreateAssetInput
{
    public string AssetCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public long CategoryId { get; init; }
    public string? AcquisitionDate { get; init; }
    public decimal Cost { get; init; }
    public decimal SalvageValue { get; init; }
    public int UsefulLifeMonths { get; init; } = 60;
    public string? DepreciationMethod { get; init; }
    public decimal AccumulatedDepreciation { get; init; }
    public string Location { get; init; } = string.Empty;
    public string TrackingId { get; init; } = string.Empty;
    public string SerialNo { get; init; } = string.Empty;
    public long OpeningBatchId { get; init; }
    public string Note { get; init; } = string.Empty;

    /// <summary>Optional extended columns keyed by PHP field name; absent keys are not written.</summary>
    public IReadOnlyDictionary<string, string> Extended { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record ErpFaDepreciationRunResult(long RunId, decimal Total, int Assets, long GlJournalId);

public sealed class ErpFixedAssetWriteService : IErpFixedAssetWriteService
{
    public const string ExpenseCode = "5100";
    public const string AccumCode = "1550";

    public static readonly IReadOnlyDictionary<string, string> DepreciationMethods = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["straight_line"] = "Straight line",
        ["declining_balance"] = "Declining balance",
        ["double_declining"] = "Double declining balance",
        ["units_of_production"] = "Units of production",
    };

    public static readonly IReadOnlyDictionary<string, int> ExtendedStrings = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["asset_group"] = 64, ["major_type"] = 64, ["property_type"] = 64,
        ["depreciation_convention"] = 32, ["posting_profile"] = 64, ["barcode"] = 128,
        ["make"] = 128, ["model"] = 128, ["manufacturer"] = 128, ["purchase_invoice_ref"] = 128,
        ["insurance_policy_no"] = 128, ["custodian"] = 128, ["gl_asset_account"] = 64,
        ["gl_depreciation_account"] = 64, ["gl_accum_depr_account"] = 64,
    };

    public static readonly string[] ExtendedInts = ["legal_entity_id", "business_unit_id", "supplier_vendor_id"];
    public static readonly string[] ExtendedDecimals = ["quantity", "insured_value"];
    public static readonly string[] ExtendedDates = ["placed_in_service_date", "disposal_date", "warranty_expiry"];

    private static readonly (string Column, string Definition)[] ExtendedColumns =
    [
        ("asset_group", "varchar(64) NOT NULL DEFAULT ''"),
        ("legal_entity_id", "int(11) NOT NULL DEFAULT 0"),
        ("business_unit_id", "int(11) NOT NULL DEFAULT 0"),
        ("asset_type", "varchar(24) NOT NULL DEFAULT 'tangible'"),
        ("major_type", "varchar(64) NOT NULL DEFAULT ''"),
        ("property_type", "varchar(64) NOT NULL DEFAULT ''"),
        ("quantity", "decimal(14,3) NOT NULL DEFAULT 1.000"),
        ("placed_in_service_date", "date DEFAULT NULL"),
        ("disposal_date", "date DEFAULT NULL"),
        ("depreciation_convention", "varchar(32) NOT NULL DEFAULT ''"),
        ("posting_profile", "varchar(64) NOT NULL DEFAULT ''"),
        ("barcode", "varchar(128) NOT NULL DEFAULT ''"),
        ("make", "varchar(128) NOT NULL DEFAULT ''"),
        ("model", "varchar(128) NOT NULL DEFAULT ''"),
        ("manufacturer", "varchar(128) NOT NULL DEFAULT ''"),
        ("supplier_vendor_id", "int(11) NOT NULL DEFAULT 0"),
        ("purchase_invoice_ref", "varchar(128) NOT NULL DEFAULT ''"),
        ("insurance_policy_no", "varchar(128) NOT NULL DEFAULT ''"),
        ("insured_value", "decimal(14,2) NOT NULL DEFAULT 0.00"),
        ("warranty_expiry", "date DEFAULT NULL"),
        ("custodian", "varchar(128) NOT NULL DEFAULT ''"),
        ("gl_asset_account", "varchar(64) NOT NULL DEFAULT ''"),
        ("gl_depreciation_account", "varchar(64) NOT NULL DEFAULT ''"),
        ("gl_accum_depr_account", "varchar(64) NOT NULL DEFAULT ''"),
    ];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlPostingService _gl;

    public ErpFixedAssetWriteService(IErpWriteConnectionFactory connections, IErpGlPostingService gl)
    {
        _connections = connections;
        _gl = gl;
    }

    public static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>PHP <c>preg_replace('/[^0-9\-]/','')</c> then <c>strlen === 7</c>.</summary>
    public static string NormalizePeriod(string? period)
    {
        var p = Regex.Replace(period ?? string.Empty, "[^0-9\\-]", string.Empty);
        if (p.Length != 7)
        {
            throw new ErpWriteException("Period must be YYYY-MM");
        }

        return p;
    }

    public static string NormalizeMethod(string? method)
        => method is not null && DepreciationMethods.ContainsKey(method) ? method : "straight_line";

    /// <summary>PHP <c>date('Y-m-d', strtotime($value))</c>; today when empty, PHP epoch fallback when unparseable.</summary>
    public static string NormalizeDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d)
            ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : "1970-01-01";
    }

    /// <summary>PHP <c>epc_erp_fa_period_depreciation_amount</c>.</summary>
    public static decimal PeriodDepreciation(decimal cost, decimal salvage, decimal accum, int lifeMonths, string? method)
    {
        var life = Math.Max(1, lifeMonths);
        var depreciable = Math.Max(0m, cost - salvage);
        var remaining = Math.Max(0m, depreciable - accum);
        if (remaining <= 0m)
        {
            return 0m;
        }

        var book = Math.Max(salvage, cost - accum);
        return method switch
        {
            "declining_balance" => Math.Min(remaining, R2(book * (2.0m / life))),
            "double_declining" => Math.Min(remaining, R2(book * (4.0m / life))),
            _ => Math.Min(remaining, R2(depreciable / life)),
        };
    }

    public static string PostedMessage(decimal total)
        => "Depreciation posted — " + total.ToString("#,##0.00", CultureInfo.InvariantCulture) + " AED";

    public async Task<long> CreateAssetAsync(ErpFaCreateAssetInput input, int adminId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var code = input.AssetCode.Trim();
        var name = input.Name.Trim();
        var cost = R2(input.Cost);
        if (code.Length == 0 || name.Length == 0 || cost <= 0m)
        {
            throw new ErpWriteException("Asset code, name and cost required");
        }

        EnsureConfigured();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var columns = await ColumnsAsync(connection, cancellationToken).ConfigureAwait(false);

        var openAccum = R2(input.AccumulatedDepreciation);
        var cols = new List<string>
        {
            "asset_code", "name", "category_id", "acquisition_date", "cost", "salvage_value",
            "useful_life_months", "depreciation_method", "accumulated_depreciation", "book_value",
            "location", "tracking_id", "serial_no", "opening_batch_id", "note", "time_created",
        };
        var vals = new List<object?>
        {
            code, name, input.CategoryId, NormalizeDate(input.AcquisitionDate), cost, R2(input.SalvageValue),
            Math.Max(1, input.UsefulLifeMonths), NormalizeMethod(input.DepreciationMethod), openAccum, Math.Max(0m, cost - openAccum),
            input.Location.Trim(), input.TrackingId.Trim(), input.SerialNo.Trim(), input.OpeningBatchId, input.Note.Trim(),
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        foreach (var (col, len) in ExtendedStrings)
        {
            if (input.Extended.TryGetValue(col, out var v) && columns.Contains(col))
            {
                var t = v.Trim();
                cols.Add(col);
                vals.Add(t.Length > len ? t[..len] : t);
            }
        }

        if (input.Extended.TryGetValue("asset_type", out var at) && columns.Contains("asset_type"))
        {
            cols.Add("asset_type");
            vals.Add(at is "tangible" or "intangible" ? at : "tangible");
        }

        foreach (var col in ExtendedInts)
        {
            if (input.Extended.TryGetValue(col, out var v) && columns.Contains(col))
            {
                cols.Add(col);
                vals.Add(long.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0L);
            }
        }

        foreach (var col in ExtendedDecimals)
        {
            if (input.Extended.TryGetValue(col, out var v) && v.Length > 0 && columns.Contains(col))
            {
                cols.Add(col);
                vals.Add(decimal.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0m);
            }
        }

        foreach (var col in ExtendedDates)
        {
            if (input.Extended.TryGetValue(col, out var v) && v.Trim().Length > 0 && v.Trim() != "0" && columns.Contains(col))
            {
                cols.Add(col);
                vals.Add(NormalizeDate(v));
            }
        }

        var sql = "INSERT INTO `epc_erp_fa_assets` (`" + string.Join("`,`", cols) + "`) VALUES (" + string.Join(",", cols.Select(_ => "?")) + ")";
        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(sql), cancellationToken, vals.ToArray()).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        await RefreshBookValueAsync(connection, id, cancellationToken).ConfigureAwait(false);
        if (input.Location.Trim().Length > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `epc_erp_fa_tracking_log` (`asset_id`,`event_type`,`location`,`note`,`event_date`,`admin_id`) VALUES (?,?,?,?,?,?)"),
                cancellationToken,
                id, "registered", input.Location, "Asset registered", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), adminId).ConfigureAwait(false);
        }

        return id;
    }

    public async Task<ErpFaDepreciationRunResult> RunDepreciationAsync(string? periodMonth, string? note, int adminId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var period = NormalizePeriod(periodMonth);
        var existing = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `id` FROM `epc_erp_fa_depreciation_runs` WHERE `period_month` = ? LIMIT 1"), cancellationToken, period).ConfigureAwait(false);
        if (existing > 0)
        {
            throw new ErpWriteException("Depreciation already posted for " + period);
        }

        var assets = new List<(long Id, decimal Cost, decimal Salvage, decimal Accum, int Life, string Method)>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `id`,`cost`,`salvage_value`,`accumulated_depreciation`,`useful_life_months`,`depreciation_method` FROM `epc_erp_fa_assets` WHERE `status` = 'active'";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                assets.Add((reader.GetInt64(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetInt32(4), reader.GetString(5)));
            }
        }

        var total = 0m;
        long runId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using (var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("INSERT INTO `epc_erp_fa_depreciation_runs` (`period_month`,`run_date`,`admin_id`,`note`) VALUES (?,?,?,?)"), cancellationToken, period, now, adminId, note ?? string.Empty).ConfigureAwait(false);
                runId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
                foreach (var a in assets)
                {
                    var amt = PeriodDepreciation(a.Cost, a.Salvage, a.Accum, a.Life, a.Method);
                    if (amt <= 0m)
                    {
                        continue;
                    }

                    var newAccum = R2(a.Accum + amt);
                    var newBv = Math.Max(a.Salvage, a.Cost - newAccum);
                    await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("UPDATE `epc_erp_fa_assets` SET `accumulated_depreciation` = ?, `book_value` = ? WHERE `id` = ?"), cancellationToken, newAccum, newBv, a.Id).ConfigureAwait(false);
                    await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("INSERT INTO `epc_erp_fa_depreciation_lines` (`run_id`,`asset_id`,`amount`,`accumulated_after`,`book_value_after`) VALUES (?,?,?,?,?)"), cancellationToken, runId, a.Id, amt, newAccum, newBv).ConfigureAwait(false);
                    total += amt;
                    if (newBv <= a.Salvage + 0.01m)
                    {
                        await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("UPDATE `epc_erp_fa_assets` SET `status` = 'fully_depreciated' WHERE `id` = ?"), cancellationToken, a.Id).ConfigureAwait(false);
                    }
                }

                await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("UPDATE `epc_erp_fa_depreciation_runs` SET `total_amount` = ? WHERE `id` = ?"), cancellationToken, R2(total), runId).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        // GL posting runs after the sub-ledger commit, as PHP does (epc_erp_gl_post_journal owns its own transaction).
        long glJournalId = 0;
        var rounded = R2(total);
        if (rounded > 0m)
        {
            await EnsureDepreciationAccountsAsync(connection, cancellationToken).ConfigureAwait(false);
            var expenseId = await CoaIdByCodeAsync(connection, ExpenseCode, cancellationToken).ConfigureAwait(false);
            var accumId = await CoaIdByCodeAsync(connection, AccumCode, cancellationToken).ConfigureAwait(false);
            if (expenseId > 0 && accumId > 0)
            {
                glJournalId = await _gl.PostJournalAsync(
                    connection,
                    new ErpGlJournalHeader
                    {
                        JournalDate = now,
                        Reference = "DEPR-" + period,
                        Description = "Fixed asset depreciation " + period,
                        SourceType = "adjustment",
                        SourceId = runId,
                    },
                    [
                        new ErpGlLine(expenseId, rounded, 0m, "Depreciation expense " + period),
                        new ErpGlLine(accumId, 0m, rounded, "Accumulated depreciation " + period),
                    ],
                    adminId,
                    cancellationToken).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_erp_fa_depreciation_runs` SET `gl_journal_id` = ? WHERE `id` = ?"), cancellationToken, glJournalId, runId).ConfigureAwait(false);
            }
        }

        return new ErpFaDepreciationRunResult(runId, rounded, assets.Count, glJournalId);
    }

    private void EnsureConfigured()
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("TenantRegistry DB is not configured.");
        }
    }

    private static async Task RefreshBookValueAsync(DbConnection connection, long assetId, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT `cost`, `accumulated_depreciation`, `salvage_value` FROM `epc_erp_fa_assets` WHERE `id` = @id LIMIT 1";
        var p = cmd.CreateParameter();
        p.ParameterName = "@id";
        p.Value = assetId;
        cmd.Parameters.Add(p);
        decimal cost, accum, salvage;
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            cost = reader.GetDecimal(0);
            accum = reader.GetDecimal(1);
            salvage = reader.GetDecimal(2);
        }

        var bv = Math.Max(salvage, cost - accum);
        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_erp_fa_assets` SET `book_value` = ? WHERE `id` = ?"), cancellationToken, R2(bv), assetId).ConfigureAwait(false);
    }

    private static async Task<HashSet<string>> ColumnsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SHOW COLUMNS FROM `epc_erp_fa_assets`";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            set.Add(reader.GetString(0));
        }

        return set;
    }

    private static Task<long> CoaIdByCodeAsync(DbConnection connection, string code, CancellationToken cancellationToken)
        => ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? AND `active` = 1 LIMIT 1"), cancellationToken, code);

    /// <summary>PHP <c>epc_erp_gl_ensure_depreciation_accounts</c>: system 5100/1550 rows when missing.</summary>
    private static async Task EnsureDepreciationAccountsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var (code, name, type, side, desc) in new[]
        {
            (ExpenseCode, "Depreciation expense", "expense", "debit", "Period depreciation of fixed assets"),
            (AccumCode, "Accumulated depreciation", "asset", "credit", "Contra-asset: cumulative depreciation on fixed assets"),
        })
        {
            if (await CoaIdByCodeAsync(connection, code, cancellationToken).ConfigureAwait(false) > 0)
            {
                continue;
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `epc_erp_coa_accounts` (`code`, `name`, `account_type`, `normal_side`, `description`, `system_flag`, `time_created`) VALUES (?, ?, ?, ?, ?, 1, ?)"),
                cancellationToken,
                code, name, type, side, desc, now).ConfigureAwait(false);
        }
    }

    /// <summary>PHP <c>epc_erp_fa_ensure_schema</c> (identical DDL; failures of ADD COLUMN are ignored as in PHP).</summary>
    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(connection, null,
            "CREATE TABLE IF NOT EXISTS `epc_erp_fa_categories` (`id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(32) NOT NULL, `name` varchar(255) NOT NULL,"
            + " `default_method` varchar(32) NOT NULL DEFAULT 'straight_line', `default_life_months` int(11) NOT NULL DEFAULT 60, `active` tinyint(1) NOT NULL DEFAULT 1,"
            + " PRIMARY KEY (`id`), UNIQUE KEY `x_code` (`code`)) ENGINE=InnoDB DEFAULT CHARSET=utf8", cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null,
            "CREATE TABLE IF NOT EXISTS `epc_erp_fa_assets` (`id` int(11) NOT NULL AUTO_INCREMENT, `asset_code` varchar(64) NOT NULL, `name` varchar(255) NOT NULL,"
            + " `category_id` int(11) NOT NULL DEFAULT 0, `acquisition_date` date NOT NULL, `cost` decimal(14,2) NOT NULL DEFAULT 0.00, `salvage_value` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `useful_life_months` int(11) NOT NULL DEFAULT 60, `depreciation_method` varchar(32) NOT NULL DEFAULT 'straight_line',"
            + " `accumulated_depreciation` decimal(14,2) NOT NULL DEFAULT 0.00, `book_value` decimal(14,2) NOT NULL DEFAULT 0.00, `location` varchar(255) DEFAULT NULL,"
            + " `tracking_id` varchar(128) DEFAULT NULL, `serial_no` varchar(128) DEFAULT NULL, `status` enum('active','disposed','fully_depreciated') NOT NULL DEFAULT 'active',"
            + " `opening_batch_id` int(11) NOT NULL DEFAULT 0, `note` text, `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `x_code` (`asset_code`),"
            + " KEY `x_cat` (`category_id`), KEY `x_status` (`status`)) ENGINE=InnoDB DEFAULT CHARSET=utf8", cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null,
            "CREATE TABLE IF NOT EXISTS `epc_erp_fa_depreciation_runs` (`id` int(11) NOT NULL AUTO_INCREMENT, `period_month` char(7) NOT NULL, `run_date` int(11) NOT NULL DEFAULT 0,"
            + " `admin_id` int(11) NOT NULL DEFAULT 0, `total_amount` decimal(14,2) NOT NULL DEFAULT 0.00, `note` varchar(255) DEFAULT NULL, PRIMARY KEY (`id`),"
            + " UNIQUE KEY `x_period` (`period_month`)) ENGINE=InnoDB DEFAULT CHARSET=utf8", cancellationToken).ConfigureAwait(false);
        await AddColumnIfMissingAsync(connection, "epc_erp_fa_depreciation_runs", "gl_journal_id", "int(11) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null,
            "CREATE TABLE IF NOT EXISTS `epc_erp_fa_depreciation_lines` (`id` int(11) NOT NULL AUTO_INCREMENT, `run_id` int(11) NOT NULL, `asset_id` int(11) NOT NULL,"
            + " `amount` decimal(14,2) NOT NULL DEFAULT 0.00, `accumulated_after` decimal(14,2) NOT NULL DEFAULT 0.00, `book_value_after` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " PRIMARY KEY (`id`), KEY `x_run` (`run_id`), KEY `x_asset` (`asset_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8", cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null,
            "CREATE TABLE IF NOT EXISTS `epc_erp_fa_tracking_log` (`id` int(11) NOT NULL AUTO_INCREMENT, `asset_id` int(11) NOT NULL, `event_type` varchar(32) NOT NULL,"
            + " `location` varchar(255) DEFAULT NULL, `note` text, `event_date` int(11) NOT NULL DEFAULT 0, `admin_id` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`),"
            + " KEY `x_asset` (`asset_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8", cancellationToken).ConfigureAwait(false);
        foreach (var (col, def) in ExtendedColumns)
        {
            await AddColumnIfMissingAsync(connection, "epc_erp_fa_assets", col, def, cancellationToken).ConfigureAwait(false);
        }

        if (await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_erp_fa_categories`", cancellationToken).ConfigureAwait(false) == 0)
        {
            foreach (var (c, n, m, l) in new[] { ("VEH", "Vehicles", "straight_line", 60), ("IT", "IT equipment", "declining_balance", 36), ("FURN", "Furniture & fixtures", "straight_line", 84), ("BLDG", "Buildings", "straight_line", 240) })
            {
                await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("INSERT INTO `epc_erp_fa_categories` (`code`,`name`,`default_method`,`default_life_months`) VALUES (?,?,?,?)"), cancellationToken, c, n, m, l).ConfigureAwait(false);
            }
        }
    }

    private static async Task AddColumnIfMissingAsync(DbConnection connection, string table, string column, string definition, CancellationToken cancellationToken)
    {
        var exists = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"), cancellationToken, table, column).ConfigureAwait(false);
        if (exists == 0)
        {
            await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `" + table + "` ADD `" + column + "` " + definition, cancellationToken).ConfigureAwait(false);
        }
    }
}
