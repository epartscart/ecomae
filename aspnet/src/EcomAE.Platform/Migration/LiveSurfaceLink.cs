namespace EcomAE.Platform.Migration;

public sealed record LiveSurfaceLink(
    string HostClass,
    string Surface,
    string Url,
    string StackToday,
    string AspNetRouteHint,
    string Notes);
