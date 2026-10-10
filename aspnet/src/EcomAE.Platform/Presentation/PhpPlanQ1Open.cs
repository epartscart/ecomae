using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-open helpers. PHP identifiers kept for the inventory:
/// <c>epc_ssf_ensure_schema</c>, <c>epc_ssf_storage_active_sql</c>,
/// <c>epc_ssf_price_data_active_sql</c>, <c>epc_ssf_disabled_warehouse_labels</c>,
/// <c>epc_ssf_filter_enabled_price_ids</c>, <c>epc_ssf_filter_agent_stock_lines</c>,
/// <c>epc_ssf_is_storage_disabled</c>, <c>epc_ssf_is_price_disabled</c>,
/// <c>epc_ssf_storage_disabled_by_price</c>, <c>epc_ssf_disabled_storage_ids</c>,
/// <c>epc_ssf_disabled_price_ids</c>, <c>epc_ssf_cp_list_rows</c>,
/// <c>epc_ssf_sync_price_from_storage</c>, <c>epc_ssf_sync_storages_from_price</c>,
/// <c>epc_ssf_write_audit</c>, <c>epc_ssf_set_toggle</c>,
/// <c>epc_ssf_filter_office_storage_bunches</c>,
/// <c>epc_sku_media_ensure_schema</c>, <c>epc_sku_media_normalize_article</c>,
/// <c>epc_sku_media_normalize_brand</c>, <c>epc_sku_media_photo_types</c>,
/// <c>epc_sku_media_value_types</c>, <c>epc_sku_media_default_spec_types</c>,
/// <c>epc_sku_media_images_dir</c>, <c>epc_sku_media_images_fs</c>,
/// <c>epc_sku_media_photo_url</c>, <c>epc_sku_media_storefront_part_url</c>,
/// <c>epc_sku_media_attach_local_photo</c>, <c>epc_sku_media_find_profile</c>,
/// <c>epc_sku_media_upsert_profile</c>, <c>epc_sku_media_list_profiles</c>,
/// <c>epc_sku_media_price_storage_map</c>, <c>epc_sku_media_search_library</c>,
/// <c>epc_sku_media_ensure_from_identity</c>, <c>epc_sku_media_public_lookup</c>,
/// <c>epc_sku_media_photos</c>, <c>epc_sku_media_add_photo</c>,
/// <c>epc_sku_media_mirror_to_catalogue</c>, <c>epc_sku_media_delete_photo</c>,
/// <c>epc_sku_media_update_photo</c>, <c>epc_sku_media_spec_bundle</c>,
/// <c>epc_sku_media_add_spec_group</c>, <c>epc_sku_media_delete_spec_group</c>,
/// <c>epc_sku_media_add_spec_row</c>, <c>epc_sku_media_update_spec_row</c>,
/// <c>epc_sku_media_delete_spec_row</c>, <c>epc_sku_media_delete_profile</c>,
/// <c>epc_sku_media_format_value</c>, <c>epc_sku_media_resolve_for_product</c>,
/// <c>epc_sku_media_full_payload</c>.
/// </summary>
public static class PhpPlanQ1Open
{
    public const string StorageFlagsPath = "content/shop/docpart/epc_storefront_storage_flags.php";
    public const string SkuMediaPath = "content/shop/catalogue/epc_sku_media.php";

    private static readonly Regex ArticleKeep = new("[^A-Z0-9]+", RegexOptions.CultureInvariant);
    private static readonly Regex BrandSpaces = new(@"\s+", RegexOptions.CultureInvariant);
    private static readonly Regex GroupCodeKeep = new("[^a-zA-Z0-9]+", RegexOptions.CultureInvariant);
    private static readonly Regex ListSplit = new(@"\s*,\s*", RegexOptions.CultureInvariant);

    public static string DocumentRoot { get; set; } = "";
    public static Func<long> Clock { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static Func<byte[]> RandomBytes4 { get; set; } = () => Guid.NewGuid().ToByteArray()[..4];
    public static Func<string, string, string, string>? PartUrlBuilder { get; set; }
    private static bool _ssfDone;
    private static bool _skuDone;
    private static Dictionary<int, Dictionary<string, object?>>? _priceMap;

    public static void Reset()
    {
        DocumentRoot = "";
        Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        RandomBytes4 = () => Guid.NewGuid().ToByteArray()[..4];
        PartUrlBuilder = null;
        _ssfDone = false;
        _skuDone = false;
        _priceMap = null;
    }

    public sealed class OpenStore
    {
        public int NextStorageId { get; set; } = 1;
        public int NextPriceId { get; set; } = 1;
        public int NextPriceDataId { get; set; } = 1;
        public int NextAuditId { get; set; } = 1;
        public int NextProfileId { get; set; } = 1;
        public int NextPhotoId { get; set; } = 1;
        public int NextGroupId { get; set; } = 1;
        public int NextSpecId { get; set; } = 1;
        public int NextCatalogueId { get; set; } = 1;
        public bool StoragesMissing { get; set; }
        public bool PriceDataMissing { get; set; }
        public bool CatalogueMissing { get; set; }
        public List<StorageRow> Storages { get; } = new();
        public List<InterfaceRow> Interfaces { get; } = new();
        public List<PriceRow> Prices { get; } = new();
        public List<PriceDataRow> PriceData { get; } = new();
        public List<AuditRow> Audits { get; } = new();
        public List<ProfileRow> Profiles { get; } = new();
        public List<PhotoRow> Photos { get; } = new();
        public List<SpecGroupRow> SpecGroups { get; } = new();
        public List<SpecRow> SpecRows { get; } = new();
        public List<CatalogueRow> Catalogue { get; } = new();
        public List<CatalogueImageRow> CatalogueImages { get; } = new();
    }

    public sealed class StorageRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string ShortName { get; set; } = "";
        public int InterfaceType { get; set; }
        public string ConnectionOptions { get; set; } = "";
        public int StorefrontTempDisabled { get; set; }
        public int Hidden { get; set; }
    }

    public sealed class InterfaceRow
    {
        public int Id { get; set; }
        public string HandlerFolder { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public sealed class PriceRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int StorefrontTempDisabled { get; set; }
    }

    public sealed class PriceDataRow
    {
        public int Id { get; set; }
        public int PriceId { get; set; }
        public string Manufacturer { get; set; } = "";
        public string Article { get; set; } = "";
        public string ArticleShow { get; set; } = "";
        public string ArticleSearch { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public int Exist { get; set; }
        public string TimeToExe { get; set; } = "";
        public string Storage { get; set; } = "";
        public string MinOrder { get; set; } = "";
    }

    public sealed class AuditRow
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = "";
        public int EntityId { get; set; }
        public string EntityName { get; set; } = "";
        public int StorefrontDisabled { get; set; }
        public int UserId { get; set; }
        public string UserLabel { get; set; } = "";
        public string CreatedAt { get; set; } = "";
    }

    public sealed class ProfileRow
    {
        public int Id { get; set; }
        public int? ProductId { get; set; }
        public string Brand { get; set; } = "";
        public string Article { get; set; } = "";
        public string ArticleKey { get; set; } = "";
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string Status { get; set; } = "active";
        public long CreatedAt { get; set; }
        public long UpdatedAt { get; set; }
    }

    public sealed class PhotoRow
    {
        public int Id { get; set; }
        public int ProfileId { get; set; }
        public string FileName { get; set; } = "";
        public string Alt { get; set; } = "";
        public string Caption { get; set; } = "";
        public string PhotoType { get; set; } = "product";
        public int SortOrder { get; set; }
        public int IsPrimary { get; set; }
        public long CreatedAt { get; set; }
    }

    public sealed class SpecGroupRow
    {
        public int Id { get; set; }
        public int ProfileId { get; set; }
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public string Icon { get; set; } = "fa-list";
        public int SortOrder { get; set; }
        public long CreatedAt { get; set; }
    }

    public sealed class SpecRow
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public int ProfileId { get; set; }
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
        public string ValueType { get; set; } = "text";
        public string Unit { get; set; } = "";
        public int SortOrder { get; set; }
        public long CreatedAt { get; set; }
    }

    public sealed class CatalogueRow
    {
        public int Id { get; set; }
        public string Caption { get; set; } = "";
        public string Alias { get; set; } = "";
    }

    public sealed class CatalogueImageRow
    {
        public int ProductId { get; set; }
        public string FileName { get; set; } = "";
    }

    public static void EpcSsfEnsureSchema(OpenStore db, bool allowAlter = false)
    {
        _ = allowAlter;
        if (_ssfDone)
        {
            return;
        }

        _ssfDone = true;
    }

    public static string EpcSsfStorageActiveSql(string alias = "")
    {
        var col = alias != "" ? alias + ".`storefront_temp_disabled`" : "`storefront_temp_disabled`";
        return " IFNULL(" + col + ", 0) = 0 ";
    }

    public static string EpcSsfPriceDataActiveSql(string alias = "")
    {
        var priceCol = alias != "" ? alias + ".`price_id`" : "`price_id`";
        return priceCol + " IN (SELECT `id` FROM `shop_docpart_prices` WHERE " + EpcSsfStorageActiveSql() + ")";
    }

    public static Dictionary<string, bool> EpcSsfDisabledWarehouseLabels(OpenStore db)
    {
        EpcSsfEnsureSchema(db);
        var output = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var row in db.Storages)
        {
            if (row.StorefrontTempDisabled != 1)
            {
                continue;
            }

            foreach (var label in new[] { row.ShortName, row.Name })
            {
                var trimmed = label.Trim();
                if (trimmed != "")
                {
                    output[MbUpper(trimmed)] = true;
                }
            }
        }

        return output;
    }

    public static List<int> EpcSsfFilterEnabledPriceIds(OpenStore db, IEnumerable<object?> priceIds)
    {
        var disabled = EpcSsfDisabledPriceIds(db);
        var raw = priceIds.Select(IntVal).ToList();
        if (disabled.Count == 0)
        {
            return raw.Where(id => id != 0).Distinct().ToList();
        }

        var output = new List<int>();
        var seen = new HashSet<int>();
        foreach (var priceId in raw)
        {
            if (priceId > 0 && !disabled.ContainsKey(priceId) && seen.Add(priceId))
            {
                output.Add(priceId);
            }
        }

        return output;
    }

    public static List<Dictionary<string, object?>> EpcSsfFilterAgentStockLines(OpenStore db, IEnumerable<object?> lines)
    {
        var list = lines.ToList();
        if (list.Count == 0)
        {
            return [];
        }

        var disabledPrices = EpcSsfDisabledPriceIds(db);
        var disabledLabels = EpcSsfDisabledWarehouseLabels(db);
        if (disabledPrices.Count == 0 && disabledLabels.Count == 0)
        {
            return list.OfType<Dictionary<string, object?>>().Select(Clone).ToList();
        }

        var output = new List<Dictionary<string, object?>>();
        foreach (var item in list)
        {
            if (item is not Dictionary<string, object?> line)
            {
                continue;
            }

            if (!Empty(Field(line, "price_id")) && disabledPrices.ContainsKey(IntVal(Field(line, "price_id"))))
            {
                continue;
            }

            var warehouse = Str(Field(line, "warehouse") ?? Field(line, "storage")).Trim();
            if (warehouse != "" && disabledLabels.ContainsKey(MbUpper(warehouse)))
            {
                continue;
            }

            output.Add(Clone(line));
        }

        return output;
    }

    public static bool EpcSsfIsStorageDisabled(OpenStore db, int storageId)
    {
        if (storageId <= 0)
        {
            return false;
        }

        EpcSsfEnsureSchema(db);
        var row = db.Storages.FirstOrDefault(s => s.Id == storageId);
        return row is not null && row.StorefrontTempDisabled == 1;
    }

    public static bool EpcSsfIsPriceDisabled(OpenStore db, int priceId)
    {
        if (priceId <= 0)
        {
            return false;
        }

        EpcSsfEnsureSchema(db);
        var row = db.Prices.FirstOrDefault(p => p.Id == priceId);
        return row is not null && row.StorefrontTempDisabled == 1;
    }

    public static bool EpcSsfStorageDisabledByPrice(OpenStore db, int priceId)
    {
        if (priceId <= 0)
        {
            return false;
        }

        if (EpcSsfIsPriceDisabled(db, priceId))
        {
            return true;
        }

        EpcSsfEnsureSchema(db);
        var needle = "\"price_id\":" + priceId;
        return db.Storages.Any(s => s.ConnectionOptions.Contains(needle, StringComparison.Ordinal) && s.StorefrontTempDisabled == 1);
    }

    public static Dictionary<int, bool> EpcSsfDisabledStorageIds(OpenStore db)
    {
        EpcSsfEnsureSchema(db);
        var output = new Dictionary<int, bool>();
        foreach (var row in db.Storages.Where(s => s.StorefrontTempDisabled == 1))
        {
            output[row.Id] = true;
        }

        return output;
    }

    public static Dictionary<int, bool> EpcSsfDisabledPriceIds(OpenStore db)
    {
        EpcSsfEnsureSchema(db);
        var output = new Dictionary<int, bool>();
        foreach (var row in db.Prices.Where(p => p.StorefrontTempDisabled == 1))
        {
            output[row.Id] = true;
        }

        return output;
    }

    public static List<Dictionary<string, object?>> EpcSsfCpListRows(OpenStore db)
    {
        EpcSsfEnsureSchema(db);
        var rows = new List<Dictionary<string, object?>>();
        var priceIdsWithStorage = new HashSet<int>();
        foreach (var storage in db.Storages.OrderBy(s => s.Id))
        {
            var iface = db.Interfaces.FirstOrDefault(i => i.Id == storage.InterfaceType);
            var priceId = PriceIdFromOptions(storage.ConnectionOptions);
            if (priceId > 0)
            {
                priceIdsWithStorage.Add(priceId);
            }

            var handler = iface?.HandlerFolder ?? "";
            var typeLabel = handler == "prices" ? "Price list warehouse" : "Warehouse";
            if (handler == "treelax_catalogue")
            {
                typeLabel = "Catalogue warehouse";
            }
            else if (handler != "" && handler != "prices")
            {
                typeLabel = "External supplier";
            }

            rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["entity_type"] = "storage",
                ["entity_id"] = storage.Id,
                ["name"] = storage.Name,
                ["short_name"] = storage.ShortName,
                ["type_label"] = typeLabel,
                ["price_id"] = priceId,
                ["storefront_disabled"] = storage.StorefrontTempDisabled == 1,
                ["hidden"] = storage.Hidden == 1
            });
        }

        foreach (var price in db.Prices.OrderBy(p => p.Id))
        {
            if (priceIdsWithStorage.Contains(price.Id))
            {
                continue;
            }

            rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["entity_type"] = "price_list",
                ["entity_id"] = price.Id,
                ["name"] = price.Name,
                ["short_name"] = "",
                ["type_label"] = "Price list (unlinked)",
                ["price_id"] = price.Id,
                ["storefront_disabled"] = price.StorefrontTempDisabled == 1,
                ["hidden"] = false,
                ["row_count"] = db.PriceData.Count(d => d.PriceId == price.Id)
            });
        }

        return rows;
    }

    public static void EpcSsfSyncPriceFromStorage(OpenStore db, int storageId, int disabled)
    {
        var storage = db.Storages.FirstOrDefault(s => s.Id == storageId);
        if (storage is null)
        {
            return;
        }

        var priceId = PriceIdFromOptions(storage.ConnectionOptions);
        if (priceId <= 0)
        {
            return;
        }

        var price = db.Prices.FirstOrDefault(p => p.Id == priceId);
        if (price is not null)
        {
            price.StorefrontTempDisabled = disabled;
        }
    }

    public static void EpcSsfSyncStoragesFromPrice(OpenStore db, int priceId, int disabled)
    {
        var needle = "\"price_id\":" + priceId;
        foreach (var storage in db.Storages.Where(s => s.ConnectionOptions.Contains(needle, StringComparison.Ordinal)))
        {
            storage.StorefrontTempDisabled = disabled;
        }
    }

    public static void EpcSsfWriteAudit(OpenStore db, string entityType, int entityId, string name, int disabled, int userId, string userLabel)
    {
        EpcSsfEnsureSchema(db, true);
        db.Audits.Add(new AuditRow
        {
            Id = db.NextAuditId++,
            EntityType = entityType,
            EntityId = entityId,
            EntityName = name,
            StorefrontDisabled = disabled,
            UserId = userId,
            UserLabel = userLabel,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(Clock()).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        });
    }

    public static Dictionary<string, object?> EpcSsfSetToggle(OpenStore db, string entityType, int entityId, int disabled, int userId = 0, string userLabel = "")
    {
        entityType = entityType == "price_list" ? "price_list" : "storage";
        disabled = disabled != 0 ? 1 : 0;
        if (entityId <= 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Invalid entity id" };
        }

        EpcSsfEnsureSchema(db, true);
        string name;
        if (entityType == "storage")
        {
            var storage = db.Storages.FirstOrDefault(s => s.Id == entityId);
            name = storage?.Name ?? "";
            if (name == "")
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Storage not found" };
            }

            storage!.StorefrontTempDisabled = disabled;
            EpcSsfSyncPriceFromStorage(db, entityId, disabled);
        }
        else
        {
            var price = db.Prices.FirstOrDefault(p => p.Id == entityId);
            name = price?.Name ?? "";
            if (name == "")
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Price list not found" };
            }

            price!.StorefrontTempDisabled = disabled;
            EpcSsfSyncStoragesFromPrice(db, entityId, disabled);
        }

        EpcSsfWriteAudit(db, entityType, entityId, name, disabled, userId, userLabel);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["storefront_disabled"] = disabled,
            ["entity_name"] = name
        };
    }

    public static List<Dictionary<string, object?>> EpcSsfFilterOfficeStorageBunches(OpenStore db, IEnumerable<object?> bunches)
    {
        var disabled = EpcSsfDisabledStorageIds(db);
        var list = bunches.ToList();
        if (disabled.Count == 0)
        {
            return list.OfType<Dictionary<string, object?>>().Select(Clone).ToList();
        }

        var output = new List<Dictionary<string, object?>>();
        foreach (var item in list)
        {
            if (item is not Dictionary<string, object?> bunch)
            {
                continue;
            }

            var proto = Field(bunch, "protocol_version");
            if (IntVal(proto) == 3 && !Empty(Field(bunch, "office_storage_bunches")) && Field(bunch, "office_storage_bunches") is IEnumerable<object?> nestedIn)
            {
                var nested = new List<Dictionary<string, object?>>();
                foreach (var nbItem in nestedIn)
                {
                    if (nbItem is not Dictionary<string, object?> nb)
                    {
                        continue;
                    }

                    var sid = IntVal(Field(nb, "storage_id"));
                    if (sid > 0 && disabled.ContainsKey(sid))
                    {
                        continue;
                    }

                    nested.Add(Clone(nb));
                }

                if (nested.Count == 0)
                {
                    continue;
                }

                var copy = Clone(bunch);
                copy["office_storage_bunches"] = nested;
                output.Add(copy);
                continue;
            }

            if (Str(proto) == "server")
            {
                output.Add(Clone(bunch));
                continue;
            }

            var storageId = IntVal(Field(bunch, "storage_id"));
            if (storageId > 0 && disabled.ContainsKey(storageId))
            {
                continue;
            }

            output.Add(Clone(bunch));
        }

        return output;
    }

    public static void EpcSkuMediaEnsureSchema(OpenStore db)
    {
        _ = db;
        if (_skuDone)
        {
            return;
        }

        _skuDone = true;
    }

    public static string EpcSkuMediaNormalizeArticle(string article)
    {
        article = article.Trim().ToUpperInvariant();
        return ArticleKeep.Replace(article, "");
    }

    public static string EpcSkuMediaNormalizeBrand(string brand)
    {
        brand = BrandSpaces.Replace(brand, " ").Trim();
        return brand == "" ? "" : MbUpper(brand);
    }

    public static Dictionary<string, string> EpcSkuMediaPhotoTypes()
        => new(StringComparer.Ordinal)
        {
            ["product"] = "Product",
            ["packaging"] = "Packaging",
            ["detail"] = "Detail / close-up",
            ["diagram"] = "Diagram / drawing",
            ["install"] = "Installation",
            ["datasheet"] = "Datasheet shot",
            ["other"] = "Other"
        };

    public static Dictionary<string, string> EpcSkuMediaValueTypes()
        => new(StringComparer.Ordinal)
        {
            ["text"] = "Text",
            ["number"] = "Number",
            ["bool"] = "Yes / No",
            ["list"] = "List (comma-separated)",
            ["rich"] = "Rich text"
        };

    public static List<Dictionary<string, object?>> EpcSkuMediaDefaultSpecTypes()
        =>
        [
            Dict(("name", "Technical"), ("code", "technical"), ("icon", "fa-cogs")),
            Dict(("name", "Dimensions"), ("code", "dimensions"), ("icon", "fa-arrows-alt")),
            Dict(("name", "Materials"), ("code", "materials"), ("icon", "fa-cube")),
            Dict(("name", "Electrical"), ("code", "electrical"), ("icon", "fa-bolt")),
            Dict(("name", "Compatibility"), ("code", "compatibility"), ("icon", "fa-car")),
            Dict(("name", "Packaging"), ("code", "packaging"), ("icon", "fa-archive")),
            Dict(("name", "Performance"), ("code", "performance"), ("icon", "fa-tachometer")),
            Dict(("name", "Safety"), ("code", "safety"), ("icon", "fa-shield")),
            Dict(("name", "Custom"), ("code", "custom"), ("icon", "fa-list"))
        ];

    public static string EpcSkuMediaImagesDir() => "/content/files/images/sku_media/";

    public static string EpcSkuMediaImagesFs()
    {
        var dir = DocumentRoot.TrimEnd('/', '\\') + EpcSkuMediaImagesDir();
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string EpcSkuMediaPhotoUrl(string fileName)
    {
        fileName = Path.GetFileName(fileName.Replace('\\', '/'));
        if (fileName is "" or "." or "..")
        {
            return "";
        }

        return EpcSkuMediaImagesDir() + Uri.EscapeDataString(fileName);
    }

    public static string EpcSkuMediaStorefrontPartUrl(string brand, string article, string langHref = "/en")
    {
        brand = brand.Trim();
        article = article.Trim();
        if (article == "")
        {
            return "";
        }

        if (PartUrlBuilder is not null)
        {
            var built = PartUrlBuilder(langHref, brand, article);
            if (built != "")
            {
                return built;
            }
        }

        langHref = langHref.TrimEnd('/');
        if (langHref == "")
        {
            langHref = "/en";
        }

        var artKey = EpcSkuMediaNormalizeArticle(article);
        if (artKey == "")
        {
            return "";
        }

        if (brand == "")
        {
            return langHref + "/parts/brands/" + Uri.EscapeDataString(artKey);
        }

        return langHref + "/parts/" + Uri.EscapeDataString(brand.ToUpperInvariant()) + "/" + Uri.EscapeDataString(artKey);
    }

    public static Dictionary<string, object?> EpcSkuMediaAttachLocalPhoto(OpenStore db, int profileId, string sourcePath, Dictionary<string, object?>? meta = null)
    {
        EpcSkuMediaEnsureSchema(db);
        meta ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        if (profileId <= 0)
        {
            return Err("Missing profile");
        }

        if (sourcePath == "" || !File.Exists(sourcePath))
        {
            return Err("Source image missing");
        }

        var ext = Path.GetExtension(sourcePath).TrimStart('.').ToLowerInvariant();
        if (ext is not ("jpg" or "jpeg" or "png" or "gif" or "webp"))
        {
            return Err("Unsupported image type");
        }

        var dir = EpcSkuMediaImagesFs();
        var saved = "sku_" + profileId + "_" + Clock() + "_" + Convert.ToHexString(RandomBytes4()).ToLowerInvariant() + "." + (ext == "jpeg" ? "jpg" : ext);
        try
        {
            File.Copy(sourcePath, Path.Combine(dir, saved), true);
        }
        catch
        {
            return Err("Could not copy image");
        }

        var sort = IntVal(Field(meta, "sort_order"));
        if (sort <= 0)
        {
            sort = (db.Photos.Where(p => p.ProfileId == profileId).Select(p => p.SortOrder).DefaultIfEmpty(0).Max()) + 10;
        }

        var isPrimary = Empty(Field(meta, "is_primary")) ? 0 : 1;
        if (isPrimary == 1)
        {
            foreach (var photo in db.Photos.Where(p => p.ProfileId == profileId))
            {
                photo.IsPrimary = 0;
            }
        }
        else if (db.Photos.Count(p => p.ProfileId == profileId) == 0)
        {
            isPrimary = 1;
        }

        var photoType = Str(Field(meta, "photo_type") ?? "product").Trim();
        if (!EpcSkuMediaPhotoTypes().ContainsKey(photoType))
        {
            photoType = "product";
        }

        var now = Clock();
        var id = db.NextPhotoId++;
        db.Photos.Add(new PhotoRow
        {
            Id = id,
            ProfileId = profileId,
            FileName = saved,
            Alt = Str(Field(meta, "alt")).Trim(),
            Caption = Str(Field(meta, "caption")).Trim(),
            PhotoType = photoType,
            SortOrder = sort,
            IsPrimary = isPrimary,
            CreatedAt = now
        });
        TouchProfile(db, profileId, now);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["id"] = id,
            ["file_name"] = saved,
            ["url"] = EpcSkuMediaPhotoUrl(saved)
        };
    }

    public static Dictionary<string, object?>? EpcSkuMediaFindProfile(OpenStore db, int profileId = 0, int productId = 0, string brand = "", string article = "")
    {
        EpcSkuMediaEnsureSchema(db);
        if (profileId > 0)
        {
            return SnapProfile(db.Profiles.FirstOrDefault(p => p.Id == profileId));
        }

        if (productId > 0)
        {
            var byProduct = db.Profiles.Where(p => p.ProductId == productId).OrderByDescending(p => p.Id).FirstOrDefault();
            if (byProduct is not null)
            {
                return SnapProfile(byProduct);
            }
        }

        brand = EpcSkuMediaNormalizeBrand(brand);
        var key = EpcSkuMediaNormalizeArticle(article);
        if (brand != "" && key != "")
        {
            return SnapProfile(db.Profiles
                .Where(p => string.Equals(p.Brand, brand, StringComparison.OrdinalIgnoreCase) && p.ArticleKey == key)
                .OrderByDescending(p => p.Id)
                .FirstOrDefault());
        }

        if (key != "")
        {
            return SnapProfile(db.Profiles.Where(p => p.ArticleKey == key).OrderByDescending(p => p.Id).FirstOrDefault());
        }

        return null;
    }

    public static Dictionary<string, object?> EpcSkuMediaUpsertProfile(OpenStore db, Dictionary<string, object?> data)
    {
        EpcSkuMediaEnsureSchema(db);
        var id = IntVal(Field(data, "id"));
        var productId = IntVal(Field(data, "product_id"));
        var brand = EpcSkuMediaNormalizeBrand(Str(Field(data, "brand")));
        var article = Str(Field(data, "article")).Trim();
        var key = EpcSkuMediaNormalizeArticle(article != "" ? article : Str(Field(data, "article_key")));
        var title = Str(Field(data, "title")).Trim();
        var subtitle = Str(Field(data, "subtitle")).Trim();
        var status = Str(Field(data, "status") ?? "active").Trim();
        if (status == "")
        {
            status = "active";
        }

        var now = Clock();
        if (id <= 0)
        {
            var existing = EpcSkuMediaFindProfile(db, 0, productId, brand, article != "" ? article : key);
            if (existing is not null)
            {
                id = IntVal(Field(existing, "id"));
            }
        }

        if (id > 0)
        {
            var row = db.Profiles.FirstOrDefault(p => p.Id == id);
            if (row is not null)
            {
                row.ProductId = productId > 0 ? productId : null;
                row.Brand = brand;
                row.Article = article != "" ? article : key;
                row.ArticleKey = key;
                row.Title = title;
                row.Subtitle = subtitle;
                row.Status = status;
                row.UpdatedAt = now;
            }
        }
        else
        {
            id = db.NextProfileId++;
            db.Profiles.Add(new ProfileRow
            {
                Id = id,
                ProductId = productId > 0 ? productId : null,
                Brand = brand,
                Article = article != "" ? article : key,
                ArticleKey = key,
                Title = title,
                Subtitle = subtitle,
                Status = status,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        return EpcSkuMediaFindProfile(db, id) ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = id };
    }

    public static List<Dictionary<string, object?>> EpcSkuMediaListProfiles(OpenStore db, string q = "", int limit = 100, int offset = 0)
    {
        EpcSkuMediaEnsureSchema(db);
        limit = Math.Max(1, Math.Min(500, limit));
        offset = Math.Max(0, offset);
        q = q.Trim();
        IEnumerable<ProfileRow> rows = db.Profiles.OrderByDescending(p => p.UpdatedAt).ThenByDescending(p => p.Id);
        if (q != "")
        {
            var key = EpcSkuMediaNormalizeArticle(q);
            rows = rows.Where(p =>
                p.Brand.Contains(q, StringComparison.Ordinal) ||
                p.Article.Contains(q, StringComparison.Ordinal) ||
                p.ArticleKey.Contains(q, StringComparison.Ordinal) ||
                p.Title.Contains(q, StringComparison.Ordinal) ||
                p.ArticleKey == key);
        }

        return rows.Skip(offset).Take(limit).Select(p =>
        {
            var snap = SnapProfile(p)!;
            snap["photo_count"] = db.Photos.Count(ph => ph.ProfileId == p.Id);
            snap["group_count"] = db.SpecGroups.Count(g => g.ProfileId == p.Id);
            snap["spec_count"] = db.SpecRows.Count(r => r.ProfileId == p.Id);
            return snap;
        }).ToList();
    }

    public static Dictionary<int, Dictionary<string, object?>> EpcSkuMediaPriceStorageMap(OpenStore db)
    {
        if (_priceMap is not null)
        {
            return _priceMap;
        }

        var cache = new Dictionary<int, Dictionary<string, object?>>();
        if (db.StoragesMissing)
        {
            _priceMap = cache;
            return cache;
        }

        foreach (var row in db.Storages)
        {
            var priceId = PriceIdFromOptions(row.ConnectionOptions);
            if (priceId <= 0)
            {
                continue;
            }

            cache[priceId] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["storage_id"] = row.Id,
                ["short_name"] = row.ShortName != "" ? row.ShortName : row.Name,
                ["name"] = row.Name
            };
        }

        _priceMap = cache;
        return cache;
    }

    public static List<Dictionary<string, object?>> EpcSkuMediaSearchLibrary(OpenStore db, string q = "", int limit = 120)
    {
        EpcSkuMediaEnsureSchema(db);
        limit = Math.Max(1, Math.Min(200, limit));
        q = q.Trim();
        var output = new List<Dictionary<string, object?>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var priceMap = EpcSkuMediaPriceStorageMap(db);
        foreach (var profile in EpcSkuMediaListProfiles(db, q, limit, 0))
        {
            var brand = Str(Field(profile, "brand"));
            var article = Str(Field(profile, "article"));
            var key = EpcSkuMediaNormalizeArticle(article != "" ? article : Str(Field(profile, "article_key")));
            var sig = EpcSkuMediaNormalizeBrand(brand) + "|" + key;
            if (sig != "|" && !seen.Add(sig))
            {
                continue;
            }

            var articleShow = article != "" ? article : key;
            output.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = IntVal(Field(profile, "id")),
                ["source"] = "profile",
                ["brand"] = brand,
                ["article"] = articleShow,
                ["article_show"] = articleShow,
                ["title"] = Str(Field(profile, "title")),
                ["warehouse"] = "",
                ["product_id"] = IntVal(Field(profile, "product_id")),
                ["photo_count"] = IntVal(Field(profile, "photo_count")),
                ["spec_count"] = IntVal(Field(profile, "spec_count")),
                ["group_count"] = IntVal(Field(profile, "group_count")),
                ["has_profile"] = true,
                ["status"] = Str(Field(profile, "status") ?? "active"),
                ["storefront_url"] = EpcSkuMediaStorefrontPartUrl(brand, articleShow)
            });
        }

        var supplierLimit = Math.Max(20, limit - output.Count);
        IEnumerable<PriceDataRow> supplierSrc = db.PriceDataMissing ? [] : db.PriceData;
        if (q != "")
        {
            var key = EpcSkuMediaNormalizeArticle(q);
            supplierSrc = supplierSrc.Where(d =>
                d.Manufacturer.Contains(q, StringComparison.Ordinal) ||
                d.Article.Contains(q, StringComparison.Ordinal) ||
                d.ArticleShow.Contains(q, StringComparison.Ordinal) ||
                d.ArticleSearch.Contains(q, StringComparison.Ordinal) ||
                d.Name.Contains(q, StringComparison.Ordinal) ||
                d.ArticleSearch == key);
        }
        else
        {
            supplierSrc = supplierSrc.Where(d => d.Manufacturer != "" && d.ArticleSearch != "");
        }

        var supplierRows = supplierSrc
            .GroupBy(d => (d.Manufacturer, d.ArticleSearch))
            .Select(g => new
            {
                manufacturer = g.Key.Manufacturer,
                article = g.Max(x => x.Article),
                article_show = g.Max(x => x.ArticleShow),
                article_search = g.Key.ArticleSearch,
                name = g.Max(x => x.Name),
                price_id = g.Max(x => x.PriceId),
                offer_count = g.Count(),
                max_id = g.Max(x => x.Id)
            })
            .OrderByDescending(x => x.max_id)
            .Take(supplierLimit)
            .ToList();

        foreach (var row in supplierRows)
        {
            var brand = EpcSkuMediaNormalizeBrand(row.manufacturer ?? "");
            var articleShow = (row.article_show ?? "").Trim();
            if (articleShow == "")
            {
                articleShow = (row.article ?? "").Trim();
            }

            var key = EpcSkuMediaNormalizeArticle(articleShow != "" ? articleShow : row.article_search);
            if (brand == "" || key == "")
            {
                continue;
            }

            var sig = brand + "|" + key;
            if (seen.Contains(sig))
            {
                foreach (var existing in output)
                {
                    if (Str(Field(existing, "brand")) == brand
                        && EpcSkuMediaNormalizeArticle(Str(Field(existing, "article"))) == key
                        && Str(Field(existing, "warehouse")) == "")
                    {
                        if (priceMap.TryGetValue(row.price_id, out var mapped))
                        {
                            existing["warehouse"] = mapped["short_name"];
                        }
                    }
                }

                continue;
            }

            seen.Add(sig);
            var wh = priceMap.TryGetValue(row.price_id, out var map) ? Str(Field(map, "short_name")) : "";
            var profile = EpcSkuMediaFindProfile(db, 0, 0, brand, key);
            var hasProfile = profile is not null;
            var artOut = articleShow != "" ? articleShow : key;
            output.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = hasProfile ? IntVal(Field(profile!, "id")) : 0,
                ["source"] = "supplier",
                ["brand"] = brand,
                ["article"] = artOut,
                ["article_show"] = artOut,
                ["title"] = row.name ?? "",
                ["warehouse"] = wh,
                ["product_id"] = 0,
                ["photo_count"] = 0,
                ["spec_count"] = 0,
                ["group_count"] = 0,
                ["has_profile"] = hasProfile,
                ["status"] = hasProfile ? Str(Field(profile!, "status") ?? "active") : "new",
                ["offer_count"] = row.offer_count,
                ["storefront_url"] = EpcSkuMediaStorefrontPartUrl(brand, artOut)
            });
        }

        var catBudget = Math.Max(0, limit - output.Count);
        if (catBudget > 0 && !db.CatalogueMissing)
        {
            IEnumerable<CatalogueRow> cats = db.Catalogue.OrderByDescending(c => c.Id);
            if (q != "")
            {
                cats = cats.Where(c => c.Caption.Contains(q, StringComparison.Ordinal) || c.Alias.Contains(q, StringComparison.Ordinal));
            }
            else
            {
                catBudget = Math.Min(15, catBudget);
            }

            foreach (var item in cats.Take(catBudget))
            {
                if (item.Id <= 0)
                {
                    continue;
                }

                var profile = EpcSkuMediaFindProfile(db, 0, item.Id, "", "");
                output.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = profile is not null ? IntVal(Field(profile, "id")) : 0,
                    ["source"] = "catalogue",
                    ["brand"] = profile is not null ? Str(Field(profile, "brand")) : "",
                    ["article"] = profile is not null ? Str(Field(profile, "article")) : "",
                    ["article_show"] = profile is not null ? Str(Field(profile, "article")) : "",
                    ["title"] = item.Caption,
                    ["warehouse"] = "",
                    ["product_id"] = item.Id,
                    ["photo_count"] = 0,
                    ["spec_count"] = 0,
                    ["group_count"] = 0,
                    ["has_profile"] = profile is not null,
                    ["status"] = profile is not null ? Str(Field(profile, "status") ?? "active") : "new"
                });
            }
        }

        return output.Take(limit).ToList();
    }

    public static Dictionary<string, object?> EpcSkuMediaEnsureFromIdentity(OpenStore db, string brand, string article, string title = "", int productId = 0)
    {
        brand = EpcSkuMediaNormalizeBrand(brand);
        article = article.Trim();
        var key = EpcSkuMediaNormalizeArticle(article);
        var existing = EpcSkuMediaFindProfile(db, 0, productId, brand, article != "" ? article : key);
        if (existing is not null)
        {
            return existing;
        }

        if (title == "" && brand != "" && key != "")
        {
            title = brand + " " + (article != "" ? article : key);
        }

        return EpcSkuMediaUpsertProfile(db, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["product_id"] = productId,
            ["brand"] = brand,
            ["article"] = article != "" ? article : key,
            ["title"] = title,
            ["status"] = "active"
        });
    }

    public static Dictionary<string, object?> EpcSkuMediaPublicLookup(OpenStore db, string brand = "", string article = "", int productId = 0)
    {
        var empty = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["url"] = "",
            ["photos"] = new List<object?>(),
            ["specs"] = new List<object?>(),
            ["profile"] = null
        };
        var profile = EpcSkuMediaResolveForProduct(db, productId, brand, article);
        if (profile is null)
        {
            return empty;
        }

        var status = Str(Field(profile, "status") ?? "active");
        if (status is "hidden" or "draft")
        {
            return empty;
        }

        var payload = EpcSkuMediaFullPayload(db, IntVal(Field(profile, "id")));
        if (payload is null)
        {
            return empty;
        }

        var photos = new List<Dictionary<string, object?>>();
        var primaryUrl = "";
        if (payload["photos"] is IEnumerable<object?> photoItems)
        {
            foreach (var item in photoItems)
            {
                if (item is not Dictionary<string, object?> ph)
                {
                    continue;
                }

                var url = Str(Field(ph, "url"));
                if (url == "")
                {
                    continue;
                }

                photos.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["url"] = url,
                    ["alt"] = Str(Field(ph, "alt")),
                    ["caption"] = Str(Field(ph, "caption")),
                    ["photo_type"] = Str(Field(ph, "photo_type") ?? "product"),
                    ["is_primary"] = !Empty(Field(ph, "is_primary"))
                });
                if (primaryUrl == "" || !Empty(Field(ph, "is_primary")))
                {
                    primaryUrl = url;
                }
            }
        }

        var specs = new List<Dictionary<string, object?>>();
        if (payload["spec_groups"] is IEnumerable<object?> groups)
        {
            foreach (var item in groups)
            {
                if (item is not Dictionary<string, object?> group)
                {
                    continue;
                }

                var rows = new List<Dictionary<string, object?>>();
                if (Field(group, "rows") is IEnumerable<object?> groupRows)
                {
                    foreach (var rowItem in groupRows)
                    {
                        if (rowItem is not Dictionary<string, object?> row)
                        {
                            continue;
                        }

                        rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["label"] = Str(Field(row, "label")),
                            ["value"] = Str(Field(row, "display") ?? Field(row, "value")),
                            ["value_type"] = Str(Field(row, "value_type") ?? "text")
                        });
                    }
                }

                if (rows.Count == 0)
                {
                    continue;
                }

                specs.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["name"] = Str(Field(group, "name") ?? "Specifications"),
                    ["icon"] = Str(Field(group, "icon") ?? "fa-list"),
                    ["rows"] = rows
                });
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["url"] = primaryUrl,
            ["photos"] = photos,
            ["specs"] = specs,
            ["profile"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = IntVal(Field(profile, "id")),
                ["brand"] = Str(Field(profile, "brand")),
                ["article"] = Str(Field(profile, "article")),
                ["title"] = Str(Field(profile, "title"))
            }
        };
    }

    public static List<Dictionary<string, object?>> EpcSkuMediaPhotos(OpenStore db, int profileId)
    {
        if (profileId <= 0)
        {
            return [];
        }

        EpcSkuMediaEnsureSchema(db);
        return db.Photos
            .Where(p => p.ProfileId == profileId)
            .OrderByDescending(p => p.IsPrimary)
            .ThenBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .Select(SnapPhoto)
            .ToList();
    }

    public static Dictionary<string, object?> EpcSkuMediaAddPhoto(OpenStore db, int profileId, Dictionary<string, object?> file, Dictionary<string, object?>? meta = null)
    {
        EpcSkuMediaEnsureSchema(db);
        _ = file;
        _ = meta;
        if (profileId <= 0)
        {
            return Err("Missing profile");
        }

        return Err("No upload");
    }

    public static void EpcSkuMediaMirrorToCatalogue(OpenStore db, int productId, string fileName)
    {
        if (productId <= 0 || fileName == "")
        {
            return;
        }

        try
        {
            var legacyDir = DocumentRoot.TrimEnd('/', '\\') + "/content/files/images/products_images/";
            var src = Path.Combine(EpcSkuMediaImagesFs(), Path.GetFileName(fileName));
            if (!File.Exists(src) || !Directory.Exists(Path.GetDirectoryName(legacyDir.TrimEnd('/', '\\'))!))
            {
                return;
            }

            Directory.CreateDirectory(legacyDir);
            var legacyName = "sku_m_" + Path.GetFileName(fileName);
            var dest = Path.Combine(legacyDir, legacyName);
            if (!File.Exists(dest))
            {
                File.Copy(src, dest, false);
            }

            if (!db.CatalogueImages.Any(i => i.ProductId == productId && i.FileName == legacyName))
            {
                db.CatalogueImages.Add(new CatalogueImageRow { ProductId = productId, FileName = legacyName });
            }
        }
        catch
        {
            // Non-fatal — SKU media remains source of truth.
        }
    }

    public static bool EpcSkuMediaDeletePhoto(OpenStore db, int photoId)
    {
        EpcSkuMediaEnsureSchema(db);
        var row = db.Photos.FirstOrDefault(p => p.Id == photoId);
        if (row is null)
        {
            return false;
        }

        db.Photos.Remove(row);
        var file = Path.GetFileName(row.FileName);
        if (file != "")
        {
            var path = Path.Combine(EpcSkuMediaImagesFs(), file);
            if (File.Exists(path) && db.Photos.Count(p => p.FileName == file) == 0)
            {
                try { File.Delete(path); } catch { /* best effort */ }
            }
        }

        TouchProfile(db, row.ProfileId, Clock());
        return true;
    }

    public static bool EpcSkuMediaUpdatePhoto(OpenStore db, int photoId, Dictionary<string, object?> meta)
    {
        EpcSkuMediaEnsureSchema(db);
        var row = db.Photos.FirstOrDefault(p => p.Id == photoId);
        if (row is null)
        {
            return false;
        }

        var alt = meta.ContainsKey("alt") ? Str(Field(meta, "alt")).Trim() : row.Alt;
        var caption = meta.ContainsKey("caption") ? Str(Field(meta, "caption")).Trim() : row.Caption;
        var photoType = meta.ContainsKey("photo_type") ? Str(Field(meta, "photo_type")).Trim() : row.PhotoType;
        if (!EpcSkuMediaPhotoTypes().ContainsKey(photoType))
        {
            photoType = row.PhotoType;
        }

        var sort = meta.ContainsKey("sort_order") ? IntVal(Field(meta, "sort_order")) : row.SortOrder;
        var isPrimary = meta.ContainsKey("is_primary")
            ? (Empty(Field(meta, "is_primary")) ? 0 : 1)
            : row.IsPrimary;
        if (isPrimary == 1)
        {
            foreach (var photo in db.Photos.Where(p => p.ProfileId == row.ProfileId))
            {
                photo.IsPrimary = 0;
            }
        }

        row.Alt = alt;
        row.Caption = caption;
        row.PhotoType = photoType;
        row.SortOrder = sort;
        row.IsPrimary = isPrimary;
        TouchProfile(db, row.ProfileId, Clock());
        return true;
    }

    public static Dictionary<string, object?> EpcSkuMediaSpecBundle(OpenStore db, int profileId)
    {
        EpcSkuMediaEnsureSchema(db);
        if (profileId <= 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["groups"] = new List<object?>(),
                ["rows"] = new List<object?>()
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["groups"] = db.SpecGroups.Where(g => g.ProfileId == profileId).OrderBy(g => g.SortOrder).ThenBy(g => g.Id).Select(SnapGroup).ToList(),
            ["rows"] = db.SpecRows.Where(r => r.ProfileId == profileId).OrderBy(r => r.SortOrder).ThenBy(r => r.Id).Select(SnapSpec).ToList()
        };
    }

    public static Dictionary<string, object?> EpcSkuMediaAddSpecGroup(OpenStore db, int profileId, Dictionary<string, object?> data)
    {
        EpcSkuMediaEnsureSchema(db);
        if (profileId <= 0)
        {
            return Err("Missing profile");
        }

        var name = Str(Field(data, "name")).Trim();
        if (name == "")
        {
            return Err("Group name required");
        }

        var code = Str(Field(data, "code")).Trim();
        if (code == "")
        {
            code = GroupCodeKeep.Replace(name, "_").ToLowerInvariant();
            if (code == "")
            {
                code = "custom";
            }
        }

        var icon = Str(Field(data, "icon") ?? "fa-list").Trim();
        if (icon == "")
        {
            icon = "fa-list";
        }

        var sort = IntVal(Field(data, "sort_order"));
        if (sort <= 0)
        {
            sort = (db.SpecGroups.Where(g => g.ProfileId == profileId).Select(g => g.SortOrder).DefaultIfEmpty(0).Max()) + 10;
        }

        var now = Clock();
        var id = db.NextGroupId++;
        db.SpecGroups.Add(new SpecGroupRow
        {
            Id = id,
            ProfileId = profileId,
            Name = name,
            Code = code,
            Icon = icon,
            SortOrder = sort,
            CreatedAt = now
        });
        TouchProfile(db, profileId, now);
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["id"] = id };
    }

    public static bool EpcSkuMediaDeleteSpecGroup(OpenStore db, int groupId)
    {
        EpcSkuMediaEnsureSchema(db);
        var group = db.SpecGroups.FirstOrDefault(g => g.Id == groupId);
        if (group is null)
        {
            return false;
        }

        db.SpecRows.RemoveAll(r => r.GroupId == groupId);
        db.SpecGroups.Remove(group);
        TouchProfile(db, group.ProfileId, Clock());
        return true;
    }

    public static Dictionary<string, object?> EpcSkuMediaAddSpecRow(OpenStore db, int groupId, Dictionary<string, object?> data)
    {
        EpcSkuMediaEnsureSchema(db);
        var group = db.SpecGroups.FirstOrDefault(g => g.Id == groupId);
        if (group is null)
        {
            return Err("Group not found");
        }

        var label = Str(Field(data, "label")).Trim();
        if (label == "")
        {
            return Err("Label required");
        }

        var valueType = Str(Field(data, "value_type") ?? "text").Trim();
        if (!EpcSkuMediaValueTypes().ContainsKey(valueType))
        {
            valueType = "text";
        }

        var value = Str(Field(data, "value"));
        if (valueType == "bool")
        {
            var lower = value.ToLowerInvariant();
            value = !Empty(Field(data, "value")) && value != "0" && lower != "no" && lower != "false" ? "1" : "0";
        }

        var unit = Str(Field(data, "unit")).Trim();
        var sort = IntVal(Field(data, "sort_order"));
        if (sort <= 0)
        {
            sort = (db.SpecRows.Where(r => r.GroupId == groupId).Select(r => r.SortOrder).DefaultIfEmpty(0).Max()) + 10;
        }

        var now = Clock();
        var id = db.NextSpecId++;
        db.SpecRows.Add(new SpecRow
        {
            Id = id,
            GroupId = groupId,
            ProfileId = group.ProfileId,
            Label = label,
            Value = value,
            ValueType = valueType,
            Unit = unit,
            SortOrder = sort,
            CreatedAt = now
        });
        TouchProfile(db, group.ProfileId, now);
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["id"] = id };
    }

    public static bool EpcSkuMediaUpdateSpecRow(OpenStore db, int rowId, Dictionary<string, object?> data)
    {
        EpcSkuMediaEnsureSchema(db);
        var row = db.SpecRows.FirstOrDefault(r => r.Id == rowId);
        if (row is null)
        {
            return false;
        }

        var label = data.ContainsKey("label") ? Str(Field(data, "label")).Trim() : row.Label;
        var valueType = data.ContainsKey("value_type") ? Str(Field(data, "value_type")).Trim() : row.ValueType;
        if (!EpcSkuMediaValueTypes().ContainsKey(valueType))
        {
            valueType = row.ValueType;
        }

        var value = data.ContainsKey("value") ? Str(Field(data, "value")) : row.Value;
        if (valueType == "bool")
        {
            var lower = value.ToLowerInvariant();
            value = value != "" && value != "0" && lower != "no" && lower != "false" ? "1" : "0";
        }

        var unit = data.ContainsKey("unit") ? Str(Field(data, "unit")).Trim() : row.Unit;
        var sort = data.ContainsKey("sort_order") ? IntVal(Field(data, "sort_order")) : row.SortOrder;
        row.Label = label;
        row.Value = value;
        row.ValueType = valueType;
        row.Unit = unit;
        row.SortOrder = sort;
        TouchProfile(db, row.ProfileId, Clock());
        return true;
    }

    public static bool EpcSkuMediaDeleteSpecRow(OpenStore db, int rowId)
    {
        EpcSkuMediaEnsureSchema(db);
        var row = db.SpecRows.FirstOrDefault(r => r.Id == rowId);
        if (row is null)
        {
            return false;
        }

        db.SpecRows.Remove(row);
        TouchProfile(db, row.ProfileId, Clock());
        return true;
    }

    public static bool EpcSkuMediaDeleteProfile(OpenStore db, int profileId)
    {
        EpcSkuMediaEnsureSchema(db);
        if (profileId <= 0)
        {
            return false;
        }

        var photos = EpcSkuMediaPhotos(db, profileId);
        db.SpecRows.RemoveAll(r => r.ProfileId == profileId);
        db.SpecGroups.RemoveAll(g => g.ProfileId == profileId);
        db.Photos.RemoveAll(p => p.ProfileId == profileId);
        db.Profiles.RemoveAll(p => p.Id == profileId);
        foreach (var ph in photos)
        {
            var file = Path.GetFileName(Str(Field(ph, "file_name")));
            if (file == "")
            {
                continue;
            }

            var path = Path.Combine(EpcSkuMediaImagesFs(), file);
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { /* best effort */ }
            }
        }

        return true;
    }

    public static string EpcSkuMediaFormatValue(string value, string valueType, string unit = "")
    {
        string output;
        switch (valueType)
        {
            case "bool":
                var lower = value.ToLowerInvariant();
                output = value == "1" || lower == "yes" || lower == "true" ? "Yes" : "No";
                break;
            case "list":
                var parts = ListSplit.Split(value).Select(p => p.Trim()).Where(p => p != "").ToList();
                output = string.Join(", ", parts);
                break;
            case "rich":
                output = value;
                break;
            default:
                output = value;
                break;
        }

        if (unit != "" && valueType is not ("bool" or "rich"))
        {
            output = (output + " " + unit).Trim();
        }

        return output;
    }

    public static Dictionary<string, object?>? EpcSkuMediaResolveForProduct(OpenStore db, int productId, string brand = "", string article = "")
    {
        if (productId <= 0 && brand == "" && article == "")
        {
            return null;
        }

        return EpcSkuMediaFindProfile(db, 0, productId, brand, article);
    }

    public static Dictionary<string, object?>? EpcSkuMediaFullPayload(OpenStore db, int profileId)
    {
        var profile = EpcSkuMediaFindProfile(db, profileId);
        if (profile is null)
        {
            return null;
        }

        var photos = EpcSkuMediaPhotos(db, profileId);
        foreach (var ph in photos)
        {
            ph["url"] = EpcSkuMediaPhotoUrl(Str(Field(ph, "file_name")));
        }

        var bundle = EpcSkuMediaSpecBundle(db, profileId);
        var grouped = new Dictionary<int, Dictionary<string, object?>>();
        if (bundle["groups"] is IEnumerable<object?> groups)
        {
            foreach (var item in groups)
            {
                if (item is not Dictionary<string, object?> group)
                {
                    continue;
                }

                var gid = IntVal(Field(group, "id"));
                var copy = Clone(group);
                copy["rows"] = new List<Dictionary<string, object?>>();
                grouped[gid] = copy;
            }
        }

        if (bundle["rows"] is IEnumerable<object?> rows)
        {
            foreach (var item in rows)
            {
                if (item is not Dictionary<string, object?> row)
                {
                    continue;
                }

                var gid = IntVal(Field(row, "group_id"));
                if (!grouped.TryGetValue(gid, out var group))
                {
                    continue;
                }

                var copy = Clone(row);
                copy["display"] = EpcSkuMediaFormatValue(
                    Str(Field(row, "value")),
                    Str(Field(row, "value_type") ?? "text"),
                    Str(Field(row, "unit")));
                ((List<Dictionary<string, object?>>)group["rows"]!).Add(copy);
            }
        }

        var storefront = EpcSkuMediaStorefrontPartUrl(Str(Field(profile, "brand")), Str(Field(profile, "article")));
        profile["storefront_url"] = storefront;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["profile"] = profile,
            ["photos"] = photos,
            ["spec_groups"] = grouped.Values.ToList(),
            ["photo_types"] = EpcSkuMediaPhotoTypes(),
            ["value_types"] = EpcSkuMediaValueTypes(),
            ["default_spec_types"] = EpcSkuMediaDefaultSpecTypes(),
            ["storefront_url"] = storefront
        };
    }

    private static void TouchProfile(OpenStore db, int profileId, long now)
    {
        var row = db.Profiles.FirstOrDefault(p => p.Id == profileId);
        if (row is not null)
        {
            row.UpdatedAt = now;
        }
    }

    private static Dictionary<string, object?>? SnapProfile(ProfileRow? row)
    {
        if (row is null)
        {
            return null;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = row.Id,
            ["product_id"] = row.ProductId,
            ["brand"] = row.Brand,
            ["article"] = row.Article,
            ["article_key"] = row.ArticleKey,
            ["title"] = row.Title,
            ["subtitle"] = row.Subtitle,
            ["status"] = row.Status,
            ["created_at"] = row.CreatedAt,
            ["updated_at"] = row.UpdatedAt
        };
    }

    private static Dictionary<string, object?> SnapPhoto(PhotoRow row)
        => new(StringComparer.Ordinal)
        {
            ["id"] = row.Id,
            ["profile_id"] = row.ProfileId,
            ["file_name"] = row.FileName,
            ["alt"] = row.Alt,
            ["caption"] = row.Caption,
            ["photo_type"] = row.PhotoType,
            ["sort_order"] = row.SortOrder,
            ["is_primary"] = row.IsPrimary,
            ["created_at"] = row.CreatedAt
        };

    private static Dictionary<string, object?> SnapGroup(SpecGroupRow row)
        => new(StringComparer.Ordinal)
        {
            ["id"] = row.Id,
            ["profile_id"] = row.ProfileId,
            ["name"] = row.Name,
            ["code"] = row.Code,
            ["icon"] = row.Icon,
            ["sort_order"] = row.SortOrder,
            ["created_at"] = row.CreatedAt
        };

    private static Dictionary<string, object?> SnapSpec(SpecRow row)
        => new(StringComparer.Ordinal)
        {
            ["id"] = row.Id,
            ["group_id"] = row.GroupId,
            ["profile_id"] = row.ProfileId,
            ["label"] = row.Label,
            ["value"] = row.Value,
            ["value_type"] = row.ValueType,
            ["unit"] = row.Unit,
            ["sort_order"] = row.SortOrder,
            ["created_at"] = row.CreatedAt
        };

    private static int PriceIdFromOptions(string json)
    {
        try
        {
            var opts = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
            if (opts is null || !opts.TryGetValue("price_id", out var raw) || Empty(raw))
            {
                return 0;
            }

            return IntVal(raw);
        }
        catch
        {
            return 0;
        }
    }

    private static Dictionary<string, object?> Err(string message)
        => new(StringComparer.Ordinal) { ["ok"] = false, ["error"] = message };

    private static Dictionary<string, object?> Dict(params (string Key, object? Value)[] pairs)
    {
        var d = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            d[key] = value;
        }

        return d;
    }

    private static Dictionary<string, object?> Clone(Dictionary<string, object?> src)
        => new(src, StringComparer.Ordinal);

    private static object? Field(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) ? value : null;

    private static string Str(object? value)
        => value switch
        {
            null => "",
            string s => s,
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String => je.GetString() ?? "",
            System.Text.Json.JsonElement je => je.ToString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };

    private static int IntVal(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        if (value is int i)
        {
            return i;
        }

        if (value is long l)
        {
            return (int)l;
        }

        if (value is bool b)
        {
            return b ? 1 : 0;
        }

        if (value is System.Text.Json.JsonElement je)
        {
            if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out var n))
            {
                return n;
            }

            value = je.ToString();
        }

        var s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        if (s == "" || s == "0")
        {
            return 0;
        }

        if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            return (int)d;
        }

        return 0;
    }

    private static bool Empty(object? value)
        => value switch
        {
            null => true,
            bool b => !b,
            int i => i == 0,
            long l => l == 0,
            double d => d == 0,
            string s => s is "" or "0",
            System.Collections.ICollection c => c.Count == 0,
            System.Text.Json.JsonElement je => je.ValueKind is System.Text.Json.JsonValueKind.Null
                or System.Text.Json.JsonValueKind.False
                || (je.ValueKind == System.Text.Json.JsonValueKind.String && je.GetString() is "" or "0")
                || (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.GetDouble() == 0)
                || (je.ValueKind == System.Text.Json.JsonValueKind.Array && je.GetArrayLength() == 0),
            _ => Str(value) is "" or "0"
        };

    private static string MbUpper(string value)
        => value.ToUpper(CultureInfo.GetCultureInfo("en-US"));
}
