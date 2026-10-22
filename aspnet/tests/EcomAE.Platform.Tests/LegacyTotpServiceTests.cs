using EcomAE.Platform.Auth;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// RFC 6238 TOTP verification — the code is the same algorithm every
/// authenticator app implements (HMAC-SHA1, 30 s steps, 6 digits).
/// </summary>
public sealed class LegacyTotpServiceTests
{
    [Fact]
    public void Compute_matches_rfc6238_sha1_test_vector()
    {
        // RFC 6238 Appendix B: secret "12345678901234567890" (ASCII) at T=59s → counter 1 → 94287082 (8 digits) → 287082 (6 digits).
        var secret = "12345678901234567890"u8.ToArray();
        Assert.Equal("287082", LegacyTotpService.Compute(secret, 1));
    }

    [Fact]
    public void Compute_produces_six_digits()
    {
        var secret = new byte[20];
        new Random(42).NextBytes(secret);
        var code = LegacyTotpService.Compute(secret, 1_700_000_000L / 30);
        Assert.Matches("^[0-9]{6}$", code);
    }

    [Fact]
    public void Base32_round_trips()
    {
        var secret = "Hello, EcomAE 123!"u8.ToArray();
        var encoded = LegacyTotpService.Base32Encode(secret);
        Assert.Equal(secret, LegacyTotpService.Base32Decode(encoded));
    }

    [Fact]
    public void Verify_accepts_current_code_and_rejects_garbage()
    {
        var secret = new byte[20];
        new Random(7).NextBytes(secret);
        var base32 = LegacyTotpService.Base32Encode(secret);
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var code = LegacyTotpService.Compute(secret, counter);

        Assert.True(LegacyTotpService.Verify(base32, code));
        Assert.True(LegacyTotpService.Verify(base32, " " + code)); // whitespace tolerant
        Assert.False(LegacyTotpService.Verify(base32, "0000000"));
        Assert.False(LegacyTotpService.Verify(base32, "abcdef"));
        Assert.False(LegacyTotpService.Verify(base32, null));
    }

    [Fact]
    public void Verify_accepts_adjacent_step_but_not_far_drift()
    {
        var secret = new byte[20];
        new Random(11).NextBytes(secret);
        var base32 = LegacyTotpService.Base32Encode(secret);
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        Assert.True(LegacyTotpService.Verify(base32, LegacyTotpService.Compute(secret, counter - 1)));
        Assert.True(LegacyTotpService.Verify(base32, LegacyTotpService.Compute(secret, counter + 1)));
        var far = LegacyTotpService.Compute(secret, counter + 5);
        if (far != LegacyTotpService.Compute(secret, counter)
            && far != LegacyTotpService.Compute(secret, counter - 1)
            && far != LegacyTotpService.Compute(secret, counter + 1))
        {
            Assert.False(LegacyTotpService.Verify(base32, far));
        }
    }
}
