using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed class ErpRealEstateWriteService : IErpRealEstateWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpAuditLogWriter _audit;
    private readonly IErpGlPostingService _gl;

    public ErpRealEstateWriteService(
        IErpWriteConnectionFactory connections,
        IErpAuditLogWriter audit,
        IErpGlPostingService gl)
    {
        _connections = connections;
        _audit = audit;
        _gl = gl;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpRealEstateWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var action = (request.Action ?? string.Empty).Trim().ToLowerInvariant();
        if (action.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Action is required.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await _gl.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await new ErpVoucherNumberService().EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpRealEstateGl.EnsureCoaAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = action switch
            {
                "save_opportunity" => await SaveOpportunityAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "convert_opportunity" => await ConvertOpportunityAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_property" => await SavePropertyAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_unit" => await SaveUnitAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_owner" => await SaveOwnerAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_acquisition" => await SaveAcquisitionAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "complete_acquisition" => await CompleteAcquisitionAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_disposal" => await SaveDisposalAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_valuation" => await SaveValuationAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_tenant" => await SaveTenantAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_lease" => await SaveLeaseAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "renew_lease" => await RenewLeaseAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "terminate_lease" => await TerminateLeaseAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_cheque" => await SaveChequeAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "cheque_status" => await ChequeStatusAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_deposit" => await SaveDepositAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_maintenance" => await SaveMaintenanceAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_project" => await SaveProjectAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_project_unit" => await SaveProjectUnitAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_sale" => await SaveSaleAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_contractor" => await SaveContractorAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_contract" => await SaveContractAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_variation" => await SaveVariationAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_certificate" => await SaveCertificateAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_boq_line" => await SaveBoqLineAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_escrow_receipt" => await SaveEscrowReceiptAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_escrow_disbursement" => await SaveEscrowDisbursementAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_milestone" => await SaveMilestoneAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "generate_instalments" => await GenerateInstalmentsAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "collect_instalment" => await CollectInstalmentAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "allocate_shared_cost" => await AllocateSharedCostAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "save_document" => await SaveDocumentAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "ack_alert" => await AckAlertAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "void" or "void_document" => await VoidDocumentAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "reverse" or "reverse_document" => await ReverseDocumentAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                "delete" or "delete_draft" => await DeleteDraftAsync(connection, tx, request, cancellationToken).ConfigureAwait(false),
                _ => ErpSimpleWriteResult.Fail("unknown_action", "Unknown real-estate action: " + action),
            };

            if (!result.Succeeded)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return result;
            }

            await _audit.LogAsync(
                connection,
                tx,
                request.ActorId,
                action,
                "real_estate",
                result.Id,
                result.Message,
                new Dictionary<string, string?>
                {
                    ["action"] = action,
                    ["code"] = request.Code,
                    ["amount"] = request.Amount.ToString(CultureInfo.InvariantCulture),
                },
                cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        foreach (var sql in Schema)
        {
            await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken).ConfigureAwait(false);
        }

        foreach (var alter in PropertyAlters)
        {
            try
            {
                await ErpDb.ExecuteAsync(connection, null, alter, cancellationToken).ConfigureAwait(false);
            }
            catch (DbException)
            {
            }
        }
    }

    private static readonly string[] Schema =
    [
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_properties` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
          `company_id` INT UNSIGNED NOT NULL DEFAULT 0,
          `code` VARCHAR(40) NOT NULL DEFAULT '',
          `name` VARCHAR(200) NOT NULL DEFAULT '',
          `type` VARCHAR(40) NOT NULL DEFAULT 'residential',
          `ownership` VARCHAR(40) NOT NULL DEFAULT 'owned',
          `address` TEXT,
          `city` VARCHAR(100) NOT NULL DEFAULT '',
          `country` CHAR(2) NOT NULL DEFAULT 'AE',
          `area_sqm` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `market_value` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `currency` CHAR(3) NOT NULL DEFAULT 'AED',
          `status` VARCHAR(40) NOT NULL DEFAULT 'active',
          `acquired_date` DATE DEFAULT NULL,
          `disposed_date` DATE DEFAULT NULL,
          `notes` TEXT,
          `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`),
          UNIQUE KEY `uk_code` (`company_id`,`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_opportunities` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `code` VARCHAR(40) NOT NULL,
          `name` VARCHAR(190) NOT NULL,
          `stage` VARCHAR(40) NOT NULL DEFAULT 'screening',
          `source` VARCHAR(80) NOT NULL DEFAULT '',
          `seller` VARCHAR(190) NOT NULL DEFAULT '',
          `broker` VARCHAR(190) NOT NULL DEFAULT '',
          `emirate` VARCHAR(40) NOT NULL DEFAULT '',
          `city` VARCHAR(80) NOT NULL DEFAULT '',
          `asking_price` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `offer_price` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `indicative_value` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `expected_rent` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `opex` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `equity` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `holding_months` INT NOT NULL DEFAULT 60,
          `legal_entity_id` BIGINT NOT NULL DEFAULT 0,
          `notes` VARCHAR(255) NOT NULL DEFAULT '',
          `status` VARCHAR(40) NOT NULL DEFAULT 'open',
          `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
          `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
          PRIMARY KEY (`id`),
          UNIQUE KEY `uk_re_opp_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_units` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `property_id` BIGINT NOT NULL,
          `project_id` BIGINT NOT NULL DEFAULT 0,
          `code` VARCHAR(40) NOT NULL,
          `name` VARCHAR(190) NOT NULL,
          `unit_type` VARCHAR(40) NOT NULL DEFAULT 'apartment',
          `floor_no` VARCHAR(20) NOT NULL DEFAULT '',
          `area_sqm` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `bedrooms` INT NOT NULL DEFAULT 0,
          `status` VARCHAR(40) NOT NULL DEFAULT 'available',
          `asking_price` DECIMAL(18,4) NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`),
          UNIQUE KEY `uk_re_unit` (`property_id`,`code`),
          KEY `idx_re_unit_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_owners` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `property_id` BIGINT NOT NULL,
          `legal_entity_id` BIGINT NOT NULL DEFAULT 0,
          `owner_name` VARCHAR(190) NOT NULL,
          `ownership_pct` DECIMAL(9,4) NOT NULL DEFAULT 100,
          `effective_from` DATE DEFAULT NULL,
          `effective_to` DATE DEFAULT NULL,
          `status` VARCHAR(20) NOT NULL DEFAULT 'active',
          PRIMARY KEY (`id`),
          KEY `idx_re_owner_prop` (`property_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_acquisitions` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `property_id` BIGINT NOT NULL,
          `opportunity_id` BIGINT NOT NULL DEFAULT 0,
          `legal_entity_id` BIGINT NOT NULL DEFAULT 0,
          `code` VARCHAR(40) NOT NULL,
          `seller` VARCHAR(190) NOT NULL DEFAULT '',
          `broker` VARCHAR(190) NOT NULL DEFAULT '',
          `purchase_price` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `eligible_costs` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `vat_amount` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `agreement_date` DATE DEFAULT NULL,
          `completion_date` DATE DEFAULT NULL,
          `status` VARCHAR(40) NOT NULL DEFAULT 'draft',
          `gl_journal_id` BIGINT NOT NULL DEFAULT 0,
          `notes` VARCHAR(255) NOT NULL DEFAULT '',
          PRIMARY KEY (`id`),
          UNIQUE KEY `uk_re_acq_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_disposals` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `property_id` BIGINT NOT NULL,
          `code` VARCHAR(40) NOT NULL,
          `buyer` VARCHAR(190) NOT NULL DEFAULT '',
          `sale_price` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `disposal_costs` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `mortgage_settlement` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `carrying_basis` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `completion_date` DATE DEFAULT NULL,
          `status` VARCHAR(40) NOT NULL DEFAULT 'draft',
          `gl_journal_id` BIGINT NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`),
          UNIQUE KEY `uk_re_disp_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_valuations` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `property_id` BIGINT NOT NULL,
          `code` VARCHAR(40) NOT NULL,
          `valuation_date` DATE NOT NULL,
          `purpose` VARCHAR(40) NOT NULL DEFAULT 'management',
          `method` VARCHAR(40) NOT NULL DEFAULT 'market',
          `estimated_value` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `appraised_value` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `approved_fair_value` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `valuer` VARCHAR(190) NOT NULL DEFAULT '',
          `status` VARCHAR(40) NOT NULL DEFAULT 'draft',
          `gl_journal_id` BIGINT NOT NULL DEFAULT 0,
          `notes` VARCHAR(255) NOT NULL DEFAULT '',
          PRIMARY KEY (`id`),
          KEY `idx_re_val_prop` (`property_id`,`valuation_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_tenants` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `code` VARCHAR(40) NOT NULL,
          `name` VARCHAR(190) NOT NULL,
          `contact` VARCHAR(190) NOT NULL DEFAULT '',
          `trade_licence` VARCHAR(80) NOT NULL DEFAULT '',
          `kyc_status` VARCHAR(40) NOT NULL DEFAULT 'pending',
          `status` VARCHAR(40) NOT NULL DEFAULT 'active',
          PRIMARY KEY (`id`),
          UNIQUE KEY `uk_re_tenant_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_leases` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `code` VARCHAR(40) NOT NULL,
          `property_id` BIGINT NOT NULL,
          `unit_id` BIGINT NOT NULL DEFAULT 0,
          `tenant_id` BIGINT NOT NULL,
          `legal_entity_id` BIGINT NOT NULL DEFAULT 0,
          `start_date` DATE NOT NULL,
          `end_date` DATE NOT NULL,
          `annual_rent` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `frequency` VARCHAR(20) NOT NULL DEFAULT 'monthly',
          `deposit` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `vat_code` VARCHAR(20) NOT NULL DEFAULT 'standard',
          `ejari_ref` VARCHAR(80) NOT NULL DEFAULT '',
          `ejari_status` VARCHAR(40) NOT NULL DEFAULT 'not_registered',
          `status` VARCHAR(40) NOT NULL DEFAULT 'draft',
          `prior_lease_id` BIGINT NOT NULL DEFAULT 0,
          `notes` VARCHAR(255) NOT NULL DEFAULT '',
          PRIMARY KEY (`id`),
          UNIQUE KEY `uk_re_lease_code` (`code`),
          KEY `idx_re_lease_prop` (`property_id`,`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_rent_invoices` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `lease_id` BIGINT NOT NULL,
          `period_date` DATE NOT NULL,
          `amount` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `allocated` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `status` VARCHAR(20) NOT NULL DEFAULT 'open',
          `invoice_id` BIGINT NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`),
          UNIQUE KEY `uk_re_rent` (`lease_id`,`period_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_cheques` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `code` VARCHAR(40) NOT NULL,
          `cheque_no` VARCHAR(40) NOT NULL,
          `lease_id` BIGINT NOT NULL DEFAULT 0,
          `invoice_id` BIGINT NOT NULL DEFAULT 0,
          `tenant_id` BIGINT NOT NULL DEFAULT 0,
          `property_id` BIGINT NOT NULL DEFAULT 0,
          `amount` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `bank_name` VARCHAR(120) NOT NULL DEFAULT '',
          `cheque_date` DATE DEFAULT NULL,
          `deposit_date` DATE DEFAULT NULL,
          `clearance_date` DATE DEFAULT NULL,
          `status` VARCHAR(40) NOT NULL DEFAULT 'received',
          `replacement_id` BIGINT NOT NULL DEFAULT 0,
          `gl_journal_id` BIGINT NOT NULL DEFAULT 0,
          `gl_clear_journal_id` BIGINT NOT NULL DEFAULT 0,
          `notes` VARCHAR(255) NOT NULL DEFAULT '',
          PRIMARY KEY (`id`),
          UNIQUE KEY `uk_re_chq` (`cheque_no`,`bank_name`),
          KEY `idx_re_chq_status` (`status`,`cheque_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_deposits` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `lease_id` BIGINT NOT NULL,
          `tenant_id` BIGINT NOT NULL,
          `amount` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `held` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `status` VARCHAR(40) NOT NULL DEFAULT 'held',
          `gl_journal_id` BIGINT NOT NULL DEFAULT 0,
          `notes` VARCHAR(255) NOT NULL DEFAULT '',
          PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_projects` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `code` VARCHAR(40) NOT NULL,
          `name` VARCHAR(190) NOT NULL,
          `property_id` BIGINT NOT NULL DEFAULT 0,
          `legal_entity_id` BIGINT NOT NULL DEFAULT 0,
          `emirate` VARCHAR(40) NOT NULL DEFAULT '',
          `stage` VARCHAR(40) NOT NULL DEFAULT 'feasibility',
          `land_cost` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `original_budget` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `revised_budget` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `actual_cost` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `expected_sales` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `escrow_required` TINYINT NOT NULL DEFAULT 0,
          `status` VARCHAR(40) NOT NULL DEFAULT 'active',
          PRIMARY KEY (`id`),
          UNIQUE KEY `uk_re_proj_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_sales` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `code` VARCHAR(40) NOT NULL,
          `project_id` BIGINT NOT NULL DEFAULT 0,
          `unit_id` BIGINT NOT NULL,
          `buyer` VARCHAR(190) NOT NULL,
          `sale_price` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `collected` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `invoiced` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `recognized` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `status` VARCHAR(40) NOT NULL DEFAULT 'reserved',
          `vat_code` VARCHAR(20) NOT NULL DEFAULT 'standard',
          `gl_journal_id` BIGINT NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`),
          KEY `idx_re_sale_unit` (`unit_id`,`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_contractors` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `code` VARCHAR(40) NOT NULL,
          `name` VARCHAR(190) NOT NULL,
          `trade` VARCHAR(80) NOT NULL DEFAULT '',
          `licence` VARCHAR(80) NOT NULL DEFAULT '',
          `trn` VARCHAR(40) NOT NULL DEFAULT '',
          `status` VARCHAR(40) NOT NULL DEFAULT 'approved',
          PRIMARY KEY (`id`),
          UNIQUE KEY `uk_re_con_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_contracts` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `code` VARCHAR(40) NOT NULL,
          `project_id` BIGINT NOT NULL,
          `contractor_id` BIGINT NOT NULL,
          `original_amount` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `variations` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `retention_pct` DECIMAL(9,4) NOT NULL DEFAULT 10,
          `advance` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `advance_recovered` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `status` VARCHAR(40) NOT NULL DEFAULT 'active',
          PRIMARY KEY (`id`),
          UNIQUE KEY `uk_re_ctr_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_certificates` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `code` VARCHAR(40) NOT NULL,
          `contract_id` BIGINT NOT NULL,
          `previous_certified` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `current_measured` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `materials_on_site` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `approved_variations` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `advance_recovery` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `retention` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `other_deductions` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `vat_amount` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `net_payable` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `status` VARCHAR(40) NOT NULL DEFAULT 'draft',
          `gl_journal_id` BIGINT NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`),
          UNIQUE KEY `uk_re_cert_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_documents` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `code` VARCHAR(40) NOT NULL,
          `doc_type` VARCHAR(40) NOT NULL DEFAULT 'other',
          `related_kind` VARCHAR(40) NOT NULL DEFAULT 'property',
          `related_id` BIGINT NOT NULL DEFAULT 0,
          `title` VARCHAR(190) NOT NULL,
          `expiry_date` DATE DEFAULT NULL,
          `status` VARCHAR(40) NOT NULL DEFAULT 'uploaded',
          PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_alerts` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `kind` VARCHAR(40) NOT NULL,
          `title` VARCHAR(190) NOT NULL,
          `due_date` DATE DEFAULT NULL,
          `related_id` BIGINT NOT NULL DEFAULT 0,
          `status` VARCHAR(20) NOT NULL DEFAULT 'open',
          PRIMARY KEY (`id`),
          KEY `idx_re_alert` (`kind`,`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_boq_lines` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `contract_id` BIGINT NOT NULL,
          `code` VARCHAR(40) NOT NULL,
          `description` VARCHAR(190) NOT NULL DEFAULT '',
          `trade` VARCHAR(80) NOT NULL DEFAULT '',
          `qty` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `rate` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `amount` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `status` VARCHAR(40) NOT NULL DEFAULT 'draft',
          PRIMARY KEY (`id`),
          KEY `idx_re_boq_ctr` (`contract_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_escrow` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `project_id` BIGINT NOT NULL,
          `kind` VARCHAR(20) NOT NULL DEFAULT 'receipt',
          `amount` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `certificate_id` BIGINT NOT NULL DEFAULT 0,
          `reference` VARCHAR(80) NOT NULL DEFAULT '',
          `txn_date` DATE DEFAULT NULL,
          `status` VARCHAR(40) NOT NULL DEFAULT 'received',
          `gl_journal_id` BIGINT NOT NULL DEFAULT 0,
          `notes` VARCHAR(255) NOT NULL DEFAULT '',
          PRIMARY KEY (`id`),
          KEY `idx_re_escrow_proj` (`project_id`,`kind`,`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_milestones` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `project_id` BIGINT NOT NULL,
          `code` VARCHAR(40) NOT NULL,
          `name` VARCHAR(190) NOT NULL,
          `due_date` DATE DEFAULT NULL,
          `percent` DECIMAL(9,4) NOT NULL DEFAULT 0,
          `status` VARCHAR(40) NOT NULL DEFAULT 'planned',
          PRIMARY KEY (`id`),
          KEY `idx_re_ms_proj` (`project_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_instalments` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `sale_id` BIGINT NOT NULL,
          `milestone_id` BIGINT NOT NULL DEFAULT 0,
          `due_date` DATE DEFAULT NULL,
          `percent` DECIMAL(9,4) NOT NULL DEFAULT 0,
          `amount` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `collected` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `status` VARCHAR(40) NOT NULL DEFAULT 'due',
          `gl_journal_id` BIGINT NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`),
          KEY `idx_re_inst_sale` (`sale_id`,`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_re_cost_allocs` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
          `project_id` BIGINT NOT NULL,
          `unit_id` BIGINT NOT NULL,
          `basis` VARCHAR(20) NOT NULL DEFAULT 'area',
          `weight` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `amount` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `status` VARCHAR(40) NOT NULL DEFAULT 'allocated',
          PRIMARY KEY (`id`),
          KEY `idx_re_alloc_proj` (`project_id`,`unit_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_erp_property_maintenance` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
          `property_id` INT UNSIGNED NOT NULL DEFAULT 0,
          `type` VARCHAR(40) NOT NULL DEFAULT 'preventive',
          `description` TEXT,
          `vendor` VARCHAR(200) NOT NULL DEFAULT '',
          `scheduled_date` DATE DEFAULT NULL,
          `completed_date` DATE DEFAULT NULL,
          `cost` DECIMAL(18,4) NOT NULL DEFAULT 0,
          `currency` CHAR(3) NOT NULL DEFAULT 'AED',
          `status` VARCHAR(40) NOT NULL DEFAULT 'planned',
          `gl_journal_id` BIGINT NOT NULL DEFAULT 0,
          `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
    ];

    private static readonly string[] PropertyAlters =
    [
        "ALTER TABLE `epc_erp_properties` ADD COLUMN `emirate` VARCHAR(40) NOT NULL DEFAULT ''",
        "ALTER TABLE `epc_erp_properties` ADD COLUMN `community` VARCHAR(80) NOT NULL DEFAULT ''",
        "ALTER TABLE `epc_erp_properties` ADD COLUMN `plot_no` VARCHAR(40) NOT NULL DEFAULT ''",
        "ALTER TABLE `epc_erp_properties` ADD COLUMN `title_deed` VARCHAR(80) NOT NULL DEFAULT ''",
        "ALTER TABLE `epc_erp_properties` ADD COLUMN `tenure` VARCHAR(40) NOT NULL DEFAULT 'freehold'",
        "ALTER TABLE `epc_erp_properties` ADD COLUMN `legal_entity_id` BIGINT NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_erp_properties` ADD COLUMN `acquisition_cost` DECIMAL(18,4) NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_erp_properties` ADD COLUMN `capex` DECIMAL(18,4) NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_erp_properties` ADD COLUMN `fair_value` DECIMAL(18,4) NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_erp_properties` ADD COLUMN `mortgage_balance` DECIMAL(18,4) NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_erp_properties` ADD COLUMN `annual_opex` DECIMAL(18,4) NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_erp_properties` ADD COLUMN `investment_class` VARCHAR(40) NOT NULL DEFAULT 'investment'",
        "ALTER TABLE `epc_erp_properties` MODIFY `status` VARCHAR(40) NOT NULL DEFAULT 'active'",
        "ALTER TABLE `epc_erp_properties` MODIFY `type` VARCHAR(40) NOT NULL DEFAULT 'residential'",
        "ALTER TABLE `epc_erp_properties` MODIFY `ownership` VARCHAR(40) NOT NULL DEFAULT 'owned'",
        "ALTER TABLE `epc_erp_re_valuations` ADD COLUMN `gl_journal_id` BIGINT NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_erp_re_deposits` ADD COLUMN `gl_journal_id` BIGINT NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_erp_re_cheques` ADD COLUMN `gl_journal_id` BIGINT NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_erp_re_cheques` ADD COLUMN `gl_clear_journal_id` BIGINT NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_erp_re_certificates` ADD COLUMN `gl_journal_id` BIGINT NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_erp_re_sales` ADD COLUMN `gl_journal_id` BIGINT NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_erp_property_maintenance` ADD COLUMN `gl_journal_id` BIGINT NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_erp_re_units` ADD COLUMN `allocated_cost` DECIMAL(18,4) NOT NULL DEFAULT 0",
    ];

    private static async Task<ErpSimpleWriteResult> SaveOpportunityAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        var code = Clip(r.Code, 40);
        var name = Clip(r.Name, 190);
        if (code.Length == 0 || name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Opportunity code and name are required.");
        }

        if (r.Id > 0)
        {
            var current = await ScalarText(c, tx, "SELECT `status` FROM `epc_erp_re_opportunities` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);
            if (!ErpPostedTransactionPolicy.CanModify("opportunity", current))
            {
                return PostedBlock(current, "opportunity");
            }

            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_opportunities` SET `name`=?,`stage`=?,`seller`=?,`emirate`=?,`city`=?,`asking_price`=?,`offer_price`=?,`indicative_value`=?,`expected_rent`=?,`opex`=?,`equity`=?,`legal_entity_id`=?,`notes`=?,`status`=? WHERE `id`=?"),
                ct, name, Clip(r.Status, 40, "screening"), Clip(r.Counterparty, 190), Clip(r.Emirate, 40), Clip(r.City, 80),
                Money(r.Amount), Money(r.Amount2), Money(r.Amount3), Money(r.Percent), Money(r.Area), Money(r.Amount2),
                r.LegalEntityId, Clip(r.Note, 255), "open", r.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Investment opportunity saved", r.Id);
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_opportunities` (`code`,`name`,`stage`,`seller`,`emirate`,`city`,`asking_price`,`offer_price`,`indicative_value`,`expected_rent`,`opex`,`equity`,`legal_entity_id`,`notes`,`status`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
            ct, code, name, Clip(r.Status, 40, "screening"), Clip(r.Counterparty, 190), Clip(r.Emirate, 40), Clip(r.City, 80),
            Money(r.Amount), Money(r.Amount2), Money(r.Amount3), Money(r.Percent), Money(r.Area), Money(r.Amount2),
            r.LegalEntityId, Clip(r.Note, 255), "open").ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Investment opportunity saved", await LastId(c, tx, ct).ConfigureAwait(false));
    }

    private async Task<ErpSimpleWriteResult> ConvertOpportunityAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.OpportunityId <= 0 && r.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Opportunity id is required to complete a purchase.");
        }

        var oppId = r.OpportunityId > 0 ? r.OpportunityId : r.Id;
        var current = await ScalarText(c, tx, "SELECT `status` FROM `epc_erp_re_opportunities` WHERE `id`=?", ct, oppId).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(current))
        {
            return ErpSimpleWriteResult.Fail("not_found", "Opportunity was not found.");
        }

        if (!ErpPostedTransactionPolicy.CanModify("opportunity", current))
        {
            return PostedBlock(current, "opportunity");
        }

        await using var command = c.CreateCommand();
        command.Transaction = tx;
        command.CommandText = ErpDb.Positional(
            "SELECT `id`,`code`,`name`,`offer_price`,`asking_price`,`indicative_value`,`emirate`,`city`,`legal_entity_id`,`seller` FROM `epc_erp_re_opportunities` WHERE `id`=? LIMIT 1");
        ErpDb.AddParameters(command, oppId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("not_found", "Opportunity was not found.");
        }

        var code = reader.GetString(1);
        var name = reader.GetString(2);
        var offer = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3);
        var asking = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4);
        var price = offer > 0 ? offer : asking;
        var emirate = reader.IsDBNull(6) ? "" : reader.GetString(6);
        var city = reader.IsDBNull(7) ? "" : reader.GetString(7);
        var le = reader.IsDBNull(8) ? 0L : Convert.ToInt64(reader.GetValue(8), CultureInfo.InvariantCulture);
        var seller = reader.IsDBNull(9) ? "" : reader.GetString(9);
        await reader.DisposeAsync().ConfigureAwait(false);

        var propCode = "PR-" + code;
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_properties` (`company_id`,`code`,`name`,`type`,`ownership`,`city`,`country`,`status`,`acquired_date`,`created_at`,`emirate`,`legal_entity_id`,`acquisition_cost`,`fair_value`) VALUES (0,?,?,?,?,?, 'AE','active',?,?,?,?,?,?)"),
            ct, propCode, name, Clip(r.Category, 40, "residential"), "owned", city,
            DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(), emirate, le, Money(price), Money(price)).ConfigureAwait(false);
        var propertyId = await LastId(c, tx, ct).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_acquisitions` (`property_id`,`opportunity_id`,`legal_entity_id`,`code`,`seller`,`purchase_price`,`eligible_costs`,`completion_date`,`status`) VALUES (?,?,?,?,?,?,?,?,'completed')"),
            ct, propertyId, oppId, le, "ACQ-" + code, seller, Money(price), Money(r.Amount),
            DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ConfigureAwait(false);
        var acqId = await LastId(c, tx, ct).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_opportunities` SET `status`='acquired',`stage`='acquired' WHERE `id`=?"),
            ct, oppId).ConfigureAwait(false);
        var cost = ErpRealEstateCalc.AcquisitionCost(Money(price), Money(r.Amount));
        var journalId = await PostIfrsAsync(c, tx, r.ActorId, le, acqId, "RE-ACQ-" + acqId.ToString(CultureInfo.InvariantCulture),
            "IAS 40 investment property acquisition " + propCode, ErpRealEstateIfrs.Acquisition(cost), ct).ConfigureAwait(false);
        await ErpRealEstateGl.StampAsync(c, tx, "epc_erp_re_acquisitions", acqId, journalId, ct).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Opportunity purchased into owned property " + propCode + ". Posted to the balance sheet.", propertyId);
    }

    private static async Task<ErpSimpleWriteResult> SavePropertyAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        var code = Clip(r.Code, 40);
        var name = Clip(r.Name, 190);
        if (code.Length == 0 || name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Property code and name are required.");
        }

        var status = Clip(r.Status, 40, "active");
        if (r.Id > 0)
        {
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "UPDATE `epc_erp_properties` SET `name`=?,`type`=?,`city`=?,`address`=?,`status`=?,`emirate`=?,`title_deed`=?,`tenure`=?,`legal_entity_id`=?,`area_sqm`=?,`annual_opex`=?,`mortgage_balance`=?,`notes`=? WHERE `id`=?"),
                ct, name, Clip(r.Category, 40, "residential"), Clip(r.City, 80), Clip(r.Address, 500), status,
                Clip(r.Emirate, 40), Clip(r.TitleDeed, 80), Clip(r.Tenure, 40, "freehold"), r.LegalEntityId,
                Money(r.Area), Money(r.Amount2), Money(r.Amount3), Clip(r.Note, 255), r.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Property saved", r.Id);
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_properties` (`company_id`,`code`,`name`,`type`,`ownership`,`address`,`city`,`country`,`area_sqm`,`status`,`created_at`,`emirate`,`title_deed`,`tenure`,`legal_entity_id`,`annual_opex`,`mortgage_balance`,`notes`) VALUES (0,?,?,?,?,?,?, 'AE',?,?,?,?,?,?,?,?,?,?)"),
            ct, code, name, Clip(r.Category, 40, "residential"), "owned", Clip(r.Address, 500), Clip(r.City, 80),
            Money(r.Area), status, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Clip(r.Emirate, 40),
            Clip(r.TitleDeed, 80), Clip(r.Tenure, 40, "freehold"), r.LegalEntityId, Money(r.Amount2), Money(r.Amount3),
            Clip(r.Note, 255)).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Property saved", await LastId(c, tx, ct).ConfigureAwait(false));
    }

    private static async Task<ErpSimpleWriteResult> SaveUnitAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.PropertyId <= 0 || string.IsNullOrWhiteSpace(r.Code))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Property and unit code are required.");
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_units` (`property_id`,`project_id`,`code`,`name`,`unit_type`,`floor_no`,`area_sqm`,`bedrooms`,`status`,`asking_price`) VALUES (?,?,?,?,?,?,?,?,?,?)"),
            ct, r.PropertyId, r.ProjectId, Clip(r.Code, 40), Clip(r.Name, 190, Clip(r.Code, 40)),
            Clip(r.Category, 40, "apartment"), Clip(r.Note, 20), Money(r.Area), r.Count,
            Clip(r.Status, 40, "available"), Money(r.Amount)).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Unit saved", await LastId(c, tx, ct).ConfigureAwait(false));
    }

    private static async Task<ErpSimpleWriteResult> SaveOwnerAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.PropertyId <= 0 || string.IsNullOrWhiteSpace(r.Name))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Property and owner name are required.");
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_owners` (`property_id`,`legal_entity_id`,`owner_name`,`ownership_pct`,`effective_from`,`status`) VALUES (?,?,?,?,?, 'active')"),
            ct, r.PropertyId, r.LegalEntityId, Clip(r.Name, 190), Money(r.Percent <= 0 ? 100 : r.Percent), r.Date1).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Ownership interest saved", await LastId(c, tx, ct).ConfigureAwait(false));
    }

    private static async Task<ErpSimpleWriteResult> SaveAcquisitionAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.PropertyId <= 0 || string.IsNullOrWhiteSpace(r.Code))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Property and acquisition code are required.");
        }

        var cost = ErpRealEstateCalc.AcquisitionCost(r.Amount, r.Amount2);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_acquisitions` (`property_id`,`opportunity_id`,`legal_entity_id`,`code`,`seller`,`purchase_price`,`eligible_costs`,`vat_amount`,`agreement_date`,`status`,`notes`) VALUES (?,?,?,?,?,?,?,?,?,'draft',?)"),
            ct, r.PropertyId, r.OpportunityId, r.LegalEntityId, Clip(r.Code, 40), Clip(r.Counterparty, 190),
            Money(r.Amount), Money(r.Amount2), Money(r.Amount3), r.Date1, Clip(r.Note, 255)).ConfigureAwait(false);
        var id = await LastId(c, tx, ct).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_properties` SET `acquisition_cost`=? WHERE `id`=?"),
            ct, cost, r.PropertyId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Acquisition recorded (cost " + cost.ToString("0.00", CultureInfo.InvariantCulture) + " AED)", id);
    }

    private async Task<ErpSimpleWriteResult> CompleteAcquisitionAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Acquisition id is required.");
        }

        var current = await ScalarText(c, tx, "SELECT `status` FROM `epc_erp_re_acquisitions` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(current))
        {
            return ErpSimpleWriteResult.Fail("not_found", "Acquisition was not found.");
        }

        if (!ErpPostedTransactionPolicy.CanModify("acquisition", current))
        {
            return PostedBlock(current, "acquisition");
        }

        var purchase = await ScalarDec(c, tx, "SELECT IFNULL(`purchase_price`,0) FROM `epc_erp_re_acquisitions` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);
        var eligible = await ScalarDec(c, tx, "SELECT IFNULL(`eligible_costs`,0) FROM `epc_erp_re_acquisitions` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);
        var le = await ScalarLong(c, tx, "SELECT IFNULL(`legal_entity_id`,0) FROM `epc_erp_re_acquisitions` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);
        var existingGl = await ScalarLong(c, tx, "SELECT IFNULL(`gl_journal_id`,0) FROM `epc_erp_re_acquisitions` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_acquisitions` SET `status`='completed',`completion_date`=COALESCE(`completion_date`, CURRENT_DATE) WHERE `id`=? AND `status`='draft'"),
            ct, r.Id).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_properties` p JOIN `epc_erp_re_acquisitions` a ON a.property_id=p.id SET p.status='active', p.acquired_date=COALESCE(p.acquired_date, CURRENT_DATE), p.acquisition_cost=a.purchase_price+a.eligible_costs WHERE a.id=?"),
            ct, r.Id).ConfigureAwait(false);

        if (existingGl <= 0)
        {
            var cost = ErpRealEstateCalc.AcquisitionCost(purchase, eligible);
            var journalId = await PostIfrsAsync(c, tx, r.ActorId, CompanyOf(r, le), r.Id,
                "RE-ACQ-" + r.Id.ToString(CultureInfo.InvariantCulture),
                "IAS 40 investment property acquisition",
                ErpRealEstateIfrs.Acquisition(cost), ct).ConfigureAwait(false);
            await ErpRealEstateGl.StampAsync(c, tx, "epc_erp_re_acquisitions", r.Id, journalId, ct).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Acquisition completed. Investment property posted to the balance sheet (IAS 40).", r.Id);
    }

    private async Task<ErpSimpleWriteResult> SaveDisposalAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.PropertyId <= 0 || string.IsNullOrWhiteSpace(r.Code))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Property and disposal code are required.");
        }

        var carrying = await ScalarDec(
            c,
            tx,
            "SELECT COALESCE(NULLIF(IFNULL(`fair_value`,0),0), IFNULL(`acquisition_cost`,0)+IFNULL(`capex`,0)) FROM `epc_erp_properties` WHERE `id`=?",
            ct,
            r.PropertyId).ConfigureAwait(false);
        var proceeds = ErpRealEstateCalc.NetSaleProceeds(r.Amount, r.Amount2, r.Amount3);
        var gain = ErpRealEstateCalc.RealizedGain(proceeds, carrying);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_disposals` (`property_id`,`code`,`buyer`,`sale_price`,`disposal_costs`,`mortgage_settlement`,`carrying_basis`,`completion_date`,`status`) VALUES (?,?,?,?,?,?,?,?,'completed')"),
            ct, r.PropertyId, Clip(r.Code, 40), Clip(r.Counterparty, 190), Money(r.Amount), Money(r.Amount2), Money(r.Amount3),
            carrying, r.Date1).ConfigureAwait(false);
        var id = await LastId(c, tx, ct).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_properties` SET `status`='disposed',`disposed_date`=CURRENT_DATE WHERE `id`=?"),
            ct, r.PropertyId).ConfigureAwait(false);
        var le = await CompanyForPropertyAsync(c, tx, r, r.PropertyId, ct).ConfigureAwait(false);
        var journalId = await PostIfrsAsync(c, tx, r.ActorId, le, id,
            "RE-DSP-" + id.ToString(CultureInfo.InvariantCulture),
            "IAS 40 disposal of investment property",
            ErpRealEstateIfrs.Disposal(proceeds, carrying), ct).ConfigureAwait(false);
        await ErpRealEstateGl.StampAsync(c, tx, "epc_erp_re_disposals", id, journalId, ct).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Disposal posted. Realized " + gain.ToString("0.00", CultureInfo.InvariantCulture) + " AED is on the income statement.", id);
    }

    private async Task<ErpSimpleWriteResult> SaveValuationAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.PropertyId <= 0 || string.IsNullOrWhiteSpace(r.Code))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Property and valuation code are required.");
        }

        var previous = await ScalarDec(
            c,
            tx,
            "SELECT COALESCE(NULLIF(IFNULL(`fair_value`,0),0), IFNULL(`acquisition_cost`,0)+IFNULL(`capex`,0)) FROM `epc_erp_properties` WHERE `id`=?",
            ct,
            r.PropertyId).ConfigureAwait(false);
        var fair = Money(r.Amount3 > 0 ? r.Amount3 : (r.Amount2 > 0 ? r.Amount2 : r.Amount));
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_valuations` (`property_id`,`code`,`valuation_date`,`purpose`,`method`,`estimated_value`,`appraised_value`,`approved_fair_value`,`valuer`,`status`,`notes`) VALUES (?,?,?,?,?,?,?,?,?,'approved',?)"),
            ct, r.PropertyId, Clip(r.Code, 40), r.Date1 ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Clip(r.Status, 40, "management"), Clip(r.Category, 40, "market"), Money(r.Amount), Money(r.Amount2), fair,
            Clip(r.Counterparty, 190), Clip(r.Note, 255)).ConfigureAwait(false);
        var id = await LastId(c, tx, ct).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_properties` SET `fair_value`=?,`market_value`=? WHERE `id`=?"),
            ct, fair, fair, r.PropertyId).ConfigureAwait(false);
        var le = await CompanyForPropertyAsync(c, tx, r, r.PropertyId, ct).ConfigureAwait(false);
        var journalId = await PostIfrsAsync(c, tx, r.ActorId, le, id,
            "RE-FV-" + id.ToString(CultureInfo.InvariantCulture),
            "IAS 40 fair-value movement through profit or loss",
            ErpRealEstateIfrs.FairValueMovement(previous, fair), ct).ConfigureAwait(false);
        await ErpRealEstateGl.StampAsync(c, tx, "epc_erp_re_valuations", id, journalId, ct).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Valuation saved. Fair-value movement posted to the income statement (IAS 40). Acquisition cost was not overwritten.", id);
    }

    private static async Task<ErpSimpleWriteResult> SaveTenantAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Code) || string.IsNullOrWhiteSpace(r.Name))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Tenant code and name are required.");
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_tenants` (`code`,`name`,`contact`,`trade_licence`,`kyc_status`,`status`) VALUES (?,?,?,?,?,?)"),
            ct, Clip(r.Code, 40), Clip(r.Name, 190), Clip(r.Counterparty, 190), Clip(r.TitleDeed, 80),
            Clip(r.Category, 40, "pending"), Clip(r.Status, 40, "active")).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Tenant saved", await LastId(c, tx, ct).ConfigureAwait(false));
    }

    private async Task<ErpSimpleWriteResult> SaveLeaseAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.PropertyId <= 0 || r.TenantId <= 0 || string.IsNullOrWhiteSpace(r.Code) || string.IsNullOrWhiteSpace(r.Date1) || string.IsNullOrWhiteSpace(r.Date2))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Lease code, property, tenant, start and end dates are required.");
        }

        if (!DateOnly.TryParse(r.Date1, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            || !DateOnly.TryParse(r.Date2, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)
            || end < start)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Lease dates are invalid.");
        }

        var freq = Clip(r.Frequency, 20, "monthly");
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_leases` (`code`,`property_id`,`unit_id`,`tenant_id`,`legal_entity_id`,`start_date`,`end_date`,`annual_rent`,`frequency`,`deposit`,`vat_code`,`ejari_ref`,`ejari_status`,`status`,`notes`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?, 'not_registered','active',?)"),
            ct, Clip(r.Code, 40), r.PropertyId, r.UnitId, r.TenantId, r.LegalEntityId, r.Date1, r.Date2,
            Money(r.Amount), freq, Money(r.Amount2), Clip(r.VatCode, 20, "standard"), Clip(r.TitleDeed, 80),
            Clip(r.Note, 255)).ConfigureAwait(false);
        var leaseId = await LastId(c, tx, ct).ConfigureAwait(false);
        await GenerateScheduleAsync(c, tx, leaseId, start, end, Money(r.Amount), freq, ct).ConfigureAwait(false);
        if (r.Amount2 > 0)
        {
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "INSERT INTO `epc_erp_re_deposits` (`lease_id`,`tenant_id`,`amount`,`held`,`status`) VALUES (?,?,?,?,'held')"),
                ct, leaseId, r.TenantId, Money(r.Amount2), Money(r.Amount2)).ConfigureAwait(false);
            var depositId = await LastId(c, tx, ct).ConfigureAwait(false);
            var journalId = await PostIfrsAsync(c, tx, r.ActorId, CompanyOf(r, r.LegalEntityId), depositId,
                "RE-DEP-" + depositId.ToString(CultureInfo.InvariantCulture),
                "Tenant security deposit — liability, not income",
                ErpRealEstateIfrs.DepositHeld(r.Amount2), ct).ConfigureAwait(false);
            await ErpRealEstateGl.StampAsync(c, tx, "epc_erp_re_deposits", depositId, journalId, ct).ConfigureAwait(false);
        }

        if (r.UnitId > 0)
        {
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_units` SET `status`='occupied' WHERE `id`=? AND `status`='available'"),
                ct, r.UnitId).ConfigureAwait(false);
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_alerts` (`kind`,`title`,`due_date`,`related_id`,`status`) VALUES ('lease_expiry',?,?,?,'open')"),
            ct, "Lease " + Clip(r.Code, 40) + " expires", r.Date2, leaseId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Lease saved and rent schedule generated. Ejari stays unregistered until authority confirmation.", leaseId);
    }

    private static async Task GenerateScheduleAsync(
        DbConnection c, DbTransaction tx, long leaseId, DateOnly start, DateOnly end, decimal annual, string freq, CancellationToken ct)
    {
        var n = ErpRealEstateCalc.PeriodCount(freq, start, end);
        var instalment = ErpRealEstateCalc.InstalmentAmount(annual, freq);
        var cursor = start;
        for (var i = 0; i < n; i++)
        {
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "INSERT INTO `epc_erp_re_rent_invoices` (`lease_id`,`period_date`,`amount`,`status`) VALUES (?,?,?,'open')"),
                ct, leaseId, cursor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), instalment).ConfigureAwait(false);
            cursor = freq switch
            {
                "weekly" => cursor.AddDays(7),
                "quarterly" => cursor.AddMonths(3),
                "semi_annual" => cursor.AddMonths(6),
                "annual" => cursor.AddYears(1),
                _ => cursor.AddMonths(1),
            };
            if (cursor > end)
            {
                break;
            }
        }
    }

    private static async Task<ErpSimpleWriteResult> RenewLeaseAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.LeaseId <= 0 || string.IsNullOrWhiteSpace(r.Date1) || string.IsNullOrWhiteSpace(r.Date2))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Existing lease and new dates are required.");
        }

        var leaseStatus = await ScalarText(c, tx, "SELECT `status` FROM `epc_erp_re_leases` WHERE `id`=?", ct, r.LeaseId).ConfigureAwait(false);
        if (ErpPostedTransactionPolicy.IsVoided(leaseStatus))
        {
            return ErpSimpleWriteResult.Fail("posted", ErpPostedTransactionPolicy.AlreadyClosed);
        }

        await using var command = c.CreateCommand();
        command.Transaction = tx;
        command.CommandText = ErpDb.Positional(
            "SELECT `code`,`property_id`,`unit_id`,`tenant_id`,`legal_entity_id`,`annual_rent`,`frequency`,`deposit`,`vat_code` FROM `epc_erp_re_leases` WHERE `id`=? LIMIT 1");
        ErpDb.AddParameters(command, r.LeaseId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("not_found", "Lease was not found.");
        }

        var code = reader.GetString(0) + "-R";
        var propertyId = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
        var unitId = Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture);
        var tenantId = Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture);
        var le = Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture);
        var rent = reader.GetDecimal(5);
        var freq = reader.GetString(6);
        var deposit = reader.GetDecimal(7);
        var vat = reader.GetString(8);
        await reader.DisposeAsync().ConfigureAwait(false);

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_leases` SET `status`='renewed' WHERE `id`=?"),
            ct, r.LeaseId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_leases` (`code`,`property_id`,`unit_id`,`tenant_id`,`legal_entity_id`,`start_date`,`end_date`,`annual_rent`,`frequency`,`deposit`,`vat_code`,`status`,`prior_lease_id`) VALUES (?,?,?,?,?,?,?,?,?,?,?,'active',?)"),
            ct, code, propertyId, unitId, tenantId, le, r.Date1, r.Date2, rent, freq, deposit, vat, r.LeaseId).ConfigureAwait(false);
        var id = await LastId(c, tx, ct).ConfigureAwait(false);
        if (DateOnly.TryParse(r.Date1, out var start) && DateOnly.TryParse(r.Date2, out var end))
        {
            await GenerateScheduleAsync(c, tx, id, start, end, rent, freq, ct).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Lease renewed. Prior lease " + r.LeaseId + " kept as history.", id);
    }

    private static async Task<ErpSimpleWriteResult> TerminateLeaseAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.LeaseId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Lease id is required.");
        }

        var current = await ScalarText(c, tx, "SELECT `status` FROM `epc_erp_re_leases` WHERE `id`=?", ct, r.LeaseId).ConfigureAwait(false);
        if (ErpPostedTransactionPolicy.IsVoided(current))
        {
            return ErpSimpleWriteResult.Fail("posted", ErpPostedTransactionPolicy.AlreadyClosed);
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_leases` SET `status`='terminated' WHERE `id`=? AND `status` NOT IN ('voided','reversed')"),
            ct, r.LeaseId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_units` u JOIN `epc_erp_re_leases` l ON l.unit_id=u.id SET u.status='available' WHERE l.id=? AND u.status='occupied'"),
            ct, r.LeaseId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Lease terminated. History preserved.", r.LeaseId);
    }

    private async Task<ErpSimpleWriteResult> SaveChequeAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.ChequeNo) || r.Amount <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cheque number and amount are required.");
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_cheques` (`code`,`cheque_no`,`lease_id`,`invoice_id`,`tenant_id`,`property_id`,`amount`,`bank_name`,`cheque_date`,`status`,`notes`) VALUES (?,?,?,?,?,?,?,?,?,'received',?)"),
            ct, Clip(r.Code, 40, "CHQ-" + Clip(r.ChequeNo, 30)), Clip(r.ChequeNo, 40), r.LeaseId, r.RelatedId,
            r.TenantId, r.PropertyId, Money(r.Amount), Clip(r.BankName, 120), r.Date1, Clip(r.Note, 255)).ConfigureAwait(false);
        var id = await LastId(c, tx, ct).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_alerts` (`kind`,`title`,`due_date`,`related_id`,`status`) VALUES ('cheque_deposit',?,?,?,'open')"),
            ct, "Cheque " + Clip(r.ChequeNo, 40) + " due for deposit", r.Date1, id).ConfigureAwait(false);
        var le = await CompanyForPropertyAsync(c, tx, r, r.PropertyId, ct).ConfigureAwait(false);
        var journalId = await PostIfrsAsync(c, tx, r.ActorId, le, id,
            "RE-CHQ-" + id.ToString(CultureInfo.InvariantCulture),
            "PDC received — cheques under collection, not cash",
            ErpRealEstateIfrs.ChequeReceived(r.Amount), ct).ConfigureAwait(false);
        await ErpRealEstateGl.StampAsync(c, tx, "epc_erp_re_cheques", id, journalId, ct).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Cheque received. It is not cash until bank clearance. Rental income is on the income statement; the PDC sits on the balance sheet.", id);
    }

    private async Task<ErpSimpleWriteResult> ChequeStatusAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        var status = Clip(r.Status, 40).ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "received", "held", "due_for_deposit", "deposited", "cleared", "bounced", "cancelled", "replaced", "returned",
        };
        if (r.Id <= 0 || !allowed.Contains(status))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cheque id and a valid status are required.");
        }

        var current = await ScalarText(c, tx, "SELECT `status` FROM `epc_erp_re_cheques` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);
        if (ErpPostedTransactionPolicy.IsVoided(current)
            || string.Equals(current, "bounced", StringComparison.OrdinalIgnoreCase)
            || string.Equals(current, "replaced", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("posted", ErpPostedTransactionPolicy.RefuseModify);
        }

        if (string.Equals(current, "cleared", StringComparison.OrdinalIgnoreCase))
        {
            if (status != "bounced")
            {
                return ErpSimpleWriteResult.Fail("posted", ErpPostedTransactionPolicy.RefuseModify);
            }

            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_cheques` SET `status`='bounced' WHERE `id`=?"),
                ct, r.Id).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "INSERT INTO `epc_erp_re_alerts` (`kind`,`title`,`related_id`,`status`) VALUES ('cheque_bounce',?,?,'open')"),
                ct, "Bounced after clearance " + r.Id, r.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Cheque bounced after clearance. Prior cleared allocation and cash journal were left intact.", r.Id);
        }

        if (status == "cleared")
        {
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_cheques` SET `status`='cleared',`clearance_date`=CURRENT_DATE WHERE `id`=? AND `status` IN ('deposited','held','due_for_deposit')"),
                ct, r.Id).ConfigureAwait(false);
            await AllocateChequeAsync(c, tx, r.Id, ct).ConfigureAwait(false);
            var amount = await ScalarDec(c, tx, "SELECT IFNULL(`amount`,0) FROM `epc_erp_re_cheques` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);
            var propertyId = await ScalarLong(c, tx, "SELECT IFNULL(`property_id`,0) FROM `epc_erp_re_cheques` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);
            var le = await CompanyForPropertyAsync(c, tx, r, propertyId, ct).ConfigureAwait(false);
            var journalId = await PostIfrsAsync(c, tx, r.ActorId, le, r.Id,
                "RE-CHQ-CLR-" + r.Id.ToString(CultureInfo.InvariantCulture),
                "PDC cleared into bank",
                ErpRealEstateIfrs.ChequeCleared(amount), ct).ConfigureAwait(false);
            await ErpRealEstateGl.StampAsync(c, tx, "epc_erp_re_cheques", r.Id, journalId, ct, "gl_clear_journal_id").ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Cheque cleared after bank confirmation. Cash is now on the balance sheet.", r.Id);
        }

        if (status == "bounced")
        {
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_cheques` SET `status`='bounced' WHERE `id`=?"),
                ct, r.Id).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "INSERT INTO `epc_erp_re_alerts` (`kind`,`title`,`related_id`,`status`) VALUES ('cheque_bounce',?,?,'open')"),
                ct, "Bounced cheque " + r.Id, r.Id).ConfigureAwait(false);
            var receivedJournal = await ScalarLong(c, tx, "SELECT IFNULL(`gl_journal_id`,0) FROM `epc_erp_re_cheques` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);
            await ErpRealEstateGl.ReverseAsync(_gl, c, tx, r.ActorId, receivedJournal, ct).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Cheque marked bounced. Receipt allocation was not created. The PDC and rental income journals were reversed.", r.Id);
        }

        if (status == "replaced")
        {
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_cheques` SET `status`='replaced',`replacement_id`=? WHERE `id`=?"),
                ct, r.RelatedId, r.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Cheque replaced. History kept.", r.Id);
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_cheques` SET `status`=?,`deposit_date`=CASE WHEN ?='deposited' THEN CURRENT_DATE ELSE `deposit_date` END WHERE `id`=?"),
            ct, status, status, r.Id).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Cheque status updated to " + status, r.Id);
    }

    private static async Task AllocateChequeAsync(DbConnection c, DbTransaction tx, long chequeId, CancellationToken ct)
    {
        await using var command = c.CreateCommand();
        command.Transaction = tx;
        command.CommandText = ErpDb.Positional(
            "SELECT `lease_id`,`invoice_id`,`amount` FROM `epc_erp_re_cheques` WHERE `id`=? LIMIT 1");
        ErpDb.AddParameters(command, chequeId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        var leaseId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
        var invoiceId = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
        var amount = reader.GetDecimal(2);
        await reader.DisposeAsync().ConfigureAwait(false);
        if (invoiceId > 0)
        {
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_rent_invoices` SET `allocated`=LEAST(`amount`,`allocated`+?), `status`=CASE WHEN `allocated`+?>=`amount` THEN 'paid' ELSE 'partial' END WHERE `id`=?"),
                ct, amount, amount, invoiceId).ConfigureAwait(false);
            return;
        }

        if (leaseId > 0)
        {
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_rent_invoices` SET `allocated`=LEAST(`amount`,`allocated`+?), `status`=CASE WHEN `allocated`+?>=`amount` THEN 'paid' ELSE 'partial' END WHERE `lease_id`=? AND `status` IN ('open','partial') ORDER BY `period_date` LIMIT 1"),
                ct, amount, amount, leaseId).ConfigureAwait(false);
        }
    }

    private async Task<ErpSimpleWriteResult> SaveDepositAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.LeaseId <= 0 || r.Amount <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Lease and deposit amount are required.");
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_deposits` (`lease_id`,`tenant_id`,`amount`,`held`,`status`,`notes`) VALUES (?,?,?,?,'held',?)"),
            ct, r.LeaseId, r.TenantId, Money(r.Amount), Money(r.Amount), Clip(r.Note, 255)).ConfigureAwait(false);
        var id = await LastId(c, tx, ct).ConfigureAwait(false);
        var journalId = await PostIfrsAsync(c, tx, r.ActorId, CompanyOf(r, r.LegalEntityId), id,
            "RE-DEP-" + id.ToString(CultureInfo.InvariantCulture),
            "Tenant security deposit — liability, not income",
            ErpRealEstateIfrs.DepositHeld(r.Amount), ct).ConfigureAwait(false);
        await ErpRealEstateGl.StampAsync(c, tx, "epc_erp_re_deposits", id, journalId, ct).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Security deposit held as a liability on the balance sheet, not income.", id);
    }

    private async Task<ErpSimpleWriteResult> SaveMaintenanceAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.PropertyId <= 0 || string.IsNullOrWhiteSpace(r.Name))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Property and work-order description are required.");
        }

        var nature = Clip(r.VatCode, 20, "opex").ToLowerInvariant();
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_property_maintenance` (`property_id`,`type`,`description`,`vendor`,`scheduled_date`,`cost`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?)"),
            ct, r.PropertyId, Clip(r.Category, 40, "corrective"), Clip(r.Name, 500), Clip(r.Counterparty, 190),
            r.Date1, Money(r.Amount), Clip(r.Status, 40, "planned"), DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        var id = await LastId(c, tx, ct).ConfigureAwait(false);
        if (r.Amount > 0)
        {
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "UPDATE `epc_erp_properties` SET `capex`=`capex`+? WHERE `id`=? AND ?='capex'"),
                ct, Money(r.Amount), r.PropertyId, nature).ConfigureAwait(false);
            var le = await CompanyForPropertyAsync(c, tx, r, r.PropertyId, ct).ConfigureAwait(false);
            var recipe = nature == "capex"
                ? ErpRealEstateIfrs.CapitalExpenditure(r.Amount)
                : ErpRealEstateIfrs.OperatingExpense(r.Amount);
            var journalId = await PostIfrsAsync(c, tx, r.ActorId, le, id,
                "RE-WO-" + id.ToString(CultureInfo.InvariantCulture),
                nature == "capex" ? "Subsequent cost capitalised to investment property" : "Property operating expense",
                recipe, ct).ConfigureAwait(false);
            await ErpRealEstateGl.StampAsync(c, tx, "epc_erp_property_maintenance", id, journalId, ct).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok(
            nature == "capex"
                ? "Maintenance work order saved. Capex posted to the balance sheet."
                : "Maintenance work order saved. Opex posted to the income statement.",
            id);
    }

    private static async Task<ErpSimpleWriteResult> SaveProjectAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Code) || string.IsNullOrWhiteSpace(r.Name))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project code and name are required.");
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_projects` (`code`,`name`,`property_id`,`legal_entity_id`,`emirate`,`stage`,`land_cost`,`original_budget`,`revised_budget`,`expected_sales`,`escrow_required`,`status`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)"),
            ct, Clip(r.Code, 40), Clip(r.Name, 190), r.PropertyId, r.LegalEntityId, Clip(r.Emirate, 40),
            Clip(r.Status, 40, "feasibility"), Money(r.Amount), Money(r.Amount2), Money(r.Amount2), Money(r.Amount3),
            r.Count > 0 ? 1 : 0, "active").ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Development project saved", await LastId(c, tx, ct).ConfigureAwait(false));
    }

    private static async Task<ErpSimpleWriteResult> SaveProjectUnitAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.ProjectId <= 0 || string.IsNullOrWhiteSpace(r.Code))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project and unit code are required.");
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_units` (`property_id`,`project_id`,`code`,`name`,`unit_type`,`area_sqm`,`status`,`asking_price`) VALUES (?,?,?,?,?,?, 'available',?)"),
            ct, r.PropertyId, r.ProjectId, Clip(r.Code, 40), Clip(r.Name, 190, Clip(r.Code, 40)),
            Clip(r.Category, 40, "apartment"), Money(r.Area), Money(r.Amount)).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Saleable unit added to project inventory", await LastId(c, tx, ct).ConfigureAwait(false));
    }

    private async Task<ErpSimpleWriteResult> SaveSaleAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.UnitId <= 0 || string.IsNullOrWhiteSpace(r.Name) || r.Amount <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unit, buyer and sale price are required.");
        }

        var blocked = await ScalarLong(c, tx,
            "SELECT COUNT(*) FROM `epc_erp_re_sales` WHERE `unit_id`=? AND `status` IN ('reserved','sold','handed_over')",
            ct, r.UnitId).ConfigureAwait(false);
        if (blocked > 0)
        {
            return ErpSimpleWriteResult.Fail("unit_sold", "This unit is already reserved or sold.");
        }

        var status = Clip(r.Status, 40, "reserved").ToLowerInvariant();
        if (status is not ("reserved" or "sold" or "handed_over" or "recognized"))
        {
            status = "reserved";
        }

        var recognized = Money(r.Amount3 > 0 ? r.Amount3 : (status is "sold" or "handed_over" or "recognized" ? r.Amount : 0));
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_sales` (`code`,`project_id`,`unit_id`,`buyer`,`sale_price`,`collected`,`invoiced`,`recognized`,`status`,`vat_code`) VALUES (?,?,?,?,?,?,0,?,?,?)"),
            ct, Clip(r.Code, 40, "SAL-" + r.UnitId), r.ProjectId, r.UnitId, Clip(r.Name, 190), Money(r.Amount),
            Money(r.Amount2), recognized, status, Clip(r.VatCode, 20, "standard")).ConfigureAwait(false);
        var id = await LastId(c, tx, ct).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_units` SET `status`=? WHERE `id`=?"),
            ct, status == "reserved" ? "reserved" : "sold", r.UnitId).ConfigureAwait(false);

        if (recognized > 0)
        {
            var allocated = await ScalarDec(c, tx, "SELECT IFNULL(`allocated_cost`,0) FROM `epc_erp_re_units` WHERE `id`=?", ct, r.UnitId).ConfigureAwait(false);
            var asking = await ScalarDec(c, tx, "SELECT IFNULL(`asking_price`,0) FROM `epc_erp_re_units` WHERE `id`=?", ct, r.UnitId).ConfigureAwait(false);
            var unitCost = ErpRealEstateCalc.UnitCostOfSale(allocated, asking);
            var journalId = await PostIfrsAsync(c, tx, r.ActorId, CompanyOf(r, r.LegalEntityId), id,
                "RE-SAL-" + id.ToString(CultureInfo.InvariantCulture),
                "IFRS 15 development revenue when performance obligations are met",
                ErpRealEstateIfrs.DevelopmentSale(recognized, unitCost), ct).ConfigureAwait(false);
            await ErpRealEstateGl.StampAsync(c, tx, "epc_erp_re_sales", id, journalId, ct).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Unit sold. IFRS 15 revenue posted to the income statement.", id);
        }

        return ErpSimpleWriteResult.Ok("Unit reserved. Reservation stays off the statements until sold (IFRS 15).", id);
    }

    private static async Task<ErpSimpleWriteResult> SaveContractorAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Code) || string.IsNullOrWhiteSpace(r.Name))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Contractor code and name are required.");
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_contractors` (`code`,`name`,`trade`,`licence`,`trn`,`status`) VALUES (?,?,?,?,?,?)"),
            ct, Clip(r.Code, 40), Clip(r.Name, 190), Clip(r.Category, 80), Clip(r.TitleDeed, 80),
            Clip(r.VatCode, 40), Clip(r.Status, 40, "approved")).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Contractor saved", await LastId(c, tx, ct).ConfigureAwait(false));
    }

    private static async Task<ErpSimpleWriteResult> SaveContractAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.ProjectId <= 0 || r.RelatedId <= 0 || string.IsNullOrWhiteSpace(r.Code))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project, contractor and contract code are required.");
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_contracts` (`code`,`project_id`,`contractor_id`,`original_amount`,`retention_pct`,`advance`,`status`) VALUES (?,?,?,?,?,?,'active')"),
            ct, Clip(r.Code, 40), r.ProjectId, r.RelatedId, Money(r.Amount), r.Percent <= 0 ? 10m : r.Percent, Money(r.Amount2)).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Construction contract saved", await LastId(c, tx, ct).ConfigureAwait(false));
    }

    private static async Task<ErpSimpleWriteResult> SaveVariationAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.ContractId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Contract id is required.");
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_contracts` SET `variations`=`variations`+? WHERE `id`=?"),
            ct, Money(r.Amount), r.ContractId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Approved variation added to the contract.", r.ContractId);
    }

    private async Task<ErpSimpleWriteResult> SaveCertificateAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.ContractId <= 0 || string.IsNullOrWhiteSpace(r.Code))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Contract and certificate code are required.");
        }

        var net = ErpRealEstateCalc.CertificateNet(
            r.Amount, r.Amount2, r.Amount3, r.Percent, r.Area, Money(r.Count), 0, 0);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_certificates` (`code`,`contract_id`,`previous_certified`,`current_measured`,`materials_on_site`,`approved_variations`,`advance_recovery`,`retention`,`net_payable`,`status`) VALUES (?,?,?,?,?,?,?,?,?,'approved')"),
            ct, Clip(r.Code, 40), r.ContractId, Money(r.Amount), Money(r.Amount2), Money(r.Amount3), Money(r.Percent),
            Money(r.Area), Money(r.Count), net).ConfigureAwait(false);
        var id = await LastId(c, tx, ct).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_contracts` SET `advance_recovered`=`advance_recovered`+? WHERE `id`=?"),
            ct, Money(r.Area), r.ContractId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_projects` p JOIN `epc_erp_re_contracts` c ON c.project_id=p.id SET p.actual_cost=p.actual_cost+? WHERE c.id=?"),
            ct, net, r.ContractId).ConfigureAwait(false);
        var journalId = await PostIfrsAsync(c, tx, r.ActorId, CompanyOf(r, r.LegalEntityId), id,
            "RE-CERT-" + id.ToString(CultureInfo.InvariantCulture),
            "Construction certificate — CIP asset and contractor payable",
            ErpRealEstateIfrs.Certificate(net), ct).ConfigureAwait(false);
        await ErpRealEstateGl.StampAsync(c, tx, "epc_erp_re_certificates", id, journalId, ct).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Payment certificate posted. Net payable " + net.ToString("0.00", CultureInfo.InvariantCulture) + " AED is on the balance sheet.", id);
    }

    private static async Task<ErpSimpleWriteResult> SaveBoqLineAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.ContractId <= 0 || string.IsNullOrWhiteSpace(r.Code))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Contract and BOQ line code are required.");
        }

        var qty = r.Amount;
        var rate = r.Amount2;
        var amount = ErpRealEstateCalc.BoqAmount(qty, rate);
        if (amount <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "BOQ qty and rate must produce a positive amount.");
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_boq_lines` (`contract_id`,`code`,`description`,`trade`,`qty`,`rate`,`amount`,`status`) VALUES (?,?,?,?,?,?,?,'draft')"),
            ct, r.ContractId, Clip(r.Code, 40), Clip(r.Name, 190), Clip(r.Category, 80), Money(qty), Money(rate), amount).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(
            "BOQ line saved. Amount " + amount.ToString("0.00", CultureInfo.InvariantCulture) + " AED (qty × rate). Tender grid stays operational until certified.",
            await LastId(c, tx, ct).ConfigureAwait(false));
    }

    private async Task<ErpSimpleWriteResult> SaveEscrowReceiptAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.ProjectId <= 0 || r.Amount <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project and escrow receipt amount are required.");
        }

        var required = await ScalarLong(c, tx, "SELECT IFNULL(`escrow_required`,0) FROM `epc_erp_re_projects` WHERE `id`=?", ct, r.ProjectId).ConfigureAwait(false);
        if (required <= 0)
        {
            return ErpSimpleWriteResult.Fail("escrow", "This project is not flagged for escrow. Set escrow required on the project first.");
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_escrow` (`project_id`,`kind`,`amount`,`reference`,`txn_date`,`status`,`notes`) VALUES (?, 'receipt', ?, ?, ?, 'received', ?)"),
            ct, r.ProjectId, Money(r.Amount), Clip(r.Code, 80, "ESC-IN"), DateOrNull(r.Date1), Clip(r.Note, 255)).ConfigureAwait(false);
        var id = await LastId(c, tx, ct).ConfigureAwait(false);
        var journalId = await PostIfrsAsync(c, tx, r.ActorId, CompanyOf(r, r.LegalEntityId), id,
            "RE-ESC-IN-" + id.ToString(CultureInfo.InvariantCulture),
            "Project escrow receipt — restricted cash and IFRS 15 contract liability",
            ErpRealEstateIfrs.EscrowReceipt(r.Amount), ct).ConfigureAwait(false);
        await ErpRealEstateGl.StampAsync(c, tx, "epc_erp_re_escrow", id, journalId, ct).ConfigureAwait(false);
        var (receipts, disbursed) = await EscrowTotalsAsync(c, tx, r.ProjectId, ct).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(
            "Escrow receipt posted to the balance sheet (restricted cash, not income). Balance "
            + ErpRealEstateCalc.EscrowBalance(receipts, disbursed).ToString("0.00", CultureInfo.InvariantCulture) + " AED.",
            id);
    }

    private async Task<ErpSimpleWriteResult> SaveEscrowDisbursementAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.ProjectId <= 0 || r.Amount <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project and disbursement amount are required.");
        }

        var required = await ScalarLong(c, tx, "SELECT IFNULL(`escrow_required`,0) FROM `epc_erp_re_projects` WHERE `id`=?", ct, r.ProjectId).ConfigureAwait(false);
        if (required <= 0)
        {
            return ErpSimpleWriteResult.Fail("escrow", "This project is not flagged for escrow. Permitted disbursements are blocked.");
        }

        var (receipts, disbursed) = await EscrowTotalsAsync(c, tx, r.ProjectId, ct).ConfigureAwait(false);
        if (!ErpRealEstateCalc.EscrowDisbursementPermitted(receipts, disbursed, r.Amount))
        {
            return ErpSimpleWriteResult.Fail(
                "escrow",
                "Disbursement is not permitted. Escrow receipts "
                + receipts.ToString("0.00", CultureInfo.InvariantCulture)
                + " AED, already disbursed "
                + disbursed.ToString("0.00", CultureInfo.InvariantCulture)
                + " AED.");
        }

        var certificateId = r.RelatedId > 0 ? r.RelatedId : r.ContractId;
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_escrow` (`project_id`,`kind`,`amount`,`certificate_id`,`reference`,`txn_date`,`status`,`notes`) VALUES (?, 'disbursement', ?, ?, ?, ?, 'disbursed', ?)"),
            ct, r.ProjectId, Money(r.Amount), certificateId, Clip(r.Code, 80, "ESC-OUT"), DateOrNull(r.Date1), Clip(r.Note, 255)).ConfigureAwait(false);
        var id = await LastId(c, tx, ct).ConfigureAwait(false);
        var journalId = await PostIfrsAsync(c, tx, r.ActorId, CompanyOf(r, r.LegalEntityId), id,
            "RE-ESC-OUT-" + id.ToString(CultureInfo.InvariantCulture),
            certificateId > 0
                ? "Permitted escrow disbursement settling contractor payable"
                : "Permitted escrow disbursement to construction in progress",
            ErpRealEstateIfrs.EscrowDisbursement(r.Amount, certificateId > 0), ct).ConfigureAwait(false);
        await ErpRealEstateGl.StampAsync(c, tx, "epc_erp_re_escrow", id, journalId, ct).ConfigureAwait(false);
        var balance = ErpRealEstateCalc.EscrowBalance(receipts, disbursed + Money(r.Amount));
        return ErpSimpleWriteResult.Ok(
            "Permitted escrow disbursement posted. Remaining escrow "
            + balance.ToString("0.00", CultureInfo.InvariantCulture) + " AED.",
            id);
    }

    private static async Task<ErpSimpleWriteResult> SaveMilestoneAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.ProjectId <= 0 || string.IsNullOrWhiteSpace(r.Code) || string.IsNullOrWhiteSpace(r.Name))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project, milestone code and name are required.");
        }

        if (r.Percent <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Milestone percent of the sale price is required.");
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_milestones` (`project_id`,`code`,`name`,`due_date`,`percent`,`status`) VALUES (?,?,?,?,?,'planned')"),
            ct, r.ProjectId, Clip(r.Code, 40), Clip(r.Name, 190), DateOrNull(r.Date1), r.Percent).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Construction milestone saved. Off-plan instalments can now be generated from it.", await LastId(c, tx, ct).ConfigureAwait(false));
    }

    private static async Task<ErpSimpleWriteResult> GenerateInstalmentsAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        var saleId = r.SaleId > 0 ? r.SaleId : r.RelatedId;
        if (saleId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Sale id is required to generate the instalment calendar.");
        }

        var existing = await ScalarLong(c, tx,
            "SELECT COUNT(*) FROM `epc_erp_re_instalments` WHERE `sale_id`=? AND `status` NOT IN ('voided','reversed')",
            ct, saleId).ConfigureAwait(false);
        if (existing > 0)
        {
            return ErpSimpleWriteResult.Fail("exists", "Instalments already exist for this sale.");
        }

        var price = await ScalarDec(c, tx, "SELECT IFNULL(`sale_price`,0) FROM `epc_erp_re_sales` WHERE `id`=?", ct, saleId).ConfigureAwait(false);
        var projectId = await ScalarLong(c, tx, "SELECT IFNULL(`project_id`,0) FROM `epc_erp_re_sales` WHERE `id`=?", ct, saleId).ConfigureAwait(false);
        if (price <= 0 || projectId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Sale must have a price and a development project.");
        }

        var milestones = new List<(long Id, decimal Percent, string Due)>();
        await using (var command = c.CreateCommand())
        {
            command.Transaction = tx;
            command.CommandText = ErpDb.Positional(
                "SELECT `id`,`percent`,IFNULL(`due_date`,'') FROM `epc_erp_re_milestones` WHERE `project_id`=? AND `status` NOT IN ('voided','reversed') ORDER BY `id`");
            ErpDb.AddParameters(command, projectId);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                milestones.Add((
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture),
                    Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? ""));
            }
        }

        if (milestones.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Add project milestones before generating the off-plan calendar.");
        }

        var amounts = milestones.Select(m => ErpRealEstateCalc.OffPlanInstalment(price, m.Percent)).ToList();
        if (Math.Abs(milestones.Sum(m => m.Percent) - 100m) < 0.011m && amounts.Count > 0)
        {
            var head = amounts.Take(amounts.Count - 1).Sum();
            amounts[^1] = Money(price - head);
        }

        for (var i = 0; i < milestones.Count; i++)
        {
            var due = string.IsNullOrWhiteSpace(milestones[i].Due) ? (object)DBNull.Value : milestones[i].Due;
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "INSERT INTO `epc_erp_re_instalments` (`sale_id`,`milestone_id`,`due_date`,`percent`,`amount`,`collected`,`status`) VALUES (?,?,?,?,?,0,'due')"),
                ct, saleId, milestones[i].Id, due, milestones[i].Percent, amounts[i]).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok(
            milestones.Count.ToString(CultureInfo.InvariantCulture) + " off-plan instalments generated from construction milestones.",
            saleId);
    }

    private async Task<ErpSimpleWriteResult> CollectInstalmentAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Instalment id is required.");
        }

        var current = await ScalarText(c, tx, "SELECT `status` FROM `epc_erp_re_instalments` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(current))
        {
            return ErpSimpleWriteResult.Fail("not_found", "Instalment was not found.");
        }

        if (!ErpPostedTransactionPolicy.CanModify("instalment", current))
        {
            return PostedBlock(current, "instalment");
        }

        var due = await ScalarDec(c, tx, "SELECT IFNULL(`amount`,0) FROM `epc_erp_re_instalments` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);
        var already = await ScalarDec(c, tx, "SELECT IFNULL(`collected`,0) FROM `epc_erp_re_instalments` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);
        var collect = r.Amount > 0 ? Money(r.Amount) : Money(due - already);
        if (collect <= 0 || collect > Money(due - already))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Collection must be a positive amount not exceeding the instalment balance.");
        }

        var saleId = await ScalarLong(c, tx, "SELECT `sale_id` FROM `epc_erp_re_instalments` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);
        var saleStatus = await ScalarText(c, tx, "SELECT `status` FROM `epc_erp_re_sales` WHERE `id`=?", ct, saleId).ConfigureAwait(false);
        var projectId = await ScalarLong(c, tx, "SELECT IFNULL(`project_id`,0) FROM `epc_erp_re_sales` WHERE `id`=?", ct, saleId).ConfigureAwait(false);
        var escrow = projectId > 0
            && await ScalarLong(c, tx, "SELECT IFNULL(`escrow_required`,0) FROM `epc_erp_re_projects` WHERE `id`=?", ct, projectId).ConfigureAwait(false) > 0;
        var recognized = saleStatus is "sold" or "handed_over" or "recognized";

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_instalments` SET `collected`=`collected`+?, `status`=CASE WHEN `collected`+?>=`amount` THEN 'collected' ELSE 'due' END WHERE `id`=?"),
            ct, collect, collect, r.Id).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_sales` SET `collected`=`collected`+? WHERE `id`=?"),
            ct, collect, saleId).ConfigureAwait(false);

        var journalId = await PostIfrsAsync(c, tx, r.ActorId, CompanyOf(r, r.LegalEntityId), r.Id,
            "RE-INST-" + r.Id.ToString(CultureInfo.InvariantCulture),
            recognized
                ? "Off-plan collection against IFRS 15 receivable"
                : "Off-plan collection — contract liability, not revenue",
            ErpRealEstateIfrs.OffPlanCollection(collect, escrow, recognized), ct).ConfigureAwait(false);
        await ErpRealEstateGl.StampAsync(c, tx, "epc_erp_re_instalments", r.Id, journalId, ct).ConfigureAwait(false);

        if (escrow)
        {
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "INSERT INTO `epc_erp_re_escrow` (`project_id`,`kind`,`amount`,`reference`,`txn_date`,`status`,`gl_journal_id`,`notes`) VALUES (?, 'receipt', ?, ?, CURRENT_DATE, 'received', ?, ?)"),
                ct, projectId, collect, "INST-" + r.Id.ToString(CultureInfo.InvariantCulture), journalId,
                "Off-plan instalment collection into escrow").ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok(
            recognized
                ? "Instalment collected against the IFRS 15 receivable on the balance sheet."
                : "Instalment collected as an IFRS 15 contract liability. It is not development revenue until the unit is sold.",
            r.Id);
    }

    private static async Task<ErpSimpleWriteResult> AllocateSharedCostAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.ProjectId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project id is required to allocate shared cost.");
        }

        var land = await ScalarDec(c, tx, "SELECT IFNULL(`land_cost`,0) FROM `epc_erp_re_projects` WHERE `id`=?", ct, r.ProjectId).ConfigureAwait(false);
        var actual = await ScalarDec(c, tx, "SELECT IFNULL(`actual_cost`,0) FROM `epc_erp_re_projects` WHERE `id`=?", ct, r.ProjectId).ConfigureAwait(false);
        var total = r.Amount > 0 ? Money(r.Amount) : Money(land + actual);
        if (total <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Nothing to allocate. Set a project land/actual cost or pass an amount.");
        }

        var basis = Clip(r.Category, 20, "area").ToLowerInvariant();
        if (basis is not ("area" or "equal"))
        {
            basis = "area";
        }

        var units = new List<(long Id, decimal Area)>();
        await using (var command = c.CreateCommand())
        {
            command.Transaction = tx;
            command.CommandText = ErpDb.Positional(
                "SELECT `id`,IFNULL(`area_sqm`,0) FROM `epc_erp_re_units` WHERE `project_id`=? ORDER BY `id`");
            ErpDb.AddParameters(command, r.ProjectId);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                units.Add((
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture)));
            }
        }

        if (units.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Add saleable units to the project before allocating shared cost.");
        }

        var weights = units.Select(u => basis == "equal" ? 1m : u.Area).ToArray();
        var parts = ErpRealEstateCalc.AllocateSharedCost(total, weights);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_cost_allocs` SET `status`='reversed' WHERE `project_id`=? AND `status`='allocated'"),
            ct, r.ProjectId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_units` SET `allocated_cost`=0 WHERE `project_id`=?"),
            ct, r.ProjectId).ConfigureAwait(false);

        for (var i = 0; i < units.Count; i++)
        {
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "INSERT INTO `epc_erp_re_cost_allocs` (`project_id`,`unit_id`,`basis`,`weight`,`amount`,`status`) VALUES (?,?,?,?,?,'allocated')"),
                ct, r.ProjectId, units[i].Id, basis, weights[i], parts[i]).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_units` SET `allocated_cost`=? WHERE `id`=?"),
                ct, parts[i], units[i].Id).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok(
            "Shared cost of " + total.ToString("0.00", CultureInfo.InvariantCulture)
            + " AED allocated across " + units.Count.ToString(CultureInfo.InvariantCulture)
            + " units by " + basis + ". IFRS 15 unit cost will use the allocated figure.",
            r.ProjectId);
    }

    private static async Task<(decimal Receipts, decimal Disbursed)> EscrowTotalsAsync(
        DbConnection c, DbTransaction tx, long projectId, CancellationToken ct)
    {
        var receipts = await ScalarDec(c, tx,
            "SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_re_escrow` WHERE `project_id`=? AND `kind`='receipt' AND `status` NOT IN ('voided','reversed')",
            ct, projectId).ConfigureAwait(false);
        var disbursed = await ScalarDec(c, tx,
            "SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_re_escrow` WHERE `project_id`=? AND `kind`='disbursement' AND `status` NOT IN ('voided','reversed')",
            ct, projectId).ConfigureAwait(false);
        return (receipts, disbursed);
    }

    private static async Task<ErpSimpleWriteResult> SaveDocumentAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Document title is required.");
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_erp_re_documents` (`code`,`doc_type`,`related_kind`,`related_id`,`title`,`expiry_date`,`status`) VALUES (?,?,?,?,?,?, 'uploaded')"),
            ct, Clip(r.Code, 40, "DOC"), Clip(r.Category, 40, "other"), Clip(r.Status, 40, "property"),
            r.RelatedId, Clip(r.Name, 190), r.Date1).ConfigureAwait(false);
        var id = await LastId(c, tx, ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(r.Date1))
        {
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "INSERT INTO `epc_erp_re_alerts` (`kind`,`title`,`due_date`,`related_id`,`status`) VALUES ('document_expiry',?,?,?,'open')"),
                ct, "Document expiry: " + Clip(r.Name, 80), r.Date1, id).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Document register row saved", id);
    }

    private static async Task<ErpSimpleWriteResult> AckAlertAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        if (r.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Alert id is required.");
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `epc_erp_re_alerts` SET `status`='acknowledged' WHERE `id`=?"),
            ct, r.Id).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Alert acknowledged", r.Id);
    }

    private async Task<ErpSimpleWriteResult> VoidDocumentAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        var kind = ErpPostedTransactionPolicy.NormalizeKind(r.Category ?? r.Status);
        if (r.Id <= 0 || kind.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Document kind and id are required to void.");
        }

        var map = TableFor(kind);
        if (map is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "That document kind cannot be voided.");
        }

        var current = await ScalarText(c, tx, "SELECT `status` FROM `" + map.Value.Table + "` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(current))
        {
            return ErpSimpleWriteResult.Fail("not_found", "Document was not found.");
        }

        if (!ErpPostedTransactionPolicy.CanVoid(kind, current))
        {
            return ErpSimpleWriteResult.Fail("posted", ErpPostedTransactionPolicy.AlreadyClosed);
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `" + map.Value.Table + "` SET `status`='voided' WHERE `id`=?"),
            ct, r.Id).ConfigureAwait(false);
        await UnwindAsync(c, tx, kind, r.Id, r.ActorId, ct).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(kind + " voided. The original row stays on file.", r.Id);
    }

    private async Task<ErpSimpleWriteResult> ReverseDocumentAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        var kind = ErpPostedTransactionPolicy.NormalizeKind(r.Category ?? r.Status);
        if (r.Id <= 0 || kind.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Document kind and id are required to reverse.");
        }

        var map = TableFor(kind);
        if (map is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "That document kind cannot be reversed.");
        }

        var current = await ScalarText(c, tx, "SELECT `status` FROM `" + map.Value.Table + "` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);
        if (!ErpPostedTransactionPolicy.CanReverse(kind, current))
        {
            return ErpSimpleWriteResult.Fail("posted", ErpPostedTransactionPolicy.IsVoided(current) ? ErpPostedTransactionPolicy.AlreadyClosed : ErpPostedTransactionPolicy.RefuseModify);
        }

        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "UPDATE `" + map.Value.Table + "` SET `status`='reversed' WHERE `id`=?"),
            ct, r.Id).ConfigureAwait(false);
        await UnwindAsync(c, tx, kind, r.Id, r.ActorId, ct).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Posted " + kind + " reversed. The original row stays on file.", r.Id);
    }

    private async Task<ErpSimpleWriteResult> DeleteDraftAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, CancellationToken ct)
    {
        var kind = ErpPostedTransactionPolicy.NormalizeKind(r.Category ?? r.Status);
        if (r.Id <= 0 || kind.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Document kind and id are required to delete.");
        }

        var map = TableFor(kind);
        if (map is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "That document kind cannot be deleted.");
        }

        var current = await ScalarText(c, tx, "SELECT `status` FROM `" + map.Value.Table + "` WHERE `id`=?", ct, r.Id).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(current))
        {
            return ErpSimpleWriteResult.Fail("not_found", "Document was not found.");
        }

        if (!ErpPostedTransactionPolicy.CanDelete(kind, current))
        {
            return ErpSimpleWriteResult.Fail("posted", ErpPostedTransactionPolicy.AlreadyClosed);
        }

        await UnwindAsync(c, tx, kind, r.Id, r.ActorId, ct).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "DELETE FROM `" + map.Value.Table + "` WHERE `id`=?"),
            ct, r.Id).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(kind + " deleted.", r.Id);
    }

    private async Task UnwindAsync(DbConnection c, DbTransaction tx, string kind, long id, int actorId, CancellationToken ct)
    {
        if (kind is "acquisition" or "disposal" or "valuation" or "cheque" or "deposit" or "sale" or "certificate" or "escrow" or "instalment")
        {
            try
            {
                var map = TableFor(kind);
                if (map is not null)
                {
                    var journalId = await ScalarLong(c, tx, "SELECT IFNULL(`gl_journal_id`,0) FROM `" + map.Value.Table + "` WHERE `id`=?", ct, id).ConfigureAwait(false);
                    await ErpRealEstateGl.ReverseAsync(_gl, c, tx, actorId, journalId, ct).ConfigureAwait(false);
                }

                if (kind == "cheque")
                {
                    var clearId = await ScalarLong(c, tx, "SELECT IFNULL(`gl_clear_journal_id`,0) FROM `epc_erp_re_cheques` WHERE `id`=?", ct, id).ConfigureAwait(false);
                    await ErpRealEstateGl.ReverseAsync(_gl, c, tx, actorId, clearId, ct).ConfigureAwait(false);
                }
            }
            catch (DbException)
            {
            }
        }

        switch (kind)
        {
            case "acquisition":
                await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                    "UPDATE `epc_erp_properties` p JOIN `epc_erp_re_acquisitions` a ON a.property_id=p.id SET p.status='under-evaluation' WHERE a.id=? AND p.status='active'"),
                    ct, id).ConfigureAwait(false);
                break;
            case "disposal":
                await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                    "UPDATE `epc_erp_properties` p JOIN `epc_erp_re_disposals` d ON d.property_id=p.id SET p.status='active', p.disposed_date=NULL WHERE d.id=?"),
                    ct, id).ConfigureAwait(false);
                break;
            case "lease":
                await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                    "UPDATE `epc_erp_re_units` u JOIN `epc_erp_re_leases` l ON l.unit_id=u.id SET u.status='available' WHERE l.id=? AND u.status='occupied'"),
                    ct, id).ConfigureAwait(false);
                break;
            case "sale":
                await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                    "UPDATE `epc_erp_re_units` u JOIN `epc_erp_re_sales` s ON s.unit_id=u.id SET u.status='available' WHERE s.id=?"),
                    ct, id).ConfigureAwait(false);
                break;
            case "opportunity":
                await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                    "UPDATE `epc_erp_re_opportunities` SET `stage`='screening' WHERE `id`=?"),
                    ct, id).ConfigureAwait(false);
                break;
        }
    }

    private static (string Table, bool HasCode)? TableFor(string kind)
        => kind switch
        {
            "acquisition" => ("epc_erp_re_acquisitions", true),
            "disposal" => ("epc_erp_re_disposals", true),
            "valuation" => ("epc_erp_re_valuations", true),
            "lease" => ("epc_erp_re_leases", true),
            "cheque" => ("epc_erp_re_cheques", true),
            "deposit" => ("epc_erp_re_deposits", false),
            "sale" => ("epc_erp_re_sales", true),
            "certificate" => ("epc_erp_re_certificates", true),
            "contract" => ("epc_erp_re_contracts", true),
            "opportunity" => ("epc_erp_re_opportunities", true),
            "boq" => ("epc_erp_re_boq_lines", true),
            "escrow" => ("epc_erp_re_escrow", false),
            "milestone" => ("epc_erp_re_milestones", true),
            "instalment" => ("epc_erp_re_instalments", false),
            "allocation" => ("epc_erp_re_cost_allocs", false),
            _ => null,
        };

    private Task<long> PostIfrsAsync(
        DbConnection c,
        DbTransaction tx,
        int actorId,
        long companyId,
        long sourceId,
        string reference,
        string description,
        IReadOnlyList<ErpRealEstateIfrs.JournalLine> recipe,
        CancellationToken ct)
        => ErpRealEstateGl.PostAsync(_gl, c, tx, actorId, companyId, sourceId, reference, description, recipe, ct);

    private static long CompanyOf(ErpRealEstateWriteRequest r, long fallback)
        => r.LegalEntityId > 0 ? r.LegalEntityId : fallback;

    private static async Task<long> CompanyForPropertyAsync(
        DbConnection c, DbTransaction tx, ErpRealEstateWriteRequest r, long propertyId, CancellationToken ct)
    {
        if (r.LegalEntityId > 0)
        {
            return r.LegalEntityId;
        }

        return propertyId > 0
            ? await ScalarLong(c, tx, "SELECT IFNULL(`legal_entity_id`,0) FROM `epc_erp_properties` WHERE `id`=?", ct, propertyId).ConfigureAwait(false)
            : 0;
    }

    private static ErpSimpleWriteResult PostedBlock(string? status, string kind)
        => ErpPostedTransactionPolicy.IsVoided(status)
            ? ErpSimpleWriteResult.Fail("posted", ErpPostedTransactionPolicy.AlreadyClosed)
            : ErpSimpleWriteResult.Fail("posted", ErpPostedTransactionPolicy.RefuseModify);

    private static async Task<string> ScalarText(DbConnection c, DbTransaction tx, string sql, CancellationToken ct, params object[] args)
    {
        await using var command = c.CreateCommand();
        command.Transaction = tx;
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is null or DBNull ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }

    private static async Task<long> LastId(DbConnection c, DbTransaction tx, CancellationToken ct)
        => await ErpDb.LastInsertIdAsync(c, tx, ct).ConfigureAwait(false);

    private static async Task<decimal> ScalarDec(DbConnection c, DbTransaction tx, string sql, CancellationToken ct, params object[] args)
    {
        return await ErpDb.DecimalAsync(c, tx, ErpDb.Positional(sql), ct, args).ConfigureAwait(false);
    }

    private static async Task<long> ScalarLong(DbConnection c, DbTransaction tx, string sql, CancellationToken ct, params object[] args)
        => await ErpDb.LongAsync(c, tx, ErpDb.Positional(sql), ct, args).ConfigureAwait(false);

    private static decimal Money(decimal value) => ErpRealEstateCalc.Money(value);

    private static string Clip(string? value, int max, string fallback = "")
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            text = fallback;
        }

        return text.Length <= max ? text : text[..max];
    }

    private static object DateOrNull(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length == 0 ? DBNull.Value : text;
    }
}
