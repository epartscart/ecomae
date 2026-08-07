using EcomAE.Platform.LifeOs;
using EcomAE.Platform.LifeOs.Models;
using EcomAE.Platform.LifeOs.Part4;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsPart4RuntimeTests
{
    private static ServiceProvider Build()
    {
        var sc = new ServiceCollection();
        sc.AddLifeOsPart2Scaffold();
        return sc.BuildServiceProvider();
    }

    [Fact]
    public void KernelHasTenManagers()
    {
        using var sp = Build();
        var rt = sp.GetRequiredService<ILifeOsMultimodalRuntime>();
        Assert.Equal(10, rt.KernelComponents.Count);
        Assert.Contains(rt.KernelComponents, c => c.Key == "event");
        Assert.Contains(rt.KernelComponents, c => c.Key == "sync");
    }

    [Fact]
    public void DeviceEcosystemCoversMajorFormFactors()
    {
        using var sp = Build();
        var devices = sp.GetRequiredService<ILifeOsMultimodalRuntime>().Devices;
        Assert.True(devices.Count >= 15);
        Assert.Contains(devices, d => d.Key == "phone");
        Assert.Contains(devices, d => d.Key == "glasses");
        Assert.Contains(devices, d => d.Key == "car");
        Assert.Contains(devices, d => d.Key == "smarthome");
    }

    [Fact]
    public void ModalityPipelinesIncludeVoiceVisionDesktop()
    {
        using var sp = Build();
        var pipes = sp.GetRequiredService<ILifeOsMultimodalRuntime>().ModalityPipelines;
        Assert.Contains(pipes, p => p.Modality == "voice" && p.Stages.Contains("ASR"));
        Assert.Contains(pipes, p => p.Modality == "vision" && p.Stages.Contains("OCR"));
        Assert.Contains(pipes, p => p.Modality == "desktop");
        Assert.Equal(8, pipes.Count);
    }

    [Fact]
    public void PerformanceTargetsMatchChapter41Budgets()
    {
        using var sp = Build();
        var targets = sp.GetRequiredService<ILifeOsMultimodalRuntime>().PerformanceTargets;
        Assert.Equal(10, targets.Count);
        Assert.Contains(targets, t => t.Component.Contains("Wake Word") && t.BudgetMs == 100);
        Assert.Contains(targets, t => t.Component.Contains("Notification") && t.BudgetMs == 100);
    }

    [Fact]
    public void NotificationIntelligenceDefersDuringFocus()
    {
        using var sp = Build();
        var rt = sp.GetRequiredService<ILifeOsMultimodalRuntime>();
        var deferred = rt.ClassifyNotification("Promo", "marketing", "coding");
        Assert.Equal(LifeOsNotificationPriority.Silent, deferred.Priority);
        Assert.False(deferred.Interrupt);

        var critical = rt.ClassifyNotification("emergency lockdown", "security", "coding");
        Assert.Equal(LifeOsNotificationPriority.Critical, critical.Priority);
        Assert.True(critical.Interrupt);
    }

    [Fact]
    public async Task RuntimeTickPublishesAndSyncsDevices()
    {
        using var sp = Build();
        var rt = sp.GetRequiredService<ILifeOsMultimodalRuntime>();
        var tick = await rt.ProcessInputAsync(LifeOsEventFactory.SampleVoice());

        Assert.StartsWith("RT-", tick.TickId);
        Assert.Equal("voice", tick.Channel);
        Assert.Contains("Runtime Input Manager", tick.Pipeline);
        Assert.Contains("phone", tick.Sync.ConnectedDevices);
        Assert.Equal(LifeOsRuntimeState.IdleMonitoring, tick.State);
    }

    [Fact]
    public void Part4DigestCoversChapters26To41()
    {
        using var sp = Build();
        var json = System.Text.Json.JsonSerializer.Serialize(
            sp.GetRequiredService<ILifeOsMultimodalRuntime>().FullPart4Digest());
        Assert.Contains("Runtime Kernel", json);
        Assert.Contains("Voice Intelligence", json);
        Assert.Contains("Notification Intelligence", json);
        Assert.Contains("Performance Targets", json);
        Assert.Contains("State Machine", json);
    }
}
