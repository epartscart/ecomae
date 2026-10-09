using System.Globalization;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/shop/docpart/DocpartManufacturer.php</c> class <c>DocpartManufacturer</c>:
/// trim the supplier brand, uppercase it for display, strip newlines/tabs/backslashes from the name,
/// and mark the row valid when the brand is longer than one UTF-8 character.
/// </summary>
public sealed class DocpartManufacturer
{
    public const string PhpPath = "content/shop/docpart/DocpartManufacturer.php";

    public string Manufacturer { get; }
    public object? ManufacturerId { get; }
    public string ManufacturerShow { get; }
    public string Name { get; }
    public object? StorageId { get; }
    public object? OfficeId { get; }
    public object? SynonymsSingleQuery { get; }
    public object? Params { get; }
    public bool Valid { get; }

    public DocpartManufacturer(
        string? manufacturer,
        object? manufacturerId,
        object? name,
        object? officeId,
        object? storageId,
        object? synonymsSingleQuery,
        object? parameters = null)
    {
        Manufacturer = (manufacturer ?? string.Empty).Trim();
        ManufacturerId = manufacturerId;
        ManufacturerShow = Manufacturer.ToUpper(CultureInfo.InvariantCulture);
        Name = SanitizeName(name);
        StorageId = storageId;
        OfficeId = officeId;
        SynonymsSingleQuery = synonymsSingleQuery;
        Params = parameters;
        Valid = new StringInfo(Manufacturer).LengthInTextElements > 1;
    }

    private static string SanitizeName(object? name)
    {
        var text = Convert.ToString(name, CultureInfo.InvariantCulture) ?? string.Empty;
        return text.Trim().Replace("\n", "", StringComparison.Ordinal)
            .Replace("\t", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Replace("\\", "", StringComparison.Ordinal);
    }
}
