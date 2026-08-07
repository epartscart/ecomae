namespace EcomAE.Platform.LifeOs.Part8;

public sealed class LifeOsClientExperience : ILifeOsClientExperience
{
    public IReadOnlyList<LifeOsUxPrinciple> ExperiencePrinciples { get; } =
    [
        new("natural", "Natural"),
        new("predictive", "Predictive"),
        new("non-intrusive", "Non-intrusive"),
        new("consistent", "Consistent"),
        new("adaptive", "Adaptive"),
        new("explainable", "Explainable"),
        new("accessible", "Accessible"),
        new("fast", "Fast"),
    ];

    public IReadOnlyList<LifeOsClientPlatform> ClientPlatforms { get; } =
    [
        P("web-chrome", "Web", "Chrome", "live-scaffold"),
        P("web-edge", "Web", "Edge", "live-scaffold"),
        P("web-firefox", "Web", "Firefox", "live-scaffold"),
        P("web-safari", "Web", "Safari", "live-scaffold"),
        P("desktop-win", "Desktop", "Windows", "roadmap"),
        P("desktop-mac", "Desktop", "macOS", "roadmap"),
        P("desktop-linux", "Desktop", "Linux", "roadmap"),
        P("mobile-android", "Mobile", "Android", "roadmap"),
        P("mobile-ios", "Mobile", "iOS", "roadmap"),
        P("tablet", "Mobile", "Tablet", "roadmap"),
        P("watch", "Wearables", "Smart Watches", "roadmap"),
        P("band", "Wearables", "Fitness Bands", "research"),
        P("glasses", "Wearables", "Smart Glasses", "research"),
        P("ar-glasses", "Wearables", "AR Glasses", "research"),
        P("xr", "Wearables", "XR Headsets", "research"),
        P("tv-android", "Smart TV", "Android TV", "research"),
        P("tv-apple", "Smart TV", "Apple TV", "research"),
        P("tv-fire", "Smart TV", "Fire TV", "research"),
        P("vehicle-aa", "Vehicle", "Android Automotive", "research"),
        P("vehicle-carplay", "Vehicle", "Apple CarPlay", "research"),
        P("vehicle-embed", "Vehicle", "Embedded Vehicle Systems", "research"),
        P("iot", "IoT", "IoT / Edge devices", "research"),
    ];

    public IReadOnlyList<string> DesignPrinciples { get; } =
    [
        "Minimal Interface", "AI-First Interaction", "Consistent Navigation",
        "Accessibility by Default", "Adaptive Layout", "Responsive Components",
        "Dark/Light Themes", "Context Awareness"
    ];

    public IReadOnlyList<LifeOsDesignComponent> DesignComponents { get; } =
    [
        new("buttons", "Buttons"),
        new("inputs", "Inputs"),
        new("cards", "Cards"),
        new("dialogs", "Dialogs"),
        new("navigation", "Navigation"),
        new("sidebar", "Sidebar"),
        new("toolbar", "Toolbar"),
        new("timeline", "Timeline"),
        new("activity-feed", "Activity Feed"),
        new("notifications", "Notifications"),
        new("voice-ui", "Voice UI"),
        new("ai-chat", "AI Chat Components"),
        new("widgets", "Widgets"),
        new("dashboards", "Dashboards"),
    ];

    public IReadOnlyList<LifeOsNavMethod> NavigationMethods { get; } =
    [
        new("voice", "Voice"),
        new("touch", "Touch"),
        new("mouse", "Mouse"),
        new("keyboard", "Keyboard"),
        new("gesture", "Gesture"),
        new("eye", "Eye Tracking"),
        new("controller", "Controller"),
        new("automation", "Automation"),
    ];

    public IReadOnlyList<LifeOsWorkspaceModule> AiWorkspaceModules { get; } =
    [
        new("search", "Search"),
        new("ai-chat", "AI Chat"),
        new("tasks", "Active Tasks"),
        new("calendar", "Calendar"),
        new("notifications", "Notifications"),
        new("memory", "Memory"),
        new("project", "Current Project"),
        new("workflows", "Running Workflows"),
        new("suggestions", "AI Suggestions"),
        new("quick-actions", "Quick Actions"),
    ];

    public IReadOnlyList<LifeOsSearchDomain> SearchDomains { get; } =
    [
        new("people", "People"),
        new("documents", "Documents"),
        new("emails", "Emails"),
        new("meetings", "Meetings"),
        new("projects", "Projects"),
        new("code", "Code"),
        new("images", "Images"),
        new("voice-notes", "Voice Notes"),
        new("workflows", "Workflows"),
        new("knowledge", "Knowledge"),
        new("memory", "Memory"),
        new("conversations", "Conversations"),
        new("tasks", "Tasks"),
    ];

    public IReadOnlyList<LifeOsDashboardKind> DashboardKinds { get; } =
    [
        new("personal", "Personal Dashboard",
            ["Upcoming Tasks", "Health", "Calendar", "Productivity", "Learning", "Goals", "Finance", "Notifications"]),
        new("business", "Business Dashboard",
            ["Projects", "CRM", "ERP", "Sales", "Support", "Analytics", "Automation", "Team Activity"]),
        new("executive", "Executive Dashboard",
            ["Revenue", "KPIs", "AI Insights", "Risk Indicators", "Predictions", "Organization Health", "Strategic Goals"]),
    ];

    public IReadOnlyList<string> VoiceCapabilities { get; } =
    [
        "Natural Conversation", "Interruptions", "Follow-up Questions", "Context Memory",
        "Multi-language", "Offline Commands", "Wake Word", "Continuous Conversation",
        "Adaptive Speaking Style", "Emotion-aware Responses"
    ];

    public IReadOnlyList<string> ChatCapabilities { get; } =
    [
        "Rich text", "Images", "Voice", "Video", "Documents", "Code", "Tables",
        "Charts", "Interactive Forms", "AI Suggestions", "Live Collaboration"
    ];

    public IReadOnlyList<LifeOsWidget> SmartWidgets { get; } =
    [
        new("weather", "Weather"),
        new("calendar", "Calendar"),
        new("stocks", "Stocks"),
        new("health", "Health"),
        new("tasks", "Tasks"),
        new("focus", "Focus Timer"),
        new("travel", "Travel"),
        new("meetings", "Meetings"),
        new("energy", "Energy"),
        new("goals", "Goals"),
        new("finance", "Finance"),
        new("notifications", "Notifications"),
        new("smarthome", "Smart Home"),
    ];

    public IReadOnlyList<LifeOsWorkspaceModule> ProductivityModules { get; } =
    [
        new("calendar", "Calendar"),
        new("email", "Email"),
        new("notes", "Notes"),
        new("tasks", "Tasks"),
        new("documents", "Documents"),
        new("whiteboard", "Whiteboard"),
        new("meetings", "Meetings"),
        new("ai-assistant", "AI Assistant"),
        new("knowledge", "Knowledge Base"),
        new("workflows", "Workflows"),
        new("projects", "Projects"),
    ];

    public IReadOnlyList<string> MobileFeatures { get; } =
    [
        "Offline AI", "Voice Assistant", "Camera Intelligence", "Document Scanner",
        "Business Card Scanner", "Health Integration", "Travel Assistant",
        "Smart Notifications", "Wallet", "Location Intelligence"
    ];

    public IReadOnlyList<string> DesktopFeatures { get; } =
    [
        "AI Sidebar", "Developer Assistant", "Screen Understanding", "Code Assistant",
        "Workflow Automation", "Clipboard Intelligence", "Multi-window AI",
        "Meeting Assistant", "Research Workspace", "Knowledge Search"
    ];

    public IReadOnlyList<string> GlassesCapabilities { get; } =
    [
        "Navigation", "Translation", "Object Identification", "Live Notes", "Task Guidance",
        "Meeting Assistance", "Remote Expert", "Instruction Overlay",
        "Warehouse Picking", "Construction Support"
    ];

    public IReadOnlyList<string> WearableDisplays { get; } =
    [
        "Next Meeting", "Health Alerts", "Task Reminder", "Voice Commands",
        "Emergency Actions", "Navigation", "Fitness", "Medication", "Sleep Insights"
    ];

    public IReadOnlyList<string> VehicleDashboardItems { get; } =
    [
        "Navigation", "Calendar", "Voice", "Music", "Calls", "Traffic",
        "Charging/Fuel", "Meeting ETA", "Emergency Assistance"
    ];

    public IReadOnlyList<string> ContinuityFlow { get; } =
    [
        "Phone", "Research", "Desktop", "Continue Reading", "Tablet", "Annotate", "Watch", "Reminder"
    ];

    public IReadOnlyList<LifeOsAccessibilitySupport> AccessibilitySupports { get; } =
    [
        new("screen-reader", "Screen Readers"),
        new("high-contrast", "High Contrast"),
        new("large-text", "Large Text"),
        new("voice-nav", "Voice Navigation"),
        new("keyboard", "Keyboard Navigation"),
        new("captions", "Captions"),
        new("sign", "Sign Language Extensions"),
        new("eye", "Eye Tracking"),
        new("switch", "Switch Devices"),
        new("reduced-motion", "Reduced Motion"),
        new("color-blind", "Color-Blind Friendly Themes"),
        new("i18n", "Multi-language UI"),
    ];

    public IReadOnlyList<LifeOsOfflineCapability> OfflineCapabilities { get; } =
    [
        new("wake", "Wake Word"),
        new("voice", "Voice Commands"),
        new("notes", "Notes"),
        new("tasks", "Tasks"),
        new("calendar", "Calendar"),
        new("automation", "Basic Automation"),
        new("memory", "Local Memory"),
        new("docs", "Document Viewing"),
        new("ocr", "Camera OCR"),
        new("emergency", "Emergency Commands"),
    ];

    public IReadOnlyList<string> OfflineSyncFlow { get; } =
    [
        "Offline Changes", "Local Queue", "Synchronization", "Conflict Resolution", "Cloud Update"
    ];

    public IReadOnlyList<LifeOsPersonalizationKnob> PersonalizationKnobs { get; } =
    [
        new("themes", "Themes"),
        new("accent", "Accent Colors"),
        new("fonts", "Fonts"),
        new("layout", "Layout"),
        new("voice", "Voice"),
        new("personality", "AI Personality"),
        new("widgets", "Widgets"),
        new("dashboard", "Dashboard"),
        new("shortcuts", "Shortcuts"),
        new("notifications", "Notification Rules"),
        new("privacy", "Privacy Levels"),
        new("automation", "Automation Preferences"),
    ];

    public IReadOnlyList<string> AdaptiveLearningSignals { get; } =
    [
        "Preferred work hours", "Frequently used actions", "Communication style",
        "Favorite dashboards", "Notification tolerance", "Accessibility settings"
    ];

    public IReadOnlyList<LifeOsFocusMode> FocusModes { get; } =
    [
        new("work", "Work"),
        new("meeting", "Meeting"),
        new("driving", "Driving"),
        new("study", "Study"),
        new("sleep", "Sleep"),
        new("travel", "Travel"),
        new("exercise", "Exercise"),
        new("vacation", "Vacation"),
        new("custom", "Custom"),
    ];

    public IReadOnlyList<string> NotificationPolicyFlow { get; } =
    [
        "Incoming Event", "Priority Analysis", "Current Context", "Focus Rules",
        "Deliver", "OR Delay", "OR Summarize Later"
    ];

    public IReadOnlyList<LifeOsMultiUserProfile> MultiUserProfiles { get; } =
    [
        new("individual", "Individual Users"),
        new("families", "Families"),
        new("teams", "Teams"),
        new("departments", "Departments"),
        new("organizations", "Organizations"),
        new("education", "Educational Institutions"),
        new("healthcare", "Healthcare Providers"),
        new("government", "Government Agencies"),
    ];

    public IReadOnlyList<string> DigitalTwinCapabilities { get; } =
    [
        "Personalize recommendations", "Anticipate recurring tasks", "Optimize workflows",
        "Preserve long-term context", "Simulate plans before execution"
    ];

    public IReadOnlyList<LifeOsUxMetric> ExperienceMetrics { get; } =
    [
        new("performance", "Application Startup", "scaffold"),
        new("performance", "AI Response Time", "scaffold"),
        new("performance", "Screen Transition Time", "scaffold"),
        new("performance", "Voice Response Latency", "scaffold"),
        new("performance", "Synchronization Delay", "scaffold"),
        new("performance", "Offline Recovery Time", "scaffold"),
        new("experience", "Task Completion Rate", "scaffold"),
        new("experience", "AI Acceptance Rate", "scaffold"),
        new("experience", "Search Success Rate", "scaffold"),
        new("experience", "Automation Success Rate", "scaffold"),
        new("experience", "User Satisfaction", "scaffold"),
        new("experience", "Accessibility Compliance", "scaffold"),
        new("experience", "Context Accuracy", "scaffold"),
        new("experience", "Personalization Accuracy", "scaffold"),
    ];

    public object ClientEcosystemDigest() => new
    {
        chapter = 103,
        platforms = ClientPlatforms,
        families = ClientPlatforms.Select(p => p.Family).Distinct().ToArray(),
        liveToday = new[] { "LifeOS Web Console", "/lifeos", "/lifeos/app", "Part 2–7 scaffold UIs" }
    };

    public object DesignAndNavigationDigest() => new
    {
        designSystem = new { chapter = 104, name = "Life Design System (LDS)", principles = DesignPrinciples, components = DesignComponents },
        navigation = new
        {
            chapter = 105,
            intentExample = "Show today's pending work.",
            methods = NavigationMethods,
            note = "Navigate by intent, not menus"
        }
    };

    public object WorkspaceAndSearchDigest() => new
    {
        aiWorkspace = new { chapter = 106, modules = AiWorkspaceModules },
        search = new
        {
            chapter = 107,
            kind = "semantic",
            domains = SearchDomains,
            example = "Find the document I discussed with Sarah before the Dubai meeting."
        },
        dashboards = new { chapter = 108, kinds = DashboardKinds },
        voice = new { chapter = 109, capabilities = VoiceCapabilities },
        chat = new
        {
            chapter = 110,
            capabilities = ChatCapabilities,
            layout = new[] { "Conversation", "Files", "AI Suggestions", "Tasks", "Related Memory", "Actions" }
        },
        widgets = new { chapter = 111, items = SmartWidgets },
        productivity = new { chapter = 112, modules = ProductivityModules }
    };

    public object ModalityClientsDigest() => new
    {
        mobile = new
        {
            chapter = 113,
            home = new[]
            {
                "Today's Plan", "AI Assistant", "Notifications", "Quick Actions", "Calendar",
                "Health", "Goals", "Recent Memory", "Smart Home"
            },
            features = MobileFeatures
        },
        desktop = new
        {
            chapter = 114,
            features = DesktopFeatures,
            layout = new[] { "Toolbar", "AI Sidebar", "Main Workspace", "Right Context Panel", "Bottom Terminal" }
        },
        glasses = new
        {
            chapter = 115,
            capabilities = GlassesCapabilities,
            example = new[] { "Machine", "Recognized", "Maintenance Guide", "Step-by-Step Overlay", "Voice Instructions" }
        },
        wearable = new { chapter = 116, displays = WearableDisplays },
        vehicle = new
        {
            chapter = 117,
            dashboard = VehicleDashboardItems,
            rule = "No distracting visual interactions while driving"
        }
    };

    public object ContinuityAccessibilityOfflineDigest() => new
    {
        continuity = new
        {
            chapter = 118,
            flow = ContinuityFlow,
            persists = new[] { "Context", "Cursor position", "AI conversation" }
        },
        accessibility = new { chapter = 119, supports = AccessibilitySupports },
        offline = new
        {
            chapter = 120,
            capabilities = OfflineCapabilities,
            sync = OfflineSyncFlow
        }
    };

    public object PersonalizationAndFocusDigest() => new
    {
        personalization = new
        {
            chapter = 121,
            knobs = PersonalizationKnobs,
            adaptiveSignals = AdaptiveLearningSignals,
            userControl = "Users can review, modify, or disable adaptive personalization at any time"
        },
        focus = new { chapter = 122, modes = FocusModes, notificationPolicy = NotificationPolicyFlow },
        multiUser = new
        {
            chapter = 123,
            profiles = MultiUserProfiles,
            isolation = new[]
            {
                "Separate memory", "Separate permissions", "Separate dashboards",
                "Separate AI personalization", "Shared resources only where explicitly configured"
            }
        }
    };

    public object MetricsAndTwinDigest() => new
    {
        digitalTwin = new
        {
            chapter = 124,
            optional = true,
            capabilities = DigitalTwinCapabilities,
            note = "Not an autonomous replacement for the user; privacy settings and user control apply"
        },
        metrics = new { chapter = 125, items = ExperienceMetrics }
    };

    public object FullPart8Digest() => new
    {
        ok = true,
        part = 8,
        title = "Client Applications, User Experience & Cross-Platform Architecture",
        chapters = Enumerable.Range(102, 24).ToArray(),
        chapterTitles = new Dictionary<int, string>
        {
            [102] = "User Experience Vision",
            [103] = "Client Ecosystem",
            [104] = "Universal Design System (Life Design System - LDS)",
            [105] = "Universal Navigation",
            [106] = "AI Workspace",
            [107] = "Universal Search",
            [108] = "Dashboard Architecture",
            [109] = "Voice User Interface (VUI)",
            [110] = "Chat Experience",
            [111] = "Smart Widgets",
            [112] = "Productivity Workspace",
            [113] = "Mobile Experience",
            [114] = "Desktop Experience",
            [115] = "Smart Glasses Experience",
            [116] = "Wearable Experience",
            [117] = "Vehicle Experience",
            [118] = "Cross-Device Continuity",
            [119] = "Accessibility Framework",
            [120] = "Offline Experience",
            [121] = "Personalization Engine",
            [122] = "Notification & Focus Experience",
            [123] = "Multi-User Experience",
            [124] = "Digital Twin (Optional Feature)",
            [125] = "User Experience Metrics"
        },
        principles = ExperiencePrinciples,
        ecosystem = ClientEcosystemDigest(),
        designAndNav = DesignAndNavigationDigest(),
        workspaceAndSearch = WorkspaceAndSearchDigest(),
        modalityClients = ModalityClientsDigest(),
        continuityA11yOffline = ContinuityAccessibilityOfflineDigest(),
        personalizationAndFocus = PersonalizationAndFocusDigest(),
        metricsAndTwin = MetricsAndTwinDigest(),
        liveToday = new
        {
            web = "LifeOS marketing home + console + Part 2–7 scaffold UIs",
            ambient = "PhpLifeOsAmbientAudio on login/home",
            note = "Native mobile/desktop/glasses/vehicle clients not claimed"
        },
        status = "scaffold"
    };

    private static LifeOsClientPlatform P(string key, string family, string title, string status)
        => new(key, family, title, status);
}
