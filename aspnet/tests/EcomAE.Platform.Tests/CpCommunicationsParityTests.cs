using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpCommunicationsParityTests
{
    private static Dictionary<string, string> FullSmtp() => new(StringComparer.Ordinal)
    {
        ["from_name"] = "Shop",
        ["from_email"] = "shop@example.com",
        ["smtp_mode"] = "smtp",
        ["smtp_encryption"] = "tls",
        ["smtp_host"] = "smtp.example.com",
        ["smtp_port"] = "587",
        ["smtp_username"] = "shop",
        ["smtp_password"] = "secret",
    };

    [Fact]
    public void SmtpComplete_RequiresEveryPhpField()
    {
        Assert.True(CpCommunicationsDeskService.SmtpComplete(FullSmtp()));
        foreach (var key in CpCommunicationsDeskService.SmtpFields)
        {
            var config = FullSmtp();
            config[key] = " ";
            Assert.False(CpCommunicationsDeskService.SmtpComplete(config));
        }
    }

    [Fact]
    public void SenderNumber_FollowsPhpPrecedence()
    {
        Assert.Equal("+1", CpCommunicationsDeskService.SenderNumber("{\"sender_id\":\"+3\",\"from\":\"+2\",\"sender_number\":\"+1\"}"));
        Assert.Equal("+2", CpCommunicationsDeskService.SenderNumber("{\"sender_id\":\"+3\",\"from\":\"+2\",\"sender_number\":\"\"}"));
        Assert.Equal("+3", CpCommunicationsDeskService.SenderNumber("{\"sender_id\":\"+3\"}"));
        Assert.Equal(string.Empty, CpCommunicationsDeskService.SenderNumber("not json"));
    }

    [Fact]
    public void GatewaySender_DefaultsToPhpConstant()
    {
        Assert.Equal(CpSmsMsisdn.DefaultSender, CpSmsMsisdn.Sender(new Dictionary<string, string>()));
        Assert.Equal("+97150", CpSmsMsisdn.Sender(new Dictionary<string, string> { ["sender"] = "+97150" }));
    }

    [Fact]
    public void FormatDebug_MarksStaleAndOldSuccesses()
    {
        const long now = 2_000_000_000;
        Assert.Equal("idle", CpCommunicationsDeskService.FormatDebug(false, 0, 0, "", now).Pill);
        Assert.Equal(string.Empty, CpCommunicationsDeskService.FormatDebug(true, 1, now - 60, "", now).AgeClass);
        Assert.Equal("is-stale", CpCommunicationsDeskService.FormatDebug(true, 1, now - 86_400, "", now).AgeClass);
        Assert.Equal("is-old", CpCommunicationsDeskService.FormatDebug(true, 1, now - 604_800, "", now).AgeClass);
        var failed = CpCommunicationsDeskService.FormatDebug(true, 0, now, "SMTP refused", now);
        Assert.Equal("bad", failed.Pill);
        Assert.Contains("SMTP refused", failed.Meta, StringComparison.Ordinal);
    }

    [Fact]
    public void TestTypes_AreEmailAndPhoneOnly()
    {
        Assert.True(CpCommunicationsTestService.IsKnownType("email"));
        Assert.True(CpCommunicationsTestService.IsKnownType("phone"));
        Assert.False(CpCommunicationsTestService.IsKnownType("sms"));
        Assert.False(CpCommunicationsTestService.IsKnownType(""));
    }

    [Fact]
    public void ContactMatches_RequiresFullMatchLikePhp()
    {
        Assert.True(CpCommunicationsTestService.ContactMatches("a@b.co", ""));
        Assert.False(CpCommunicationsTestService.ContactMatches("", ""));
        Assert.True(CpCommunicationsTestService.ContactMatches("a@b.co", @"[^@]+@[^@]+\.\w+"));
        Assert.False(CpCommunicationsTestService.ContactMatches("x a@b.co", @"[^@\s]+@[^@]+\.\w+"));
        Assert.False(CpCommunicationsTestService.ContactMatches("a@b.co", "(unclosed"));
    }

    [Theory]
    [InlineData("0501234567", "971501234567")]
    [InlineData("+971 50 123 4567", "971501234567")]
    [InlineData("00971501234567", "971501234567")]
    [InlineData("501234567", "971501234567")]
    [InlineData("03001234567", "923001234567")]
    public void Msisdn_NormalizesUaeAndPakistan(string input, string expected)
        => Assert.Equal(expected, CpSmsMsisdn.Normalize(input));

    [Fact]
    public void ParseParameters_FlattensScalars()
    {
        var map = CpCommunicationsTestService.ParseParameters("{\"key\":\"k\",\"port\":443,\"on\":true,\"off\":false}");
        Assert.Equal("k", map["key"]);
        Assert.Equal("443", map["port"]);
        Assert.Equal("1", map["on"]);
        Assert.Equal(string.Empty, map["off"]);
        Assert.Empty(CpCommunicationsTestService.ParseParameters("{bad"));
    }

    [Fact]
    public void Gateway_ListsPhpNativeHandlers()
        => Assert.Equal(new[] { "epc_unifonic", "epc_etisalat", "epc_du", "epc_pakistan" }, CpSmsGateway.NativeHandlers);
}
