namespace EcomAE.Platform.Data.Scaffolding;

/// <summary>
/// EF Core stub entity for future ERP cash-accounts bounded context. Not production-mapped.
/// </summary>
public sealed class ErpCashAccountStub
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AccountType { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Balance { get; set; }
}
