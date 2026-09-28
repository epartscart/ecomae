namespace EcomAE.Platform.Erp;

public interface IErpFitOutQuotationWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutQuotationSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutQuotationSaveRequest(
    long Id = 0,
    long EstimateId = 0,
    string? Code = null,
    string? Title = null,
    int Revision = 1,
    DateOnly? ExpiresOn = null,
    string? Status = null);

public sealed class ErpFitOutQuotationWriteService : IErpFitOutQuotationWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpVoucherNumberService _vouchers;

    public ErpFitOutQuotationWriteService(
        IErpWriteConnectionFactory connections,
        IErpVoucherNumberService vouchers)
    {
        _connections = connections;
        _vouchers = vouchers;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutQuotationSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = Clip(request.Code, 60);
        var title = Clip(request.Title, 200);
        if (request.EstimateId <= 0
            || (request.Id > 0 && code.Length == 0)
            || title.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Estimate, quotation code, and title are required.");
        }
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var status = request.Status?.Trim().ToLowerInvariant() switch
        {
            "issued" => "issued",
            "accepted" => "accepted",
            "rejected" => "rejected",
            "expired" => "expired",
            _ => "draft"
        };
        var revision = Math.Clamp(request.Revision, 1, 10_000);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `ecomae_fitout_quotations` (
                `id` bigint NOT NULL AUTO_INCREMENT,
                `estimate_id` bigint NOT NULL,
                `code` varchar(60) NOT NULL,
                `title` varchar(200) NOT NULL,
                `revision` int NOT NULL DEFAULT 1,
                `expires_on` date NULL,
                `status` varchar(24) NOT NULL DEFAULT 'draft',
                `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (`id`),
                UNIQUE KEY `uq_ecomae_fitout_quotation_code` (`code`),
                KEY `ix_ecomae_fitout_quotation_estimate` (`estimate_id`)
            ) ENGINE=InnoDB
            """, cancellationToken).ConfigureAwait(false);
        if (request.Id == 0 && code.Length == 0)
        {
            code = await _vouchers
                .NextAsync(connection, null, "QUO", cancellationToken)
                .ConfigureAwait(false);
        }
        var expires = request.ExpiresOn?.ToDateTime(TimeOnly.MinValue);
        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(connection, null,
                ErpDb.Positional("UPDATE `ecomae_fitout_quotations` SET `estimate_id`=?,`code`=?,`title`=?,`revision`=?,`expires_on`=?,`status`=?,`updated_at_utc`=CURRENT_TIMESTAMP WHERE `id`=?"),
                cancellationToken, request.EstimateId, code, title, revision, expires, status, request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Fit-out quotation saved", request.Id);
        }
        await ErpDb.ExecuteAsync(connection, null,
            ErpDb.Positional("INSERT INTO `ecomae_fitout_quotations` (`estimate_id`,`code`,`title`,`revision`,`expires_on`,`status`) VALUES (?,?,?,?,?,?)"),
            cancellationToken, request.EstimateId, code, title, revision, expires, status).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Fit-out quotation saved", id);
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
