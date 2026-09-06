using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>accessories_listings.php</c> taxonomy twin (categories + filter terms).
/// Schema-ensure and JSON seed stay Classic.
/// </summary>
public interface ICpAccessoriesTaxonomyWriteService
{
    Task<ErpSimpleWriteResult> WriteAsync(CpAccessoriesTaxonomyWriteRequest request, CancellationToken cancellationToken = default);
}

public sealed record CpAccessoriesTaxonomyWriteRequest(
    string? Action = null,
    long Id = 0,
    long ParentId = 0,
    string? Label = null,
    string? TermType = null,
    int SortOrder = 0,
    bool Active = false);

public sealed class CpAccessoriesTaxonomyWriteService : ICpAccessoriesTaxonomyWriteService
{
    private static readonly Regex TermTypeSafe = new(
        "[^a-z_]",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public CpAccessoriesTaxonomyWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> WriteAsync(
        CpAccessoriesTaxonomyWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var action = NormalizeAction(request.Action);
        return action switch
        {
            "save_category" => await SaveCategoryAsync(request, cancellationToken).ConfigureAwait(false),
            "set_category_active" => await SetCategoryActiveAsync(request, cancellationToken).ConfigureAwait(false),
            "delete_category" => await DeleteCategoryAsync(request, cancellationToken).ConfigureAwait(false),
            "save_term" => await SaveTermAsync(request, cancellationToken).ConfigureAwait(false),
            "set_term_active" => await SetTermActiveAsync(request, cancellationToken).ConfigureAwait(false),
            "delete_term" => await DeleteTermAsync(request, cancellationToken).ConfigureAwait(false),
            _ => ErpSimpleWriteResult.Fail("invalid", "Action must be save_category, set_category_active, delete_category, save_term, set_term_active, or delete_term.")
        };
    }

    private async Task<ErpSimpleWriteResult> SaveCategoryAsync(
        CpAccessoriesTaxonomyWriteRequest request,
        CancellationToken cancellationToken)
    {
        var label = (request.Label ?? string.Empty).Trim();
        if (label.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Category label required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var parentId = request.ParentId < 0 ? 0 : request.ParentId;
        var slug = Slugify(label);
        var sortOrder = request.SortOrder;
        var editing = request.Id > 0;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (sortOrder <= 0)
        {
            sortOrder = (int)await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COALESCE(MAX(`sort_order`), 0) + 1 FROM `epc_acc_categories` WHERE `parent_id` = ?"),
                cancellationToken,
                parentId).ConfigureAwait(false);
        }

        if (editing)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_acc_categories` SET `label` = ?, `slug` = ?, `parent_id` = ?, `sort_order` = ?, `active` = 1 WHERE `id` = ?"),
                cancellationToken,
                label,
                slug,
                parentId,
                sortOrder,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Category saved", request.Id);
        }

        var unique = await UniqueCategorySlugAsync(connection, parentId, slug, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_acc_categories` (`parent_id`, `slug`, `label`, `pw_id`, `sort_order`, `active`) VALUES (?, ?, ?, 0, ?, 1)"),
            cancellationToken,
            parentId,
            unique,
            label,
            sortOrder).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Category added", id);
    }

    private async Task<ErpSimpleWriteResult> SetCategoryActiveAsync(
        CpAccessoriesTaxonomyWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A category id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_acc_categories` SET `active` = ? WHERE `id` = ?"),
            cancellationToken,
            request.Active ? 1 : 0,
            request.Id).ConfigureAwait(false);
        if (!request.Active)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_acc_categories` SET `active` = 0 WHERE `parent_id` = ?"),
                cancellationToken,
                request.Id).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok(request.Active ? "Category activated" : "Category deactivated", request.Id);
    }

    private async Task<ErpSimpleWriteResult> DeleteCategoryAsync(
        CpAccessoriesTaxonomyWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid category");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var used = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_acc_listings` WHERE `category_id` = ? OR `subcategory_id` = ?"),
            cancellationToken,
            request.Id,
            request.Id).ConfigureAwait(false);
        if (used > 0)
        {
            return ErpSimpleWriteResult.Fail("conflict", "Category has listings — deactivate instead of delete");
        }

        var childUsed = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                SELECT COUNT(*) FROM `epc_acc_listings` l
                INNER JOIN `epc_acc_categories` c ON c.id = l.subcategory_id
                WHERE c.parent_id = ?
                """),
            cancellationToken,
            request.Id).ConfigureAwait(false);
        if (childUsed > 0)
        {
            return ErpSimpleWriteResult.Fail("conflict", "Sub-categories have listings — deactivate instead");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_acc_categories` WHERE `parent_id` = ?"),
            cancellationToken,
            request.Id).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_acc_categories` WHERE `id` = ?"),
            cancellationToken,
            request.Id).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Category deleted", request.Id);
    }

    private async Task<ErpSimpleWriteResult> SaveTermAsync(
        CpAccessoriesTaxonomyWriteRequest request,
        CancellationToken cancellationToken)
    {
        var type = SanitizeTermType(request.TermType);
        var label = (request.Label ?? string.Empty).Trim();
        if (label.Length == 0 || type.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Term label required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var parentId = request.ParentId < 0 ? 0 : request.ParentId;
        var value = type == "condition" ? Slugify(label) : label;
        if (type == "condition" && value is not ("new" or "used" or "refurbished"))
        {
            value = Slugify(label);
        }

        var sortOrder = request.SortOrder;
        var editing = request.Id > 0;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (sortOrder <= 0)
        {
            sortOrder = (int)await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COALESCE(MAX(`sort_order`), 0) + 1 FROM `epc_acc_terms` WHERE `term_type` = ? AND `parent_id` = ?"),
                cancellationToken,
                type,
                parentId).ConfigureAwait(false);
        }

        if (editing)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_acc_terms` SET `label` = ?, `value` = ?, `parent_id` = ?, `sort_order` = ?, `active` = 1 WHERE `id` = ? AND `term_type` = ?"),
                cancellationToken,
                label,
                value,
                parentId,
                sortOrder,
                request.Id,
                type).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Term saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `epc_acc_terms` (`term_type`, `parent_id`, `value`, `label`, `sort_order`, `active`)
                VALUES (?, ?, ?, ?, ?, 1)
                ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `sort_order` = VALUES(`sort_order`), `active` = 1, `id` = LAST_INSERT_ID(`id`)
                """),
            cancellationToken,
            type,
            parentId,
            value,
            label,
            sortOrder).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Term added", id);
    }

    private async Task<ErpSimpleWriteResult> SetTermActiveAsync(
        CpAccessoriesTaxonomyWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A term id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_acc_terms` SET `active` = ? WHERE `id` = ?"),
            cancellationToken,
            request.Active ? 1 : 0,
            request.Id).ConfigureAwait(false);
        if (rows <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Term not found");
        }

        return ErpSimpleWriteResult.Ok(request.Active ? "Term activated" : "Term deactivated", request.Id);
    }

    private async Task<ErpSimpleWriteResult> DeleteTermAsync(
        CpAccessoriesTaxonomyWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A term id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_acc_terms` WHERE `parent_id` = ?"),
            cancellationToken,
            request.Id).ConfigureAwait(false);
        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_acc_terms` WHERE `id` = ?"),
            cancellationToken,
            request.Id).ConfigureAwait(false);
        if (rows <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Term not found");
        }

        return ErpSimpleWriteResult.Ok("Term deleted", request.Id);
    }

    private static async Task<string> UniqueCategorySlugAsync(
        System.Data.Common.DbConnection connection,
        long parentId,
        string slug,
        CancellationToken cancellationToken)
    {
        var baseSlug = slug;
        var n = 1;
        while (n < 40)
        {
            var existing = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_acc_categories` WHERE `parent_id` = ? AND `slug` = ? LIMIT 1"),
                cancellationToken,
                parentId,
                slug).ConfigureAwait(false);
            if (existing < 1)
            {
                return slug;
            }

            n++;
            slug = baseSlug + "-" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return baseSlug + "-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string NormalizeAction(string? raw)
    {
        var action = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return action switch
        {
            "save_category" or "category" or "add_category" => "save_category",
            "set_category_active" or "category_active" or "set-category-active" => "set_category_active",
            "delete_category" or "del_category" => "delete_category",
            "save_term" or "term" or "add_term" => "save_term",
            "set_term_active" or "term_active" or "set-term-active" => "set_term_active",
            "delete_term" or "del_term" => "delete_term",
            _ => action
        };
    }

    public static string Slugify(string? label)
    {
        var s = (label ?? string.Empty).Trim().ToLowerInvariant();
        s = Regex.Replace(s, "[^a-z0-9]+", "-");
        s = s.Trim('-');
        return s.Length > 0 ? s : "item";
    }

    public static string SanitizeTermType(string? raw)
        => TermTypeSafe.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);
}
