using System.Globalization;
using EcomAE.Platform.Storefront;

namespace EcomAE.Platform.Erp;

public sealed record ErpTenancyExpiryDocument(long Id, long TenantId, string Title, string DocType, string FileName);

public sealed record ErpTenancyExpiryFileLink(long NoticeId, long DocumentId);

public sealed record ErpTenancyDocumentOpen(string Path, string Name, string ContentType);

/// <summary>
/// One tenant file at a time. The stored name is generated. The original name is only a label.
/// </summary>
public static class ErpTenancyDocumentFiles
{
    public const long MaxBytes = 8 * 1024 * 1024;

    public static string Root()
        => Path.Combine(EpcRegistrationEnhanced.KycDataRoot(), "re-tenant-docs");

    public static bool AllowedExtension(string? extension)
        => extension is "pdf" or "jpg" or "jpeg" or "png" or "webp";

    public static string ExtensionOf(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? "");
        var dot = name.LastIndexOf('.');
        if (dot < 0 || dot == name.Length - 1)
        {
            return "";
        }

        return name[(dot + 1)..].Trim().ToLowerInvariant();
    }

    public static string DisplayName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? "").Replace('\\', ' ').Replace('/', ' ').Trim();
        var cleaned = new string(name.Where(ch => !char.IsControl(ch)).ToArray());
        if (cleaned.Length == 0)
        {
            cleaned = "document";
        }

        return cleaned.Length <= 120 ? cleaned : cleaned[..120];
    }

    public static string TitleOrName(string? title, string displayName)
    {
        var text = (title ?? "").Trim();
        if (text.Length == 0)
        {
            var dot = displayName.LastIndexOf('.');
            text = dot > 0 ? displayName[..dot] : displayName;
        }

        return text.Length <= 190 ? text : text[..190];
    }

    public static string DocumentType(string? raw)
    {
        var code = (raw ?? "").Trim().ToLowerInvariant();
        return code is "emirates_id" or "passport" or "visa" or "ejari" or "contract" or "other" ? code : "other";
    }

    public static string TypeLabel(string? code)
        => DocumentType(code) switch
        {
            "emirates_id" => "Emirates ID",
            "passport" => "Passport",
            "visa" => "Visa",
            "ejari" => "Ejari",
            "contract" => "Tenancy contract",
            _ => "Other",
        };

    public static int Sensitive(string docType)
        => docType is "emirates_id" or "passport" or "visa" ? 1 : 0;

    public static string StoredName(string extension)
        => Guid.NewGuid().ToString("N") + "." + extension;

    public static string StorageKey(long tenantId, string storedName)
        => tenantId.ToString(CultureInfo.InvariantCulture) + "/" + storedName;

    public static string ContentType(string? fileName)
        => ExtensionOf(fileName) switch
        {
            "pdf" => "application/pdf",
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "webp" => "image/webp",
            _ => "application/octet-stream",
        };

    /// <summary>Absolute path under the document root, or null when the key leaves that root.</summary>
    public static string? Resolve(string root, string? storageKey)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(storageKey))
        {
            return null;
        }

        if (storageKey.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(storageKey))
        {
            return null;
        }

        var fullRoot = Path.GetFullPath(root);
        var full = Path.GetFullPath(Path.Combine(fullRoot, storageKey.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = fullRoot.EndsWith(Path.DirectorySeparatorChar) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.Ordinal) ? full : null;
    }
}
