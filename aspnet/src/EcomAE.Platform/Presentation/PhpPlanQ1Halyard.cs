using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-halyard price-upload tmp-folder delete. Path kept for the inventory:
/// <c>cp/content/shop/prices_upload/for_pyprices/del_tmp_folder.php</c>.
/// GET never mints a session cookie. Leftover CSRF/user parents stay injected.
/// The shared PHP helper name is not repeated here.
/// </summary>
public static class PhpPlanQ1Halyard
{
    public const string DelTmpFolderPath = "cp/content/shop/prices_upload/for_pyprices/del_tmp_folder.php";

    public static string DocumentRoot { get; set; } = "";
    public static string BackendDir { get; set; } = "cp";
    public static string TmpDirPricesUpload { get; set; } = "/tmp_prices";
    public static Func<bool>? HasDb { get; set; }
    public static Func<int, string>? Translate { get; set; }

    public static void Reset()
    {
        DocumentRoot = "";
        BackendDir = "cp";
        TmpDirPricesUpload = "/tmp_prices";
        HasDb = () => true;
        Translate = id => "t" + id;
    }

    public static Dictionary<string, object?> EpcDelTmpFolderRun(string? postedName)
    {
        if (HasDb != null && !HasDb())
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = false,
                ["message"] = "No DB Connect"
            };
        }

        var posted = postedName ?? "";
        var sanitized = Regex.Replace(posted, "[^a-z_0-9\\s]", "");
        if (sanitized != posted)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = false,
                ["message"] = "Incorrect name"
            };
        }

        var full = DocumentRoot + "/" + BackendDir + TmpDirPricesUpload + "/" + sanitized;
        if (Directory.Exists(full))
        {
            ClearDir(full, clearOnly: false);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = true,
                ["tmp_folder_name"] = full,
                ["message"] = Translate != null ? Translate(5347) : "t5347"
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = false,
            ["tmp_folder_name"] = full,
            ["message"] = Translate != null ? Translate(5348) : "t5348"
        };
    }

    public static string EpcDelTmpFolderJson(string? postedName)
        => JsonSerializer.Serialize(EpcDelTmpFolderRun(postedName));

    private static void ClearDir(string dir, bool clearOnly)
    {
        foreach (var file in Directory.GetFileSystemEntries(dir))
        {
            if (Directory.Exists(file))
            {
                ClearDir(file, clearOnly: false);
            }
            else
            {
                var fileName = file.Split('/')[^1];
                if (fileName != "index.html")
                {
                    File.Delete(file);
                }
            }
        }

        if (!clearOnly)
        {
            try
            {
                Directory.Delete(dir);
            }
            catch
            {
            }
        }
    }
}
