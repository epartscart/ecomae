using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwMasterAuditTests
{
    [Fact]
    public void ActorPrefersEmailAndFallsBackToUserId()
    {
        Assert.Equal("ops@ecomae.com", ErpJwMasterAudit.Actor("ops@ecomae.com", 12));
        Assert.Equal("user #12", ErpJwMasterAudit.Actor("  ", 12));
        Assert.Equal("", ErpJwMasterAudit.Actor(null, 0));
    }

    [Fact]
    public void DisplayDateFormatsDateTimeAndUnix()
    {
        Assert.Equal("", ErpJwMasterAudit.DisplayDate(null));
        Assert.Equal("2026-10-13 15:30", ErpJwMasterAudit.DisplayDate(new DateTime(2026, 10, 13, 15, 30, 0, DateTimeKind.Utc)));
        Assert.Equal("2026-10-13 12:00", ErpJwMasterAudit.DisplayDate(1_791_892_800L));
    }

    [Fact]
    public void DisplayUserShowsDashWhenMissing()
    {
        Assert.Equal("—", ErpJwMasterAudit.DisplayUser(""));
        Assert.Equal("ops@ecomae.com", ErpJwMasterAudit.DisplayUser(" ops@ecomae.com "));
    }
}
