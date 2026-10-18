namespace EcomAE.Platform.Auth;

/// <summary>
/// One posted area grant (or delegation) attached to the signed-in session.
/// <see cref="AllActions"/> is true when the stored action list is empty, which means every standard action.
/// </summary>
public sealed record ErpSessionAreaGrant(string GroupKey, IReadOnlySet<string> Actions, bool AllActions);
