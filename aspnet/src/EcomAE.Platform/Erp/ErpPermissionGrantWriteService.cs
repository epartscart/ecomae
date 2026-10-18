using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Erp;

public interface IErpPermissionGrantWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpPermissionGrantWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPermissionAreaGrant(string GroupKey, IReadOnlySet<string>? Actions, bool AllActions);

public sealed record ErpPermissionGrantWriteRequest(
    int UserId,
    bool LimitUser,
    IReadOnlyList<ErpPermissionAreaGrant> Areas,
    IReadOnlySet<long>? CompanyIds,
    IReadOnlySet<long>? SiteIds,
    decimal? ApprovalLimit,
    int ActorUserId);

public sealed class ErpPermissionGrantWriteService : IErpPermissionGrantWriteService
{
    public const string RestrictedGroup = "__restricted__";

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpPermissionScopeReadService _scopes;
    private readonly IErpAuditLogWriter _audit;

    public ErpPermissionGrantWriteService(
        IErpWriteConnectionFactory connections,
        IErpPermissionScopeReadService scopes,
        IErpAuditLogWriter audit)
    {
        _connections = connections;
        _scopes = scopes;
        _audit = audit;
    }

    public static bool TryParse(
        IFormCollection form,
        int actorUserId,
        out ErpPermissionGrantWriteRequest request,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(form);
        request = new ErpPermissionGrantWriteRequest(0, false, [], null, null, null, actorUserId);
        error = string.Empty;
        if (!int.TryParse(form["user_id"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId) || userId <= 0)
        {
            error = "Choose a user.";
            return false;
        }

        var limit = LiveWriteFormBinder.Flag(form, "limit_user", "limitUser");
        var areas = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in form["grant_all"])
        {
            var key = (raw ?? string.Empty).Trim();
            if (key.Length > 0)
            {
                areas[key] = new HashSet<string>(ErpCapabilityCatalog.Actions, StringComparer.OrdinalIgnoreCase);
            }
        }

        foreach (var raw in form["grant"])
        {
            var parts = (raw ?? string.Empty).Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                continue;
            }

            if (!areas.TryGetValue(parts[0], out var actions))
            {
                actions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                areas[parts[0]] = actions;
            }

            actions.Add(parts[1]);
        }

        if (!TryLongSet(form["company_ids"].ToString(), out var companies, out error)
            || !TryLongSet(form["site_ids"].ToString(), out var sites, out error))
        {
            return false;
        }

        decimal? approval = null;
        var approvalText = form["approval_limit"].ToString().Trim();
        if (approvalText.Length > 0)
        {
            if (!decimal.TryParse(approvalText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
            {
                error = "Approval limit must be a number.";
                return false;
            }

            approval = parsed;
        }

        var parsedAreas = areas.Select(pair =>
        {
            var all = ErpCapabilityCatalog.Actions.All(action => pair.Value.Contains(action));
            IReadOnlySet<string>? saved = all ? null : pair.Value;
            return new ErpPermissionAreaGrant(pair.Key, saved, all);
        }).ToArray();

        request = new ErpPermissionGrantWriteRequest(userId, limit, parsedAreas, companies, sites, approval, actorUserId);
        return true;
    }

    public static ErpSimpleWriteResult? Validate(ErpPermissionGrantWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ActorUserId <= 0)
        {
            return ErpSimpleWriteResult.Fail("unauthorized", "Sign in required to set user access.");
        }

        if (request.UserId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose a user.");
        }

        if (!request.LimitUser)
        {
            return null;
        }

        var known = ErpCapabilityCatalog.Groups.Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actions = ErpCapabilityCatalog.Actions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var area in request.Areas)
        {
            if (!known.Contains(area.GroupKey))
            {
                return ErpSimpleWriteResult.Fail("invalid", "Choose a module that exists in ERP.");
            }

            if (area.AllActions)
            {
                continue;
            }

            foreach (var action in area.Actions ?? new HashSet<string>())
            {
                if (!actions.Contains(action))
                {
                    return ErpSimpleWriteResult.Fail("invalid", "Choose a standard action.");
                }
            }
        }

        if (request.ActorUserId == request.UserId && !KeepsOwnAdministration(request.Areas))
        {
            return ErpSimpleWriteResult.Fail(
                "self_lockout",
                "Keep Administration View and Edit on your own user so User control stays open.");
        }

        if (request.ApprovalLimit is < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Approval limit must be a number.");
        }

        return null;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpPermissionGrantWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invalid = Validate(request);
        if (invalid is not null)
        {
            return invalid;
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await _scopes.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `epc_erp_permission_grant` WHERE `user_id`=?"),
                cancellationToken,
                request.UserId).ConfigureAwait(false);

            var areas = request.LimitUser
                ? request.Areas.Where(area => area.AllActions || area.Actions is { Count: > 0 }).ToArray()
                : [];
            if (request.LimitUser && areas.Length == 0)
            {
                areas = [new ErpPermissionAreaGrant(RestrictedGroup, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "None" }, false)];
            }

            foreach (var area in areas)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_permission_grant` (`user_id`,`group_key`,`actions_json`,`company_ids_json`,`site_ids_json`,`approval_limit`,`effective_from`,`effective_to`) VALUES (?,?,?,?,?,?,NULL,NULL)"),
                    cancellationToken,
                    request.UserId,
                    area.GroupKey,
                    area.AllActions ? null : JsonSerializer.Serialize(area.Actions ?? new HashSet<string>()),
                    request.CompanyIds is { Count: > 0 } ? JsonSerializer.Serialize(request.CompanyIds) : null,
                    request.SiteIds is { Count: > 0 } ? JsonSerializer.Serialize(request.SiteIds) : null,
                    request.ApprovalLimit).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            var summary = request.LimitUser
                ? "ERP module access limited for user #" + request.UserId.ToString(CultureInfo.InvariantCulture)
                : "ERP module limits cleared for user #" + request.UserId.ToString(CultureInfo.InvariantCulture);
            await _audit.LogAsync(
                connection,
                null,
                request.ActorUserId,
                "rbac_grant_save",
                "rbac_grant",
                request.UserId,
                summary,
                new Dictionary<string, string?>
                {
                    ["user_id"] = request.UserId.ToString(CultureInfo.InvariantCulture),
                    ["limited"] = request.LimitUser ? "1" : "0",
                    ["areas"] = string.Join(",", areas.Select(area => area.GroupKey)),
                },
                cancellationToken).ConfigureAwait(false);

            var message = request.LimitUser
                ? areas.Any(area => area.GroupKey == RestrictedGroup)
                    ? "This user is limited and has no module access."
                    : "Access saved. Only the ticked modules and actions stay open for this user."
                : "Limits removed. This user keeps their current access.";
            return new ErpSimpleWriteResult(true, "ok", message, request.UserId, Math.Max(1, areas.Length));
        }
        catch (DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("db", "User access could not be saved.");
        }
    }

    private static bool KeepsOwnAdministration(IReadOnlyList<ErpPermissionAreaGrant> areas)
    {
        var admin = areas.FirstOrDefault(area => string.Equals(area.GroupKey, "administration", StringComparison.OrdinalIgnoreCase));
        if (admin is null)
        {
            return false;
        }

        if (admin.AllActions)
        {
            return true;
        }

        var actions = admin.Actions ?? new HashSet<string>();
        return actions.Contains("View") && actions.Contains("Edit");
    }

    private static bool TryLongSet(string raw, out IReadOnlySet<long>? values, out string error)
    {
        values = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var set = new HashSet<long>();
        foreach (var token in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0)
            {
                error = "Company and site ids must be numbers.";
                return false;
            }

            set.Add(id);
        }

        values = set.Count == 0 ? null : set;
        return true;
    }
}
