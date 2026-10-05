using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_ctr_ensure_schema</c> twin — verbatim DDL from
/// content/shop/finance/epc_erp_contracts.php. PHP runs this at the top of
/// every contracts entry point, so tenants provision the tables lazily.
/// </summary>
internal static class ErpContractSchema
{
    public static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_contracts` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`code` varchar(40) NOT NULL DEFAULT ''," +
            "`title` varchar(200) NOT NULL DEFAULT ''," +
            "`counterparty` varchar(200) NOT NULL DEFAULT ''," +
            "`contract_value` decimal(16,2) NOT NULL DEFAULT 0.00," +
            "`currency` varchar(3) NOT NULL DEFAULT 'AED'," +
            "`start_date` int(11) NOT NULL DEFAULT 0," +
            "`end_date` int(11) NOT NULL DEFAULT 0," +
            "`status` varchar(16) NOT NULL DEFAULT 'draft'," +
            "`version` int(11) NOT NULL DEFAULT 1," +
            "`body_text` mediumtext," +
            "`ocr_text` mediumtext," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "`time_updated` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "UNIQUE KEY `x_code` (`code`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Contracts register'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_contract_signatures` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`contract_id` int(11) NOT NULL," +
            "`signer_name` varchar(160) NOT NULL DEFAULT ''," +
            "`signer_email` varchar(160) NOT NULL DEFAULT ''," +
            "`signed_at` int(11) NOT NULL DEFAULT 0," +
            "`signature_hash` varchar(64) NOT NULL DEFAULT ''," +
            "`ip` varchar(64) NOT NULL DEFAULT ''," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_ctr` (`contract_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='E-signature ledger'",
            cancellationToken).ConfigureAwait(false);
    }
}
