using EcomAE.Platform.Auth;
using EcomAE.Platform.Security;

namespace EcomAE.Platform.Presentation;

public sealed record ErpPermissionGrant(
    int UserId,
    string GroupKey,
    IReadOnlySet<string>? Actions = null,
    IReadOnlySet<long>? CompanyIds = null,
    IReadOnlySet<long>? SiteIds = null,
    decimal? ApprovalLimit = null,
    DateTimeOffset? EffectiveFrom = null,
    DateTimeOffset? EffectiveTo = null);

public sealed record ErpPermissionDelegation(
    int PrincipalUserId,
    int DelegateUserId,
    string GroupKey,
    IReadOnlySet<string>? Actions = null,
    IReadOnlySet<long>? CompanyIds = null,
    IReadOnlySet<long>? SiteIds = null,
    decimal? ApprovalLimit = null,
    DateTimeOffset? EffectiveFrom = null,
    DateTimeOffset? EffectiveTo = null);

public sealed record ErpPermissionRequest(
    long CompanyId = 0,
    long SiteId = 0,
    decimal Amount = 0,
    DateTimeOffset? At = null);

public sealed record ErpPermissionDecision(
    bool Allowed,
    string ReasonCode,
    string Reason);

public static class ErpPermissionScopePolicy
{
    public static ErpPermissionDecision Evaluate(
        LegacySessionContext session,
        string groupKey,
        string action,
        ErpPermissionRequest request,
        IEnumerable<ErpPermissionGrant> grants,
        IEnumerable<ErpPermissionDelegation> delegations)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(delegations);

        if (!ErpCapabilityCatalog.CanAction(session, groupKey, action))
        {
            return Deny("action", "The ERP area/action is not granted.");
        }

        if (session.Permissions.Contains(EcomAePermissions.SuperErpAccess))
        {
            return Allow("super_erp", "Super ERP access granted.");
        }

        var at = request.At ?? DateTimeOffset.UtcNow;
        var candidates = grants
            .Where(grant => grant.UserId == session.UserId
                && string.Equals(grant.GroupKey, groupKey, StringComparison.OrdinalIgnoreCase))
            .Select(grant => new Candidate(grant.Actions, grant.CompanyIds, grant.SiteIds, grant.ApprovalLimit, grant.EffectiveFrom, grant.EffectiveTo))
            .Concat(delegations
                .Where(delegation => delegation.DelegateUserId == session.UserId
                    && string.Equals(delegation.GroupKey, groupKey, StringComparison.OrdinalIgnoreCase)
                    && delegation.PrincipalUserId > 0)
                .Select(delegation => new Candidate(delegation.Actions, delegation.CompanyIds, delegation.SiteIds, delegation.ApprovalLimit, delegation.EffectiveFrom, delegation.EffectiveTo)))
            .ToArray();

        if (candidates.Length == 0)
        {
            return Deny("grant_missing", "No scoped ERP permission grant is active for this user.");
        }

        if (!candidates.Any(candidate => IsActive(candidate, at)))
        {
            return Deny("effective_window", "The ERP permission is outside its effective date window.");
        }

        if (!candidates.Any(candidate => MatchesScope(candidate, request)))
        {
            return Deny(
                request.CompanyId > 0 && request.SiteId > 0 ? "company_site_scope" : request.CompanyId > 0 ? "company_scope" : "site_scope",
                "The ERP permission does not cover the requested company/site.");
        }

        if (string.Equals(action, "Approve", StringComparison.OrdinalIgnoreCase)
            && request.Amount > 0
            && !candidates.Any(candidate => MatchesApprovalLimit(candidate, request.Amount)))
        {
            return Deny("approval_limit", "The approval amount exceeds the active approval limit.");
        }

        if (!candidates.Any(candidate => MatchesAction(candidate, action)))
        {
            return Deny("action", "The scoped ERP grant does not include this action.");
        }

        return Allow("grant", "Scoped ERP permission granted.");
    }

    private static bool IsActive(Candidate candidate, DateTimeOffset at)
        => (!candidate.EffectiveFrom.HasValue || at >= candidate.EffectiveFrom.Value)
            && (!candidate.EffectiveTo.HasValue || at <= candidate.EffectiveTo.Value);

    private static bool MatchesScope(Candidate candidate, ErpPermissionRequest request)
        => (candidate.CompanyIds is null || candidate.CompanyIds.Count == 0 || candidate.CompanyIds.Contains(request.CompanyId))
            && (candidate.SiteIds is null || candidate.SiteIds.Count == 0 || candidate.SiteIds.Contains(request.SiteId));

    private static bool MatchesApprovalLimit(Candidate candidate, decimal amount)
        => !candidate.ApprovalLimit.HasValue || amount <= candidate.ApprovalLimit.Value;

    private static bool MatchesAction(Candidate candidate, string action)
        => candidate.Actions is null
            || candidate.Actions.Count == 0
            || candidate.Actions.Contains(action);

    private static ErpPermissionDecision Allow(string code, string reason)
        => new(true, code, reason);

    private static ErpPermissionDecision Deny(string code, string reason)
        => new(false, code, reason);

    private sealed record Candidate(
        IReadOnlySet<string>? Actions,
        IReadOnlySet<long>? CompanyIds,
        IReadOnlySet<long>? SiteIds,
        decimal? ApprovalLimit,
        DateTimeOffset? EffectiveFrom,
        DateTimeOffset? EffectiveTo);
}
