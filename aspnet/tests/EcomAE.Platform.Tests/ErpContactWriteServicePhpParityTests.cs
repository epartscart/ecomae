using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpContactWriteServicePhpParityTests
{
    [Fact]
    public void PartyTypeDefaultsToCustomer()
    {
        Assert.Equal("customer", ErpContactWriteService.NormalizePartyType(null));
        Assert.Equal("customer", ErpContactWriteService.NormalizePartyType("Supplier"));
        Assert.Equal("supplier", ErpContactWriteService.NormalizePartyType("supplier"));
        Assert.Equal(5, ErpContactWriteService.PartyTypes.Count);
    }

    [Fact]
    public void CodesAreUpperCasedAndTruncatedLikePhp()
    {
        Assert.Equal("AE", ErpContactWriteService.NormalizeCode(null, "AE"));
        Assert.Equal("AED", ErpContactWriteService.NormalizeCode(" aed ", "AED"));
        Assert.Equal("ABCDEFGH", ErpContactWriteService.NormalizeCode("abcdefghij", "AE"));
    }

    [Fact]
    public void SyntheticEmailMatchesPhpShape()
    {
        Assert.Matches("^erp-cust-[0-9a-f]{12}@erp\\.local$", ErpContactWriteService.SyntheticEmail());
    }

    [Fact]
    public void MessagesLimitsAndSqlMirrorPhp()
    {
        Assert.Equal("Contact name is required", ErpContactWriteService.NameRequired);
        Assert.Equal("Customer name or email is required", ErpContactWriteService.CustomerRequired);
        Assert.Equal(500, ErpContactWriteService.CustomerSyncLimit);
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "aspnet/src/EcomAE.Platform/Erp/ErpContactWriteService.cs"));
        Assert.Contains("WHERE `linked_supplier_id` = ? LIMIT 1", src, StringComparison.Ordinal);
        Assert.Contains("WHERE `linked_user_id` = ? LIMIT 1", src, StringComparison.Ordinal);
        Assert.Contains("VALUES (1, ?, 1, ?, 0, ?, 1, ?, 1)", src, StringComparison.Ordinal);
        Assert.Contains("`epc_erp_suppliers` WHERE `active` = 1", src, StringComparison.Ordinal);
        Assert.Contains("INNER JOIN `users` u ON u.`user_id` = o.`user_id` WHERE o.`user_id` > 0 LIMIT ", src, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
