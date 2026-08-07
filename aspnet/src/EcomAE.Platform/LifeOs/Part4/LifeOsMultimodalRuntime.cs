using System.Collections.Concurrent;
using EcomAE.Platform.LifeOs.EventBus;
using EcomAE.Platform.LifeOs.Models;
using EcomAE.Platform.LifeOs.Part3;

namespace EcomAE.Platform.LifeOs.Part4;

public sealed class LifeOsMultimodalRuntime : ILifeOsMultimodalRuntime
{
    private readonly ILifeOsEventBus _bus;
    private readonly ILifeOsAiCore? _aiCore;
    private readonly ConcurrentDictionary<string, string> _deviceSessions = new(StringComparer.OrdinalIgnoreCase);
    private LifeOsRuntimeState _state = LifeOsRuntimeState.IdleMonitoring;
    private string _activeConversation = "none";
    private string _currentTask = "ambient-monitor";
    private readonly List<string> _workflows = [];

    public LifeOsMultimodalRuntime(ILifeOsEventBus bus, ILifeOsAiCore aiCore)
    {
        _bus = bus;
        _aiCore = aiCore;
        foreach (var d in Devices.Where(d => d.Status is "scaffold" or "live-scaffold"))
        {
            _deviceSessions[d.Key] = "ready";
        }
    }

    public LifeOsRuntimeState CurrentState => _state;

    public IReadOnlyList<LifeOsRuntimeComponent> KernelComponents { get; } =
    [
        new("event", "Event Manager", "Event scheduling & stream processing"),
        new("device", "Device Manager", "Device registry & capability discovery"),
        new("context", "Context Manager", "Cross-device context synchronization"),
        new("ai-scheduler", "AI Scheduler", "AI execution & resource timing"),
        new("memory", "Memory Manager", "Memory synchronization"),
        new("resource", "Resource Manager", "CPU/GPU/battery allocation"),
        new("security", "Security Manager", "Auth, biometrics, policy"),
        new("notification", "Notification Manager", "Priority & interrupt decisions"),
        new("plugin", "Plugin Manager", "Modality & agent plugins"),
        new("sync", "Synchronization Manager", "Unified session across devices"),
    ];

    public IReadOnlyList<LifeOsDeviceDescriptor> Devices { get; } =
    [
        D("desktop", "Desktop", "compute", "scaffold", ["screen", "mic", "keyboard"]),
        D("laptop", "Laptop", "compute", "scaffold", ["screen", "mic", "camera", "battery"]),
        D("tablet", "Tablet", "mobile", "scaffold", ["touch", "camera", "mic"]),
        D("phone", "Phone", "mobile", "live-scaffold", ["gps", "camera", "mic", "biometrics", "notifications"]),
        D("watch", "Smart Watch", "wearable", "scaffold", ["heart-rate", "motion", "notifications"]),
        D("glasses", "Smart Glasses", "wearable", "research", ["first-person-vision", "overlay", "mic"]),
        D("earbuds", "Wireless Earbuds", "audio", "scaffold", ["mic", "speaker", "wake"]),
        D("ar", "AR Headset", "immersive", "research", ["spatial", "overlay"]),
        D("vr", "VR Headset", "immersive", "research", ["spatial"]),
        D("tv", "Smart TV", "display", "scaffold", ["display", "voice"]),
        D("car", "Vehicle", "mobility", "scaffold", ["nav", "voice", "hands-free"]),
        D("robot", "Robot", "robotics", "research", ["actuators", "vision"]),
        D("iot", "IoT Devices", "home", "scaffold", ["sensors", "actuators"]),
        D("smarthome", "Smart Home Hub", "home", "scaffold", ["lights", "locks", "climate"]),
        D("industrial", "Industrial Devices", "industry", "research", ["telemetry"]),
        D("medical", "Medical Devices", "health", "research", ["vitals"], privacy: true),
    ];

    public IReadOnlyList<LifeOsModalityPipeline> ModalityPipelines { get; } =
    [
        new("voice",
            ["Microphone", "Wake Detection", "Noise Reduction", "VAD", "Speaker Recognition",
             "Language Detection", "ASR", "Intent Detection", "Context Matching",
             "Agent Execution", "Response Generation", "TTS", "Speaker"],
            ["Natural interruption", "Conversation memory", "Emotion-aware speech",
             "Adaptive speaking speed", "Context continuation", "Language switching",
             "Offline ASR", "Offline commands", "Real-time translation",
             "Speaker identification", "Voice biometrics"]),
        new("vision",
            ["Camera", "Frame Selection", "Quality Assessment", "Object Detection", "OCR",
             "Scene Understanding", "Activity Recognition", "Knowledge Graph", "Context Engine"],
            ["Object Detection", "Document Reading", "Invoice Recognition", "Business Card Scanner",
             "QR/Barcode", "Food/Product Recognition", "Gesture/Pose", "Shopping Assistant",
             "Room Mapping", "UI/Code Screenshot Analysis"]),
        new("desktop",
            ["Desktop", "Screen Capture", "Window Detection", "OCR", "UI Understanding",
             "Code Understanding", "Context Mapping", "Knowledge Graph", "AI Analysis"],
            ["Bug Detection", "Code Review", "Document Summaries", "Spreadsheet Analysis",
             "Presentation Review", "Meeting Notes", "Email Drafting", "Workflow Automation"]),
        new("mobile",
            ["GPS", "Camera", "Microphone", "IMU", "Bluetooth", "Wi-Fi", "Battery",
             "Notifications", "NFC", "Health APIs", "Biometrics"],
            ["Pocket", "Driving", "Travel", "Workout", "Meeting", "Sleep", "Navigation", "Emergency"]),
        new("glasses",
            ["First-person Camera", "Recognition", "Permission Check", "Context",
             "Relationship", "Suggested Conversation", "Display Overlay"],
            ["Visual navigation", "Live translation", "Captions", "Barcode reading",
             "Instruction overlay", "Remote assistance"]),
        new("wearable",
            ["Heart Rate", "SpO2", "Temperature", "Stress", "Sleep", "Motion", "Calories", "Respiration"],
            ["Workout Started", "Stress Increased", "Abnormal Heart Rate", "Poor Sleep",
             "Recovery Complete", "Hydration/Standing/Medication Reminders"]),
        new("smarthome",
            ["Presence", "Scene", "Device Commands", "Energy", "Security"],
            ["Lights", "Climate", "Locks", "Cameras", "Speakers", "Appliances", "Solar"]),
        new("vehicle",
            ["Vehicle Started", "Destination", "Traffic", "Fuel/Battery", "Meeting Schedule",
             "Optimal Route", "Voice Guidance"],
            ["Navigation", "Traffic Prediction", "Charging Planning", "Hands-Free Messaging",
             "Maintenance Reminders", "Meeting ETA"]),
    ];

    public IReadOnlyList<LifeOsPerformanceTarget> PerformanceTargets { get; } =
    [
        new("Wake Word Detection", "< 100 ms", 100),
        new("Voice Activity Detection", "< 50 ms", 50),
        new("Speech-to-Text (first token)", "< 300 ms", 300),
        new("Intent Classification", "< 150 ms", 150),
        new("Agent Routing", "< 100 ms", 100),
        new("Memory Retrieval", "< 200 ms", 200),
        new("Local UI Response", "< 100 ms", 100),
        new("Cloud AI Response (streaming start)", "< 800 ms", 800),
        new("Device Context Sync", "< 500 ms", 500),
        new("Notification Decision", "< 100 ms", 100),
    ];

    public IReadOnlyList<string> InteractionModes { get; } =
    [
        "Voice", "Chat", "Visual Overlay", "Desktop Assistant", "Notification",
        "Automation", "Email", "Calendar", "Smart Display", "Wearable"
    ];

    public LifeOsSyncSnapshot UnifiedSession => new(
        SessionId: "lifeos-unified-session",
        ConnectedDevices: _deviceSessions.Keys.OrderBy(k => k).ToList(),
        ActiveConversation: _activeConversation,
        CurrentTask: _currentTask,
        RunningWorkflows: _workflows.ToList(),
        SyncedAt: DateTimeOffset.UtcNow);

    public async Task<LifeOsRuntimeTickResult> ProcessInputAsync(
        LifeOsEvent input,
        string? deviceKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var tickId = $"RT-{input.EventId}";
        var device = deviceKey ?? InferDevice(input);
        _deviceSessions[device] = "active";

        var pipeline = new List<string>
        {
            "Runtime Input Manager",
            "Event Stream Processor",
            "Context Synchronizer",
            "Cognitive Processing Engine",
            "Multi-Agent Orchestrator",
            "Response Generation Layer"
        };

        _state = LifeOsRuntimeState.MonitorEvents;
        await _bus.PublishAsync(input, cancellationToken).ConfigureAwait(false);

        _state = LifeOsRuntimeState.AnalyzeContext;
        var channel = input.Type switch
        {
            LifeOsEventType.VoiceEvent => "voice",
            LifeOsEventType.VisionEvent => "vision",
            LifeOsEventType.ScreenEvent => "desktop",
            LifeOsEventType.HealthEvent => "wearable",
            LifeOsEventType.LocationEvent => "mobile",
            LifeOsEventType.NotificationEvent => "notification",
            LifeOsEventType.AutomationEvent => "smarthome",
            _ => "multimodal"
        };

        var transcript = input.Payload.GetValueOrDefault("transcript");
        if (!string.IsNullOrWhiteSpace(transcript))
        {
            _activeConversation = transcript;
            _currentTask = "voice-assist";
        }

        _state = LifeOsRuntimeState.Reason;
        // Optional cognitive handoff (scaffold)
        _ = _aiCore;

        LifeOsNotificationDecision? note = null;
        if (input.Type == LifeOsEventType.NotificationEvent
            || input.Payload.ContainsKey("notification"))
        {
            note = ClassifyNotification(
                input.Payload.GetValueOrDefault("title") ?? "Alert",
                input.Payload.GetValueOrDefault("sender") ?? "system",
                _currentTask);
            pipeline.Add("Notification Intelligence");
        }

        _state = LifeOsRuntimeState.Respond;
        if (!_workflows.Contains($"flow:{channel}", StringComparer.Ordinal))
        {
            _workflows.Add($"flow:{channel}");
        }

        _state = LifeOsRuntimeState.Learn;
        var summary =
            $"Runtime tick on {device}/{channel}: state→{_state}; " +
            $"sync devices={_deviceSessions.Count}; task={_currentTask}";

        _state = LifeOsRuntimeState.IdleMonitoring;
        return new LifeOsRuntimeTickResult(
            tickId,
            _state,
            channel,
            summary,
            note,
            UnifiedSession,
            pipeline);
    }

    public LifeOsNotificationDecision ClassifyNotification(
        string title,
        string sender,
        string activityContext)
    {
        var id = $"NTF-{DateTimeOffset.UtcNow:HHmmssfff}";
        var focus = activityContext.Contains("coding", StringComparison.OrdinalIgnoreCase)
                    || activityContext.Contains("meeting", StringComparison.OrdinalIgnoreCase);
        var critical = title.Contains("emergency", StringComparison.OrdinalIgnoreCase)
                       || sender.Equals("security", StringComparison.OrdinalIgnoreCase);

        if (critical)
        {
            return new(id, LifeOsNotificationPriority.Critical, true,
                "Critical sender/title — always interrupt", "voice+screen");
        }

        if (focus)
        {
            return new(id, LifeOsNotificationPriority.Silent, false,
                "Focus/meeting activity — defer delivery", "queue");
        }

        if (sender.Contains("calendar", StringComparison.OrdinalIgnoreCase))
        {
            return new(id, LifeOsNotificationPriority.High, true,
                "Calendar urgency", "screen");
        }

        return new(id, LifeOsNotificationPriority.Normal, true,
            "Default delivery with learning hook", "notification");
    }

    public object FullPart4Digest() => new
    {
        ok = true,
        part = 4,
        title = "Multimodal Runtime & Human Interaction System",
        chapters = Enumerable.Range(26, 16).ToArray(),
        chapterTitles = new Dictionary<int, string>
        {
            [26] = "Multimodal Runtime Architecture",
            [27] = "Runtime Kernel",
            [28] = "Device Ecosystem",
            [29] = "Voice Intelligence Platform",
            [30] = "Vision Intelligence Platform",
            [31] = "Desktop Intelligence",
            [32] = "Mobile Intelligence",
            [33] = "Smart Glasses Runtime",
            [34] = "Wearable Intelligence",
            [35] = "Smart Home Runtime",
            [36] = "Vehicle Intelligence",
            [37] = "Notification Intelligence",
            [38] = "Human Interaction Manager",
            [39] = "Real-Time Synchronization Engine",
            [40] = "Runtime State Machine",
            [41] = "Runtime Performance Targets"
        },
        architecture = new
        {
            flow = new[]
            {
                "Human World", "Input Channels", "Runtime Input Manager",
                "Event Stream Processor", "Context Synchronizer",
                "Cognitive Processing Engine", "Multi-Agent Orchestrator",
                "Response Generation Layer"
            }
        },
        kernel = KernelComponents,
        devices = Devices,
        modalities = ModalityPipelines,
        wakeModes = Enum.GetNames<LifeOsWakeMode>(),
        interactionModes = InteractionModes,
        conversationPrinciples = new[]
        {
            "Maintain context across devices",
            "Ask only when necessary",
            "Avoid repetitive confirmations",
            "Prefer proactive suggestions with clear user control",
            "Respect quiet hours and focus modes",
            "Explain significant actions before executing them"
        },
        stateMachine = Enum.GetNames<LifeOsRuntimeState>(),
        performanceTargets = PerformanceTargets,
        sync = UnifiedSession,
        smartHomeExample = new[]
        {
            "User Sleeping", "Lights Off", "Phone Silent", "Doors Locked",
            "Temperature Adjusted", "Morning Alarm Prepared"
        },
        status = "scaffold"
    };

    private static string InferDevice(LifeOsEvent input) => input.Type switch
    {
        LifeOsEventType.VoiceEvent when input.Source.Contains("Wearable", StringComparison.OrdinalIgnoreCase)
            => "earbuds",
        LifeOsEventType.VoiceEvent => "phone",
        LifeOsEventType.VisionEvent => "glasses",
        LifeOsEventType.ScreenEvent => "desktop",
        LifeOsEventType.HealthEvent => "watch",
        LifeOsEventType.LocationEvent => "phone",
        LifeOsEventType.AutomationEvent => "smarthome",
        _ => "phone"
    };

    private static LifeOsDeviceDescriptor D(
        string key, string title, string category, string status, string[] caps, bool privacy = false)
        => new(key, title, category, status,
            privacy ? caps.Append("privacy-sensitive").ToArray() : caps);
}
