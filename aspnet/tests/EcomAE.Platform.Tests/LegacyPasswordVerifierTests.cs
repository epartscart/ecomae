using EcomAE.Platform.Auth;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyPasswordVerifierTests
{
    [Fact]
    public void VerifiesLegacyMd5WithSecretSuccession()
    {
        const string secret = "unit-test-secret";
        const string plain = "P@ssw0rd!";
        var hash = LegacyPasswordVerifier.Md5Hex(plain + secret);

        Assert.True(LegacyPasswordVerifier.IsLegacyMd5(hash));
        Assert.True(LegacyPasswordVerifier.Verify(plain, hash, secret));
        Assert.False(LegacyPasswordVerifier.Verify("wrong", hash, secret));
    }

    [Fact]
    public void VerifiesBcryptHashes()
    {
        const string plain = "bcrypt-pass";
        var hash = BCrypt.Net.BCrypt.HashPassword(plain, workFactor: 4);

        Assert.False(LegacyPasswordVerifier.IsLegacyMd5(hash));
        Assert.True(LegacyPasswordVerifier.Verify(plain, hash, secretSuccession: "unused"));
        Assert.False(LegacyPasswordVerifier.Verify("nope", hash, "unused"));
    }
}
