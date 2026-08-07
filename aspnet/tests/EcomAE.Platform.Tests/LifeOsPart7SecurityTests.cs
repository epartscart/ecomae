using EcomAE.Platform.LifeOs;
using EcomAE.Platform.LifeOs.Part7;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsPart7SecurityTests
{
    private static ServiceProvider Build()
    {
        var sc = new ServiceCollection();
        sc.AddLifeOsPart2Scaffold();
        return sc.BuildServiceProvider();
    }

    [Fact]
    public void SecurityPrinciplesIncludeZeroTrustAndPrivacyByDesign()
    {
        using var sp = Build();
        var gov = sp.GetRequiredService<ILifeOsSecurityGovernance>();
        Assert.Equal(5, gov.SecurityPrinciples.Count);
        Assert.Contains(gov.SecurityPrinciples, p => p.Key == "zero-trust");
        Assert.Contains(gov.SecurityPrinciples, p => p.Key == "privacy-by-design");
        Assert.Contains(gov.ZeroTrustNeverTrust, n => n == "AI Agent");
    }

    [Fact]
    public void IamCoversIdentityTypesAndAdaptiveMfa()
    {
        using var sp = Build();
        var gov = sp.GetRequiredService<ILifeOsSecurityGovernance>();
        Assert.True(gov.IdentityTypes.Count >= 10);
        Assert.Contains(gov.AuthenticationMethods, m => m.Key == "passkey");
        Assert.Contains(gov.AuthenticationMethods, m => m.Key == "saml");
        Assert.Contains(gov.AdaptiveMfaTriggers, t => t.Contains("High-risk", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AuthorizationIncludesRbacAbacAndAgentSandbox()
    {
        using var sp = Build();
        var gov = sp.GetRequiredService<ILifeOsSecurityGovernance>();
        Assert.Contains(gov.RbacRoles, r => r.Key == "auditor");
        Assert.Contains(gov.AbacAttributes, a => a.Key == "device-trust");
        Assert.Equal("FinanceAgent", gov.SampleAgentPermissions.Agent);
        Assert.Contains(gov.SampleAgentPermissions.Permissions, p => p == "finance.read");
    }

    [Fact]
    public void ClassificationEncryptionAndComplianceArePopulated()
    {
        using var sp = Build();
        var gov = sp.GetRequiredService<ILifeOsSecurityGovernance>();
        Assert.Contains(gov.DataClassificationLevels, l => l.Key == "restricted");
        Assert.Contains(gov.ComplianceFrameworks, f => f.Key == "gdpr");
        Assert.Contains(gov.ComplianceFrameworks, f => f.Key == "hipaa");
        var enc = System.Text.Json.JsonSerializer.Serialize(gov.EncryptionDigest());
        Assert.Contains("AES-256", enc);
        Assert.Contains("TLS 1.3", enc);
    }

    [Fact]
    public void AiGovernanceAndSafetyDecisionsCoverConfirmAndBlock()
    {
        using var sp = Build();
        var gov = sp.GetRequiredService<ILifeOsSecurityGovernance>();
        Assert.Contains(gov.SafetyDecisions, d => d.Key == "confirm");
        Assert.Contains(gov.SafetyDecisions, d => d.Key == "blocked");
        Assert.Contains(gov.AiGovernanceFlow, s => s == "Safety Engine");
        Assert.Contains(gov.ThreatSignals, t => t.Key == "agent");
    }

    [Fact]
    public void Part7DigestCoversChapters82To101()
    {
        using var sp = Build();
        var json = System.Text.Json.JsonSerializer.Serialize(
            sp.GetRequiredService<ILifeOsSecurityGovernance>().FullPart7Digest());
        Assert.Contains("Zero Trust Security Architecture", json);
        Assert.Contains("AI Safety Engine", json);
        Assert.Contains("Enterprise Deployment Models", json);
        Assert.Contains("Air-Gapped", json);
        Assert.Contains("IpHostGateMiddleware", json);
    }
}
