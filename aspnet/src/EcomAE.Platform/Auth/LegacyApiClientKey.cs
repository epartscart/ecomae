namespace EcomAE.Platform.Auth;

public sealed record LegacyApiClientKey(
    string Raw,
    string Product,
    string Prefix,
    string Sha256Hash);
