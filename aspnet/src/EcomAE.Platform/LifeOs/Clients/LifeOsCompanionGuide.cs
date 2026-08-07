namespace EcomAE.Platform.LifeOs.Clients;

/// <summary>Same-name clone guide lines for mobile Track / Talk / Listen / Guide.</summary>
public static class LifeOsCompanionGuide
{
    public static IReadOnlyList<object> Beats(string human, string clone) =>
    [
        new { key = "morning", title = "Morning", line = $"{human} → {clone}: Walk me through today.", reply = $"{clone}: Weather and priorities are ready. Start with hydration, then your first focus block." },
        new { key = "deep-work", title = "Deep work", line = $"{human} → {clone}: How should I handle this meeting?", reply = $"{clone}: Protect ninety minutes. I drafted the agenda and will summarize live." },
        new { key = "move", title = "Move / track", line = $"{human} → {clone}: Start tracking my walk.", reply = $"{clone}: Tracking on. I will coach pace and log distance for you, {human}." },
        new { key = "gym", title = "Gym", line = $"{human} → {clone}: Check my form.", reply = $"{clone}: Soft bend in the knees. Core engaged. Logging clean reps." },
        new { key = "evening", title = "Evening", line = $"{human} → {clone}: Summarize my day.", reply = $"{clone}: Strong day. I will plan tomorrow and quiet private-zone sensors." },
    ];

    public static (string Reply, string Step, IReadOnlyList<string> Actions) Reply(
        string human,
        string clone,
        string heard,
        string mode)
    {
        var h = heard.ToLowerInvariant();
        if (mode is "track" || h.Contains("track") || h.Contains("walk") || h.Contains("gym") || h.Contains("run"))
        {
            return (
                $"{clone}: Tracking with you, {human}. Say pause anytime. I am logging this session beside you.",
                "track-session",
                ["Start walk", "Log workout set", "Pause tracking"]);
        }

        if (mode is "listen" || h.Contains("listen") || h.Contains("read") || h.Contains("speak"))
        {
            return (
                $"{clone}: Listening mode. I will speak the next guide step out loud — tap Listen again for the following beat.",
                "listen-guide",
                ["Speak morning brief", "Speak focus block", "Speak evening summary"]);
        }

        if (h.Contains("meeting") || h.Contains("email") || h.Contains("focus"))
        {
            return (
                $"{clone}: Protect a focus block, {human}. I can draft the agenda and watch the calendar for conflicts.",
                "deep-work",
                ["Start focus timer", "Draft agenda", "Ask again"]);
        }

        if (h.Contains("tomorrow") || h.Contains("summar"))
        {
            return (
                $"{clone}: Here is your day so far, {human}. I will schedule tomorrow's first deep-work block at nine.",
                "evening",
                ["Plan tomorrow", "Quiet sensors", "Open routine matrix"]);
        }

        return (
            $"{clone}: I am here, {human}. Ask me to track, guide your day, or listen and speak the next step. Human control stays first.",
            "guide-home",
            ["Walk me through today", "Start tracking", "Speak next guide"]);
    }
}
