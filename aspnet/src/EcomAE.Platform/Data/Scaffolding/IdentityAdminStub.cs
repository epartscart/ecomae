namespace EcomAE.Platform.Data.Scaffolding;

/// <summary>
/// EF Core stub entity for future Identity/admin bounded context. Not production-mapped.
/// </summary>
public sealed class IdentityAdminStub
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public int IsActive { get; set; }
}
