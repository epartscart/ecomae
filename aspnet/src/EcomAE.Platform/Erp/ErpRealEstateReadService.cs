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

    public Task<ErpRealEstateSnapshot> LoadAsync(CancellationToken cancellationToken = default)
        => LoadScopedAsync(0, null, cancellationToken);

    public async Task<ErpRealEstateSnapshot> LoadScopedAsync(long propertyId, string? emirate, CancellationToken cancellationToken = default)
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
        var boq = await RowsAsync(
            connection,
            "SELECT `id`,'boq',`code`,`description`,`status`,`amount`,`qty`,CONCAT('contract ',`contract_id`,' ',`trade`,' @ ',`rate`),0 FROM `epc_erp_re_boq_lines` ORDER BY `id` DESC LIMIT 80",
            cancellationToken).ConfigureAwait(false);
        var escrow = await RowsAsync(
            connection,
            "SELECT `id`,'escrow',CONCAT(UPPER(`kind`),'-',`id`),IFNULL(`reference`,''),`status`,`amount`,0,CONCAT('project ',`project_id`,' ',IFNULL(`txn_date`,'')),0 FROM `epc_erp_re_escrow` ORDER BY `id` DESC LIMIT 80",
            cancellationToken).ConfigureAwait(false);
        var milestones = await RowsAsync(
            connection,
            "SELECT `id`,'milestone',`code`,`name`,`status`,`percent`,0,CONCAT('project ',`project_id`,' due ',IFNULL(`due_date`,'')),0 FROM `epc_erp_re_milestones` ORDER BY `id` DESC LIMIT 80",
            cancellationToken).ConfigureAwait(false);
        var instalments = await RowsAsync(
            connection,
            "SELECT `id`,'instalment',CONCAT('INST-',`id`),CONCAT('sale ',`sale_id`),`status`,`amount`,`collected`,CONCAT('due ',IFNULL(`due_date`,''),' ',`percent`,'%'),0 FROM `epc_erp_re_instalments` ORDER BY `id` DESC LIMIT 80",
            cancellationToken).ConfigureAwait(false);
        var allocations = await RowsAsync(
            connection,
            "SELECT `id`,'allocation',CONCAT('ALLOC-',`id`),CONCAT('unit ',`unit_id`),`status`,`amount`,`weight`,CONCAT('project ',`project_id`,' ',`basis`),0 FROM `epc_erp_re_cost_allocs` ORDER BY `id` DESC LIMIT 80",
            cancellationToken).ConfigureAwait(false);
        var documents = await RowsAsync(
            connection,
            "SELECT `id`,'document',`code`,`title`,`status`,0,0,CONCAT(`doc_type`,' ',IFNULL(`storage_key`,''),' ',IFNULL(`expiry_date`,'')),0 FROM `epc_erp_re_documents` ORDER BY `id` DESC LIMIT 40",
            cancellationToken).ConfigureAwait(false);
        var portfolios = await RowsAsync(
            connection,
            "SELECT `id`,'portfolio',`code`,`name`,`status`,0,0,CONCAT('owner ',`owner_name`),0 FROM `epc_erp_re_portfolios` ORDER BY `id` DESC LIMIT 40",
            cancellationToken).ConfigureAwait(false);
        var communities = await RowsAsync(
            connection,
            "SELECT `id`,'community',`code`,`name`,`status`,0,0,CONCAT(`emirate`,' ',`city`),0 FROM `epc_erp_re_communities` ORDER BY `id` DESC LIMIT 40",
            cancellationToken).ConfigureAwait(false);
        var floors = await RowsAsync(
            connection,
            "SELECT `id`,'floor',`code`,`name`,`status`,0,0,CONCAT('property ',`property_id`,' floor ',`floor_no`),0 FROM `epc_erp_re_floors` ORDER BY `id` DESC LIMIT 40",
            cancellationToken).ConfigureAwait(false);
        var expenses = await RowsAsync(
            connection,
            "SELECT `id`,'expense',`code`,`category`,`status`,`amount`,`vat_amount`,CONCAT('property ',`property_id`,' capex=',`is_capex`),0 FROM `epc_erp_re_expenses` ORDER BY `id` DESC LIMIT 40",
            cancellationToken).ConfigureAwait(false);
        properties = portfolios.Concat(communities).Concat(floors).Concat(properties).ToList();
        maintenance = expenses.Concat(maintenance).ToList();

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
        var escrowIn = escrow.Where(e => e.Code.StartsWith("RECEIPT", StringComparison.OrdinalIgnoreCase) && e.Status is not ("voided" or "reversed")).Sum(e => e.Amount);
        var escrowOut = escrow.Where(e => e.Code.StartsWith("DISBURSEMENT", StringComparison.OrdinalIgnoreCase) && e.Status is not ("voided" or "reversed")).Sum(e => e.Amount);
        var landlord = await LandlordCardsAsync(connection, propertyId, emirate, cancellationToken).ConfigureAwait(false);
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
            costToComplete,
            ErpRealEstateCalc.EscrowBalance(escrowIn, escrowOut),
            landlord.Vacant,
            landlord.Recognized,
            landlord.Collected,
            landlord.Outstanding,
            landlord.Pdc,
            landlord.Due,
            landlord.Opex,
            ErpRealEstateCalc.LandlordNoi(landlord.Recognized, landlord.Opex),
            ErpRealEstateCalc.NetCashFlow(landlord.Collected, landlord.Opex),
            landlord.Capex);

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
            boq,
            escrow,
            milestones,
            instalments,
            allocations,
            documents,
            alerts,
            ErpRealEstateCalc.Definitions,
            string.Empty);
    }

    private static async Task<(int Vacant, decimal Recognized, decimal Collected, decimal Outstanding, decimal Pdc, int Due, decimal Opex, decimal Capex)> LandlordCardsAsync(
        DbConnection connection,
        long propertyId,
        string? emirate,
        CancellationToken cancellationToken)
    {
        try
        {
            var place = new string((emirate ?? string.Empty).Trim().Where(ch => char.IsLetterOrDigit(ch) || ch is ' ' or '-').Take(40).ToArray());
            var prop = propertyId > 0 ? propertyId.ToString(CultureInfo.InvariantCulture) : "";
            string Prop(string column) => prop.Length == 0 ? "" : " AND `" + column + "`=" + prop;
            string Place(string column)
            {
                if (place.Length == 0)
                {
                    return "";
                }

                return column == "id"
                    ? " AND `emirate`='" + place + "'"
                    : " AND `" + column + "` IN (SELECT `id` FROM `epc_erp_properties` WHERE `emirate`='" + place + "')";
            }

            var vacant = (int)await ErpDb.LongAsync(connection, null,
                "SELECT COUNT(*) FROM `epc_erp_re_units` WHERE `status` IN ('available','vacant')" + Prop("property_id") + Place("property_id"),
                cancellationToken).ConfigureAwait(false);
            var collected = await ErpDb.DecimalAsync(connection, null,
                "SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_re_cheques` WHERE `status`='cleared'" + Prop("property_id") + Place("property_id"),
                cancellationToken).ConfigureAwait(false);
            var pdc = await ErpDb.DecimalAsync(connection, null,
                "SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_re_cheques` WHERE `status` IN ('received','in_custody','held','on_hold','due_for_deposit')" + Prop("property_id") + Place("property_id"),
                cancellationToken).ConfigureAwait(false);
            var due = (int)await ErpDb.LongAsync(connection, null,
                "SELECT COUNT(*) FROM `epc_erp_re_cheques` WHERE `status` IN ('received','in_custody','held','on_hold','due_for_deposit') AND `cheque_date` IS NOT NULL AND `cheque_date`<=DATE_ADD(CURRENT_DATE, INTERVAL 30 DAY)" + Prop("property_id") + Place("property_id"),
                cancellationToken).ConfigureAwait(false);
            var leaseFilter = Prop("property_id") + Place("property_id");
            var outstanding = await ErpDb.DecimalAsync(connection, null,
                "SELECT IFNULL(SUM(`amount`-`allocated`),0) FROM `epc_erp_re_rent_invoices` WHERE `status` NOT IN ('voided','reversed','cancelled')"
                + (leaseFilter.Length == 0 ? "" : " AND `lease_id` IN (SELECT `id` FROM `epc_erp_re_leases` WHERE 1=1" + leaseFilter + ")"),
                cancellationToken).ConfigureAwait(false);
            var opexMaint = await ErpDb.DecimalAsync(connection, null,
                "SELECT IFNULL(SUM(`cost`),0) FROM `epc_erp_property_maintenance` WHERE `cost_nature`='opex' AND IFNULL(`gl_journal_id`,0)>0 AND `status` NOT IN ('voided','reversed','cancelled')" + Prop("property_id") + Place("property_id"),
                cancellationToken).ConfigureAwait(false);
            var opexExp = await ErpDb.DecimalAsync(connection, null,
                "SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_re_expenses` WHERE `is_capex`=0 AND `noi_excluded`=0 AND `gl_journal_id`>0 AND `status` NOT IN ('voided','reversed','cancelled','draft')" + Prop("property_id") + Place("property_id"),
                cancellationToken).ConfigureAwait(false);
            var capexMaint = await ErpDb.DecimalAsync(connection, null,
                "SELECT IFNULL(SUM(`cost`),0) FROM `epc_erp_property_maintenance` WHERE `cost_nature`='capex' AND IFNULL(`gl_journal_id`,0)>0 AND `status` NOT IN ('voided','reversed','cancelled')" + Prop("property_id") + Place("property_id"),
                cancellationToken).ConfigureAwait(false);
            var capexExp = await ErpDb.DecimalAsync(connection, null,
                "SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_re_expenses` WHERE `is_capex`=1 AND `gl_journal_id`>0 AND `status` NOT IN ('voided','reversed','cancelled','draft')" + Prop("property_id") + Place("property_id"),
                cancellationToken).ConfigureAwait(false);

            decimal recognized = 0;
            var yearStart = new DateOnly(DateTime.UtcNow.Year, 1, 1);
            var yearEnd = new DateOnly(DateTime.UtcNow.Year, 12, 31);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT `annual_rent`,`start_date`,`end_date` FROM `epc_erp_re_leases` WHERE `status`='active'" + leaseFilter;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!TryDate(reader.GetValue(1), out var start) || !TryDate(reader.GetValue(2), out var end))
                    {
                        continue;
                    }

                    var annual = reader.IsDBNull(0) ? 0m : Convert.ToDecimal(reader.GetValue(0), CultureInfo.InvariantCulture);
                    recognized += ErpRealEstateCalc.RecognizedRent(annual, start, end, yearStart, yearEnd);
                }
            }

            return (vacant, ErpRealEstateCalc.Money(recognized), collected, outstanding, pdc, due, ErpRealEstateCalc.Money(opexMaint + opexExp), ErpRealEstateCalc.Money(capexMaint + capexExp));
        }
        catch (DbException)
        {
            return (0, 0, 0, 0, 0, 0, 0, 0);
        }
    }

    private static bool TryDate(object? value, out DateOnly date)
    {
        if (value is DateTime dt)
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        return DateOnly.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static ErpRealEstateSnapshot Empty(string message)
        => new(
            new ErpRealEstateKpis(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [],
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
