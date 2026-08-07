namespace EcomAE.Platform.LifeOs.Demo;

public sealed record LifeOsDemoScenario(
    string Key,
    string Title,
    string Persona,
    string Transcript,
    string Domain,
    string Story,
    IReadOnlyList<string> SampleContext);

public sealed record LifeOsDemoRunResult(
    string ScenarioKey,
    string Transcript,
    string Story,
    object Perceive,
    object Decide,
    object Act,
    object Learn,
    object SampleData,
    IReadOnlyList<string> HowItWorks);
