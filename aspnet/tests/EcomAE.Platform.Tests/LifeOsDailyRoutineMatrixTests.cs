using EcomAE.Platform.LifeOs.Purpose;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsDailyRoutineMatrixTests
{
    [Fact]
    public void Digest_covers_operator_matrix_and_complete_24x7()
    {
        var matrix = new LifeOsDailyRoutineMatrix();
        var digest = matrix.Digest();

        Assert.True(digest.Complete24x7);
        Assert.Equal("covered", digest.CoverageVerdict);
        Assert.Equal(5, digest.CoreRows);
        Assert.Equal(3, digest.ContinuityRows);
        Assert.Equal(8, digest.Segments.Count);
        Assert.Equal(digest.Segments.Count, digest.CoveredRows);

        Assert.Contains(digest.Segments, s => s.Key == "morning-routine" && s.Mode.Contains("Morning"));
        Assert.Contains(digest.Segments, s => s.Key == "deep-work" && s.Mode.Contains("Deep Work"));
        Assert.Contains(digest.Segments, s => s.Key == "lunch-outdoor" && s.Mode.Contains("Lunch"));
        Assert.Contains(digest.Segments, s => s.Key == "gym-health" && s.Mode.Contains("Gym"));
        Assert.Contains(digest.Segments, s => s.Key == "evening-rest" && s.Mode.Contains("Evening"));

        Assert.Contains(digest.Segments, s => s.ProactiveAssistance.Contains("wearable biometrics"));
        Assert.Contains(digest.Segments, s => s.ProactiveAssistance.Contains("joint angles"));
        Assert.Contains(digest.Segments, s => s.ProactiveAssistance.Contains("disables sensors in private zones"));
        Assert.All(digest.Segments, s => Assert.False(string.IsNullOrWhiteSpace(s.ClonedVoiceSample)));
    }
}
