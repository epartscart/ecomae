using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// One read model for the fit-out workspace (/erp/fitout/workspace). Every list
/// honours the top project + period bar: project_id on the row and the
/// YYYY-MM period on the row's date column. "All projects" keeps the filter empty.
/// </summary>
public interface IErpFitOutWorkspaceReadService
{
    Task<ErpFitOutWorkspaceSnapshot> LoadAsync(
        long projectId,
        string? period,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutRow(
    long Id,
    string Kind,
    string Code,
    string Name,
    string Status,
    decimal Amount,
    decimal Amount2,
    string Detail);

public sealed record ErpFitOutProject(
    long Id,
    string Code,
    string Name,
    decimal ContractValue,
    decimal BudgetCost,
    string Status);

public sealed record ErpFitOutWorkspaceKpis(
    int Projects,
    decimal ContractValue,
    decimal Committed,
    decimal Actual,
    decimal Certified,
    decimal Wip,
    decimal ProfitLoss,
    int PendingApprovals);

public sealed record ErpFitOutWorkspaceSnapshot(
    IReadOnlyList<ErpFitOutProject> Projects,
    ErpFitOutWorkspaceKpis Kpis,
    IReadOnlyList<ErpFitOutRow> CostCodes,
    IReadOnlyList<ErpFitOutRow> Estimates,
    IReadOnlyList<ErpFitOutRow> BoqLines,
    IReadOnlyList<ErpFitOutRow> Quotations,
    IReadOnlyList<ErpFitOutRow> Contracts,
    IReadOnlyList<ErpFitOutRow> Budgets,
    IReadOnlyList<ErpFitOutRow> CostLedger,
    IReadOnlyList<ErpFitOutRow> MaterialMovements,
    IReadOnlyList<ErpFitOutRow> ProcurementLinks,
    IReadOnlyList<ErpFitOutRow> ThreeWayMatches,
    IReadOnlyList<ErpFitOutRow> Claims,
    IReadOnlyList<ErpFitOutRow> Subcontracts,
    IReadOnlyList<ErpFitOutRow> SiteRecords,
    IReadOnlyList<ErpFitOutRow> Recognitions,
    IReadOnlyList<ErpFitOutRow> Allocations,
    string Message);

public sealed class ErpFitOutWorkspaceReadService : IErpFitOutWorkspaceReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutWorkspaceReadService(IErpWriteConnectionFactory connections)
        => _connections = connections;

    public async Task<ErpFitOutWorkspaceSnapshot> LoadAsync(
        long projectId,
        string? period,
        CancellationToken cancellationToken = default)
    {
        var empty = new ErpFitOutWorkspaceKpis(0, 0, 0, 0, 0, 0, 0, 0);
        if (!_connections.IsConfigured)
        {
            return new ErpFitOutWorkspaceSnapshot(
                [], empty, [], [], [], [], [], [], [], [], [], [], [], [], [], [], [],
                "TenantRegistry DB is not configured.");
        }

        var periodIso = NormalizePeriod(period);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var message = string.Empty;
        try
        {
            var projects = await LoadProjectsAsync(connection, cancellationToken).ConfigureAwait(false);
            var costCodes = await LoadRowsAsync(
                connection,
                """
                SELECT c.`id`, 'cost_code', c.`code`, c.`name`, c.`cost_type`, 0, 0,
                       CONCAT('project ', c.`project_id`, IF(c.`parent_id`>0, CONCAT(' · parent ', c.`parent_id`), ''))
                FROM `ecomae_fitout_cost_codes` c
                """,
                "c.`project_id`", null, projectId, periodIso, cancellationToken).ConfigureAwait(false);
            var estimates = await LoadRowsAsync(
                connection,
                """
                SELECT e.`id`, 'estimate', e.`code`, e.`title`, e.`status`, e.`markup_percent`, e.`revision`,
                       CONCAT('project ', e.`project_id`)
                FROM `ecomae_fitout_estimates` e
                """,
                "e.`project_id`", null, projectId, periodIso, cancellationToken).ConfigureAwait(false);
            var boqLines = await LoadRowsAsync(
                connection,
                """
                SELECT b.`id`, 'boq_line', IFNULL(b.`section`,''), b.`description`, IFNULL(b.`cost_type`,'material'),
                       b.`quantity`*b.`unit_rate`, b.`quantity`,
                       CONCAT('estimate ', b.`estimate_id`, ' · unit rate ', b.`unit_rate`)
                FROM `ecomae_fitout_boq_lines` b
                INNER JOIN `ecomae_fitout_estimates` e ON e.`id` = b.`estimate_id`
                """,
                "e.`project_id`", null, projectId, periodIso, cancellationToken).ConfigureAwait(false);
            var quotations = await LoadRowsAsync(
                connection,
                """
                SELECT q.`id`, 'quotation', q.`code`, q.`title`, q.`status`, q.`revision`, 0,
                       CONCAT('estimate ', q.`estimate_id`, IFNULL(CONCAT(' · expires ', q.`expires_on`),''))
                FROM `ecomae_fitout_quotations` q
                INNER JOIN `ecomae_fitout_estimates` e ON e.`id` = q.`estimate_id`
                """,
                "e.`project_id`", null, projectId, periodIso, cancellationToken).ConfigureAwait(false);
            var contracts = await LoadRowsAsync(
                connection,
                """
                SELECT t.`contract_id`, 'contract_terms',
                       CONCAT('ADV ', t.`advance_percent`, '%'),
                       CONCAT('Contract ', t.`contract_id`), 'active',
                       t.`retention_percent`, t.`warranty_months`,
                       'advance % · retention % · warranty months'
                FROM `ecomae_fitout_contract_terms` t
                """,
                "t.`contract_id`", null, projectId, periodIso, cancellationToken).ConfigureAwait(false);
            var budgets = await LoadRowsAsync(
                connection,
                """
                SELECT b.`id`, 'budget', IFNULL(b.`category`,'general'),
                       CONCAT('Project ', b.`project_id`), 'posted', b.`cost_budget`, b.`revenue_budget`,
                       IFNULL(FROM_UNIXTIME(b.`time_created`),'')
                FROM `epc_prja_budget` b
                """,
                "b.`project_id`", "b.`time_created`", projectId, periodIso, cancellationToken, unixDate: true).ConfigureAwait(false);
            var ledger = await LoadRowsAsync(
                connection,
                """
                SELECT t.`id`, 'txn', IFNULL(t.`category`,'general'), t.`description`, t.`txn_type`, t.`amount`, 0,
                       IFNULL(FROM_UNIXTIME(t.`txn_date`),'')
                FROM `epc_prja_txn` t
                """,
                "t.`project_id`", "t.`txn_date`", projectId, periodIso, cancellationToken, unixDate: true).ConfigureAwait(false);
            var movements = await LoadRowsAsync(
                connection,
                """
                SELECT m.`id`, 'material', m.`item_code`, m.`description`, m.`movement`,
                       m.`quantity`*m.`unit_cost`, m.`quantity`,
                       CONCAT('project ', m.`project_id`, ' · ', IFNULL(m.`reference`,''))
                FROM `ecomae_fitout_material_movements` m
                """,
                "m.`project_id`", "m.`created_at_utc`", projectId, periodIso, cancellationToken).ConfigureAwait(false);
            var links = await LoadRowsAsync(
                connection,
                """
                SELECT l.`id`, 'procurement', CONCAT('PO ', l.`purchase_order_id`), l.`description`, l.`status`,
                       l.`committed_amount`, l.`quantity`,
                       CONCAT('project ', l.`project_id`, ' · req ', l.`requisition_id`, ' · grn ', l.`goods_receipt_id`)
                FROM `ecomae_fitout_procurement_links` l
                """,
                "l.`project_id`", null, projectId, periodIso, cancellationToken).ConfigureAwait(false);
            var matches = await LoadRowsAsync(
                connection,
                """
                SELECT m.`id`, 'three_way', IFNULL(m.`invoice_reference`,''), '3-way match', m.`match_status`,
                       m.`variance_amount`, m.`invoiced_amount`,
                       CONCAT('ordered ', m.`ordered_amount`, ' · received ', m.`received_amount`,
                              ' · invoiced ', m.`invoiced_amount`, ' · tol ', m.`tolerance_percent`, '%')
                FROM `ecomae_fitout_three_way_matches` m
                """,
                "m.`project_id`", null, projectId, periodIso, cancellationToken).ConfigureAwait(false);
            var claims = await LoadRowsAsync(
                connection,
                """
                SELECT r.`id`, r.`record_type`, IFNULL(r.`reference`,''), r.`title`, r.`status`,
                       r.`amount`, r.`completion_percent`,
                       CONCAT('project ', r.`project_id`, ' · ', IFNULL(DATE_FORMAT(r.`event_date`,'%Y-%m-%d'),''))
                FROM `ecomae_fitout_delivery_records` r
                """,
                "r.`project_id`", "r.`event_date`", projectId, periodIso, cancellationToken,
                kindFilter: "r.`record_type` IN ('progress_claim','client_progress_claim','client_payment_certificate','retention_recovery','retention_release','advance_recovery','final_settlement')").ConfigureAwait(false);
            var subcontracts = await LoadRowsAsync(
                connection,
                """
                SELECT r.`id`, r.`record_type`, IFNULL(r.`reference`,''), r.`title`, r.`status`,
                       r.`amount`, r.`completion_percent`,
                       CONCAT('project ', r.`project_id`, ' · ', IFNULL(DATE_FORMAT(r.`event_date`,'%Y-%m-%d'),''))
                FROM `ecomae_fitout_delivery_records` r
                """,
                "r.`project_id`", "r.`event_date`", projectId, periodIso, cancellationToken,
                kindFilter: "r.`record_type` IN ('subcontract_order','subcontract_measurement','subcontract_certification','subcontractor_progress_claim','subcontract_payment_certificate','work_completion_certificate')").ConfigureAwait(false);
            var site = await LoadRowsAsync(
                connection,
                """
                SELECT r.`id`, r.`record_type`, IFNULL(r.`reference`,''), r.`title`, r.`status`,
                       r.`amount`, r.`completion_percent`,
                       CONCAT('project ', r.`project_id`, ' · ', IFNULL(r.`description`,''), ' · ', IFNULL(DATE_FORMAT(r.`event_date`,'%Y-%m-%d'),''))
                FROM `ecomae_fitout_delivery_records` r
                """,
                "r.`project_id`", "r.`event_date`", projectId, periodIso, cancellationToken,
                kindFilter: "r.`record_type` IN ('site_daily_report','site_photo','variation','variation_approval','rfi','drawing_revision','qa_inspection','snag','equipment_usage','timesheet','supplier_rfq','vendor_bill','payment_voucher','approval_request','site_engineer_approval','project_manager_approval')").ConfigureAwait(false);
            var recognitions = await LoadRowsAsync(
                connection,
                """
                SELECT r.`id`, 'recognition', IFNULL(r.`method`,'poc'),
                       CONCAT('Project ', r.`project_id`, ' · rev ', r.`recognized_revenue`,
                              ' · cost ', r.`recognized_cost`), 'posted',
                       r.`pct_complete`*100, r.`wip`,
                       IFNULL(FROM_UNIXTIME(r.`as_of`),'')
                FROM `epc_prja_recognition` r
                """,
                "r.`project_id`", "r.`as_of`", projectId, periodIso, cancellationToken, unixDate: true).ConfigureAwait(false);
            var weightedProgress = await LoadRowsAsync(
                connection,
                """
                SELECT r.`id`, 'weighted_progress', IFNULL(r.`reference`,''), r.`title`, r.`status`,
                       r.`amount`, r.`completion_percent`,
                       CONCAT('project ', r.`project_id`)
                FROM `ecomae_fitout_delivery_records` r
                """,
                "r.`project_id`", "r.`event_date`", projectId, periodIso, cancellationToken,
                kindFilter: "r.`record_type` = 'weighted_progress'").ConfigureAwait(false);
            var allocations = await LoadRowsAsync(
                connection,
                """
                SELECT a.`id`, 'allocation', IFNULL(a.`run_label`,''),
                       CONCAT('Cost centre ', a.`cost_center_id`), 'posted', a.`amount`, a.`source_cost`,
                       IFNULL(FROM_UNIXTIME(a.`time_created`),'')
                FROM `epc_cc_allocations` a
                """,
                null, "a.`time_created`", 0, periodIso, cancellationToken, unixDate: true).ConfigureAwait(false);
            allocations = allocations.Concat(weightedProgress).OrderBy(r => r.Id).ToArray();

            var kpis = await LoadKpisAsync(connection, projectId, projects, cancellationToken).ConfigureAwait(false);
            return new ErpFitOutWorkspaceSnapshot(
                projects, kpis, costCodes, estimates, boqLines, quotations, contracts,
                budgets, ledger, movements, links, matches, claims, subcontracts, site,
                recognitions, allocations, message);
        }
        catch (DbException ex)
        {
            return new ErpFitOutWorkspaceSnapshot(
                [], empty, [], [], [], [], [], [], [], [], [], [], [], [], [], [], [],
                "Fit-out tables are not provisioned yet: " + ex.Message);
        }
    }

    private static async Task<IReadOnlyList<ErpFitOutProject>> LoadProjectsAsync(
        DbConnection connection, CancellationToken ct)
    {
        var rows = new List<ErpFitOutProject>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT `id`, IFNULL(`code`,''), IFNULL(`name`,''),
                       COALESCE(`contract_value`,0), COALESCE(`budget_cost`,0), IFNULL(`status`,'open')
                FROM `epc_prj_projects` ORDER BY `id`
                """;
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                rows.Add(new(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader.GetValue(4), CultureInfo.InvariantCulture),
                    reader.GetString(5)));
            }
        }
        catch (DbException)
        {
        }

        return rows;
    }

    private static async Task<ErpFitOutWorkspaceKpis> LoadKpisAsync(
        DbConnection connection,
        long projectId,
        IReadOnlyList<ErpFitOutProject> projects,
        CancellationToken ct)
    {
        var scoped = projectId > 0 ? projects.Where(p => p.Id == projectId).ToArray() : projects;
        var contract = scoped.Sum(p => p.ContractValue);
        var committed = await ScalarMoneyAsync(
            connection,
            "SELECT COALESCE(SUM(`committed_amount`),0) FROM `ecomae_fitout_procurement_links`"
            + (projectId > 0 ? " WHERE `project_id`=" + projectId.ToString(CultureInfo.InvariantCulture) : string.Empty),
            ct).ConfigureAwait(false);
        var materialActual = await ScalarMoneyAsync(
            connection,
            "SELECT COALESCE(SUM(`quantity`*`unit_cost`),0) FROM `ecomae_fitout_material_movements` WHERE `movement`='issue'"
            + (projectId > 0 ? " AND `project_id`=" + projectId.ToString(CultureInfo.InvariantCulture) : string.Empty),
            ct).ConfigureAwait(false);
        var txnCost = await ScalarMoneyAsync(
            connection,
            "SELECT COALESCE(SUM(`amount`),0) FROM `epc_prja_txn` WHERE `txn_type`='cost'"
            + (projectId > 0 ? " AND `project_id`=" + projectId.ToString(CultureInfo.InvariantCulture) : string.Empty),
            ct).ConfigureAwait(false);
        var txnRevenue = await ScalarMoneyAsync(
            connection,
            "SELECT COALESCE(SUM(`amount`),0) FROM `epc_prja_txn` WHERE `txn_type` IN ('revenue','billing')"
            + (projectId > 0 ? " AND `project_id`=" + projectId.ToString(CultureInfo.InvariantCulture) : string.Empty),
            ct).ConfigureAwait(false);
        var certified = await ScalarMoneyAsync(
            connection,
            """
            SELECT COALESCE(SUM(`amount`),0) FROM `ecomae_fitout_delivery_records`
            WHERE `record_type` IN ('client_progress_claim','client_payment_certificate','progress_claim')
              AND `status`='approved'
            """
            + (projectId > 0 ? " AND `project_id`=" + projectId.ToString(CultureInfo.InvariantCulture) : string.Empty),
            ct).ConfigureAwait(false);
        var pending = await ScalarLongAsync(
            connection,
            """
            SELECT COUNT(*) FROM `ecomae_fitout_delivery_records`
            WHERE `status`='pending' AND `record_type` IN
            ('approval_request','site_engineer_approval','project_manager_approval','variation_approval',
             'subcontract_payment_certificate','client_payment_certificate','retention_release',
             'final_settlement','work_completion_certificate','vendor_bill','payment_voucher')
            """
            + (projectId > 0 ? " AND `project_id`=" + projectId.ToString(CultureInfo.InvariantCulture) : string.Empty),
            ct).ConfigureAwait(false);
        var actual = ErpFitOutCalc.Money(materialActual + txnCost);
        var wip = ErpFitOutCalc.Money(Math.Max(0m, actual - certified));
        return new ErpFitOutWorkspaceKpis(
            scoped.Count, contract, committed, actual, certified, wip,
            ErpFitOutCalc.Money(txnRevenue - actual), (int)pending);
    }

    private static async Task<IReadOnlyList<ErpFitOutRow>> LoadRowsAsync(
        DbConnection connection,
        string select,
        string? projectColumn,
        string? dateColumn,
        long projectId,
        string? periodIso,
        CancellationToken ct,
        string? kindFilter = null,
        bool unixDate = false)
    {
        var clauses = new List<string>();
        if (kindFilter is not null)
        {
            clauses.Add(kindFilter);
        }
        if (projectId > 0 && projectColumn is not null)
        {
            clauses.Add(projectColumn + "=" + projectId.ToString(CultureInfo.InvariantCulture));
        }
        if (periodIso is not null && dateColumn is not null)
        {
            clauses.Add(unixDate
                ? "DATE_FORMAT(FROM_UNIXTIME(" + dateColumn + "),'%Y-%m')='" + periodIso + "'"
                : "DATE_FORMAT(" + dateColumn + ",'%Y-%m')='" + periodIso + "'");
        }

        var sql = select + (clauses.Count > 0 ? " WHERE " + string.Join(" AND ", clauses) : string.Empty)
                  + " ORDER BY 1 LIMIT 400";
        var rows = new List<ErpFitOutRow>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                rows.Add(new(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty,
                    reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture),
                    reader.IsDBNull(6) ? 0m : Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture),
                    Convert.ToString(reader.GetValue(7), CultureInfo.InvariantCulture) ?? string.Empty));
            }
        }
        catch (DbException)
        {
        }

        return rows;
    }

    private static async Task<decimal> ScalarMoneyAsync(DbConnection connection, string sql, CancellationToken ct)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return value is null or DBNull ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
        catch (DbException)
        {
            return 0m;
        }
    }

    private static async Task<long> ScalarLongAsync(DbConnection connection, string sql, CancellationToken ct)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return value is null or DBNull ? 0L : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch (DbException)
        {
            return 0L;
        }
    }

    private static string? NormalizePeriod(string? period)
    {
        var text = (period ?? string.Empty).Trim();
        return text.Length == 7
            && text[4] == '-'
            && int.TryParse(text[..4], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && int.TryParse(text[5..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var month)
            && month is >= 1 and <= 12
                ? text
                : null;
    }
}
