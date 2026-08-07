using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.Part3;

public sealed class LifeOsPerceptionEngine : ILifeOsPerceptionEngine
{
    public IReadOnlyList<string> SupportedInputs { get; } =
    [
        "Voice", "Camera", "Desktop", "Mobile", "Wearables", "IoT Devices", "GPS",
        "Bluetooth", "Wi-Fi", "Calendar", "Emails", "Files", "Cloud Storage",
        "Browser", "Microphone", "Motion Sensors", "Health Sensors", "Smart Home", "Vehicle"
    ];

    public LifeOsPerceptionResult Perceive(LifeOsEvent input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var modality = input.Type switch
        {
            LifeOsEventType.VoiceEvent => "voice",
            LifeOsEventType.VisionEvent => "vision",
            LifeOsEventType.ScreenEvent => "desktop",
            LifeOsEventType.HealthEvent => "health-sensor",
            LifeOsEventType.LocationEvent => "gps",
            _ => "multimodal"
        };

        var pipeline = modality == "voice"
            ? VoicePipeline(input)
            : modality == "vision"
                ? VisionPipeline(input)
                : GenericPipeline(input, modality);

        var transcript = input.Payload.GetValueOrDefault("transcript") ?? input.Type.ToString();
        var entities = ExtractEntities(transcript);

        return new LifeOsPerceptionResult(
            $"PER-{input.EventId}",
            modality,
            pipeline,
            $"semantic:{modality}:{transcript}",
            entities);
    }

    public object Digest() => new
    {
        chapter = 14,
        title = "Perception Engine",
        supportedInputs = SupportedInputs,
        voicePipeline = new[]
        {
            "Microphone", "VAD", "Noise Suppression", "Speaker ID", "ASR",
            "Language Detection", "Intent Recognition", "Entity Extraction", "Memory Matching"
        },
        visionCapabilities = new[]
        {
            "Object Detection", "Scene Understanding", "OCR", "Pose Estimation",
            "Gesture Recognition", "Face Recognition (optional)", "Environment Mapping",
            "Document Understanding", "UI Understanding", "Code Screenshot Analysis"
        },
        desktopIntelligence = new[]
        {
            "IDE", "Browser", "Word", "Excel", "PDF", "PowerPoint", "Terminal",
            "Email", "Slack", "Teams", "Zoom", "GitHub", "Figma", "VS Code",
            "JetBrains", "Docker", "Kubernetes Dashboard", "Cloud Consoles"
        },
        status = "scaffold"
    };

    private static IReadOnlyList<LifeOsPerceptionStage> VoicePipeline(LifeOsEvent input)
    {
        var t = input.Payload.GetValueOrDefault("transcript") ?? "";
        return
        [
            new("Raw Input", input.Source, 1),
            new("Normalization", "pcm→features", 0.95),
            new("Noise Reduction", "suppressed", 0.9),
            new("Voice Activity Detection", "speech", 0.93),
            new("Speaker Identification", "primary-user", 0.7),
            new("Speech Recognition", t, 0.91),
            new("Language Detection", "en", 0.88),
            new("Intent Recognition", GuessIntent(t), 0.84),
            new("Entity Extraction", string.Join(",", ExtractEntities(t)), 0.8),
            new("Memory Matching", "project:lifeos", 0.75),
            new("Semantic Representation", $"voice://{t}", 0.82),
            new("Knowledge Graph", "node:utterance", 0.7),
        ];
    }

    private static IReadOnlyList<LifeOsPerceptionStage> VisionPipeline(LifeOsEvent input) =>
    [
        new("Raw Input", input.Source, 1),
        new("Normalization", "frame-keyframe", 0.94),
        new("Classification", "scene", 0.8),
        new("Object Extraction", "ui-or-document", 0.76),
        new("OCR", "optional-text", 0.7),
        new("Context Association", "desktop-or-environment", 0.72),
        new("Semantic Representation", "vision://scene", 0.74),
        new("Knowledge Graph", "node:visual", 0.68),
    ];

    private static IReadOnlyList<LifeOsPerceptionStage> GenericPipeline(LifeOsEvent input, string modality) =>
    [
        new("Raw Input", $"{modality}:{input.Source}", 1),
        new("Normalization", modality, 0.9),
        new("Classification", input.Type.ToString(), 0.85),
        new("Object Extraction", "payload-keys", 0.8),
        new("Context Association", "crm-pending", 0.75),
        new("Semantic Representation", $"{modality}://event", 0.78),
        new("Knowledge Graph", "node:event", 0.7),
    ];

    private static string GuessIntent(string t)
    {
        if (t.Contains("schedule", StringComparison.OrdinalIgnoreCase)
            || t.Contains("meeting", StringComparison.OrdinalIgnoreCase))
        {
            return "schedule";
        }

        if (t.Contains("code", StringComparison.OrdinalIgnoreCase))
        {
            return "coding-assist";
        }

        return string.IsNullOrWhiteSpace(t) ? "ambient-observe" : "general-assist";
    }

    private static List<string> ExtractEntities(string text)
    {
        var list = new List<string>();
        if (text.Contains("meeting", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("meeting");
        }

        if (text.Contains("tomorrow", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("tomorrow");
        }

        if (text.Contains("LifeOS", StringComparison.OrdinalIgnoreCase))
        {
            list.Add("LifeOS");
        }

        if (list.Count == 0 && !string.IsNullOrWhiteSpace(text))
        {
            list.Add("utterance");
        }

        return list;
    }
}
