namespace EcomAE.Platform.LifeOs.Cinematic;

public interface ILifeOsCinematicFilm
{
    LifeOsCinematicFilmDigest Digest();

    string MasterPrompt { get; }

    /// <summary>Primary theatre film (human + same-name AI clone daily routine).</summary>
    string VideoUrl { get; }

    /// <summary>Muted loop-optimized hero background video.</summary>
    string HeroVideoUrl { get; }

    /// <summary>Download URL with Content-Disposition attachment (?download=1).</summary>
    string VideoDownloadUrl { get; }

    /// <summary>Legacy download path kept for bookmarks: /lifeos/cinematic/*.mp4</summary>
    string VideoLegacyUrl { get; }

    /// <summary>Poster image for the HTML5 video element.</summary>
    string PosterUrl { get; }

    /// <summary>Shared human / clone identity name used in the film.</summary>
    string ProtagonistName { get; }

    IReadOnlyList<LifeOsCinematicScene> Scenes { get; }
}

public sealed record LifeOsCinematicScene(
    int Number,
    string Key,
    string Title,
    string Beat,
    int StartSeconds,
    int DurationSeconds,
    string Visual,
    string OnScreenText,
    string? KeyframeUrl,
    IReadOnlyList<string> Beats);

public sealed record LifeOsCinematicFilmDigest(
    string Product,
    string Title,
    string AspectRatio,
    string Resolution,
    string FrameRate,
    int DurationSeconds,
    string Style,
    string Mood,
    string Music,
    string ColorPalette,
    string RenderStatus,
    string ProtagonistName,
    string VideoUrl,
    string HeroVideoUrl,
    string PosterUrl,
    string MasterPrompt,
    IReadOnlyList<LifeOsCinematicScene> Scenes,
    IReadOnlyList<string> DeliveryNotes);
