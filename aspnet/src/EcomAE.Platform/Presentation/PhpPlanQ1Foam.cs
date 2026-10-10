using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-foam accessories marketplace DB. PHP identifiers kept for the inventory:
/// <c>epc_acc_taxonomy_json_path</c>, <c>epc_acc_load_taxonomy_json</c>,
/// <c>epc_acc_ensure_schema</c>, <c>epc_acc_photos_fs_dir</c>,
/// <c>epc_acc_photo_public_url</c>, <c>epc_acc_storefront_url</c>,
/// <c>epc_acc_is_outbound_external_url</c>, <c>epc_acc_photos_list</c>,
/// <c>epc_acc_photos_sync_listing</c>, <c>epc_acc_photos_add</c>,
/// <c>epc_acc_photos_delete</c>, <c>epc_acc_photos_set_primary</c>,
/// <c>epc_acc_photos_add_many_from_files</c>, <c>epc_acc_slugify</c>,
/// <c>epc_acc_uae_cities</c>, <c>epc_acc_legacy_pk_cities</c>,
/// <c>epc_acc_migrate_uae_locale</c>, <c>epc_acc_seed_terms_from_json</c>,
/// <c>epc_acc_get_terms</c>, <c>epc_acc_term_labels</c>, <c>epc_acc_save_term</c>,
/// <c>epc_acc_set_term_active</c>, <c>epc_acc_delete_term</c>,
/// <c>epc_acc_admin_category_tree</c>, <c>epc_acc_save_category</c>,
/// <c>epc_acc_set_category_active</c>, <c>epc_acc_delete_category</c>,
/// <c>epc_acc_seed_categories_from_json</c>, <c>epc_acc_get_category_tree</c>,
/// <c>epc_acc_add_listing</c>, <c>epc_acc_get_listing</c>,
/// <c>epc_acc_update_listing</c>, <c>epc_acc_set_listing_status</c>,
/// <c>epc_acc_delete_listing</c>, <c>epc_acc_admin_search</c>,
/// <c>epc_acc_marketplace_search</c>.
/// Path: <c>content/shop/docpart/epc_accessories_db.php</c>.
/// GET never mints a session cookie. Taxonomy parent stays injected.
/// Do not write leftover finance CRM helpers or leftover demand intelligence.
/// </summary>
public static class PhpPlanQ1Foam
{
    public const string AccessoriesDbPath = "content/shop/docpart/epc_accessories_db.php";

    private static readonly Regex SlugJunk = new(@"[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex TermType = new(@"[^a-z_]", RegexOptions.Compiled);
    private static readonly string[] AllowedExt = ["jpg", "jpeg", "png", "gif", "webp"];
    private static readonly string[] ConditionValues = ["new", "used", "refurbished"];
    private static readonly string[] SeedTermTypes = ["make", "city", "condition", "year"];

    public static Func<string>? TaxonomyJsonPath { get; set; }
    public static Func<Dictionary<string, object?>>? LoadTaxonomyJson { get; set; }
    public static Func<int>? Clock { get; set; }
    public static string DocumentRoot { get; set; } = "";
    public static Func<string, bool>? IsUploadedFile { get; set; }
    public static Func<string, int?>? ExifImageType { get; set; }
    public static Func<string>? RandomHex8 { get; set; }

    private static Dictionary<string, object?>? MigrateDone;

    public static void Reset()
    {
        TaxonomyJsonPath = null;
        LoadTaxonomyJson = null;
        Clock = null;
        DocumentRoot = "";
        IsUploadedFile = null;
        ExifImageType = null;
        RandomHex8 = null;
        MigrateDone = null;
    }

    public static string EpcAccTaxonomyJsonPath()
    {
        if (TaxonomyJsonPath != null)
        {
            return TaxonomyJsonPath();
        }

        return DocumentRoot + "/content/general_pages/epc_pakwheels_accessories_taxonomy.json";
    }

    public static Dictionary<string, object?> EpcAccLoadTaxonomyJson()
    {
        if (LoadTaxonomyJson != null)
        {
            return LoadTaxonomyJson();
        }

        var path = EpcAccTaxonomyJsonPath();
        if (!File.Exists(path))
        {
            return EmptyTax();
        }

        try
        {
            var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllText(path));
            return data ?? EmptyTax();
        }
        catch
        {
            return EmptyTax();
        }
    }

    public static void EpcAccEnsureSchema(MySqlConnection db)
    {
        Exec(db, """
            CREATE TABLE IF NOT EXISTS `epc_acc_categories` (
                `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
                `parent_id` INT UNSIGNED NOT NULL DEFAULT 0,
                `slug` VARCHAR(120) NOT NULL,
                `label` VARCHAR(190) NOT NULL,
                `pw_id` INT UNSIGNED NOT NULL DEFAULT 0,
                `sort_order` INT NOT NULL DEFAULT 0,
                `active` TINYINT(1) NOT NULL DEFAULT 1,
                PRIMARY KEY (`id`),
                UNIQUE KEY `slug_parent` (`slug`, `parent_id`),
                KEY `parent_id` (`parent_id`),
                KEY `active` (`active`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);
        Exec(db, """
            CREATE TABLE IF NOT EXISTS `epc_acc_listings` (
                `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
                `category_id` INT UNSIGNED NOT NULL DEFAULT 0,
                `subcategory_id` INT UNSIGNED NOT NULL DEFAULT 0,
                `title` VARCHAR(255) NOT NULL,
                `description` TEXT NULL,
                `make` VARCHAR(120) NOT NULL DEFAULT '',
                `model` VARCHAR(120) NOT NULL DEFAULT '',
                `year` VARCHAR(16) NOT NULL DEFAULT '',
                `city` VARCHAR(120) NOT NULL DEFAULT '',
                `condition_type` VARCHAR(32) NOT NULL DEFAULT 'new',
                `price` DECIMAL(12,2) NOT NULL DEFAULT 0,
                `compare_price` DECIMAL(12,2) NOT NULL DEFAULT 0,
                `currency` VARCHAR(8) NOT NULL DEFAULT 'AED',
                `image_url` VARCHAR(500) NOT NULL DEFAULT '',
                `external_url` VARCHAR(500) NOT NULL DEFAULT '',
                `photo_count` INT NOT NULL DEFAULT 1,
                `featured` TINYINT(1) NOT NULL DEFAULT 0,
                `stock_qty` INT NOT NULL DEFAULT 0,
                `status` VARCHAR(32) NOT NULL DEFAULT 'published',
                `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
                `updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                KEY `category_id` (`category_id`),
                KEY `subcategory_id` (`subcategory_id`),
                KEY `make` (`make`),
                KEY `city` (`city`),
                KEY `status_price` (`status`, `price`),
                KEY `featured` (`featured`),
                KEY `updated_at` (`updated_at`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);
        var cols = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (var row in Query(db, "SHOW COLUMNS FROM `epc_acc_listings`"))
            {
                cols.Add(Str(row.GetValueOrDefault("Field")).ToLowerInvariant());
            }
        }
        catch
        {
            cols.Clear();
        }

        var alters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["year"] = "ADD COLUMN `year` VARCHAR(16) NOT NULL DEFAULT '' AFTER `model`",
            ["compare_price"] = "ADD COLUMN `compare_price` DECIMAL(12,2) NOT NULL DEFAULT 0 AFTER `price`",
            ["photo_count"] = "ADD COLUMN `photo_count` INT NOT NULL DEFAULT 1 AFTER `external_url`",
            ["featured"] = "ADD COLUMN `featured` TINYINT(1) NOT NULL DEFAULT 0 AFTER `photo_count`"
        };
        foreach (var (name, ddl) in alters)
        {
            if (cols.Count == 0 || !cols.Contains(name))
            {
                try { Exec(db, "ALTER TABLE `epc_acc_listings` " + ddl); }
                catch { /* ignore */ }
            }
        }

        try { Exec(db, "ALTER TABLE `epc_acc_listings` MODIFY COLUMN `currency` VARCHAR(8) NOT NULL DEFAULT 'AED'"); }
        catch { /* ignore */ }

        Exec(db, """
            CREATE TABLE IF NOT EXISTS `epc_acc_terms` (
                `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
                `term_type` VARCHAR(32) NOT NULL,
                `parent_id` INT UNSIGNED NOT NULL DEFAULT 0,
                `value` VARCHAR(190) NOT NULL,
                `label` VARCHAR(190) NOT NULL,
                `sort_order` INT NOT NULL DEFAULT 0,
                `active` TINYINT(1) NOT NULL DEFAULT 1,
                PRIMARY KEY (`id`),
                UNIQUE KEY `type_value_parent` (`term_type`, `value`, `parent_id`),
                KEY `term_type_active` (`term_type`, `active`),
                KEY `parent_id` (`parent_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);
        Exec(db, """
            CREATE TABLE IF NOT EXISTS `epc_acc_photos` (
                `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
                `listing_id` INT UNSIGNED NOT NULL,
                `file_name` VARCHAR(255) NOT NULL,
                `sort_order` INT NOT NULL DEFAULT 0,
                `is_primary` TINYINT(1) NOT NULL DEFAULT 0,
                `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                KEY `listing_id` (`listing_id`),
                KEY `listing_primary` (`listing_id`, `is_primary`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);
    }

    public static string EpcAccPhotosFsDir()
    {
        var root = DocumentRoot.TrimEnd('/', '\\');
        var dir = root + "/content/files/images/accessories/";
        if (!Directory.Exists(dir))
        {
            try { Directory.CreateDirectory(dir); }
            catch { /* leftover PHP @mkdir */ }
        }

        return dir;
    }

    public static string EpcAccPhotoPublicUrl(string fileName)
    {
        fileName = Path.GetFileName(fileName.Trim());
        return fileName == "" ? "" : "/content/files/images/accessories/" + Uri.EscapeDataString(fileName);
    }

    public static string EpcAccStorefrontUrl(object? listingOrId, string langHref = "/en")
    {
        langHref = (langHref != "" ? langHref : "/en").TrimEnd('/');
        var id = 0;
        var cat = "";
        var sub = "";
        if (listingOrId is IDictionary<string, object?> row)
        {
            id = ToInt(row.TryGetValue("id", out var v) ? v : 0);
            cat = Str(row.TryGetValue("category_slug", out var cs) ? cs : row.TryGetValue("category", out var c) ? c : "").Trim();
            sub = Str(row.TryGetValue("subcategory_slug", out var ss) ? ss : row.TryGetValue("subcategory", out var s) ? s : "").Trim();
        }
        else
        {
            id = ToInt(listingOrId);
        }

        var qs = new List<string>();
        if (id > 0)
        {
            qs.Add("id=" + id.ToString(CultureInfo.InvariantCulture));
        }

        if (cat != "")
        {
            qs.Add("category=" + Uri.EscapeDataString(cat).Replace("%20", "+", StringComparison.Ordinal));
        }

        if (sub != "")
        {
            qs.Add("subcategory=" + Uri.EscapeDataString(sub).Replace("%20", "+", StringComparison.Ordinal));
        }

        var path = langHref + "/accessories-spare-parts";
        return qs.Count == 0 ? path : path + "?" + string.Join('&', qs);
    }

    public static bool EpcAccIsOutboundExternalUrl(string url)
    {
        url = url.Trim();
        if (url == "")
        {
            return false;
        }

        // Leftover PHP uses # delimiters and [/?#], so preg_match warns and never matches.
        // Same-site accessories URLs therefore fall through to https / relative-path checks.
        if (Regex.IsMatch(url, @"^https?://", RegexOptions.IgnoreCase))
        {
            return true;
        }

        return url.Length > 0 && url[0] == '/';
    }

    public static List<Dictionary<string, object?>> EpcAccPhotosList(MySqlConnection db, int listingId)
    {
        if (listingId <= 0)
        {
            return [];
        }

        EpcAccEnsureSchema(db);
        var outRows = new List<Dictionary<string, object?>>();
        foreach (var row in Query(db,
                     "SELECT `id`, `listing_id`, `file_name`, `sort_order`, `is_primary`, `created_at` FROM `epc_acc_photos` WHERE `listing_id` = ? ORDER BY `is_primary` DESC, `sort_order` ASC, `id` ASC",
                     listingId))
        {
            var file = Str(row.GetValueOrDefault("file_name"));
            outRows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = ToInt(row.GetValueOrDefault("id")),
                ["listing_id"] = ToInt(row.GetValueOrDefault("listing_id")),
                ["file_name"] = file,
                ["url"] = EpcAccPhotoPublicUrl(file),
                ["sort_order"] = ToInt(row.GetValueOrDefault("sort_order")),
                ["is_primary"] = PhpTruthy(row.GetValueOrDefault("is_primary")),
                ["created_at"] = ToInt(row.GetValueOrDefault("created_at"))
            });
        }

        return outRows;
    }

    public static void EpcAccPhotosSyncListing(MySqlConnection db, int listingId)
    {
        if (listingId <= 0)
        {
            return;
        }

        var photos = EpcAccPhotosList(db, listingId);
        var count = photos.Count;
        var primaryUrl = "";
        foreach (var p in photos)
        {
            if (PhpTruthy(p.GetValueOrDefault("is_primary")) && !PhpEmpty(p.GetValueOrDefault("url")))
            {
                primaryUrl = Str(p.GetValueOrDefault("url"));
                break;
            }
        }

        if (primaryUrl == "" && photos.Count > 0)
        {
            primaryUrl = Str(photos[0].GetValueOrDefault("url"));
        }

        try
        {
            if (count > 0 && primaryUrl != "")
            {
                Exec(db, "UPDATE `epc_acc_listings` SET `image_url` = ?, `photo_count` = ?, `updated_at` = ? WHERE `id` = ?",
                    primaryUrl, count, Now(), listingId);
            }
            else
            {
                Exec(db, "UPDATE `epc_acc_listings` SET `photo_count` = 1, `updated_at` = ? WHERE `id` = ?", Now(), listingId);
            }
        }
        catch
        {
            /* ignore */
        }
    }

    public static Dictionary<string, object?> EpcAccPhotosAdd(MySqlConnection db, int listingId, Dictionary<string, object?> file, bool asPrimary = false)
    {
        EpcAccEnsureSchema(db);
        if (listingId <= 0)
        {
            return Fail("Save the listing first, then upload photos.");
        }

        if (ToInt(Scalar(db, "SELECT `id` FROM `epc_acc_listings` WHERE `id` = ? LIMIT 1", listingId)) == 0)
        {
            return Fail("Listing not found");
        }

        var tmp = Str(file.GetValueOrDefault("tmp_name"));
        if (PhpEmpty(tmp) || !(IsUploadedFile?.Invoke(tmp) ?? File.Exists(tmp)))
        {
            return Fail("No upload");
        }

        if (ToInt(file.GetValueOrDefault("error")) != 0)
        {
            return Fail("Upload error");
        }

        var size = ToInt(file.GetValueOrDefault("size"));
        if (size <= 0 || size > 8 * 1024 * 1024)
        {
            return Fail("Image must be under 8 MB");
        }

        var orig = Str(file.GetValueOrDefault("name"));
        if (orig == "")
        {
            orig = "photo.jpg";
        }

        var ext = Path.GetExtension(orig).TrimStart('.').ToLowerInvariant();
        if (!AllowedExt.Contains(ext, StringComparer.Ordinal))
        {
            return Fail("Use JPG, PNG, GIF or WEBP");
        }

        if (ExifImageType != null)
        {
            var type = ExifImageType(tmp);
            var okTypes = new HashSet<int> { 2, 3, 1, 18 };
            if (type is null || !okTypes.Contains(type.Value))
            {
                return Fail("Invalid image file");
            }
        }

        var dir = EpcAccPhotosFsDir();
        if (!Directory.Exists(dir))
        {
            return Fail("Photo folder is not writable");
        }

        try
        {
            if (new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                return Fail("Photo folder is not writable");
            }
        }
        catch
        {
            return Fail("Photo folder is not writable");
        }

        var saved = "acc_" + listingId + "_" + Now() + "_" + (RandomHex8?.Invoke() ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant()) + "." + (ext == "jpeg" ? "jpg" : ext);
        var dest = dir + saved;
        try
        {
            File.Copy(tmp, dest, true);
        }
        catch
        {
            return Fail("Could not save file");
        }

        try { File.SetAttributes(dest, FileAttributes.Normal); } catch { /* ignore */ }

        var sort = ToInt(Scalar(db, "SELECT COALESCE(MAX(`sort_order`),0) FROM `epc_acc_photos` WHERE `listing_id` = ?", listingId)) + 10;
        var cnt = ToInt(Scalar(db, "SELECT COUNT(*) FROM `epc_acc_photos` WHERE `listing_id` = ?", listingId));
        var isPrimary = asPrimary || cnt == 0 ? 1 : 0;
        if (isPrimary == 1)
        {
            Exec(db, "UPDATE `epc_acc_photos` SET `is_primary` = 0 WHERE `listing_id` = ?", listingId);
        }

        Exec(db, "INSERT INTO `epc_acc_photos` (`listing_id`, `file_name`, `sort_order`, `is_primary`, `created_at`) VALUES (?, ?, ?, ?, ?)",
            listingId, saved, sort, isPrimary, Now());
        var id = LastId(db);
        EpcAccPhotosSyncListing(db, listingId);
        var photos = EpcAccPhotosList(db, listingId);
        Dictionary<string, object?>? photo = null;
        foreach (var p in photos)
        {
            if (ToInt(p.GetValueOrDefault("id")) == id)
            {
                photo = p;
                break;
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["id"] = id,
            ["url"] = EpcAccPhotoPublicUrl(saved),
            ["photo"] = photo,
            ["photos"] = photos
        };
    }

    public static Dictionary<string, object?> EpcAccPhotosDelete(MySqlConnection db, int listingId, int photoId)
    {
        EpcAccEnsureSchema(db);
        if (listingId <= 0 || photoId <= 0)
        {
            return Fail("Invalid photo");
        }

        var row = Query(db, "SELECT `id`, `file_name`, `is_primary` FROM `epc_acc_photos` WHERE `id` = ? AND `listing_id` = ? LIMIT 1", photoId, listingId).FirstOrDefault();
        if (row == null)
        {
            return Fail("Photo not found");
        }

        Exec(db, "DELETE FROM `epc_acc_photos` WHERE `id` = ?", photoId);
        var file = Path.GetFileName(Str(row.GetValueOrDefault("file_name")));
        if (file != "")
        {
            var path = EpcAccPhotosFsDir() + file;
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { /* ignore */ }
            }
        }

        if (PhpTruthy(row.GetValueOrDefault("is_primary")))
        {
            var nextId = ToInt(Scalar(db, "SELECT `id` FROM `epc_acc_photos` WHERE `listing_id` = ? ORDER BY `sort_order` ASC, `id` ASC LIMIT 1", listingId));
            if (nextId > 0)
            {
                Exec(db, "UPDATE `epc_acc_photos` SET `is_primary` = 1 WHERE `id` = ?", nextId);
            }
        }

        EpcAccPhotosSyncListing(db, listingId);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["photos"] = EpcAccPhotosList(db, listingId)
        };
    }

    public static Dictionary<string, object?> EpcAccPhotosSetPrimary(MySqlConnection db, int listingId, int photoId)
    {
        EpcAccEnsureSchema(db);
        if (ToInt(Scalar(db, "SELECT `id` FROM `epc_acc_photos` WHERE `id` = ? AND `listing_id` = ? LIMIT 1", photoId, listingId)) == 0)
        {
            return Fail("Photo not found");
        }

        Exec(db, "UPDATE `epc_acc_photos` SET `is_primary` = 0 WHERE `listing_id` = ?", listingId);
        Exec(db, "UPDATE `epc_acc_photos` SET `is_primary` = 1 WHERE `id` = ?", photoId);
        EpcAccPhotosSyncListing(db, listingId);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["photos"] = EpcAccPhotosList(db, listingId)
        };
    }

    public static Dictionary<string, object?> EpcAccPhotosAddManyFromFiles(MySqlConnection db, int listingId, Dictionary<string, object?> filesField)
    {
        var ok = 0;
        var failed = 0;
        var errors = new List<string>();
        if (listingId <= 0 || PhpEmpty(filesField.GetValueOrDefault("name")))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = 0,
                ["failed"] = 0,
                ["errors"] = errors
            };
        }

        var names = filesField.GetValueOrDefault("name");
        if (names is not IEnumerable || names is string)
        {
            var one = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = filesField.GetValueOrDefault("name") ?? "",
                ["type"] = filesField.GetValueOrDefault("type") ?? "",
                ["tmp_name"] = filesField.GetValueOrDefault("tmp_name") ?? "",
                ["error"] = filesField.GetValueOrDefault("error") ?? 4,
                ["size"] = filesField.GetValueOrDefault("size") ?? 0
            };
            var res = EpcAccPhotosAdd(db, listingId, one);
            if (PhpTruthy(res.GetValueOrDefault("ok")))
            {
                ok++;
            }
            else
            {
                failed++;
                errors.Add(Str(res.GetValueOrDefault("error"), "Upload failed"));
            }

            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = ok,
                ["failed"] = failed,
                ["errors"] = errors
            };
        }

        var nameList = EnumList(names).ToList();
        var typeList = EnumList(filesField.GetValueOrDefault("type")).ToList();
        var tmpList = EnumList(filesField.GetValueOrDefault("tmp_name")).ToList();
        var errList = EnumList(filesField.GetValueOrDefault("error")).ToList();
        var sizeList = EnumList(filesField.GetValueOrDefault("size")).ToList();
        for (var i = 0; i < nameList.Count; i++)
        {
            var one = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = i < nameList.Count ? nameList[i] : "",
                ["type"] = i < typeList.Count ? typeList[i] : "",
                ["tmp_name"] = i < tmpList.Count ? tmpList[i] : "",
                ["error"] = i < errList.Count ? errList[i] : 4,
                ["size"] = i < sizeList.Count ? sizeList[i] : 0
            };
            if (ToInt(one["error"]) == 4 || Str(one["tmp_name"]) == "")
            {
                continue;
            }

            var res = EpcAccPhotosAdd(db, listingId, one);
            if (PhpTruthy(res.GetValueOrDefault("ok")))
            {
                ok++;
            }
            else
            {
                failed++;
                errors.Add(Str(res.GetValueOrDefault("error"), "Upload failed"));
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = ok,
            ["failed"] = failed,
            ["errors"] = errors
        };
    }

    public static string EpcAccSlugify(string label)
    {
        var s = label.Trim().ToLowerInvariant();
        s = SlugJunk.Replace(s, "-").Trim('-');
        return s != "" ? s : "item";
    }

    public static List<string> EpcAccUaeCities()
    {
        var tax = EpcAccLoadTaxonomyJson();
        var fromJson = new List<string>();
        foreach (var city in EnumList(tax.GetValueOrDefault("cities")))
        {
            var value = Str(city).Trim();
            if (value != "")
            {
                fromJson.Add(value);
            }
        }

        if (fromJson.Count > 0)
        {
            return fromJson.Distinct(StringComparer.Ordinal).ToList();
        }

        return
        [
            "Dubai", "Abu Dhabi", "Sharjah", "Ajman", "Ras Al Khaimah", "Fujairah", "Umm Al Quwain", "Al Ain"
        ];
    }

    public static List<string> EpcAccLegacyPkCities() =>
    [
        "Karachi", "Lahore", "Okara", "Islamabad", "Sialkot", "Mirpur Khas", "Rawalpindi",
        "Peshawar", "Faisalabad", "Gujranwala", "Multan", "Quetta", "Hyderabad", "Bahawalpur"
    ];

    public static Dictionary<string, object?> EpcAccMigrateUaeLocale(MySqlConnection db)
    {
        if (MigrateDone != null)
        {
            return MigrateDone;
        }

        EpcAccEnsureSchema(db);
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["cities_active"] = 0,
            ["cities_deactivated"] = 0,
            ["listings_city"] = 0,
            ["listings_currency"] = 0,
            ["titles"] = 0
        };
        var uae = EpcAccUaeCities();
        var pk = EpcAccLegacyPkCities();
        var cityMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Karachi"] = "Dubai",
            ["Lahore"] = "Abu Dhabi",
            ["Islamabad"] = "Abu Dhabi",
            ["Rawalpindi"] = "Al Ain",
            ["Peshawar"] = "Sharjah",
            ["Faisalabad"] = "Ajman",
            ["Gujranwala"] = "Ajman",
            ["Multan"] = "Ras Al Khaimah",
            ["Quetta"] = "Fujairah",
            ["Hyderabad"] = "Sharjah",
            ["Bahawalpur"] = "Umm Al Quwain",
            ["Sialkot"] = "Ras Al Khaimah",
            ["Mirpur Khas"] = "Fujairah",
            ["Okara"] = "Al Ain"
        };

        var i = 0;
        foreach (var city in uae)
        {
            i++;
            Exec(db, "INSERT INTO `epc_acc_terms` (`term_type`, `parent_id`, `value`, `label`, `sort_order`, `active`) VALUES ('city', 0, ?, ?, ?, 1) ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `sort_order` = VALUES(`sort_order`), `active` = 1",
                city, city, i);
            result["cities_active"] = ToInt(result["cities_active"]) + 1;
        }

        var uaeLookup = uae.ToHashSet(StringComparer.Ordinal);
        foreach (var row in Query(db, "SELECT `id`, `value` FROM `epc_acc_terms` WHERE `term_type` = 'city' AND `active` = 1"))
        {
            var val = Str(row.GetValueOrDefault("value")).Trim();
            if (val != "" && !uaeLookup.Contains(val))
            {
                Exec(db, "UPDATE `epc_acc_terms` SET `active` = 0 WHERE `id` = ?", ToInt(row.GetValueOrDefault("id")));
                result["cities_deactivated"] = ToInt(result["cities_deactivated"]) + 1;
            }
        }

        foreach (var oldCity in pk)
        {
            Exec(db, "UPDATE `epc_acc_terms` SET `active` = 0 WHERE `term_type` = 'city' AND `value` = ?", oldCity);
        }

        var now = Now();
        foreach (var (from, to) in cityMap)
        {
            result["listings_city"] = ToInt(result["listings_city"]) + Exec(db, "UPDATE `epc_acc_listings` SET `city` = ?, `updated_at` = ? WHERE `city` = ?", to, now, from);
        }

        foreach (var row in Query(db, "SELECT DISTINCT `city` FROM `epc_acc_listings` WHERE TRIM(`city`) <> ''"))
        {
            var city = Str(row.GetValueOrDefault("city")).Trim();
            if (city == "" || uaeLookup.Contains(city))
            {
                continue;
            }

            result["listings_city"] = ToInt(result["listings_city"]) + Exec(db, "UPDATE `epc_acc_listings` SET `city` = ?, `updated_at` = ? WHERE `city` = ?", "Dubai", now, city);
        }

        foreach (var (from, to) in cityMap)
        {
            var like = "%| " + from + "%";
            foreach (var row in Query(db, "SELECT `id`, `title` FROM `epc_acc_listings` WHERE `title` LIKE ?", like))
            {
                var title = Str(row.GetValueOrDefault("title"));
                var next = title.Replace("| " + from, "| " + to, StringComparison.Ordinal).Replace(from, to, StringComparison.Ordinal);
                if (next != title)
                {
                    Exec(db, "UPDATE `epc_acc_listings` SET `title` = ?, `updated_at` = ? WHERE `id` = ?", next, now, ToInt(row.GetValueOrDefault("id")));
                    result["titles"] = ToInt(result["titles"]) + 1;
                }
            }
        }

        result["listings_currency"] = Exec(db, "UPDATE `epc_acc_listings` SET `currency` = 'AED', `updated_at` = ? WHERE `currency` <> 'AED' OR `currency` = '' OR `currency` IS NULL", now);
        MigrateDone = result;
        return result;
    }

    public static Dictionary<string, object?> EpcAccSeedTermsFromJson(MySqlConnection db)
    {
        EpcAccEnsureSchema(db);
        var tax = EpcAccLoadTaxonomyJson();
        var counts = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["makes"] = 0,
            ["cities"] = 0,
            ["conditions"] = 0,
            ["years"] = 0
        };
        var i = 0;
        foreach (var makeObj in EnumList(tax.GetValueOrDefault("makes")))
        {
            var make = Str(makeObj).Trim();
            if (make == "")
            {
                continue;
            }

            i++;
            Exec(db, "INSERT INTO `epc_acc_terms` (`term_type`, `parent_id`, `value`, `label`, `sort_order`, `active`) VALUES (?, 0, ?, ?, ?, 1) ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `sort_order` = VALUES(`sort_order`)",
                "make", make, make, i);
            counts["makes"] = ToInt(counts["makes"]) + 1;
        }

        i = 0;
        foreach (var cityObj in EnumList(tax.GetValueOrDefault("cities")))
        {
            var city = Str(cityObj).Trim();
            if (city == "")
            {
                continue;
            }

            i++;
            Exec(db, "INSERT INTO `epc_acc_terms` (`term_type`, `parent_id`, `value`, `label`, `sort_order`, `active`) VALUES (?, 0, ?, ?, ?, 1) ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `sort_order` = VALUES(`sort_order`)",
                "city", city, city, i);
            counts["cities"] = ToInt(counts["cities"]) + 1;
        }

        i = 0;
        foreach (var (label, value) in new[] { ("New", "new"), ("Used", "used") })
        {
            i++;
            Exec(db, "INSERT INTO `epc_acc_terms` (`term_type`, `parent_id`, `value`, `label`, `sort_order`, `active`) VALUES (?, 0, ?, ?, ?, 1) ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `sort_order` = VALUES(`sort_order`)",
                "condition", value, label, i);
            counts["conditions"] = ToInt(counts["conditions"]) + 1;
        }

        var yearNow = YearNow();
        i = 0;
        for (var y = yearNow; y >= yearNow - 30; y--)
        {
            i++;
            Exec(db, "INSERT INTO `epc_acc_terms` (`term_type`, `parent_id`, `value`, `label`, `sort_order`, `active`) VALUES (?, 0, ?, ?, ?, 1) ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `sort_order` = VALUES(`sort_order`)",
                "year", y.ToString(CultureInfo.InvariantCulture), y.ToString(CultureInfo.InvariantCulture), i);
            counts["years"] = ToInt(counts["years"]) + 1;
        }

        return counts;
    }

    public static List<Dictionary<string, object?>> EpcAccGetTerms(MySqlConnection db, string type, bool includeInactive = false, int parentId = -1)
    {
        EpcAccEnsureSchema(db);
        type = TermType.Replace(type.ToLowerInvariant(), "");
        var sql = "SELECT `id`, `term_type`, `parent_id`, `value`, `label`, `sort_order`, `active` FROM `epc_acc_terms` WHERE `term_type` = ?";
        var bind = new List<object?> { type };
        if (!includeInactive)
        {
            sql += " AND `active` = 1";
        }

        if (parentId >= 0)
        {
            sql += " AND `parent_id` = ?";
            bind.Add(parentId);
        }

        sql += " ORDER BY `sort_order` ASC, `label` ASC";
        var rows = Query(db, sql, bind.ToArray());
        if (rows.Count == 0 && SeedTermTypes.Contains(type, StringComparer.Ordinal))
        {
            EpcAccSeedTermsFromJson(db);
            rows = Query(db, sql, bind.ToArray());
        }

        return rows.Select(row => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = ToInt(row.GetValueOrDefault("id")),
            ["term_type"] = Str(row.GetValueOrDefault("term_type")),
            ["parent_id"] = ToInt(row.GetValueOrDefault("parent_id")),
            ["value"] = Str(row.GetValueOrDefault("value")),
            ["label"] = Str(row.GetValueOrDefault("label")),
            ["sort_order"] = ToInt(row.GetValueOrDefault("sort_order")),
            ["active"] = ToInt(row.GetValueOrDefault("active"))
        }).ToList();
    }

    public static List<string> EpcAccTermLabels(MySqlConnection db, string type)
        => EpcAccGetTerms(db, type).Select(term => Str(term.GetValueOrDefault("label"))).ToList();

    public static int EpcAccSaveTerm(MySqlConnection db, string type, string label, int id = 0, int parentId = 0, int sortOrder = 0)
    {
        EpcAccEnsureSchema(db);
        type = TermType.Replace(type.ToLowerInvariant(), "");
        label = label.Trim();
        if (label == "" || type == "")
        {
            return 0;
        }

        var value = type == "condition" ? EpcAccSlugify(label) : label;
        if (type == "condition" && !ConditionValues.Contains(value, StringComparer.Ordinal))
        {
            value = EpcAccSlugify(label);
        }

        if (sortOrder <= 0)
        {
            sortOrder = ToInt(Scalar(db, "SELECT COALESCE(MAX(`sort_order`), 0) + 1 FROM `epc_acc_terms` WHERE `term_type` = ? AND `parent_id` = ?", type, parentId));
        }

        if (id > 0)
        {
            Exec(db, "UPDATE `epc_acc_terms` SET `label` = ?, `value` = ?, `parent_id` = ?, `sort_order` = ?, `active` = 1 WHERE `id` = ? AND `term_type` = ?",
                label, value, parentId, sortOrder, id, type);
            return id;
        }

        Exec(db, "INSERT INTO `epc_acc_terms` (`term_type`, `parent_id`, `value`, `label`, `sort_order`, `active`) VALUES (?, ?, ?, ?, ?, 1) ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `sort_order` = VALUES(`sort_order`), `active` = 1, `id` = LAST_INSERT_ID(`id`)",
            type, parentId, value, label, sortOrder);
        return LastId(db);
    }

    public static bool EpcAccSetTermActive(MySqlConnection db, int id, bool active)
    {
        EpcAccEnsureSchema(db);
        return Exec(db, "UPDATE `epc_acc_terms` SET `active` = ? WHERE `id` = ?", active ? 1 : 0, id) > 0;
    }

    public static bool EpcAccDeleteTerm(MySqlConnection db, int id)
    {
        EpcAccEnsureSchema(db);
        Exec(db, "DELETE FROM `epc_acc_terms` WHERE `parent_id` = ?", id);
        return Exec(db, "DELETE FROM `epc_acc_terms` WHERE `id` = ?", id) > 0;
    }

    public static List<Dictionary<string, object?>> EpcAccAdminCategoryTree(MySqlConnection db)
    {
        EpcAccEnsureSchema(db);
        var rows = Query(db, "SELECT `id`, `parent_id`, `slug`, `label`, `pw_id`, `sort_order`, `active` FROM `epc_acc_categories` ORDER BY `parent_id` ASC, `sort_order` ASC, `label` ASC");
        if (rows.Count == 0)
        {
            EpcAccSeedCategoriesFromJson(db);
            rows = Query(db, "SELECT `id`, `parent_id`, `slug`, `label`, `pw_id`, `sort_order`, `active` FROM `epc_acc_categories` ORDER BY `parent_id` ASC, `sort_order` ASC, `label` ASC");
        }

        return BuildAdminTree(rows);
    }

    public static int EpcAccSaveCategory(MySqlConnection db, string label, int parentId = 0, int id = 0, int sortOrder = 0)
    {
        EpcAccEnsureSchema(db);
        label = label.Trim();
        if (label == "")
        {
            return 0;
        }

        var slug = EpcAccSlugify(label);
        if (sortOrder <= 0)
        {
            sortOrder = ToInt(Scalar(db, "SELECT COALESCE(MAX(`sort_order`), 0) + 1 FROM `epc_acc_categories` WHERE `parent_id` = ?", parentId));
        }

        if (id > 0)
        {
            Exec(db, "UPDATE `epc_acc_categories` SET `label` = ?, `slug` = ?, `parent_id` = ?, `sort_order` = ?, `active` = 1 WHERE `id` = ?",
                label, slug, parentId, sortOrder, id);
            return id;
        }

        var baseSlug = slug;
        var n = 1;
        while (true)
        {
            var existing = ToInt(Scalar(db, "SELECT `id` FROM `epc_acc_categories` WHERE `parent_id` = ? AND `slug` = ? LIMIT 1", parentId, slug));
            if (existing < 1)
            {
                break;
            }

            n++;
            slug = baseSlug + "-" + n.ToString(CultureInfo.InvariantCulture);
        }

        Exec(db, "INSERT INTO `epc_acc_categories` (`parent_id`, `slug`, `label`, `pw_id`, `sort_order`, `active`) VALUES (?, ?, ?, 0, ?, 1)",
            parentId, slug, label, sortOrder);
        return LastId(db);
    }

    public static bool EpcAccSetCategoryActive(MySqlConnection db, int id, bool active)
    {
        EpcAccEnsureSchema(db);
        Exec(db, "UPDATE `epc_acc_categories` SET `active` = ? WHERE `id` = ?", active ? 1 : 0, id);
        if (!active)
        {
            Exec(db, "UPDATE `epc_acc_categories` SET `active` = 0 WHERE `parent_id` = ?", id);
        }

        return true;
    }

    public static Dictionary<string, object?> EpcAccDeleteCategory(MySqlConnection db, int id)
    {
        EpcAccEnsureSchema(db);
        if (id < 1)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Invalid category" };
        }

        if (ToInt(Scalar(db, "SELECT COUNT(*) FROM `epc_acc_listings` WHERE `category_id` = ? OR `subcategory_id` = ?", id, id)) > 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Category has listings — deactivate instead of delete" };
        }

        if (ToInt(Scalar(db, "SELECT COUNT(*) FROM `epc_acc_listings` l INNER JOIN `epc_acc_categories` c ON c.id = l.subcategory_id WHERE c.parent_id = ?", id)) > 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Sub-categories have listings — deactivate instead" };
        }

        Exec(db, "DELETE FROM `epc_acc_categories` WHERE `parent_id` = ?", id);
        Exec(db, "DELETE FROM `epc_acc_categories` WHERE `id` = ?", id);
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "Category deleted" };
    }

    public static Dictionary<string, object?> EpcAccSeedCategoriesFromJson(MySqlConnection db, bool reset = false)
    {
        EpcAccEnsureSchema(db);
        if (reset)
        {
            Exec(db, "DELETE FROM `epc_acc_listings`");
            Exec(db, "DELETE FROM `epc_acc_categories`");
        }

        var tax = EpcAccLoadTaxonomyJson();
        var parents = EnumList(tax.GetValueOrDefault("categories")).ToList();
        var parentCount = 0;
        var childCount = 0;
        var order = 0;
        foreach (var parentObj in parents)
        {
            order++;
            var parent = AsDict(parentObj);
            if (parent == null)
            {
                continue;
            }

            var pslug = Str(parent.GetValueOrDefault("slug")).Trim();
            var plabel = Str(parent.TryGetValue("label", out var lb) ? lb : pslug).Trim();
            if (pslug == "")
            {
                continue;
            }

            Exec(db, "INSERT INTO `epc_acc_categories` (`parent_id`, `slug`, `label`, `pw_id`, `sort_order`, `active`) VALUES (?, ?, ?, ?, ?, 1) ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `pw_id` = VALUES(`pw_id`), `sort_order` = VALUES(`sort_order`), `active` = 1",
                0, pslug, plabel, ToInt(parent.GetValueOrDefault("pw_id")), order);
            parentCount++;
            var parentId = ToInt(Scalar(db, "SELECT `id` FROM `epc_acc_categories` WHERE `parent_id` = 0 AND `slug` = ? LIMIT 1", pslug));
            var cOrder = 0;
            foreach (var childObj in EnumList(parent.GetValueOrDefault("children")))
            {
                cOrder++;
                var child = AsDict(childObj);
                if (child == null)
                {
                    continue;
                }

                var cslug = Str(child.GetValueOrDefault("slug")).Trim();
                var clabel = Str(child.TryGetValue("label", out var cl) ? cl : cslug).Trim();
                if (cslug == "" || parentId < 1)
                {
                    continue;
                }

                Exec(db, "INSERT INTO `epc_acc_categories` (`parent_id`, `slug`, `label`, `pw_id`, `sort_order`, `active`) VALUES (?, ?, ?, ?, ?, 1) ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `pw_id` = VALUES(`pw_id`), `sort_order` = VALUES(`sort_order`), `active` = 1",
                    parentId, cslug, clabel, ToInt(child.GetValueOrDefault("pw_id")), cOrder);
                childCount++;
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["parents"] = parentCount, ["children"] = childCount };
    }

    public static List<Dictionary<string, object?>> EpcAccGetCategoryTree(MySqlConnection db)
    {
        EpcAccEnsureSchema(db);
        var rows = Query(db, "SELECT `id`, `parent_id`, `slug`, `label`, `pw_id`, `sort_order` FROM `epc_acc_categories` WHERE `active` = 1 ORDER BY `parent_id` ASC, `sort_order` ASC, `label` ASC");
        if (rows.Count == 0)
        {
            EpcAccSeedCategoriesFromJson(db);
            rows = Query(db, "SELECT `id`, `parent_id`, `slug`, `label`, `pw_id`, `sort_order` FROM `epc_acc_categories` WHERE `active` = 1 ORDER BY `parent_id` ASC, `sort_order` ASC, `label` ASC");
        }

        var parents = new Dictionary<int, Dictionary<string, object?>>();
        var children = new List<Dictionary<string, object?>>();
        foreach (var row in rows)
        {
            var pid = ToInt(row.GetValueOrDefault("parent_id"));
            if (pid == 0)
            {
                parents[ToInt(row.GetValueOrDefault("id"))] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = ToInt(row.GetValueOrDefault("id")),
                    ["slug"] = row.GetValueOrDefault("slug"),
                    ["label"] = row.GetValueOrDefault("label"),
                    ["pw_id"] = ToInt(row.GetValueOrDefault("pw_id")),
                    ["children"] = new List<Dictionary<string, object?>>(),
                    ["count"] = 0
                };
            }
            else
            {
                children.Add(row);
            }
        }

        foreach (var row in children)
        {
            var pid = ToInt(row.GetValueOrDefault("parent_id"));
            if (!parents.TryGetValue(pid, out var parent))
            {
                continue;
            }

            ((List<Dictionary<string, object?>>)parent["children"]!).Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = ToInt(row.GetValueOrDefault("id")),
                ["slug"] = row.GetValueOrDefault("slug"),
                ["label"] = row.GetValueOrDefault("label"),
                ["pw_id"] = ToInt(row.GetValueOrDefault("pw_id")),
                ["count"] = 0
            });
        }

        return parents.Values.ToList();
    }

    public static int EpcAccAddListing(MySqlConnection db, Dictionary<string, object?> data)
    {
        EpcAccEnsureSchema(db);
        var now = Now();
        var currency = Str(data.GetValueOrDefault("currency"), "AED").Trim();
        if (currency == "")
        {
            currency = "AED";
        }

        var status = Str(data.GetValueOrDefault("status"), "published").Trim();
        if (status == "")
        {
            status = "published";
        }

        Exec(db, """
            INSERT INTO `epc_acc_listings`
            (`category_id`, `subcategory_id`, `title`, `description`, `make`, `model`, `year`, `city`, `condition_type`,
             `price`, `compare_price`, `currency`, `image_url`, `external_url`, `photo_count`, `featured`,
             `stock_qty`, `status`, `created_at`, `updated_at`)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            ToInt(data.GetValueOrDefault("category_id")),
            ToInt(data.GetValueOrDefault("subcategory_id")),
            Str(data.GetValueOrDefault("title")).Trim(),
            Str(data.GetValueOrDefault("description")).Trim(),
            Str(data.GetValueOrDefault("make")).Trim(),
            Str(data.GetValueOrDefault("model")).Trim(),
            Str(data.GetValueOrDefault("year")).Trim(),
            Str(data.GetValueOrDefault("city")).Trim(),
            Str(data.GetValueOrDefault("condition_type"), "new").Trim(),
            ToDouble(data.GetValueOrDefault("price")),
            ToDouble(data.GetValueOrDefault("compare_price")),
            currency,
            Str(data.GetValueOrDefault("image_url")).Trim(),
            Str(data.GetValueOrDefault("external_url")).Trim(),
            Math.Max(1, ToInt(data.GetValueOrDefault("photo_count"), 1)),
            PhpTruthy(data.GetValueOrDefault("featured")) ? 1 : 0,
            ToInt(data.GetValueOrDefault("stock_qty")),
            status,
            now,
            now);
        return LastId(db);
    }

    public static Dictionary<string, object?>? EpcAccGetListing(MySqlConnection db, int id)
    {
        EpcAccEnsureSchema(db);
        return Query(db, """
            SELECT l.*, c.slug AS category_slug, c.label AS category_label,
                s.slug AS subcategory_slug, s.label AS subcategory_label
            FROM `epc_acc_listings` l
            LEFT JOIN `epc_acc_categories` c ON c.id = l.category_id
            LEFT JOIN `epc_acc_categories` s ON s.id = l.subcategory_id
            WHERE l.id = ? LIMIT 1
            """, id).FirstOrDefault();
    }

    public static bool EpcAccUpdateListing(MySqlConnection db, int id, Dictionary<string, object?> data)
    {
        EpcAccEnsureSchema(db);
        var currency = Str(data.GetValueOrDefault("currency"), "AED").Trim();
        if (currency == "")
        {
            currency = "AED";
        }

        var status = Str(data.GetValueOrDefault("status"), "published").Trim();
        if (status == "")
        {
            status = "published";
        }

        Exec(db, """
            UPDATE `epc_acc_listings` SET
                `category_id` = ?, `subcategory_id` = ?, `title` = ?, `description` = ?,
                `make` = ?, `model` = ?, `year` = ?, `city` = ?, `condition_type` = ?,
                `price` = ?, `compare_price` = ?, `currency` = ?, `image_url` = ?, `external_url` = ?,
                `photo_count` = ?, `featured` = ?, `stock_qty` = ?, `status` = ?, `updated_at` = ?
            WHERE `id` = ?
            """,
            ToInt(data.GetValueOrDefault("category_id")),
            ToInt(data.GetValueOrDefault("subcategory_id")),
            Str(data.GetValueOrDefault("title")).Trim(),
            Str(data.GetValueOrDefault("description")).Trim(),
            Str(data.GetValueOrDefault("make")).Trim(),
            Str(data.GetValueOrDefault("model")).Trim(),
            Str(data.GetValueOrDefault("year")).Trim(),
            Str(data.GetValueOrDefault("city")).Trim(),
            Str(data.GetValueOrDefault("condition_type"), "new").Trim(),
            ToDouble(data.GetValueOrDefault("price")),
            ToDouble(data.GetValueOrDefault("compare_price")),
            currency,
            Str(data.GetValueOrDefault("image_url")).Trim(),
            Str(data.GetValueOrDefault("external_url")).Trim(),
            Math.Max(1, ToInt(data.GetValueOrDefault("photo_count"), 1)),
            PhpTruthy(data.GetValueOrDefault("featured")) ? 1 : 0,
            ToInt(data.GetValueOrDefault("stock_qty")),
            status,
            Now(),
            id);
        return true;
    }

    public static bool EpcAccSetListingStatus(MySqlConnection db, int id, string status)
    {
        EpcAccEnsureSchema(db);
        status = status.Trim();
        if (status == "")
        {
            status = "draft";
        }

        Exec(db, "UPDATE `epc_acc_listings` SET `status` = ?, `updated_at` = ? WHERE `id` = ?", status, Now(), id);
        return true;
    }

    public static bool EpcAccDeleteListing(MySqlConnection db, int id)
    {
        EpcAccEnsureSchema(db);
        if (id > 0)
        {
            try
            {
                foreach (var p in EpcAccPhotosList(db, id))
                {
                    var file = Path.GetFileName(Str(p.GetValueOrDefault("file_name")));
                    if (file != "")
                    {
                        var path = EpcAccPhotosFsDir() + file;
                        if (File.Exists(path))
                        {
                            try { File.Delete(path); } catch { /* ignore */ }
                        }
                    }
                }

                Exec(db, "DELETE FROM `epc_acc_photos` WHERE `listing_id` = ?", id);
            }
            catch
            {
                /* ignore */
            }
        }

        Exec(db, "DELETE FROM `epc_acc_listings` WHERE `id` = ?", id);
        return true;
    }

    public static Dictionary<string, object?> EpcAccAdminSearch(MySqlConnection db, Dictionary<string, object?>? filters = null)
    {
        EpcAccEnsureSchema(db);
        filters ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var tree = EpcAccGetCategoryTree(db);
        var q = Str(filters.GetValueOrDefault("q")).Trim();
        var category = Str(filters.GetValueOrDefault("category")).Trim();
        var subcategory = Str(filters.GetValueOrDefault("subcategory")).Trim();
        var status = Str(filters.GetValueOrDefault("status")).Trim();
        var make = Str(filters.GetValueOrDefault("make")).Trim();
        var page = Math.Max(1, ToInt(filters.GetValueOrDefault("page"), 1));
        var perPage = Math.Max(10, Math.Min(100, ToInt(filters.GetValueOrDefault("per_page"), 50)));
        ResolveTreeIds(tree, category, subcategory, out var categoryId, out var subcategoryId);

        var where = new List<string> { "1=1" };
        var bind = new List<object?>();
        if (categoryId > 0)
        {
            where.Add("l.`category_id` = ?");
            bind.Add(categoryId);
        }

        if (subcategoryId > 0)
        {
            where.Add("l.`subcategory_id` = ?");
            bind.Add(subcategoryId);
        }

        if (status != "")
        {
            where.Add("l.`status` = ?");
            bind.Add(status);
        }

        if (make != "")
        {
            where.Add("l.`make` = ?");
            bind.Add(make);
        }

        if (q != "")
        {
            where.Add("(l.`title` LIKE ? OR l.`make` LIKE ? OR l.`model` LIKE ? OR l.`city` LIKE ? OR l.`id` = ?)");
            var like = "%" + q + "%";
            bind.Add(like);
            bind.Add(like);
            bind.Add(like);
            bind.Add(like);
            bind.Add(ToInt(q));
        }

        var whereSql = string.Join(" AND ", where);
        var total = ToInt(Scalar(db, "SELECT COUNT(*) FROM `epc_acc_listings` l WHERE " + whereSql, bind.ToArray()));
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)perPage));
        if (page > pages)
        {
            page = pages;
        }

        var offset = (page - 1) * perPage;
        var items = Query(db, """
            SELECT l.*, c.label AS category_label, c.slug AS category_slug,
                s.label AS subcategory_label, s.slug AS subcategory_slug
            FROM `epc_acc_listings` l
            LEFT JOIN `epc_acc_categories` c ON c.id = l.category_id
            LEFT JOIN `epc_acc_categories` s ON s.id = l.subcategory_id
            WHERE 
            """ + whereSql + " ORDER BY l.`updated_at` DESC, l.`id` DESC LIMIT " + perPage + " OFFSET " + offset, bind.ToArray());
        var statusCounts = new Dictionary<string, object?>(StringComparer.Ordinal);
        try
        {
            foreach (var row in Query(db, "SELECT `status`, COUNT(*) AS cnt FROM `epc_acc_listings` GROUP BY `status`"))
            {
                statusCounts[Str(row.GetValueOrDefault("status"))] = ToInt(row.GetValueOrDefault("cnt"));
            }
        }
        catch
        {
            statusCounts.Clear();
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["total"] = total,
            ["page"] = page,
            ["pages"] = pages,
            ["per_page"] = perPage,
            ["items"] = items,
            ["tree"] = tree,
            ["status_counts"] = statusCounts
        };
    }

    public static Dictionary<string, object?> EpcAccMarketplaceSearch(MySqlConnection db, Dictionary<string, object?>? filters = null)
    {
        EpcAccEnsureSchema(db);
        filters ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var tree = EpcAccGetCategoryTree(db);
        var tax = EpcAccLoadTaxonomyJson();
        var q = Str(filters.GetValueOrDefault("q")).Trim();
        var category = Str(filters.GetValueOrDefault("category")).Trim();
        var subcategory = Str(filters.GetValueOrDefault("subcategory")).Trim();
        var make = Str(filters.GetValueOrDefault("make")).Trim();
        var model = Str(filters.GetValueOrDefault("model")).Trim();
        var city = Str(filters.GetValueOrDefault("city")).Trim();
        var condition = Str(filters.GetValueOrDefault("condition")).Trim();
        var priceMin = ToDouble(filters.GetValueOrDefault("price_min"));
        var priceMax = ToDouble(filters.GetValueOrDefault("price_max"));
        var listingId = ToInt(filters.TryGetValue("id", out var idv) ? idv : filters.GetValueOrDefault("listing_id"));
        var sort = Str(filters.GetValueOrDefault("sort"), "updated-desc");
        var page = Math.Max(1, ToInt(filters.GetValueOrDefault("page"), 1));
        var perPage = Math.Max(12, Math.Min(48, ToInt(filters.GetValueOrDefault("per_page"), 24)));
        ResolveTreeIds(tree, category, subcategory, out var categoryId, out var subcategoryId);

        var where = new List<string> { "l.`status` = 'published'" };
        var bind = new List<object?>();
        if (listingId > 0)
        {
            where.Add("l.`id` = ?");
            bind.Add(listingId);
        }

        if (listingId < 1)
        {
            if (categoryId > 0)
            {
                where.Add("l.`category_id` = ?");
                bind.Add(categoryId);
            }

            if (subcategoryId > 0)
            {
                where.Add("l.`subcategory_id` = ?");
                bind.Add(subcategoryId);
            }

            if (make != "")
            {
                where.Add("l.`make` = ?");
                bind.Add(make);
            }

            if (model != "")
            {
                where.Add("l.`model` LIKE ?");
                bind.Add("%" + model + "%");
            }

            if (city != "")
            {
                where.Add("l.`city` = ?");
                bind.Add(city);
            }

            if (condition != "")
            {
                where.Add("l.`condition_type` = ?");
                bind.Add(condition.ToLowerInvariant());
            }

            if (priceMin > 0)
            {
                where.Add("l.`price` >= ?");
                bind.Add(priceMin);
            }

            if (priceMax > 0)
            {
                where.Add("l.`price` <= ?");
                bind.Add(priceMax);
            }

            if (q != "")
            {
                where.Add("(l.`title` LIKE ? OR l.`description` LIKE ? OR l.`make` LIKE ? OR l.`model` LIKE ?)");
                var like = "%" + q + "%";
                bind.Add(like);
                bind.Add(like);
                bind.Add(like);
                bind.Add(like);
            }
        }

        var whereSql = string.Join(" AND ", where);
        var orderSql = sort switch
        {
            "price-asc" => "l.`featured` DESC, l.`price` ASC, l.`updated_at` DESC",
            "price-desc" => "l.`featured` DESC, l.`price` DESC, l.`updated_at` DESC",
            "updated-asc" => "l.`featured` DESC, l.`updated_at` ASC",
            "top-sales" => "l.`featured` DESC, l.`stock_qty` DESC, l.`updated_at` DESC",
            _ => "l.`featured` DESC, l.`updated_at` DESC, l.`id` DESC"
        };
        var total = ToInt(Scalar(db, "SELECT COUNT(*) FROM `epc_acc_listings` l WHERE " + whereSql, bind.ToArray()));
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)perPage));
        if (page > pages)
        {
            page = pages;
        }

        var offset = (page - 1) * perPage;
        var items = new List<Dictionary<string, object?>>();
        foreach (var row in Query(db, """
            SELECT l.*, c.label AS category_label, c.slug AS category_slug,
                s.label AS subcategory_label, s.slug AS subcategory_slug
            FROM `epc_acc_listings` l
            LEFT JOIN `epc_acc_categories` c ON c.id = l.category_id
            LEFT JOIN `epc_acc_categories` s ON s.id = l.subcategory_id
            WHERE 
            """ + whereSql + " ORDER BY " + orderSql + " LIMIT " + perPage + " OFFSET " + offset, bind.ToArray()))
        {
            var lid = ToInt(row.GetValueOrDefault("id"));
            var photos = EpcAccPhotosList(db, lid);
            var photoUrls = new List<Dictionary<string, object?>>();
            foreach (var ph in photos)
            {
                var url = Str(ph.GetValueOrDefault("url")).Trim();
                if (url != "")
                {
                    photoUrls.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["id"] = ToInt(ph.GetValueOrDefault("id")),
                        ["url"] = url,
                        ["is_primary"] = PhpTruthy(ph.GetValueOrDefault("is_primary"))
                    });
                }
            }

            var imageUrl = Str(row.GetValueOrDefault("image_url")).Trim();
            if (imageUrl == "" && photoUrls.Count > 0)
            {
                imageUrl = Str(photoUrls[0].GetValueOrDefault("url"));
                foreach (var pu in photoUrls)
                {
                    if (PhpTruthy(pu.GetValueOrDefault("is_primary")))
                    {
                        imageUrl = Str(pu.GetValueOrDefault("url"));
                        break;
                    }
                }
            }

            var photoCount = Math.Max(photoUrls.Count, Math.Max(ToInt(row.GetValueOrDefault("photo_count")), imageUrl != "" ? 1 : 0));
            var detailUrl = EpcAccStorefrontUrl(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = lid,
                ["category"] = Str(row.GetValueOrDefault("category_slug")),
                ["subcategory"] = Str(row.GetValueOrDefault("subcategory_slug"))
            });
            var rawExternal = Str(row.GetValueOrDefault("external_url")).Trim();
            var outbound = EpcAccIsOutboundExternalUrl(rawExternal) ? rawExternal : "";
            items.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = lid,
                ["title"] = row.GetValueOrDefault("title"),
                ["description"] = row.GetValueOrDefault("description"),
                ["make"] = row.GetValueOrDefault("make"),
                ["model"] = row.GetValueOrDefault("model"),
                ["year"] = row.ContainsKey("year") ? Str(row.GetValueOrDefault("year")) : "",
                ["city"] = row.GetValueOrDefault("city"),
                ["condition"] = row.GetValueOrDefault("condition_type"),
                ["price"] = ToDouble(row.GetValueOrDefault("price")),
                ["compare_price"] = row.ContainsKey("compare_price") ? ToDouble(row.GetValueOrDefault("compare_price")) : 0d,
                ["currency"] = row.GetValueOrDefault("currency"),
                ["image_url"] = imageUrl,
                ["photos"] = photoUrls,
                ["external_url"] = outbound,
                ["detail_url"] = detailUrl,
                ["url"] = detailUrl,
                ["photo_count"] = Math.Max(1, photoCount),
                ["featured"] = PhpTruthy(row.GetValueOrDefault("featured")),
                ["stock_qty"] = ToInt(row.GetValueOrDefault("stock_qty")),
                ["category"] = row.GetValueOrDefault("category_slug"),
                ["category_label"] = Str(row.GetValueOrDefault("category_label")),
                ["subcategory"] = row.GetValueOrDefault("subcategory_slug"),
                ["subcategory_label"] = Str(row.GetValueOrDefault("subcategory_label")),
                ["updated_at"] = ToInt(row.GetValueOrDefault("updated_at"))
            });
        }

        var catCounts = new Dictionary<int, int>();
        var subCounts = new Dictionary<int, int>();
        var makeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var cityCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var fr in Query(db, "SELECT category_id, subcategory_id, make, city, COUNT(*) AS cnt FROM `epc_acc_listings` WHERE `status` = 'published' GROUP BY category_id, subcategory_id, make, city"))
        {
            var cid = ToInt(fr.GetValueOrDefault("category_id"));
            var sid = ToInt(fr.GetValueOrDefault("subcategory_id"));
            var cnt = ToInt(fr.GetValueOrDefault("cnt"));
            if (cid > 0)
            {
                catCounts[cid] = catCounts.GetValueOrDefault(cid) + cnt;
            }

            if (sid > 0)
            {
                subCounts[sid] = subCounts.GetValueOrDefault(sid) + cnt;
            }

            var m = Str(fr.GetValueOrDefault("make")).Trim();
            if (m != "")
            {
                makeCounts[m] = makeCounts.GetValueOrDefault(m) + cnt;
            }

            var c = Str(fr.GetValueOrDefault("city")).Trim();
            if (c != "")
            {
                cityCounts[c] = cityCounts.GetValueOrDefault(c) + cnt;
            }
        }

        var facetCats = new List<Dictionary<string, object?>>();
        foreach (var parent in tree)
        {
            var subs = new List<Dictionary<string, object?>>();
            foreach (var child in (List<Dictionary<string, object?>>)parent["children"]!)
            {
                subs.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = child["id"],
                    ["slug"] = child["slug"],
                    ["label"] = child["label"],
                    ["count"] = subCounts.GetValueOrDefault(ToInt(child["id"]))
                });
            }

            facetCats.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = parent["id"],
                ["slug"] = parent["slug"],
                ["label"] = parent["label"],
                ["count"] = catCounts.GetValueOrDefault(ToInt(parent["id"])),
                ["subs"] = subs
            });
        }

        var makesTax = EpcAccTermLabels(db, "make");
        if (makesTax.Count == 0)
        {
            makesTax = EnumList(tax.GetValueOrDefault("makes")).Select(x => Str(x)).Where(x => x != "").ToList();
        }

        var citiesTax = EpcAccTermLabels(db, "city");
        if (citiesTax.Count == 0)
        {
            citiesTax = EnumList(tax.GetValueOrDefault("cities")).Select(x => Str(x)).Where(x => x != "").ToList();
        }

        var facetMakes = makesTax.Select(mName => new Dictionary<string, object?>(StringComparer.Ordinal) { ["make"] = mName, ["count"] = makeCounts.GetValueOrDefault(mName) }).ToList();
        foreach (var (mName, cnt) in makeCounts)
        {
            if (!makesTax.Contains(mName, StringComparer.Ordinal))
            {
                facetMakes.Add(new Dictionary<string, object?>(StringComparer.Ordinal) { ["make"] = mName, ["count"] = cnt });
            }
        }

        var facetCities = citiesTax.Select(cName => new Dictionary<string, object?>(StringComparer.Ordinal) { ["city"] = cName, ["count"] = cityCounts.GetValueOrDefault(cName) }).ToList();
        foreach (var (cName, cnt) in cityCounts)
        {
            if (!citiesTax.Contains(cName, StringComparer.Ordinal))
            {
                facetCities.Add(new Dictionary<string, object?>(StringComparer.Ordinal) { ["city"] = cName, ["count"] = cnt });
            }
        }

        var facetConditions = EpcAccGetTerms(db, "condition")
            .Select(cond => new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = cond["value"], ["label"] = cond["label"] })
            .ToList();
        if (facetConditions.Count == 0)
        {
            facetConditions =
            [
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = "new", ["label"] = "New" },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = "used", ["label"] = "Used" }
            ];
        }

        var from = total == 0 ? 0 : offset + 1;
        var to = Math.Min(total, offset + items.Count);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["total"] = total,
            ["page"] = page,
            ["per_page"] = perPage,
            ["pages"] = pages,
            ["from"] = from,
            ["to"] = to,
            ["items"] = items,
            ["facets"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["categories"] = facetCats,
                ["makes"] = facetMakes,
                ["cities"] = facetCities,
                ["conditions"] = facetConditions
            },
            ["taxonomy"] = facetCats,
            ["makes"] = makesTax,
            ["cities"] = citiesTax,
            ["sort"] = sort,
            ["source"] = "epc_acc_listings",
            ["empty_catalog"] = total == 0
        };
    }

    private static List<Dictionary<string, object?>> BuildAdminTree(List<Dictionary<string, object?>> rows)
    {
        var parents = new Dictionary<int, Dictionary<string, object?>>();
        var children = new List<Dictionary<string, object?>>();
        foreach (var row in rows)
        {
            if (ToInt(row.GetValueOrDefault("parent_id")) == 0)
            {
                parents[ToInt(row.GetValueOrDefault("id"))] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = ToInt(row.GetValueOrDefault("id")),
                    ["slug"] = row.GetValueOrDefault("slug"),
                    ["label"] = row.GetValueOrDefault("label"),
                    ["pw_id"] = ToInt(row.GetValueOrDefault("pw_id")),
                    ["sort_order"] = ToInt(row.GetValueOrDefault("sort_order")),
                    ["active"] = ToInt(row.GetValueOrDefault("active")),
                    ["children"] = new List<Dictionary<string, object?>>()
                };
            }
            else
            {
                children.Add(row);
            }
        }

        foreach (var row in children)
        {
            var pid = ToInt(row.GetValueOrDefault("parent_id"));
            if (!parents.TryGetValue(pid, out var parent))
            {
                continue;
            }

            ((List<Dictionary<string, object?>>)parent["children"]!).Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = ToInt(row.GetValueOrDefault("id")),
                ["slug"] = row.GetValueOrDefault("slug"),
                ["label"] = row.GetValueOrDefault("label"),
                ["pw_id"] = ToInt(row.GetValueOrDefault("pw_id")),
                ["sort_order"] = ToInt(row.GetValueOrDefault("sort_order")),
                ["active"] = ToInt(row.GetValueOrDefault("active")),
                ["parent_id"] = pid
            });
        }

        return parents.Values.ToList();
    }

    private static void ResolveTreeIds(List<Dictionary<string, object?>> tree, string category, string subcategory, out int categoryId, out int subcategoryId)
    {
        categoryId = 0;
        subcategoryId = 0;
        foreach (var parent in tree)
        {
            if (category != "" && (Str(parent.GetValueOrDefault("slug")) == category || Str(parent.GetValueOrDefault("id")) == category))
            {
                categoryId = ToInt(parent.GetValueOrDefault("id"));
            }

            foreach (var child in (List<Dictionary<string, object?>>)parent["children"]!)
            {
                if (subcategory != "" && (Str(child.GetValueOrDefault("slug")) == subcategory || Str(child.GetValueOrDefault("id")) == subcategory))
                {
                    subcategoryId = ToInt(child.GetValueOrDefault("id"));
                    if (categoryId < 1)
                    {
                        categoryId = ToInt(parent.GetValueOrDefault("id"));
                    }
                }
            }
        }
    }

    private static Dictionary<string, object?> EmptyTax() => new(StringComparer.Ordinal)
    {
        ["categories"] = new List<object?>(),
        ["makes"] = new List<object?>(),
        ["cities"] = new List<object?>(),
        ["filters"] = new List<object?>()
    };

    private static Dictionary<string, object?> Fail(string error) => new(StringComparer.Ordinal) { ["ok"] = false, ["error"] = error };

    private static int Now() => Clock?.Invoke() ?? (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static int YearNow() => Clock != null
        ? DateTimeOffset.FromUnixTimeSeconds(Clock()).UtcDateTime.Year
        : DateTime.UtcNow.Year;

    private static bool PhpTruthy(object? value)
    {
        if (value is null or false or 0 or 0L or 0d or 0m)
        {
            return false;
        }

        if (value is string s)
        {
            return s != "" && s != "0";
        }

        if (value is bool b)
        {
            return b;
        }

        return ToInt(value) != 0;
    }

    private static bool PhpEmpty(object? value)
    {
        if (value is null or false)
        {
            return true;
        }

        if (value is string s)
        {
            return s == "" || s == "0";
        }

        if (value is ICollection c)
        {
            return c.Count == 0;
        }

        return !PhpTruthy(value);
    }

    private static IEnumerable<object?> EnumList(object? value)
    {
        switch (value)
        {
            case null:
            case string:
                if (value is string s && s != "")
                {
                    yield return s;
                }

                yield break;
            case IDictionary<string, object?> dict:
                yield return dict;
                yield break;
            case IEnumerable enumerable:
                foreach (var item in enumerable)
                {
                    if (item != null)
                    {
                        yield return item;
                    }
                }

                yield break;
        }
    }

    private static Dictionary<string, object?>? AsDict(object? value)
    {
        if (value is Dictionary<string, object?> d)
        {
            return d;
        }

        if (value is IDictionary<string, object?> id)
        {
            return new Dictionary<string, object?>(id, StringComparer.Ordinal);
        }

        return null;
    }

    private static string Str(object? value, string fallback = "")
    {
        if (value is null)
        {
            return fallback;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return text ?? fallback;
    }

    private static int ToInt(object? value, int fallback = 0)
    {
        if (value is null or false)
        {
            return fallback;
        }

        if (value is true)
        {
            return 1;
        }

        if (value is int i)
        {
            return i;
        }

        if (value is long l)
        {
            return (int)l;
        }

        if (value is uint u)
        {
            return (int)u;
        }

        if (value is decimal dec)
        {
            return (int)dec;
        }

        if (value is double db)
        {
            return (int)db;
        }

        return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    private static double ToDouble(object? value)
    {
        if (value is null or false)
        {
            return 0;
        }

        if (value is double d)
        {
            return d;
        }

        if (value is float f)
        {
            return f;
        }

        if (value is decimal m)
        {
            return (double)m;
        }

        if (value is int i)
        {
            return i;
        }

        return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static int Exec(MySqlConnection db, string sql, params object?[] args)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = Bind(cmd, sql, args);
        return cmd.ExecuteNonQuery();
    }

    private static object? Scalar(MySqlConnection db, string sql, params object?[] args)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = Bind(cmd, sql, args);
        return cmd.ExecuteScalar();
    }

    private static int LastId(MySqlConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT LAST_INSERT_ID()";
        return ToInt(cmd.ExecuteScalar());
    }

    private static List<Dictionary<string, object?>> Query(MySqlConnection db, string sql, params object?[] args)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = Bind(cmd, sql, args);
        using var reader = cmd.ExecuteReader();
        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string Bind(MySqlCommand cmd, string sql, IReadOnlyList<object?> args)
    {
        var n = 0;
        var chars = new System.Text.StringBuilder(sql.Length + args.Count * 2);
        foreach (var ch in sql)
        {
            if (ch == '?')
            {
                var name = "@p" + n.ToString(CultureInfo.InvariantCulture);
                cmd.Parameters.AddWithValue(name, args[n] ?? DBNull.Value);
                chars.Append(name);
                n++;
            }
            else
            {
                chars.Append(ch);
            }
        }

        return chars.ToString();
    }
}