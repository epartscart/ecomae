namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_petty_cash_save</c> / <c>epc_jewel_petty_cash_save</c> twin.
/// PHP inserts <c>PCV</c> into <c>epc_jewel_voucher</c>. Schema-ensure stays PHP.
/// </summary>
public interface IErpJwPettyCashWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwPettyCashSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwPettyCashSaveRequest(
    int CompanyId = 0,
    string? Branch = null,
    string? VocDate = null,
    int VocNo = 0,
    string? PayTo = null,
    string? PaidTo = null,
    string? CashAccount = null,
    string? AccountCode = null,
    string? Narration = null,
    decimal Total = 0,
    decimal GrandTotal = 0,
    decimal TotalAmount = 0);

public sealed class ErpJwPettyCashWriteService : IErpJwPettyCashWriteService
{
    private readonly IErpJwVoucherWriteService _vouchers;

    public ErpJwPettyCashWriteService(IErpJwVoucherWriteService vouchers)
    {
        _vouchers = vouchers;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwPettyCashSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payTo = FirstNonEmpty(request.PayTo, request.PaidTo);
        if (payTo.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Payee is required.");
        }

        var account = FirstNonEmpty(request.CashAccount, request.AccountCode);
        if (account.Length == 0)
        {
            account = "PETTY-CASH";
        }

        var total = request.Total != 0 ? request.Total : (request.GrandTotal != 0 ? request.GrandTotal : request.TotalAmount);
        var written = await _vouchers.SaveAsync(
            new ErpJwVoucherSaveRequest(
                CompanyId: request.CompanyId,
                VocType: "PCV",
                Branch: request.Branch,
                VocDate: request.VocDate,
                VocNo: request.VocNo,
                PartyCode: account,
                PartyName: payTo,
                Narration: request.Narration,
                NetAmount: total,
                GrossTotal: total),
            cancellationToken).ConfigureAwait(false);
        if (!written.Succeeded)
        {
            return written;
        }

        return ErpSimpleWriteResult.Ok("Petty cash voucher saved", written.Id);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            var raw = (value ?? string.Empty).Trim();
            if (raw.Length > 0)
            {
                return raw;
            }
        }

        return string.Empty;
    }
}
