using System.Security.Cryptography;

namespace EcomAE.Platform.Mail;

internal static class PlatformMailSecrets
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$%*";

    public static string NewMailboxPassword()
    {
        Span<char> chars = stackalloc char[20];
        chars[0] = 'E';
        chars[1] = 'p';
        chars[2] = 'c';
        for (var i = 3; i < chars.Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }
}
