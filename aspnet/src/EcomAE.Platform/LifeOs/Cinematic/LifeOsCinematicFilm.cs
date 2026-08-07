namespace EcomAE.Platform.LifeOs.Cinematic;

/// <summary>
/// Production bible for the LifeOS™ 3-minute ultra-premium cinematic launch film.
/// Keyframe storyboard is live in-product; full 4K60 video render awaits Higgsfield (or VFX pipeline).
/// </summary>
public sealed class LifeOsCinematicFilm : ILifeOsCinematicFilm
{
    public const int TotalDurationSeconds = 180;

    public string VideoUrl { get; } = LifeOsCinematicAssets.UrlFor("lifeos-cinematic-launch-3min.mp4");

    public string VideoDownloadUrl { get; } = LifeOsCinematicAssets.DownloadUrlFor("lifeos-cinematic-launch-3min.mp4");

    public string VideoLegacyUrl { get; } = LifeOsCinematicAssets.LegacyUrlFor("lifeos-cinematic-launch-3min.mp4");

    public string PosterUrl { get; } = LifeOsCinematicAssets.UrlFor("lifeos-cinematic-scene01-earth.png");

    public string MasterPrompt { get; } = """
Create a 3-minute ultra-premium cinematic 3D product launch film for "LifeOS", the world's first AI Operating System.

Style:
Apple keynote meets Iron Man's JARVIS interface meets futuristic holographic operating system.

Visual quality:
Photorealistic, Hollywood VFX, Unreal Engine 5 quality, ray tracing, volumetric lighting, global illumination, cinematic camera movement, futuristic glass UI, holographic blue and white interface with subtle cyan accents.

Opening Scene:
The Earth is shown from space at night. Billions of glowing data connections appear across continents. A glowing AI core emerges above the planet. Text appears:
"LifeOS — One Intelligence. Every Device. Every Business. Every Person."

Scene 2:
Zoom into a modern smart city. Follow one professional from home to office. Show LifeOS seamlessly running across phone, laptop, smartwatch, smart glasses, vehicle, and office display. Context follows the user automatically.

Scene 3:
Show the LifeOS AI Brain with animated modules:
Memory Engine, Context Engine, Planner, Multi-Agent System, Knowledge Graph, Workflow Engine, Automation Engine, Voice Intelligence, Vision Intelligence, Security Shield.
Data flows dynamically between modules in real time.

Scene 4:
Demonstrate a voice command: "Prepare tomorrow's board meeting."
LifeOS gathers emails, CRM data, ERP reports, calendar events, financial dashboards, and documents. It creates a meeting agenda, presentation, action list, and sends invitations automatically.

Scene 5:
Visualize enterprise integrations: ERP, CRM, HRMS, Finance, Inventory, Projects, Analytics, Cloud Services — all connected through a glowing central LifeOS Core.

Scene 6:
Show multiple AI agents collaborating simultaneously: Sales, Finance, Legal, Developer, Healthcare, Research, Customer Support. Each exchanges knowledge in a synchronized multi-agent network.

Scene 7:
Display holographic dashboards showing predictive analytics, workflow automation, knowledge graphs, and business KPIs updating live.

Scene 8:
Demonstrate cross-device continuity: phone → laptop → tablet → smartwatch → AR smart glasses — without losing context.

Scene 9:
Reveal the global cloud infrastructure: thousands of Kubernetes clusters, AI data centers, edge nodes, and secure encrypted connections illuminate a world map.

Scene 10:
Show the LifeOS Marketplace with developers publishing AI agents, plugins, and applications. Organizations install them instantly.

Final Scene:
Camera zooms out to the glowing Earth. Holographic LifeOS logo with slogan:
"LifeOS — The Operating System for Human Intelligence."

Mood: Inspiring, premium, trustworthy, visionary.
Music: Epic orchestral blended with futuristic electronic ambience.
Pacing: Fast, elegant, cinematic, smooth transitions, minimal UI clutter.
Color palette: White, silver, deep black, blue, cyan, subtle gold highlights.
Aspect ratio: 16:9. Resolution: 4K HDR at 60fps.
""";

    public IReadOnlyList<LifeOsCinematicScene> Scenes { get; } =
    [
        new(
            1,
            "earth-open",
            "Earth from space",
            "Opening",
            0,
            16,
            "Night Earth · continental data lattice · AI core rising above the planet",
            "LifeOS — One Intelligence. Every Device. Every Business. Every Person.",
            LifeOsCinematicAssets.UrlFor("lifeos-cinematic-scene01-earth.png"),
            [
                "Orbital wide shot of Earth at night",
                "Billions of cyan data filaments ignite across continents",
                "Luminous AI core emerges above the atmosphere",
                "Title lockup fades in with glass refraction",
            ]),
        new(
            2,
            "city-commute",
            "Smart city continuity",
            "Living OS",
            16,
            16,
            "Follow one professional home → vehicle → office; LifeOS on every surface",
            "Context follows you.",
            LifeOsCinematicAssets.UrlFor("lifeos-cinematic-scene02-continuity.png"),
            [
                "Dolly into illuminated smart city",
                "Phone → watch → glasses → vehicle HUD → laptop → wall display",
                "Soft context trail connects devices without UI clutter",
            ]),
        new(
            3,
            "ai-brain",
            "LifeOS AI Brain",
            "Architecture",
            32,
            18,
            "Glass neural core with ten animated engines exchanging live data",
            "The Intelligence Core",
            LifeOsCinematicAssets.UrlFor("lifeos-cinematic-scene03-brain.png"),
            [
                "Memory · Context · Planner · Multi-Agent · Knowledge Graph",
                "Workflow · Automation · Voice · Vision · Security Shield",
                "Real-time data rivers between modules",
            ]),
        new(
            4,
            "voice-board",
            "Voice: board meeting",
            "Demo",
            50,
            20,
            "Voice command assembles CRM/ERP/calendar into agenda, deck, actions, invites",
            "“Prepare tomorrow's board meeting.”",
            LifeOsCinematicAssets.UrlFor("lifeos-cinematic-scene04-voice.png"),
            [
                "Voice waveform → intent recognition",
                "Emails, CRM, ERP, calendar, finance panels orbit the core",
                "Agenda + presentation + action list materialize",
                "Invitations dispatch with confirm-first elegance",
            ]),
        new(
            5,
            "enterprise-hub",
            "Enterprise integrations",
            "Platform",
            70,
            14,
            "ERP · CRM · HRMS · Finance · Inventory · Projects · Analytics · Cloud around LifeOS Core",
            "One core. Every system.",
            null,
            [
                "Radial enterprise constellation",
                "Encrypted light paths into the central core",
                "Subtle gold accents on trusted connections",
            ]),
        new(
            6,
            "multi-agent",
            "Multi-agent collaboration",
            "Agents",
            84,
            16,
            "Sales, Finance, Legal, Developer, Healthcare, Research, Support agents synchronize",
            "Many specialists. One intelligence.",
            LifeOsCinematicAssets.UrlFor("lifeos-cinematic-scene06-agents.png"),
            [
                "Seven luminous agent silhouettes",
                "Knowledge packets exchange in sync",
                "Network pulse settles into harmony",
            ]),
        new(
            7,
            "holographic-kpis",
            "Holographic dashboards",
            "Insight",
            100,
            14,
            "Predictive analytics, workflows, knowledge graphs, live KPIs",
            "See what is coming — before it arrives.",
            null,
            [
                "Floating glass dashboards",
                "KPIs tick upward with cinematic restraint",
                "Knowledge graph blooms in cyan",
            ]),
        new(
            8,
            "device-handoff",
            "Cross-device continuity",
            "Continuity",
            114,
            16,
            "Phone → laptop → tablet → watch → AR glasses without losing context",
            "Start anywhere. Finish everywhere.",
            LifeOsCinematicAssets.UrlFor("lifeos-cinematic-scene02-continuity.png"),
            [
                "Match-cut handoff between devices",
                "Task state persists as a single luminous thread",
                "AR glasses complete the action",
            ]),
        new(
            9,
            "global-cloud",
            "Global cloud fabric",
            "Infrastructure",
            130,
            16,
            "Kubernetes clusters, AI data centers, edge nodes, encrypted world map",
            "Built for the planet.",
            null,
            [
                "World map ignites with secure routes",
                "Cluster constellations pulse",
                "Edge nodes spark near cities",
            ]),
        new(
            10,
            "marketplace",
            "LifeOS Marketplace",
            "Ecosystem",
            146,
            14,
            "Developers publish agents and plugins; organizations install instantly",
            "Extend the OS.",
            null,
            [
                "Marketplace shelves of luminous modules",
                "One-tap install beam into enterprise stacks",
            ]),
        new(
            11,
            "finale",
            "Finale — Earth + logo",
            "Close",
            160,
            20,
            "Pull back to glowing Earth; holographic LifeOS mark and slogan",
            "LifeOS — The Operating System for Human Intelligence.",
            LifeOsCinematicAssets.UrlFor("lifeos-cinematic-scene10-finale.png"),
            [
                "Camera retreats to orbital scale",
                "Holographic logo resolves with gold rim light",
                "Slogan lockup · music resolves · hold for brand",
            ]),
    ];

    public LifeOsCinematicFilmDigest Digest() => new(
        "LifeOS™",
        "LifeOS — Ultra-premium cinematic product launch film",
        "16:9",
        "4K HDR",
        "60fps",
        TotalDurationSeconds,
        "Apple keynote × JARVIS × holographic OS · Unreal Engine 5 / Hollywood VFX",
        "Inspiring, premium, trustworthy, visionary",
        "Epic orchestral + futuristic electronic ambience",
        "White, silver, deep black, blue, cyan, subtle gold",
        "live-on-frontend · 3:00 MP4 via /lifeos/media/* file endpoint (UseStaticFiles not enabled)",
        VideoUrl,
        PosterUrl,
        MasterPrompt.Trim(),
        Scenes,
        [
            "Watch: GET /lifeos/cinematic-app (HTML5 video + timed storyboard)",
            "Home embed: /lifeos film section",
            "Media: GET /lifeos/media/lifeos-cinematic-launch-3min.mp4 (range-enabled)",
            "Legacy download alias: GET /lifeos/cinematic/lifeos-cinematic-launch-3min.mp4",
            "Forced download: GET /lifeos/media/...?download=1",
            "JSON bible: GET /lifeos/cinematic",
            "Keyframes under /lifeos/media/*.png",
            "Same board-meeting voice beat as /lifeos/demo-app sample scenario",
        ]);
}
