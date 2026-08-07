namespace EcomAE.Platform.LifeOs.Cinematic;

/// <summary>
/// Serves LifeOS cinematic media from wwwroot. The platform does not call
/// <c>UseStaticFiles</c>, so bare wwwroot paths 404 on live.
/// Primary: <c>/lifeos/media/{file}</c>
/// Legacy alias: <c>/lifeos/cinematic/{file}</c> (download links still use this).
/// Exact <c>/lifeos/cinematic</c> remains the JSON digest from <see cref="Modules.LifeOsModule"/>.
/// </summary>
public static class LifeOsCinematicAssets
{
    public const string MediaPrefix = "/lifeos/media";

    /// <summary>Legacy public download path used by older links and bookmarks.</summary>
    public const string LegacyCinematicPrefix = "/lifeos/cinematic";

    private static readonly HashSet<string> AllowList = new(StringComparer.OrdinalIgnoreCase)
    {
        "lifeos-daily-clone-routine.mp4",
        "lifeos-daily-clone-routine-hero.mp4",
        "lifeos-cinematic-launch-3min.mp4",
        "lifeos-clone-scene01-morning.png",
        "lifeos-clone-scene02-deepwork.png",
        "lifeos-clone-scene03-lunch.png",
        "lifeos-clone-scene04-gym.png",
        "lifeos-clone-scene05-evening.png",
        "lifeos-cinematic-scene01-earth.png",
        "lifeos-cinematic-scene02-continuity.png",
        "lifeos-cinematic-scene03-brain.png",
        "lifeos-cinematic-scene04-voice.png",
        "lifeos-cinematic-scene06-agents.png",
        "lifeos-cinematic-scene10-finale.png",
    };

    public static void Map(IEndpointRouteBuilder endpoints, IWebHostEnvironment env)
    {
        endpoints.MapGet(MediaPrefix + "/{fileName}", (HttpContext http, string fileName) =>
            Serve(env, http, fileName));

        // Keep old download URLs working: /lifeos/cinematic/*.mp4
        endpoints.MapGet(LegacyCinematicPrefix + "/{fileName}", (HttpContext http, string fileName) =>
            Serve(env, http, fileName));
    }

    public static string UrlFor(string fileName) => $"{MediaPrefix}/{fileName}";

    public static string LegacyUrlFor(string fileName) => $"{LegacyCinematicPrefix}/{fileName}";

    public static string DownloadUrlFor(string fileName) => $"{UrlFor(fileName)}?download=1";

    private static IResult Serve(IWebHostEnvironment env, HttpContext http, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains('/')
            || fileName.Contains('\\')
            || fileName.Contains("..", StringComparison.Ordinal)
            || !AllowList.Contains(fileName))
        {
            return Results.NotFound(new { ok = false, error = "unknown-lifeos-media" });
        }

        var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath;

        var path = Path.GetFullPath(Path.Combine(webRoot, "lifeos", "cinematic", fileName));
        var root = Path.GetFullPath(Path.Combine(webRoot, "lifeos", "cinematic"));
        if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
        {
            return Results.NotFound(new { ok = false, error = "missing-lifeos-media", file = fileName });
        }

        if (LooksLikeGitLfsPointer(path))
        {
            return Results.Problem(
                detail: "LifeOS media is a Git LFS pointer — run git lfs pull before publish.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "lifeos-media-lfs-pointer");
        }

        // ?download=1 (or any non-zero download query) → Content-Disposition: attachment
        var forceDownload = http.Request.Query.TryGetValue("download", out var downloadVals)
            && !string.Equals(downloadVals.ToString(), "0", StringComparison.OrdinalIgnoreCase);

        return Results.File(
            path,
            ContentTypeFor(fileName),
            fileDownloadName: forceDownload ? fileName : null,
            enableRangeProcessing: true);
    }

    private static bool LooksLikeGitLfsPointer(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length > 1024)
            {
                return false;
            }

            using var reader = new StreamReader(path);
            var head = reader.ReadLine() ?? "";
            return head.StartsWith("version https://git-lfs.github.com/", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string ContentTypeFor(string fileName)
    {
        if (fileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            return "video/mp4";
        }

        if (fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return "image/png";
        }

        return "application/octet-stream";
    }
}
