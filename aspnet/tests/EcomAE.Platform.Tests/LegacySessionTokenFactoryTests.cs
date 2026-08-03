using EcomAE.Platform.Auth;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacySessionTokenFactoryTests
{
    [Fact]
    public void AdminAndCustomerFormulasDifferAndMatchPhpShapes()
    {
        const string secret = "unit-secret";
        const string contact = "ops@example.com";
        const long time = 1_700_000_000L;
        const int userId = 42;

        var admin = LegacySessionTokenFactory.AdminSessionToken(contact, time, secret);
        var customer = LegacySessionTokenFactory.CustomerSessionToken(contact, userId, time, secret);

        Assert.Equal(32, admin.Length);
        Assert.Equal(32, customer.Length);
        Assert.NotEqual(admin, customer);

        // Sanity: regenerating with same inputs is stable (deterministic md5).
        Assert.Equal(admin, LegacySessionTokenFactory.AdminSessionToken(contact, time, secret));
        Assert.Equal(customer, LegacySessionTokenFactory.CustomerSessionToken(contact, userId, time, secret));
    }

    [Fact]
    public void CsrfGuardKeyUsesSecretSessionIpAndUa()
    {
        var a = LegacySessionTokenFactory.CsrfGuardKey("s", "tok", "1.2.3.4", "UA");
        var b = LegacySessionTokenFactory.CsrfGuardKey("s", "tok", "9.9.9.9", "UA");
        Assert.Equal(40, a.Length);
        Assert.NotEqual(a, b);
    }
}
