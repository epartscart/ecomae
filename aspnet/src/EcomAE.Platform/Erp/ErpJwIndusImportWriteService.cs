using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace EcomAE.Platform.Erp;

public sealed record ErpJwIndusImportFile(string TemplateKey, string Csv);

public sealed record ErpJwIndusImportIssue(string Template, int Line, string Message);

public sealed record ErpJwIndusImportOutcome(
    bool Ok,
    bool DryRun,
    string Code,
    string Message,
    int Rows,
    int Inserted,
    int Updated,
    int Skipped,
    IReadOnlyList<ErpJwIndusImportIssue> Issues,
    IReadOnlyList<string> Notes);

/// <summary>
/// Guarded INDUS → ecomae jewellery CSV loader. Dry-run by default; writes only
/// with confirmWrites and only into a sandbox database (<c>*_sandbox</c>).
/// Every imported row is recorded in <c>ecomae_jw_indus_import_registry</c>
/// (source_ref → row id) so re-imports skip duplicates, child files resolve
/// their parent voucher, and Clear removes only imported rows. After the
/// writes it moves stock weight/value per voucher line and posts the GL so
/// stock, metal and trial-balance reports agree.
/// </summary>
public interface IErpJwIndusImportWriteService
{
    Task<ErpJwIndusImportOutcome> ImportAsync(IReadOnlyList<ErpJwIndusImportFile> files, bool confirmWrites, string actor, CancellationToken cancellationToken = default);

    Task<ErpJwIndusImportOutcome> LoadSamplesAsync(bool confirmWrites, string actor, CancellationToken cancellationToken = default);

    Task<string> ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class ErpJwIndusImportWriteService : IErpJwIndusImportWriteService
{
    public const string RegistryTable = "ecomae_jw_indus_import_registry";
    private const string SampleResourcePrefix = "EcomAE.Platform.IndusSamples.";

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpJewelleryGlPostWriteService _glPost;
    private readonly IErpGlPostingService _gl;

    public ErpJwIndusImportWriteService(
        IErpWriteConnectionFactory connections,
        IErpJewelleryGlPostWriteService glPost,
        IErpGlPostingService gl)
    {
        _connections = connections;
        _glPost = glPost;
        _gl = gl;
    }

    /// <summary>Only sandbox (UAT) databases — and throwaway test schemas — accept INDUS imports.</summary>
    public static bool IsSandboxDatabase(string? database)
    {
        var name = (database ?? string.Empty).Trim();
        return name.EndsWith("_sandbox", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("ecomae_cpw_", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<(string Key, string Csv)> SampleFiles()
    {
        var assembly = typeof(ErpJwIndusImportWriteService).Assembly;
        var list = new List<(int Step, string Key, string Csv)>();
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(SampleResourcePrefix, StringComparison.Ordinal) || !name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var file = name[SampleResourcePrefix.Length..^4];
            var underscore = file.IndexOf('_', StringComparison.Ordinal);
            if (underscore <= 0)
            {
                continue;
            }

            var template = ErpJwIndusImportSpec.Find(file[(underscore + 1)..]);
            if (template is null)
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            list.Add((template.Step, template.Key, reader.ReadToEnd()));
        }

        return list.OrderBy(x => x.Step).Select(x => (x.Key, x.Csv)).ToList();
    }

    public Task<ErpJwIndusImportOutcome> LoadSamplesAsync(bool confirmWrites, string actor, CancellationToken cancellationToken = default)
        => ImportCoreAsync(SampleFiles().Select(s => new ErpJwIndusImportFile(s.Key, s.Csv)).ToList(), confirmWrites, actor, true, cancellationToken);

    public Task<ErpJwIndusImportOutcome> ImportAsync(IReadOnlyList<ErpJwIndusImportFile> files, bool confirmWrites, string actor, CancellationToken cancellationToken = default)
        => ImportCoreAsync(files, confirmWrites, actor, false, cancellationToken);

    private async Task<ErpJwIndusImportOutcome> ImportCoreAsync(
        IReadOnlyList<ErpJwIndusImportFile> files,
        bool confirmWrites,
        string actor,
        bool samples,
        CancellationToken cancellationToken)
    {
        var issues = new List<ErpJwIndusImportIssue>();
        var notes = new List<string>();
        if (files.Count == 0)
        {
            return Fail("empty", "No CSV files supplied.", issues, notes, !confirmWrites);
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "TenantRegistry DB is not configured.", issues, notes, !confirmWrites);
        }

        var batches = new List<(ErpJwIndusTemplate Template, List<ErpJwIndusCsv.Row> Rows)>();
        foreach (var file in files)
        {
            var template = ErpJwIndusImportSpec.Find(file.TemplateKey);
            if (template is null)
            {
                issues.Add(new(file.TemplateKey, 0, "Unknown template. Use one of: " + string.Join(", ", ErpJwIndusImportSpec.Templates.Select(t => t.Key))));
                continue;
            }

            var rows = ErpJwIndusCsv.Validate(template, file.Csv, issues);
            batches.Add((template, rows));
        }

        batches = batches.OrderBy(b => b.Template.Step).ToList();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var database = Convert.ToString(await ErpDb.ScalarAsync(connection, null, "SELECT DATABASE()", cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        if (!IsSandboxDatabase(database))
        {
            return Fail("sandbox_only", "INDUS imports run only on the sandbox (UAT) database — this tenant database is live. Use the -sandbox host.", issues, notes, !confirmWrites);
        }

        await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureRegistryAsync(connection, cancellationToken).ConfigureAwait(false);
        await _gl.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpAutoPartsGl.EnsureCoaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpJewelleryGl.EnsureCoaAsync(connection, cancellationToken).ConfigureAwait(false);
        var companyId = await ErpLazySchema.ResolveActiveCompanyIdAsync(connection, 0, cancellationToken).ConfigureAwait(false);

        // Duplicate + parent checks against what is already imported.
        var pendingParents = new HashSet<string>(StringComparer.Ordinal);
        var total = 0;
        var skipped = 0;
        foreach (var (template, rows) in batches)
        {
            foreach (var row in rows)
            {
                total++;
                if (pendingParents.Contains(template.Key + "|" + row.SourceRef))
                {
                    issues.Add(new(template.Key, row.Line, "Duplicate source_ref '" + row.SourceRef + "' across uploaded files."));
                    continue;
                }

                if (await RegistryIdAsync(connection, null, template.Key, row.SourceRef, cancellationToken).ConfigureAwait(false) > 0)
                {
                    row.AlreadyImported = true;
                    skipped++;
                    continue;
                }

                if (template.ParentKey is not null
                    && !pendingParents.Contains(template.ParentKey + "|" + row.ParentRef)
                    && await RegistryRowAsync(connection, null, template.ParentKey, row.ParentRef, cancellationToken).ConfigureAwait(false) <= 0)
                {
                    issues.Add(new(template.Key, row.Line, "parent_ref '" + row.ParentRef + "' not found in " + template.ParentKey + " — import the " + template.ParentKey + " file first."));
                }

                pendingParents.Add(template.Key + "|" + row.SourceRef);
            }

            if (template.Key == "coa")
            {
                var fresh = rows.Where(r => !r.AlreadyImported).ToList();
                var dr = fresh.Sum(r => r.Dec("opening_debit"));
                var cr = fresh.Sum(r => r.Dec("opening_credit"));
                if (dr != cr)
                {
                    issues.Add(new("coa", 0, "Opening trial balance does not balance: debit " + dr.ToString("N2", CultureInfo.InvariantCulture) + " vs credit " + cr.ToString("N2", CultureInfo.InvariantCulture) + "."));
                }
            }
        }

        if (issues.Count > 0)
        {
            return new ErpJwIndusImportOutcome(false, !confirmWrites, "validation", issues.Count + " validation issue(s) — nothing written. Fix the CSV and retry.", total, 0, 0, skipped, issues, notes);
        }

        if (!confirmWrites)
        {
            return new ErpJwIndusImportOutcome(true, true, "dry_run", "Dry run OK — " + (total - skipped) + " row(s) ready to import, " + skipped + " already imported. Re-submit with confirmWrites to write into " + database + ".", total, 0, 0, skipped, issues, notes);
        }

        var inserted = 0;
        var updated = 0;
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                foreach (var (template, rows) in batches)
                {
                    foreach (var row in rows.Where(r => !r.AlreadyImported))
                    {
                        var (rowId, isInsert) = await WriteRowAsync(connection, transaction, template, row, companyId, actor, cancellationToken).ConfigureAwait(false);
                        if (isInsert) inserted++; else updated++;
                        var postFlag = template.Key == "metal_stock" ? (row.Flag("post_opening", true) ? 1 : 0) : 1;
                        await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional(
                            "INSERT INTO `" + RegistryTable + "` (`template`,`source_ref`,`table_name`,`row_id`,`inserted`,`post_flag`,`posted`,`amount_dr`,`amount_cr`,`ref_date`,`created_by`) VALUES (?,?,?,?,?,?,0,?,?,?,?)"),
                            cancellationToken, template.Key, row.SourceRef, template.Table, rowId, isInsert ? 1 : 0, postFlag,
                            row.Dec("opening_debit"), row.Dec("opening_credit"), row.DateText("opening_date"), actor).ConfigureAwait(false);
                    }
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        if (samples)
        {
            await SeedPrintProfileAsync(connection, companyId, cancellationToken).ConfigureAwait(false);
        }

        notes.AddRange(await FinalizeAsync(connection, companyId, cancellationToken).ConfigureAwait(false));
        var message = "Imported " + inserted + " new and " + updated + " updated row(s) into " + database + (skipped > 0 ? " (" + skipped + " already imported, skipped)" : "") + ". " + string.Join(" ", notes);
        return new ErpJwIndusImportOutcome(true, false, "ok", message.Trim(), total, inserted, updated, skipped, issues, notes);
    }

    private static ErpJwIndusImportOutcome Fail(string code, string message, List<ErpJwIndusImportIssue> issues, List<string> notes, bool dryRun)
        => new(false, dryRun, code, message, 0, 0, 0, 0, issues, notes);

    private static async Task<(long Id, bool Inserted)> WriteRowAsync(
        DbConnection connection,
        DbTransaction transaction,
        ErpJwIndusTemplate template,
        ErpJwIndusCsv.Row row,
        long companyId,
        string actor,
        CancellationToken ct)
    {
        if (template.Key == "coa")
        {
            return await WriteCoaAsync(connection, transaction, row, ct).ConfigureAwait(false);
        }

        var cols = new List<string>();
        var vals = new List<object?>();
        if (template.HasCompany)
        {
            cols.Add("company_id");
            vals.Add(companyId);
        }

        if (template.ParentKey is not null && template.ParentColumn is not null)
        {
            cols.Add(template.ParentColumn);
            vals.Add(await RegistryRowAsync(connection, transaction, template.ParentKey, row.ParentRef, ct).ConfigureAwait(false));
        }

        foreach (var column in template.Columns.Where(c => c.Column.Length > 0))
        {
            if (!row.Values.TryGetValue(column.Header, out var value))
            {
                continue;
            }

            if (template.Key == "vouchers" && column.Header == "voc_type")
            {
                var code = (string)value!;
                cols.Add("voc_type");
                vals.Add(ErpJwIndusImportSpec.VocTypeMap.TryGetValue(code, out var mapped) ? mapped : code.ToUpperInvariant());
                cols.Add("source_voc_type");
                vals.Add(code.ToUpperInvariant());
                continue;
            }

            cols.Add(column.Column);
            vals.Add(value);
        }

        if ((template.Key is "vouchers" or "petty_cash") && !row.Values.ContainsKey("status"))
        {
            cols.Add("status");
            vals.Add("posted");
        }

        if (template.Key == "vouchers")
        {
            cols.Add("authorized");
            vals.Add(1);
        }

        if (template.CreatedByColumn is not null)
        {
            cols.Add(template.CreatedByColumn);
            vals.Add(Truncate("INDUS-IMPORT " + actor, 40));
        }

        var maxBefore = await ErpDb.LongAsync(connection, transaction, "SELECT IFNULL(MAX(`id`),0) FROM `" + template.Table + "`", ct).ConfigureAwait(false);
        var sql = new StringBuilder("INSERT INTO `").Append(template.Table).Append("` (")
            .Append(string.Join(",", cols.Select(c => "`" + c + "`")))
            .Append(") VALUES (").Append(string.Join(",", cols.Select(_ => "?"))).Append(')');
        if (template.Upsert)
        {
            sql.Append(" ON DUPLICATE KEY UPDATE `id`=LAST_INSERT_ID(`id`)");
            foreach (var c in cols.Where(c => c is not "company_id" && c != template.CreatedByColumn))
            {
                sql.Append(",`").Append(c).Append("`=VALUES(`").Append(c).Append("`)");
            }
        }

        await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional(sql.ToString()), ct, vals.ToArray()).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, transaction, ct).ConfigureAwait(false);
        return (id, id > maxBefore);
    }

    private static async Task<(long Id, bool Inserted)> WriteCoaAsync(DbConnection connection, DbTransaction transaction, ErpJwIndusCsv.Row row, CancellationToken ct)
    {
        var code = row.Text("code");
        var existing = await ErpDb.LongAsync(connection, transaction, ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code`=? LIMIT 1"), ct, code).ConfigureAwait(false);
        var type = row.Text("account_type").ToLowerInvariant();
        var side = row.Text("normal_side").ToLowerInvariant();
        if (side is not ("debit" or "credit"))
        {
            side = type is "asset" or "expense" ? "debit" : "credit";
        }

        var indus = row.Text("indus_ac_code");
        var description = "INDUS import" + (indus.Length > 0 ? " — INDUS A/C " + indus : "");
        if (existing > 0)
        {
            return (existing, false);
        }

        await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional(
            "INSERT INTO `epc_erp_coa_accounts` (`code`,`name`,`account_type`,`normal_side`,`description`,`system_flag`,`time_created`) VALUES (?,?,?,?,?,0,?)"),
            ct, code, row.Text("name"), type, side, description, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        return (await ErpDb.LastInsertIdAsync(connection, transaction, ct).ConfigureAwait(false), true);
    }

    /// <summary>Opening journals, stock movement per voucher line, then the jewellery GL bridge.</summary>
    private async Task<List<string>> FinalizeAsync(DbConnection connection, long companyId, CancellationToken ct)
    {
        var notes = new List<string>();

        // 1. Opening trial balance (one balanced journal per import run).
        var coaRows = await RegistryPendingAsync(connection, "coa", ct).ConfigureAwait(false);
        if (coaRows.Count > 0)
        {
            var recipe = new List<ErpAutoPartsIfrs.JournalLine>();
            long date = 0;
            foreach (var r in coaRows)
            {
                var code = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `code` FROM `epc_erp_coa_accounts` WHERE `id`=?"), ct, r.RowId).ConfigureAwait(false) ?? "";
                if (r.Dr != 0 || r.Cr != 0)
                {
                    recipe.Add(new(code, r.Dr, r.Cr, "INDUS opening balance"));
                }

                if (date == 0 && DateTime.TryParse(r.RefDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d))
                {
                    date = new DateTimeOffset(d, TimeSpan.Zero).ToUnixTimeSeconds();
                }
            }

            if (recipe.Count > 0)
            {
                var reference = "JW-OPEN-TB-" + coaRows[0].RegistryId.ToString(CultureInfo.InvariantCulture);
                var jid = await ErpAutoPartsGl.PostedJournalIdAsync(connection, reference, ct).ConfigureAwait(false);
                if (jid <= 0)
                {
                    jid = await ErpAutoPartsGl.PostAsync(_gl, connection, null, 0, companyId, 0, reference, "INDUS opening trial balance", recipe, date, ct).ConfigureAwait(false);
                    notes.Add(jid > 0 ? "Opening trial balance posted (" + reference + ")." : "Opening trial balance NOT posted — check account codes; it is retried on the next import.");
                }

                if (jid > 0)
                {
                    await MarkPostedAsync(connection, coaRows.Select(r => r.RegistryId), ct).ConfigureAwait(false);
                }
            }
            else
            {
                await MarkPostedAsync(connection, coaRows.Select(r => r.RegistryId), ct).ConfigureAwait(false);
            }
        }

        // 2. Opening stock value → Dr 1300 inventory / Cr 3900 opening balance equity.
        var stockRows = await RegistryPendingAsync(connection, "metal_stock", ct).ConfigureAwait(false);
        var openingPosted = 0;
        foreach (var r in stockRows)
        {
            var value = await ErpDb.DecimalAsync(connection, null, ErpDb.Positional("SELECT `stock_value` FROM `epc_jewel_metal_stock` WHERE `id`=?"), ct, r.RowId).ConfigureAwait(false);
            if (r.PostFlag && value > 0)
            {
                var reference = "JW-OPEN-STK-" + r.RowId.ToString(CultureInfo.InvariantCulture);
                if (await ErpAutoPartsGl.PostedJournalIdAsync(connection, reference, ct).ConfigureAwait(false) <= 0
                    && await ErpAutoPartsGl.PostAsync(_gl, connection, null, 0, companyId, r.RowId, reference, "INDUS opening stock",
                        ErpJewelleryGl.OpeningStock(value), OpeningDate(r.RefDate), ct).ConfigureAwait(false) > 0)
                {
                    openingPosted++;
                }
            }
        }

        await MarkPostedAsync(connection, stockRows.Select(r => r.RegistryId), ct).ConfigureAwait(false);
        if (openingPosted > 0)
        {
            notes.Add("Opening stock posted to GL 1300 for " + openingPosted + " stock code(s).");
        }

        // 3. Stock weight/value movement per voucher line.
        // Keyed on line rows so lines imported after their header still move stock exactly once.
        var lineRows = await RegistryPendingAsync(connection, "voucher_lines", ct).ConfigureAwait(false);
        var moved = 0;
        var byVoucher = new Dictionary<long, HashSet<long>>();
        foreach (var r in lineRows)
        {
            var voucherId = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `voucher_id` FROM `epc_jewel_voucher_lines` WHERE `id`=?"), ct, r.RowId).ConfigureAwait(false);
            if (voucherId <= 0) continue;
            if (!byVoucher.TryGetValue(voucherId, out var set)) byVoucher[voucherId] = set = [];
            set.Add(r.RowId);
        }

        foreach (var (voucherId, ids) in byVoucher)
        {
            moved += await ErpJewelleryGl.ApplyStockAsync(connection, voucherId, notes, ct, ids).ConfigureAwait(false);
        }

        await MarkPostedAsync(connection, lineRows.Select(r => r.RegistryId), ct).ConfigureAwait(false);
        await MarkPostedAsync(connection, (await RegistryPendingAsync(connection, "vouchers", ct).ConfigureAwait(false)).Select(r => r.RegistryId), ct).ConfigureAwait(false);
        if (moved > 0)
        {
            notes.Add("Stock moved for " + moved + " voucher line(s) (weight + value, COGS at average cost).");
        }

        // 4. GL for every voucher / petty cash row not yet journaled.
        notes.Add(await _glPost.PostAsync(0, null, ct).ConfigureAwait(false));
        return notes;
    }

    private static long OpeningDate(string? text)
        => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d)
            ? new DateTimeOffset(d, TimeSpan.Zero).ToUnixTimeSeconds()
            : 0;

    public async Task<string> ClearAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "TenantRegistry DB is not configured.";
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var database = Convert.ToString(await ErpDb.ScalarAsync(connection, null, "SELECT DATABASE()", cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        if (!IsSandboxDatabase(database))
        {
            return "Refused — INDUS import data can only be cleared on the sandbox database.";
        }

        await EnsureRegistryAsync(connection, cancellationToken).ConfigureAwait(false);
        var rows = new List<(long Id, string Template, string Table, long RowId, bool Inserted)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id`,`template`,`table_name`,`row_id`,`inserted` FROM `" + RegistryTable + "` ORDER BY `id` DESC";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3), reader.GetInt32(4) == 1));
            }
        }

        var refs = new List<string>();
        foreach (var r in rows)
        {
            var id = r.RowId.ToString(CultureInfo.InvariantCulture);
            switch (r.Template)
            {
                case "vouchers": refs.Add("JW-V-" + id + "-%"); break;
                case "petty_cash": refs.Add("JW-PC-" + id); break;
                case "metal_stock": refs.Add("JW-OPEN-STK-" + id); break;
                case "coa": refs.Add("JW-OPEN-TB-" + r.Id.ToString(CultureInfo.InvariantCulture)); break;
            }
        }

        var journals = 0;
        foreach (var pattern in refs.Distinct(StringComparer.Ordinal))
        {
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(
                "DELETE l FROM `epc_erp_gl_lines` l JOIN `epc_erp_gl_journals` j ON j.`id`=l.`journal_id` WHERE j.`reference` LIKE ?"), cancellationToken, pattern).ConfigureAwait(false);
            journals += await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `epc_erp_gl_journals` WHERE `reference` LIKE ?"), cancellationToken, pattern).ConfigureAwait(false);
        }

        var deleted = 0;
        foreach (var r in rows.Where(r => r.Inserted))
        {
            if (r.Template == "coa")
            {
                var used = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_gl_lines` WHERE `coa_id`=?"), cancellationToken, r.RowId).ConfigureAwait(false);
                if (used > 0) continue;
            }

            deleted += await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `" + r.Table + "` WHERE `id`=?"), cancellationToken, r.RowId).ConfigureAwait(false);
        }

        await ErpDb.ExecuteAsync(connection, null, "DELETE FROM `" + RegistryTable + "`", cancellationToken).ConfigureAwait(false);
        return "Removed " + deleted + " imported INDUS row(s) and " + journals + " GL journal(s) from " + database + ". Stock codes that existed before the import keep their current balances.";
    }

    private static async Task EnsureRegistryAsync(DbConnection connection, CancellationToken ct)
        => await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `" + RegistryTable + "` ("
            + "`id` BIGINT NOT NULL AUTO_INCREMENT,"
            + "`template` VARCHAR(40) NOT NULL,"
            + "`source_ref` VARCHAR(80) NOT NULL,"
            + "`table_name` VARCHAR(64) NOT NULL,"
            + "`row_id` BIGINT NOT NULL,"
            + "`inserted` TINYINT(1) NOT NULL DEFAULT 1,"
            + "`post_flag` TINYINT(1) NOT NULL DEFAULT 1,"
            + "`posted` TINYINT(1) NOT NULL DEFAULT 0,"
            + "`amount_dr` DECIMAL(16,2) NOT NULL DEFAULT 0,"
            + "`amount_cr` DECIMAL(16,2) NOT NULL DEFAULT 0,"
            + "`ref_date` VARCHAR(10) NOT NULL DEFAULT '',"
            + "`created_by` VARCHAR(80) NOT NULL DEFAULT '',"
            + "`created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,"
            + "PRIMARY KEY (`id`), UNIQUE KEY `ux_src` (`template`,`source_ref`), KEY `x_table` (`table_name`,`row_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4", ct).ConfigureAwait(false);

    private static Task<long> RegistryIdAsync(DbConnection c, DbTransaction? t, string template, string sourceRef, CancellationToken ct)
        => ErpDb.LongAsync(c, t, ErpDb.Positional("SELECT `id` FROM `" + RegistryTable + "` WHERE `template`=? AND `source_ref`=? LIMIT 1"), ct, template, sourceRef);

    private static Task<long> RegistryRowAsync(DbConnection c, DbTransaction? t, string template, string sourceRef, CancellationToken ct)
        => ErpDb.LongAsync(c, t, ErpDb.Positional("SELECT `row_id` FROM `" + RegistryTable + "` WHERE `template`=? AND `source_ref`=? LIMIT 1"), ct, template, sourceRef);

    private sealed record Pending(long RegistryId, long RowId, bool PostFlag, decimal Dr, decimal Cr, string RefDate);

    private static async Task<List<Pending>> RegistryPendingAsync(DbConnection c, string template, CancellationToken ct)
    {
        var list = new List<Pending>();
        await using var command = c.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `id`,`row_id`,`post_flag`,`amount_dr`,`amount_cr`,`ref_date` FROM `" + RegistryTable + "` WHERE `template`=? AND `posted`=0 ORDER BY `id`");
        ErpDb.AddParameters(command, template);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt32(2) == 1, reader.GetDecimal(3), reader.GetDecimal(4), reader.GetString(5)));
        }

        return list;
    }

    private static async Task MarkPostedAsync(DbConnection c, IEnumerable<long> ids, CancellationToken ct)
    {
        foreach (var id in ids)
        {
            await ErpDb.ExecuteAsync(c, null, ErpDb.Positional("UPDATE `" + RegistryTable + "` SET `posted`=1 WHERE `id`=?"), ct, id).ConfigureAwait(false);
        }
    }

    /// <summary>Sandbox samples carry the INDUS print header (company name + TRN) unless the tenant already set one.</summary>
    private static async Task SeedPrintProfileAsync(DbConnection c, long companyId, CancellationToken ct)
    {
        foreach (var (key, value) in new[]
        {
            (ErpJwVoucherPrint.CompanyNameKey, "Indus Jewellers L L C"),
            (ErpJwVoucherPrint.TrnKey, "100039551500003"),
        })
        {
            await ErpDb.TryExecuteAsync(c,
                "INSERT IGNORE INTO `epc_org_company_settings` (`company_id`,`setting_key`,`setting_value`) VALUES ("
                + companyId.ToString(CultureInfo.InvariantCulture) + ",'" + key + "','" + value + "')", ct).ConfigureAwait(false);
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

/// <summary>CSV parsing + per-template validation (pure, unit-testable).</summary>
public static class ErpJwIndusCsv
{
    public sealed class Row
    {
        public int Line { get; init; }
        public string SourceRef { get; init; } = "";
        public string ParentRef { get; init; } = "";
        public Dictionary<string, object?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool AlreadyImported { get; set; }

        public string Text(string header) => Values.TryGetValue(header, out var v) && v is not null ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? "" : "";

        public decimal Dec(string header) => Values.TryGetValue(header, out var v) && v is decimal d ? d : 0m;

        public bool Flag(string header, bool fallback) => Values.TryGetValue(header, out var v) && v is int i ? i == 1 : fallback;

        public string DateText(string header) => Text(header);
    }

    private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss"];

    public static List<string[]> Parse(string csv)
    {
        var rows = new List<string[]>();
        var text = (csv ?? string.Empty).TrimStart('\uFEFF');
        var field = new StringBuilder();
        var current = new List<string>();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = false;
                }
                else field.Append(ch);
                continue;
            }

            switch (ch)
            {
                case '"': quoted = true; break;
                case ',': current.Add(field.ToString()); field.Clear(); break;
                case '\r': break;
                case '\n':
                    current.Add(field.ToString()); field.Clear();
                    if (current.Any(c => c.Length > 0)) rows.Add(current.ToArray());
                    current.Clear();
                    break;
                default: field.Append(ch); break;
            }
        }

        current.Add(field.ToString());
        if (current.Any(c => c.Length > 0)) rows.Add(current.ToArray());
        return rows;
    }

    public static List<Row> Validate(ErpJwIndusTemplate template, string csv, List<ErpJwIndusImportIssue> issues)
    {
        var result = new List<Row>();
        var table = Parse(csv);
        if (table.Count == 0)
        {
            issues.Add(new(template.Key, 0, "File is empty."));
            return result;
        }

        var headers = table[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
        {
            if (headers[i].Length > 0 && !index.ContainsKey(headers[i])) index[headers[i]] = i;
        }

        var known = new HashSet<string>(template.Headers, StringComparer.OrdinalIgnoreCase);
        foreach (var h in headers.Where(h => h.Length > 0 && !known.Contains(h)))
        {
            issues.Add(new(template.Key, 1, "Unknown column '" + h + "' — not in the " + template.Key + " template (it would be lost)."));
        }

        var mustHave = new List<string> { ErpJwIndusImportSpec.SourceRefHeader };
        if (template.ParentKey is not null) mustHave.Add(ErpJwIndusImportSpec.ParentRefHeader);
        mustHave.AddRange(template.Columns.Where(c => c.Required).Select(c => c.Header));
        foreach (var h in mustHave.Where(h => !index.ContainsKey(h)))
        {
            issues.Add(new(template.Key, 1, "Missing required column '" + h + "'."));
        }

        if (mustHave.Any(h => !index.ContainsKey(h)))
        {
            return result;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var r = 1; r < table.Count; r++)
        {
            var cells = table[r];
            var line = r + 1;
            string Cell(string h) => index.TryGetValue(h, out var i) && i < cells.Length ? cells[i].Trim() : "";

            var sourceRef = Cell(ErpJwIndusImportSpec.SourceRefHeader);
            if (sourceRef.Length == 0) { issues.Add(new(template.Key, line, "source_ref is empty.")); continue; }
            if (sourceRef.Length > 80) { issues.Add(new(template.Key, line, "source_ref longer than 80 characters.")); continue; }
            if (!seen.Add(sourceRef)) { issues.Add(new(template.Key, line, "Duplicate source_ref '" + sourceRef + "' in file.")); continue; }

            var row = new Row { Line = line, SourceRef = sourceRef, ParentRef = Cell(ErpJwIndusImportSpec.ParentRefHeader) };
            if (template.ParentKey is not null && row.ParentRef.Length == 0)
            {
                issues.Add(new(template.Key, line, "parent_ref is empty."));
                continue;
            }

            foreach (var column in template.Columns)
            {
                var raw = Cell(column.Header);
                if (raw.Length == 0)
                {
                    if (column.Required) issues.Add(new(template.Key, line, column.Header + " is required (" + column.IndusField + ")."));
                    continue;
                }

                switch (column.Type)
                {
                    case ErpJwIndusColType.Text:
                        if (column.MaxLength > 0 && raw.Length > column.MaxLength)
                            issues.Add(new(template.Key, line, column.Header + " is longer than " + column.MaxLength + " characters."));
                        else row.Values[column.Header] = raw;
                        break;
                    case ErpJwIndusColType.Decimal:
                        if (decimal.TryParse(raw.Replace(",", "", StringComparison.Ordinal), NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) row.Values[column.Header] = d;
                        else issues.Add(new(template.Key, line, column.Header + " '" + raw + "' is not a number."));
                        break;
                    case ErpJwIndusColType.Int:
                        if (int.TryParse(raw.Replace(",", "", StringComparison.Ordinal), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) row.Values[column.Header] = n;
                        else issues.Add(new(template.Key, line, column.Header + " '" + raw + "' is not a whole number."));
                        break;
                    case ErpJwIndusColType.Date:
                        if (DateTime.TryParseExact(raw, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) row.Values[column.Header] = dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        else issues.Add(new(template.Key, line, column.Header + " '" + raw + "' is not a date (use dd/MM/yyyy)."));
                        break;
                    case ErpJwIndusColType.Flag:
                        var f = raw.ToUpperInvariant();
                        if (f is "Y" or "YES" or "1" or "TRUE") row.Values[column.Header] = 1;
                        else if (f is "N" or "NO" or "0" or "FALSE") row.Values[column.Header] = 0;
                        else issues.Add(new(template.Key, line, column.Header + " '" + raw + "' must be Y or N."));
                        break;
                }
            }

            if (template.Key == "vouchers" && row.Values.TryGetValue("voc_type", out var vt) && vt is string code
                && !ErpJwIndusImportSpec.VocTypeMap.ContainsKey(code))
            {
                issues.Add(new(template.Key, line, "voc_type '" + code + "' is not mapped. Known INDUS codes: " + string.Join(", ", ErpJwIndusImportSpec.VocTypeMap.Keys)));
            }

            if (template.Key == "vouchers" && row.Values.TryGetValue("gl_mode", out var gm) && gm is string mode && mode is not ("post" or "history"))
            {
                issues.Add(new(template.Key, line, "gl_mode must be post or history."));
            }

            if (template.Key == "coa" && row.Values.TryGetValue("account_type", out var at) && at is string type
                && type.ToLowerInvariant() is not ("asset" or "liability" or "equity" or "revenue" or "expense"))
            {
                issues.Add(new(template.Key, line, "account_type must be asset, liability, equity, revenue or expense."));
            }

            result.Add(row);
        }

        return result;
    }
}
