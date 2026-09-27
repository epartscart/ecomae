using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Cp;

/// <summary>
/// PHP <c>epc_social_crypto_key()</c> / <c>epc_social_encrypt()</c> / <c>epc_social_decrypt()</c> twin
/// (content/social_media/epc_social_media_helpers.php): AES-256-CBC with a per-tenant key derived from
/// the deploy token, random IV prefixed to the ciphertext, base64 transport.
/// Values written here stay readable by the PHP reference and vice versa.
/// </summary>
public static class CpSocialCrypto
{
    private const string DefaultDeployToken = "epartscart-deploy-2026";

    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>PHP <c>epc_deploy_token()</c>: <c>EPC_DEPLOY_TOKEN</c> or the shipped fallback.</summary>
    public static string DeployToken()
    {
        var env = Environment.GetEnvironmentVariable("EPC_DEPLOY_TOKEN");
        return string.IsNullOrEmpty(env) ? DefaultDeployToken : env;
    }

    public static byte[] Key(string siteKey)
    {
        var normalized = SiteKeySafe.Replace((siteKey ?? string.Empty).ToLowerInvariant(), string.Empty);
        return SHA256.HashData(Encoding.UTF8.GetBytes("epc_social_v1|" + DeployToken() + "|" + normalized));
    }

    public static string Encrypt(string plain, string siteKey)
    {
        if (string.IsNullOrEmpty(plain))
        {
            return string.Empty;
        }

        using var aes = Aes.Create();
        aes.Key = Key(siteKey);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();
        var cipher = aes.EncryptCbc(Encoding.UTF8.GetBytes(plain), aes.IV, PaddingMode.PKCS7);
        var payload = new byte[aes.IV.Length + cipher.Length];
        aes.IV.CopyTo(payload, 0);
        cipher.CopyTo(payload, aes.IV.Length);
        return Convert.ToBase64String(payload);
    }

    public static string Decrypt(string? encoded, string siteKey)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return string.Empty;
        }

        byte[] raw;
        try
        {
            raw = Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            return string.Empty;
        }

        if (raw.Length < 17)
        {
            return string.Empty;
        }

        try
        {
            using var aes = Aes.Create();
            aes.Key = Key(siteKey);
            aes.Mode = CipherMode.CBC;
            var iv = raw.AsSpan(0, 16).ToArray();
            var cipher = raw.AsSpan(16).ToArray();
            return Encoding.UTF8.GetString(aes.DecryptCbc(cipher, iv, PaddingMode.PKCS7));
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
    }
}
