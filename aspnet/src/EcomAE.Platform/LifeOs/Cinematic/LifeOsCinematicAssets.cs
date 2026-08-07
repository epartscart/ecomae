namespace EcomAE.Platform.LifeOs.Cinematic;

/// <summary>
/// Serves LifeOS cinematic media from wwwroot. The platform does not call
/// <c>UseStaticFiles</c>, so wwwroot paths under /lifeos/cinematic were 404 on live.
/// </summary>
public static class LifeOsCinematicAssets
{
    public const string MediaPrefix = "/lifeos/media";

    private static readonly HashSet<string> AllowList = new(StringComparer.OrdinalIgnoreCase)
    {
        "lifeos-cinematic-launch-3min.mp4",
        "lifeos-cinematic-scene01-earth.png",
        "lifeos-cinematic-scene02-continuity.png",
        "lifeos-cinematic-scene03-brain.png",
        "lifeos-cinematic-scene04-voice.png",
        "lifeos-cinematic-scene06-agents.png",
        "lifeos-cinematic-scene10-finale.png",
    };

    public static void Map(IEndpointRouteBuilder endpoints, IWebHostEnvironment env)
    {
        endpoints.MapGet(MediaPrefix + "/{fileName}", (string fileName) =>
        {
            if (string.IsNullOrWhiteSpace(fileName)
                || fileName.Contains('/')
                || fileName.Contains('\\')
                || fileName.Contains("..", StringComparison.Ordinal)
                || !AllowList.Contains(fileName))
            {
                return Results.NotFound(new { ok = false, error = "unknown-lifeos-media" });
            }

            var webRoot = env.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(env.ContentRootPath, "wwwroot");
            }

            var path = Path.GetFullPath(Path.Combine(webRoot, "lifeos", "cinematic", fileName));
            var root = Path.GetFullPath(Path.Combine(webRoot, "lifeos", "cinematic"));
            if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
            {
                return Results.NotFound(new { ok = false, error = "missing-lifeos-media", file = fileName });
            }

            // Guard against Git LFS pointer files landing in the publish tree.
            if (LooksLikeGitLfsPointer(path))
            {
                return Results.Problem(
                    detail: "LifeOS media is a Git LFS pointer — run git lfs pull before publish.",
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "lifeos-media-lfs-pointer");
            }

            var contentType = ContentTypeFor(fileName);
            return Results.File(
                path,
                contentType,
                enableRangeProcessing: true,
                fileDownloadName: null);
        });
    }

    public static string UrlFor(string fileName) => $"{MediaPrefix}/{fileName}";

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

        if (fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return "image/jpeg";
        }

        return "application/octet-stream";
    }
}
