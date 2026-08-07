namespace EcomAE.Platform.LifeOs.Purpose;

/// <summary>
/// Complete 24/7 Daily Human Routine Matrix — LifeOS purpose coverage for proactive
/// assistance and cloned-voice responses across the professional day.
/// </summary>
public sealed class LifeOsDailyRoutineMatrix : ILifeOsDailyRoutineMatrix
{
    public IReadOnlyList<LifeOsDailyRoutineSegment> Segments { get; } =
    [
        new(
            "morning-routine",
            "06:00 – 08:00",
            "Morning Routine",
            "Waking up, personal hygiene, breakfast, daily planning",
            "Tracks sleep phases via wearable biometrics, triggers dynamic wake alarm, synthesizes morning briefing in cloned voice, outlines weather and daily priorities.",
            "Good morning. You slept 7 hours 12 minutes — deep sleep was strong. Weather is clear. Top priorities: board prep at 10:00, invoice follow-ups, and a 45-minute focus block before lunch.",
            "Health + Work",
            true,
            ["Perception", "Prediction", "Planning", "Voice Intelligence", "Memory"],
            ["Wearable", "Phone", "Earbuds", "Smart display"]),
        new(
            "deep-work",
            "08:00 – 12:00",
            "Deep Work / Desktop",
            "Coding, writing, virtual meetings, data analysis",
            "Monitors active screen context, diagnoses software bugs, summarizes live audio meetings, drafts emails, and tracks focus intervals.",
            "You're 38 minutes into deep work. Meeting notes from Architecture Review are drafted. Shall I mute low-priority notifications until noon?",
            "Work",
            true,
            ["Context Engine", "Multi-Agent", "Voice", "Vision", "Notification Intelligence"],
            ["Desktop", "Laptop", "Headset", "Secondary display"]),
        new(
            "lunch-outdoor",
            "12:00 – 13:00",
            "Lunch & Outdoor",
            "Eating, outdoor walking, inspecting shops & venues",
            "Captures camera keyframes of local shopfronts upon request, evaluates foot-traffic metrics and pricing, provides oral summary via earbuds.",
            "I captured three shopfronts on your walk. The second venue has the strongest foot traffic and mid-range pricing — summary ready when you want it.",
            "Mobility + Lifestyle",
            true,
            ["Vision Intelligence", "Context", "Voice", "Knowledge Graph"],
            ["Phone camera", "Earbuds", "Wearable"]),
        new(
            "afternoon-continuity",
            "13:00 – 17:00",
            "Afternoon Continuity",
            "Meetings, follow-ups, collaborative desktop work, light mobility",
            "Carries morning context into afternoon agendas, drafts follow-ups, protects residual focus blocks, and syncs CRM/ERP flashes without breaking flow.",
            "Afternoon brief: two follow-ups pending from morning, calendar gap at 15:30 for deep work, cash/AR flash is stable.",
            "Work + Wealth",
            false,
            ["Planner", "CRM/ERP agents", "Memory", "Notification Intelligence"],
            ["Desktop", "Phone", "Laptop"]),
        new(
            "gym-health",
            "17:00 – 19:00",
            "Gym & Health",
            "Exercise, weight training, physical recovery",
            "Analyzes movement joint angles via phone camera, counts reps, delivers real-time audio form corrections, logs workout metrics.",
            "Rep three — keep your elbows tucked. Tempo looks good. Logging set: 8 reps at your target load.",
            "Health",
            true,
            ["Vision Intelligence", "Health agents", "Voice", "Memory"],
            ["Phone camera", "Earbuds", "Wearable"]),
        new(
            "commute-home",
            "19:00 – 20:00",
            "Commute / Arrival",
            "Travel home, errands, household handoff",
            "Uses ETA + home bridge context to prepare climate/lights, surfaces package and family calendar notes, and keeps irreversible actions confirm-gated.",
            "You're about 18 minutes out. Living room is warm — I can pre-cool and queue the evening family briefing.",
            "Home + Mobility",
            false,
            ["Context", "Home Manager", "Planning", "Security Shield"],
            ["Vehicle", "Phone", "Smart home", "Watch"]),
        new(
            "evening-rest",
            "20:00 – 22:00",
            "Evening & Rest",
            "Family time, studying, home prep, bedtime",
            "Delivers interactive micro-tutoring, summarizes daily progress, schedules tomorrow's tasks, disables sensors in private zones.",
            "Day summary: three priorities closed, two rolled to tomorrow. Private zones are sensor-quiet. Want a five-minute micro-tutor on tomorrow's board deck?",
            "Home + Learning",
            true,
            ["Memory", "Planner", "Voice", "Ethics / Privacy", "Personalization"],
            ["Tablet", "Smart display", "Earbuds", "Home sensors"]),
        new(
            "night-sleep",
            "22:00 – 06:00",
            "Night & Sleep Protection",
            "Sleep, recovery, emergency-only monitoring",
            "Protects sleep with DND policies, monitors wearable vitals for anomalies only, stages tomorrow's wake plan, and keeps private-zone sensors off by default.",
            "Night mode on. Sensors are quiet in private zones. Wake window set from your sleep debt and first calendar block.",
            "Health + Home",
            false,
            ["Wearable Intelligence", "Ethics / Privacy", "Prediction", "Security Shield"],
            ["Wearable", "Phone (DND)", "Home hub"]),
    ];

    public IReadOnlyList<LifeOsDailyRoutineCoverageRow> Coverage =>
        Segments.Select(s => new LifeOsDailyRoutineCoverageRow(
            s.Key,
            s.Mode,
            Covered: true,
            Status: s.IsCorePurposeRow ? "purpose-core" : "24x7-continuity",
            Evidence: $"Mapped to {string.Join(", ", s.Engines.Take(3))} · devices {string.Join(", ", s.Devices.Take(3))}"))
        .ToArray();

    public LifeOsDailyRoutineDigest Digest()
    {
        var core = Segments.Count(s => s.IsCorePurposeRow);
        var continuity = Segments.Count - core;
        var covered = Coverage.Count(c => c.Covered);
        return new(
            "LifeOS™",
            "Complete 24/7 Daily Human Routine Matrix",
            "LifeOS purpose is to run beside a professional life all day — perceive context, decide with ethics, act with confirm-first control, and learn preferences across morning, deep work, outdoor, health, evening, and sleep.",
            Complete24x7: true,
            CoreRows: core,
            ContinuityRows: continuity,
            CoveredRows: covered,
            CoverageVerdict: covered == Segments.Count ? "covered" : "partial",
            Segments,
            Coverage,
            [
                "Core purpose rows match the operator-supplied Morning → Deep Work → Lunch → Gym → Evening matrix",
                "Continuity rows fill 13:00–17:00, 19:00–20:00, and 22:00–06:00 so the day is complete 24/7",
                "Cloned-voice lines are scaffold samples — live TTS/voice-clone not claimed",
                "UI: /lifeos/routine · JSON: /lifeos/routine · Home purpose section links here",
                "Related demos: /lifeos/demo-app (board-meeting, health-focus, home-arrival)",
            ]);
    }
}
