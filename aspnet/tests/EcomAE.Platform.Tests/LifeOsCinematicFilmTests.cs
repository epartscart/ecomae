using EcomAE.Platform.LifeOs.Cinematic;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsCinematicFilmTests
{
    [Fact]
    public void Digest_is_three_minutes_with_eleven_scenes()
    {
        var film = new LifeOsCinematicFilm();
        var digest = film.Digest();

        Assert.Equal(180, digest.DurationSeconds);
        Assert.Equal("16:9", digest.AspectRatio);
        Assert.Equal("4K HDR", digest.Resolution);
        Assert.Equal(11, digest.Scenes.Count);
        Assert.Contains("Prepare tomorrow's board meeting", digest.MasterPrompt);
        Assert.Equal(0, digest.Scenes[0].StartSeconds);
        Assert.Equal(160, digest.Scenes[^1].StartSeconds);
        Assert.Equal(20, digest.Scenes[^1].DurationSeconds);
        Assert.Equal(180, digest.Scenes.Sum(s => s.DurationSeconds));
        Assert.Contains(digest.Scenes, s => s.KeyframeUrl is not null && s.KeyframeUrl.Contains("scene01"));
        Assert.Equal("/lifeos/cinematic/lifeos-cinematic-launch-3min.mp4", digest.VideoUrl);
        Assert.False(string.IsNullOrWhiteSpace(digest.PosterUrl));
        Assert.Contains("live-on-frontend", digest.RenderStatus);
    }
}
