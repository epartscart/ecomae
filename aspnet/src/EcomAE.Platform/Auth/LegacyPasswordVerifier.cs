using System.Security.Cryptography;
using System.Text;

namespace EcomAE.Platform.Auth;

/// <summary>
/// Mirrors PHP <c>epc_password_verify</c>: modern <c>password_verify</c> (bcrypt)
/// and legacy <c>md5($plain . $secret_succession)</c>.
/// </summary>
public static class LegacyPasswordVerifier
{
    public static bool Verify(string plain, string storedHash, string secretSuccession)
    {
        if (string.IsNullOrEmpty(plain) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        if (IsLegacyMd5(storedHash))
        {
            var legacy = Md5Hex(plain + (secretSuccession ?? string.Empty));
            var a = Encoding.ASCII.GetBytes(legacy);
            var b = Encoding.ASCII.GetBytes(storedHash.ToLowerInvariant());
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(plain, storedHash);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsLegacyMd5(string storedHash)
        => storedHash.Length == 32 && storedHash.All(static c => char.IsAsciiHexDigit(c));

    public static string Md5Hex(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string Sha1Hex(string value)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
