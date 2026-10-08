using System.Data.Common;
using System.Text.Json.Serialization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// CMS self-update and pack-install scripts. PHP clears <c>tmp/</c> folders, downloads update packs
/// from <c>update_server</c>, unzips packs and copies their files into the document root. ASP.NET
/// code ships through the release pipeline, so after the PHP CSRF and session gates these scripts
/// answer in their PHP shape with a refusal and touch neither disk nor network.
/// </summary>
public static partial class StorefrontPhpAjax
{
    public const string VersionClearUpdatesPath = "/cp/content/control/version_control/ajax/ajax_clear_updates_dir.php";
    public const string VersionGetUpdatePackPath = "/cp/content/control/version_control/ajax/ajax_get_update_pack.php";
    public const string VersionQueryServerPath = "/cp/content/control/version_control/ajax/ajax_query_to_server.php";
    public const string PacksClearTmpPath = "/cp/content/packs_control/ajax_clear_tmp_folder.php";
    public const string PacksDeletePath = "/cp/content/packs_control/ajax_delete_pack.php";
    public const string PacksInsertExtensionsPath = "/cp/content/packs_control/ajax_insert_extensions.php";
    public const string PacksPrepareSetupPath = "/cp/content/packs_control/ajax_prepare_setup.php";
    public const string PacksProcessingFilesPath = "/cp/content/packs_control/ajax_processing_files.php";

    public const string VersionControlStaysClassic = "CMS self-update stays Classic. ASP.NET releases ship through the deploy pipeline.";
    public const string PackInstallStaysClassic = "Pack install stays Classic. ASP.NET releases ship through the deploy pipeline.";

    public static readonly IReadOnlyList<string> VersionControlPaths = [VersionClearUpdatesPath, VersionGetUpdatePackPath, VersionQueryServerPath];

    /// <summary>Scripts whose PHP gate reads <c>fetchColumn()</c> twice from one <c>COUNT(*)</c> row.</summary>
    public static readonly IReadOnlyList<string> PackCountGatePaths = [PacksClearTmpPath, PacksDeletePath, PacksInsertExtensionsPath];

    public static readonly IReadOnlyList<string> PackSessionGatePaths = [PacksPrepareSetupPath, PacksProcessingFilesPath];

    public sealed record VersionAnswer(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("message")] string Message);

    public sealed record PackResultMessage(
        [property: JsonPropertyName("result_code")] int ResultCode,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("pack_id")] long? PackId);

    public static async Task<object> VersionControlAsync(
        DbConnection connection,
        string? adminSession,
        string? postedCsrf,
        CancellationToken cancellationToken)
    {
        var csrf = await ReadCsrfAsync(connection, adminSession, postedCsrf, cancellationToken).ConfigureAwait(false);
        return csrf.Ok ? new VersionAnswer("ERROR", VersionControlStaysClassic) : CsrfFailure(csrf.Message);
    }

    public static async Task<object> PacksAsync(
        DbConnection connection,
        string path,
        string? adminSession,
        string? postedCsrf,
        CancellationToken cancellationToken)
    {
        var csrf = await ReadCsrfAsync(connection, adminSession, postedCsrf, cancellationToken).ConfigureAwait(false);
        if (!csrf.Ok)
        {
            return CsrfFailure(csrf.Message);
        }

        long count;
        try
        {
            count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `type` = 1"),
                cancellationToken,
                adminSession ?? string.Empty).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, AdminSessionsMissing);
        }

        if (PackCountGatePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            // PHP's second fetchColumn() is false, so a signed-in admin always stops here.
            return new RawHttp(count == 0 ? "No access" : "Session duplication", "application/json;charset=utf-8;");
        }

        if (count == 0)
        {
            return new RawHttp("Forbidden", "application/json;charset=utf-8;");
        }

        return new PackResultMessage(1, PackInstallStaysClassic, null);
    }
}
