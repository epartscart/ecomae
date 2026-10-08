using System.Globalization;
using System.Reflection;
using EcomAE.Platform.Auth;
using SkiaSharp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>lib/captcha/captcha.php</c> and <c>lib/captcha/check_captcha.php</c>. The image is a random 150×70 background
/// with crossing lines under and over the code, drawn in the Agency font; the browser keeps <c>md5(code)</c> in the
/// two-minute <c>captcha</c> cookie, which the check and the registration post compare against.
/// </summary>
public static class StorefrontCaptcha
{
    public const string ImagePath = "/lib/captcha/captcha.php";

    public const string CheckPath = "/lib/captcha/check_captcha.php";

    public const string RefreshPath = "/lib/captcha/refresh.png";

    public const string CookieName = "captcha";

    public const int CookieSeconds = 120;

    public const string Chars = "abdefhknrstyz23456789";

    private const string ResourcePrefix = "EcomAE.Platform.Captcha.";

    private static readonly Lazy<byte[][]> Backgrounds = new(() => Assembly.GetExecutingAssembly().GetManifestResourceNames()
        .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".png", StringComparison.Ordinal) && n != ResourcePrefix + "refresh.png")
        .OrderBy(n => n, StringComparer.Ordinal)
        .Select(Resource)
        .ToArray());

    private static readonly Lazy<SKTypeface> Font = new(() =>
    {
        using var data = SKData.CreateCopy(Resource(ResourcePrefix + "AGENCYR.TTF"));
        return SKTypeface.FromData(data);
    });

    /// <summary>The refresh button image the form links next to the captcha.</summary>
    public static byte[] RefreshImage() => Resource(ResourcePrefix + "refresh.png");

    /// <summary>PHP <c>generate_code()</c>: 4 to 7 of <see cref="Chars"/>, shuffled.</summary>
    public static string GenerateCode(Random random)
    {
        var length = Rand(random, 4, 7);
        var code = new char[length];
        for (var i = 0; i < length; i++)
        {
            code[i] = Chars[Rand(random, 1, Chars.Length) - 1];
        }

        random.Shuffle(code);
        return new string(code);
    }

    /// <summary>The <c>captcha</c> cookie value for <paramref name="code"/>.</summary>
    public static string CookieValue(string code) => LegacyPasswordVerifier.Md5Hex(code);

    /// <summary>
    /// PHP <c>check_captcha.php</c>: <c>null</c> when no <c>captcha_check</c> was sent (PHP exits with an empty body),
    /// otherwise <c>md5(value) == $_COOKIE["captcha"]</c> with PHP 8 loose comparison.
    /// </summary>
    public static bool? Check(string? posted, string? cookie)
        => posted is null ? null : cookie is not null && StorefrontSmsHandlers.LooseEquals(LegacyPasswordVerifier.Md5Hex(posted), cookie);

    /// <summary>PHP <c>img_code()</c>: the PNG for <paramref name="code"/>.</summary>
    public static byte[] RenderPng(string code, Random random)
    {
        var lines = Rand(random, 3, 7);
        var pointSize = Rand(random, 20, 30);
        var backgrounds = Backgrounds.Value;
        using var bitmap = SKBitmap.Decode(backgrounds[Rand(random, 0, backgrounds.Length - 1)]);
        using (var canvas = new SKCanvas(bitmap))
        {
            DrawLines(canvas, random, lines, () => new SKColor((byte)Rand(random, 0, 150), (byte)Rand(random, 0, 100), (byte)Rand(random, 0, 150)));

            using var textPaint = new SKPaint { Color = new SKColor((byte)Rand(random, 0, 200), 0, (byte)Rand(random, 0, 200)), IsAntialias = true };
            using var font = new SKFont(Font.Value, pointSize * 96f / 72f) { Edging = SKFontEdging.Antialias };
            var x = Rand(random, 0, 35);
            foreach (var letter in code)
            {
                x += 15;
                var angle = Rand(random, 2, 4);
                var y = Rand(random, 50, 55);
                canvas.Save();
                canvas.Translate(x, y);
                canvas.RotateDegrees(-angle);
                canvas.DrawText(letter.ToString(), 0, 0, SKTextAlign.Left, font, textPaint);
                canvas.Restore();
            }

            DrawLines(canvas, random, lines, () => new SKColor((byte)Rand(random, 0, 255), (byte)Rand(random, 0, 200), (byte)Rand(random, 0, 255)));
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    /// <summary>The PHP cache headers of the image, in order.</summary>
    public static IReadOnlyList<(string Name, string Value)> ImageHeaders { get; } =
    [
        ("Expires", "Mon, 26 Jul 1997 05:00:00 GMT"),
        ("Last-Modified", DateTimeOffset.FromUnixTimeSeconds(10000).ToString("ddd, dd MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture) + " GMT"),
        ("Cache-Control", "no-store, no-cache, must-revalidate"),
        ("Cache-Control", "post-check=0, pre-check=0"),
        ("Pragma", "no-cache"),
    ];

    private static void DrawLines(SKCanvas canvas, Random random, int count, Func<SKColor> color)
    {
        for (var i = 0; i < count; i++)
        {
            using var paint = new SKPaint { Color = color(), IsAntialias = false, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
            canvas.DrawLine(Rand(random, 0, 20), Rand(random, 1, 50), Rand(random, 150, 180), Rand(random, 1, 50), paint);
        }
    }

    private static int Rand(Random random, int min, int max) => random.Next(min, max + 1);

    private static byte[] Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name) ?? throw new InvalidOperationException(name);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
