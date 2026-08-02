namespace EcomAE.Platform.Auth;

public sealed record LegacyApiUsageLogEntry(
    string Action,
    string Section,
    string Source,
    int? ClientId,
    string RequestPath,
    int HttpStatus,
    bool QuotaBlocked,
    string Message,
    string IpAddress);
