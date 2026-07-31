namespace EcomAE.Platform.Auth;

public sealed record LegacyApiClientRecord(
    int Id,
    string ClientKeyHash,
    string ClientKeyPrefix,
    string Product,
    string Label,
    bool Active,
    int DailyLimit,
    int CallsToday,
    DateOnly? CallsResetDate,
    string AllowedActionsJson);
