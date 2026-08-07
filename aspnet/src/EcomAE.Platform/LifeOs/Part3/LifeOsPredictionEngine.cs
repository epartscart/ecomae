namespace EcomAE.Platform.LifeOs.Part3;

public sealed class LifeOsPredictionEngine : ILifeOsPredictionEngine
{
    public IReadOnlyList<LifeOsPrediction> Predict(LifeOsCurrentRealityModel reality, string intent)
    {
        var list = new List<LifeOsPrediction>();
        var ts = DateTimeOffset.UtcNow.ToString("HHmmss");

        if (reality.FocusScore >= 85 && reality.Interruptibility == "LOW")
        {
            list.Add(new($"PRD-{ts}-1", "focus-degradation", 0.42,
                "Focus may drop after prolonged deep work",
                "Suggest a short break in 25 minutes"));
        }

        if (reality.EnergyLevel < 50)
        {
            list.Add(new($"PRD-{ts}-2", "health-risk", 0.55,
                "Energy low — productivity risk",
                "Recommend rest or lighter tasks"));
        }

        if (intent.Contains("meeting", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(reality.CalendarEvent))
        {
            list.Add(new($"PRD-{ts}-3", "schedule-conflict", 0.48,
                "Possible schedule pressure near calendar event",
                "Prepare materials before the event"));
        }

        if (reality.Activity.Contains("Coding", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new($"PRD-{ts}-4", "project-delay", 0.35,
                "Coding session may overrun if interrupted",
                "Hold notifications until interruptibility rises"));
        }

        if (list.Count == 0)
        {
            list.Add(new($"PRD-{ts}-0", "baseline", 0.3,
                "No high-probability risk forecast",
                "Continue ambient monitoring"));
        }

        return list;
    }

    public object Digest() => new
    {
        chapter = 19,
        title = "Prediction Engine",
        examples = new[]
        {
            "Meeting delays", "Battery depletion", "Traffic", "Health risks",
            "Schedule conflicts", "Budget overruns", "Project delays",
            "Exercise recovery", "Stress level", "Focus degradation"
        },
        pipeline = new[]
        {
            "Historical Data", "Current Context", "Machine Learning",
            "Probability Estimation", "Forecast", "Recommendation"
        },
        status = "scaffold"
    };
}
