namespace EcomAE.Platform.LifeOs.Cinematic;

/// <summary>
/// LifeOS™ daily-routine film: human + identical same-name AI clone from morning to evening.
/// Hero uses a muted loop; theatre uses the scored cut.
/// </summary>
public sealed class LifeOsCinematicFilm : ILifeOsCinematicFilm
{
    public const int TotalDurationSeconds = 82;

    public string ProtagonistName { get; } = "Amina";

    public string VideoUrl { get; } = LifeOsCinematicAssets.UrlFor("lifeos-daily-clone-routine.mp4");

    public string HeroVideoUrl { get; } = LifeOsCinematicAssets.UrlFor("lifeos-daily-clone-routine-hero.mp4");

    public string VideoDownloadUrl { get; } = LifeOsCinematicAssets.DownloadUrlFor("lifeos-daily-clone-routine.mp4");

    public string VideoLegacyUrl { get; } = LifeOsCinematicAssets.LegacyUrlFor("lifeos-daily-clone-routine.mp4");

    public string PosterUrl { get; } = LifeOsCinematicAssets.UrlFor("lifeos-clone-scene01-morning.png");

    public string MasterPrompt { get; } = """
Create an ultra-premium cinematic film for LifeOS: a complete day with a human and their identical AI clone.

Characters:
- Human name: Amina
- AI clone name: Amina (same name, same face — LifeOS clone twin with subtle cyan holographic rim)
- Human speaks to the clone using their own name: "Amina…" and the clone answers as Amina.

Story arc — morning to evening:
1) Morning Routine — wake, hygiene, breakfast, daily planning. Clone briefs weather and priorities.
2) Deep Work / Desktop — coding, meetings, emails. Clone advises focus, drafts, meeting prep.
3) Lunch & Outdoor — walk, shops. Clone evaluates venues and speaks through earbuds.
4) Gym & Health — training. Clone coaches form and logs reps.
5) Evening & Rest — family/study/home. Clone summarizes the day, plans tomorrow, quiets private-zone sensors.

Tone: inspiring, premium, trustworthy. Style: Apple keynote × JARVIS × holographic OS.
Palette: white, silver, deep black, blue, cyan. Aspect 16:9.
""";

    public IReadOnlyList<LifeOsCinematicScene> Scenes { get; } =
    [
        new(
            1,
            "morning",
            "Morning · Amina ↔ Amina",
            "Morning Routine",
            0,
            16,
            "Human Amina and cyan-rim clone Amina at dawn; clone briefs the day",
            "Amina → Amina: Walk me through today.",
            LifeOsCinematicAssets.UrlFor("lifeos-clone-scene01-morning.png"),
            [
                "Wearable sleep phases → dynamic wake",
                "Cloned-voice morning briefing",
                "Weather + priorities hologram",
            ]),
        new(
            2,
            "deep-work",
            "Deep work · Amina ↔ Amina",
            "Deep Work / Desktop",
            16,
            18,
            "Office desk — clone advises code, meetings, and drafts beside human Amina",
            "Amina → Amina: How should I handle this meeting?",
            LifeOsCinematicAssets.UrlFor("lifeos-clone-scene02-deepwork.png"),
            [
                "Screen context + live meeting summary",
                "Focus interval protection",
                "Draft email / agenda assist",
            ]),
        new(
            3,
            "lunch",
            "Lunch walk · Amina ↔ Amina",
            "Lunch & Outdoor",
            34,
            14,
            "City walk — clone highlights shopfronts and pricing for Amina",
            "Amina → Amina: Which venue looks best?",
            LifeOsCinematicAssets.UrlFor("lifeos-clone-scene03-lunch.png"),
            [
                "Camera keyframes of venues",
                "Foot-traffic + pricing read",
                "Oral summary via earbuds",
            ]),
        new(
            4,
            "gym",
            "Gym · Amina ↔ Amina",
            "Gym & Health",
            48,
            16,
            "Training floor — clone coaches joint angles and reps in real time",
            "Amina → Amina: Check my form.",
            LifeOsCinematicAssets.UrlFor("lifeos-clone-scene04-gym.png"),
            [
                "Phone-camera pose analysis",
                "Real-time audio form corrections",
                "Workout metrics logged",
            ]),
        new(
            5,
            "evening",
            "Evening · Amina ↔ Amina",
            "Evening & Rest",
            64,
            18,
            "Home — clone summarizes the day and plans tomorrow with Amina",
            "Amina → Amina: Summarize my day and plan tomorrow.",
            LifeOsCinematicAssets.UrlFor("lifeos-clone-scene05-evening.png"),
            [
                "Daily progress summary",
                "Tomorrow task schedule",
                "Private-zone sensors quiet",
            ]),
    ];

    public LifeOsCinematicFilmDigest Digest() => new(
        "LifeOS™",
        "LifeOS — Human + same-name AI clone · morning to evening",
        "16:9",
        "720p web (H.264)",
        "30fps",
        TotalDurationSeconds,
        "Human Amina + identical clone Amina · daily routine advice loop",
        "Inspiring, premium, trustworthy, personal",
        "Ambient score + Amina ↔ Amina spoken dialogue (stereo AAC)",
        "White, silver, deep black, blue, cyan",
        "live-on-frontend · hero background + theatre",
        ProtagonistName,
        VideoUrl,
        HeroVideoUrl,
        PosterUrl,
        MasterPrompt.Trim(),
        Scenes,
        [
            "Hero background: autoplay loop of HeroVideoUrl on /lifeos (starts muted; AMBIENT toggle unmutes dialogue)",
            "Theatre: /lifeos/cinematic-app with scene jumps and native volume controls",
            "Human and clone share the name Amina — spoken dialogue is Amina → Amina over ambient score",
            "Both MP4s include stereo AAC audio (dialogue + score)",
            "Legacy alias: /lifeos/cinematic/{file} · forced download ?download=1",
            "Aligned with /lifeos/routine 24/7 Daily Human Routine Matrix",
        ]);
}
