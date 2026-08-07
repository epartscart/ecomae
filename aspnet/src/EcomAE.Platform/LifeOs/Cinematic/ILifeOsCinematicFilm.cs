namespace EcomAE.Platform.LifeOs.Cinematic;

public interface ILifeOsCinematicFilm
{
    LifeOsCinematicFilmDigest Digest();

    string MasterPrompt { get; }

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
    string MasterPrompt,
    IReadOnlyList<LifeOsCinematicScene> Scenes,
    IReadOnlyList<string> DeliveryNotes);
