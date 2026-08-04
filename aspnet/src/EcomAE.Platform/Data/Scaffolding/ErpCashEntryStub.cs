namespace EcomAE.Platform.Data.Scaffolding;

/// <summary>
/// EF Core stub entity for future ERP cash-entries ledger bounded context. Not production-mapped.
/// </summary>
public sealed class ErpCashEntryStub
{
    public long Id { get; set; }

    public long AccountId { get; set; }

    public long TimeUnix { get; set; }

    public int Direction { get; set; }

    public decimal Amount { get; set; }

    public string Reference { get; set; } = string.Empty;
}
