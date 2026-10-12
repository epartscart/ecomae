using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed class ErpRealEstateReadService : IErpRealEstateReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpRealEstateReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpRealEstateSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return Empty("TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpRealEstateWriteService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var opportunities = await RowsAsync(
            connection,
            "SELECT `id`,'opportunity',`code`,`name`,`status`,`indicative_value`,`asking_price`,CONCAT(`emirate`,' ',`city`),0 FROM `epc_erp_re_opportunities` ORDER BY `id` DESC LIMIT 80",
            cancellationToken).ConfigureAwait(false);
        var properties = await RowsAsync(
            connection,
            "SELECT `id`,'property',`code`,`name`,`status`,IFNULL(`acquisition_cost`,0),IFNULL(`fair_value`,IFNULL(`market_value`,0)),CONCAT(IFNULL(`emirate`,''),' ',`city`), CASE WHEN `status` IN ('disposed','under-evaluation','archived') THEN 0 ELSE 1 END FROM `epc_erp_properties` ORDER BY `id` DESC LIMIT 80",
            cancellationToken).ConfigureAwait(false);
        var units = await RowsAsync(
            connection,
            "SELECT `id`,'unit',`code`,`name`,`status`,`asking_price`,`area_sqm`,CONCAT('property ',`property_id`),0 FROM `epc_erp_re_units` ORDER BY `id` DESC LIMIT 80",
            cancellationToken).ConfigureAwait(false);
        var acquisitions = await RowsAsync(
            connection,
            "SELECT `id`,'acquisition',`code`,`seller`,`status`,`purchase_price`,`eligible_costs`,CONCAT('property ',`property_id`),1 FROM `epc_erp_re_acquisitions` ORDER BY `id` DESC LIMIT 80",
            cancellationToken).ConfigureAwait(false);
        var disposals = await RowsAsync(
            connection,
            "SELECT `id`,'disposal',`code`,`buyer`,`status`,`sale_price`,`carrying_basis`,CONCAT('property ',`property_id`),0 FROM `epc_erp_re_disposals` ORDER BY `id` DESC LIMIT 40",
            cancellationToken).ConfigureAwait(false);
        var valuations = await RowsAsync(
            connection,
            "SELECT `id`,'valuation',`code`,`valuer`,`status`,`approved_fair_value`,`estimated_value`,CONCAT('property ',`property_id`,' @ ',`valuation_date`),0 FROM `epc_erp_re_valuations` ORDER BY `id` DESC LIMIT 80",
            cancellationToken).ConfigureAwait(false);
        var tenants = await RowsAsync(
            connection,
            "SELECT `id`,'tenant',`code`,`name`,`status`,0,0,`kyc_status`,0 FROM `epc_erp_re_tenants` ORDER BY `id` DESC LIMIT 80",
            cancellationToken).ConfigureAwait(false);
        var leases = await RowsAsync(
            connection,
            "SELECT `id`,'lease',`code`,CONCAT('tenant ',`tenant_id`),`status`,`annual_rent`,`deposit`,CONCAT(`start_date`,' → ',`end_date`,' ejari=',`ejari_status`),0 FROM `epc_erp_re_leases` ORDER BY `id` DESC LIMIT 80",
            cancellationToken).ConfigureAwait(false);
        var cheques = await RowsAsync(
            connection,
            "SELECT `id`,'cheque',`code`,`cheque_no`,`status`,`amount`,0,CONCAT(`bank_name`,' ',IFNULL(`cheque_date`,'')),0 FROM `epc_erp_re_cheques` ORDER BY `id` DESC LIMIT 80",
            cancellationToken).ConfigureAwait(false);
        var deposits = await RowsAsync(
            connection,
            "SELECT `id`,'deposit',CONCAT('DEP-',`id`),CONCAT('lease ',`lease_id`),`status`,`amount`,`held`,'security deposit liability',0 FROM `epc_erp_re_deposits` ORDER BY `id` DESC LIMIT 40",
            cancellationToken).ConfigureAwait(false);
        var maintenance = await RowsAsync(
            connection,
            "SELECT `id`,'maintenance',CONCAT('WO-',`id`),IFNULL(`description`,''),`status`,IFNULL(`cost`,0),0,CONCAT(`type`,' ',IFNULL(`vendor`,'')),0 FROM `epc_erp_property_maintenance` ORDER BY `id` DESC LIMIT 40",
            cancellationToken).ConfigureAwait(false);
        var projects = await RowsAsync(
            connection,
            "SELECT `id`,'project',`code`,`name`,`status`,`revised_budget`,`actual_cost`,CONCAT(`stage`,' escrow=',`escrow_required`),0 FROM `epc_erp_re_projects` ORDER BY `id` DESC LIMIT 40",
            cancellationToken).ConfigureAwait(false);
        var sales = await RowsAsync(
            connection,
            "SELECT `id`,'sale',`code`,`buyer`,`status`,`sale_price`,`collected`,CONCAT('unit ',`unit_id`,' invoiced ',`invoiced`,' recognized ',`recognized`),0 FROM `epc_erp_re_sales` ORDER BY `id` DESC LIMIT 40",
            cancellationToken).ConfigureAwait(false);
        var contractors = await RowsAsync(
            connection,
            "SELECT `id`,'contractor',`code`,`name`,`status`,0,0,`trade`,0 FROM `epc_erp_re_contractors` ORDER BY `id` DESC LIMIT 40",
            cancellationToken).ConfigureAwait(false);
        var contracts = await RowsAsync(
            connection,
            "SELECT `id`,'contract',`code`,CONCAT('contractor ',`contractor_id`),`status`,`original_amount`,`variations`,CONCAT('retention ',`retention_pct`,'%'),0 FROM `epc_erp_re_contracts` ORDER BY `id` DESC LIMIT 40",
            cancellationToken).ConfigureAwait(false);
        var certificates = await RowsAsync(
            connection,
            "SELECT `id`,'certificate',`code`,CONCAT('contract ',`contract_id`),`status`,`net_payable`,`current_measured`,'advance/retention applied',0 FROM `epc_erp_re_certificates` ORDER BY `id` DESC LIMIT 40",
            cancellationToken).ConfigureAwait(false);
        var documents = await RowsAsync(
            connection,
            "SELECT `id`,'document',`code`,`title`,`status`,0,0,CONCAT(`doc_type`,' ',IFNULL(`expiry_date`,'')),0 FROM `epc_erp_re_documents` ORDER BY `id` DESC LIMIT 40",
            cancellationToken).ConfigureAwait(false);

        var alerts = new List<ErpRealEstateAlert>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id`,`kind`,`title`,IFNULL(`due_date`,''),`status`,`related_id` FROM `epc_erp_re_alerts` ORDER BY `id` DESC LIMIT 60";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                alerts.Add(new(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? "",
                    reader.GetString(4),
                    Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture)));
            }
        }

        var owned = properties.Where(p => p.OwnedAsset).ToArray();
        var cost = owned.Sum(p => p.Amount);
        var fair = owned.Sum(p => p.Amount2);
        var annualRent = leases.Where(l => l.Status is "active" or "draft").Sum(l => l.Amount);
        var occupied = units.Count(u => u.Status is "occupied" or "reserved" or "sold");
        var invoiced = await ErpDb.DecimalAsync(connection, null, "SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_re_rent_invoices`", cancellationToken).ConfigureAwait(false);
        var collected = await ErpDb.DecimalAsync(connection, null, "SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_re_cheques` WHERE `status`='cleared'", cancellationToken).ConfigureAwait(false);
        var costToComplete = projects.Sum(p => ErpRealEstateCalc.CostToComplete(p.Amount, p.Amount2));
        var kpis = new ErpRealEstateKpis(
            opportunities.Count(o => o.Status is "open" or "screening"),
            owned.Length,
            units.Count,
            leases.Count(l => l.Status == "active"),
            cheques.Count(c => c.Status is not "cleared" and not "cancelled"),
            projects.Count,
            cost,
            fair,
            ErpRealEstateCalc.Unrealized(fair, cost),
            annualRent,
            ErpRealEstateCalc.GrossYield(annualRent, cost),
            ErpRealEstateCalc.Occupancy(occupied, units.Count),
            ErpRealEstateCalc.CollectionEfficiency(collected, invoiced),
            costToComplete);

        return new(
            kpis,
            opportunities,
            properties,
            units,
            acquisitions,
            disposals,
            valuations,
            tenants,
            leases,
            cheques,
            deposits,
            maintenance,
            projects,
            sales,
            contractors,
            contracts,
            certificates,
            documents,
            alerts,
            ErpRealEstateCalc.Definitions,
            string.Empty);
    }

    private static ErpRealEstateSnapshot Empty(string message)
        => new(
            new ErpRealEstateKpis(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [],
            ErpRealEstateCalc.Definitions,
            message);

    private static async Task<IReadOnlyList<ErpRealEstateRow>> RowsAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        var rows = new List<ErpRealEstateRow>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.IsDBNull(1) ? "" : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? "",
                    reader.IsDBNull(2) ? "" : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? "",
                    reader.IsDBNull(3) ? "" : Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? "",
                    reader.IsDBNull(4) ? "" : Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? "",
                    reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture),
                    reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture),
                    reader.IsDBNull(7) ? "" : Convert.ToString(reader.GetValue(7), CultureInfo.InvariantCulture) ?? "",
                    !reader.IsDBNull(8) && Convert.ToInt32(reader.GetValue(8), CultureInfo.InvariantCulture) != 0));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return rows;
    }
}
