using EcomAE.Platform.LifeOs.Cinematic;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsCinematicFilmTests
{
    [Fact]
    public void Digest_is_daily_clone_routine_morning_to_evening()
    {
        var film = new LifeOsCinematicFilm();
        var digest = film.Digest();

        Assert.Equal(82, digest.DurationSeconds);
        Assert.Equal("16:9", digest.AspectRatio);
        Assert.Equal("720p web (H.264)", digest.Resolution);
        Assert.Equal(5, digest.Scenes.Count);
        Assert.Equal("Amina", digest.ProtagonistName);
        Assert.Contains("identical AI clone", digest.MasterPrompt);
        Assert.Contains("Amina", digest.MasterPrompt);
        Assert.Equal(0, digest.Scenes[0].StartSeconds);
        Assert.Equal("morning", digest.Scenes[0].Key);
        Assert.Equal("evening", digest.Scenes[^1].Key);
        Assert.Equal(82, digest.Scenes.Sum(s => s.DurationSeconds));
        Assert.Contains(digest.Scenes, s => s.KeyframeUrl is not null && s.KeyframeUrl.Contains("clone-scene01"));
        Assert.Equal("/lifeos/media/lifeos-daily-clone-routine.mp4", digest.VideoUrl);
        Assert.Equal("/lifeos/media/lifeos-daily-clone-routine-hero.mp4", digest.HeroVideoUrl);
        Assert.Equal("/lifeos/media/lifeos-clone-scene01-morning.png", digest.PosterUrl);
        Assert.Equal("/lifeos/media/lifeos-daily-clone-routine.mp4?download=1", film.VideoDownloadUrl);
        Assert.Equal("/lifeos/cinematic/lifeos-daily-clone-routine.mp4", film.VideoLegacyUrl);
        Assert.Equal(
            "/lifeos/cinematic/lifeos-cinematic-launch-3min.mp4",
            LifeOsCinematicAssets.LegacyUrlFor("lifeos-cinematic-launch-3min.mp4"));
        Assert.Contains("live-on-frontend", digest.RenderStatus);
        Assert.Contains("hero background", string.Join(' ', digest.DeliveryNotes), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AAC", digest.Music, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dialogue", string.Join(' ', digest.DeliveryNotes), StringComparison.OrdinalIgnoreCase);
    }
}
