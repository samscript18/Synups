using System.Text;
using Synups.Core.Alerts;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Synups.Desktop.Integrations;
using Xunit;

namespace Synups.Tests;

public class AlertSoundTests
{
    [Fact]
    public void SoundsAreExplicitPerTransitionAndReuseAlertDeduplication()
    {
        var prefs = new AppPreferences();
        var outage = new MonitorEvent(1, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, MonitorEventKind.OutageStarted, "Test-only transition");
        var recovery = outage with { Sequence = 2, Id = Guid.NewGuid(), Kind = MonitorEventKind.Recovery };
        var policy = new AlertPolicy(); var messages = policy.Select([outage, recovery], prefs);
        Assert.All(messages, m => Assert.False(SoundAlertPolicy.ShouldPlay(m, prefs)));
        Assert.Empty(policy.Select([outage, recovery], prefs with { PlayOutageSound = true }));
        prefs = prefs with { PlayOutageSound = true };
        Assert.True(SoundAlertPolicy.ShouldPlay(messages[0], prefs)); Assert.False(SoundAlertPolicy.ShouldPlay(messages[1], prefs));
        Assert.False(SoundAlertPolicy.ShouldPlay(messages[0] with { Kind = MonitorEventKind.ProbeError }, prefs));
        Assert.Throws<ArgumentException>(() => prefs.Validate());
        Assert.NotNull((prefs with { AlertSoundFile = Path.Combine(Path.GetTempPath(), "chosen.wav") }).Validate());
    }
    [Fact]
    public async Task InvalidMissingAndCancelledSoundRequestsDoNotStartPlayback()
    {
        var sink = new AlertSoundSink();
        var message = new AlertMessage(Guid.NewGuid(), "Test-only", "relative.wav", MonitorEventKind.OutageStarted);
        Assert.False((await sink.SendAsync(message, TestContext.Current.CancellationToken)).RequestAccepted);
        message = message with { Body = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav") };
        Assert.False((await sink.SendAsync(message, TestContext.Current.CancellationToken)).RequestAccepted);
        await File.WriteAllTextAsync(message.Body, "This is not a WAVE file", TestContext.Current.CancellationToken);
        try
        {
            Assert.Contains("not a RIFF/WAVE", (await sink.SendAsync(message, TestContext.Current.CancellationToken)).Status);
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sink.SendAsync(message, cancellation.Token));
        }
        finally { File.Delete(message.Body); }
    }
    [Fact]
    public async Task SoundPreferencesSurviveRestartWithoutEnablingOtherIntegrations()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Synups-sound-" + Guid.NewGuid()); var path = Path.Combine(directory, "prefs.json");
        var expected = new AppPreferences { PlayOutageSound = true, PlayRecoverySound = true, AlertSoundFile = Path.Combine(directory, "explicit.wav") };
        try
        {
            await new PreferencesStore(path).SaveAsync(expected);
            var loaded = (await new PreferencesStore(path).LoadAsync()).Preferences;
            Assert.Equal(expected, loaded); Assert.False(loaded.NativeNotifications); Assert.False(loaded.MonitorOnLaunch);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public async Task NativeSoundRequestCompletesOrReportsAnHonestUnsupportedAudioSession()
    {
        if (Environment.GetEnvironmentVariable("SYNUPS_SOUND_TESTS") != "1") Assert.Skip("Explicit native audio request only; does not certify audible delivery.");
        var path = Path.Combine(Path.GetTempPath(), "Synups-explicit-audio-" + Guid.NewGuid() + ".wav");
        // Test-only 100ms PCM tone. Not an application measurement, outage, generated production asset or OS notification.
        using (var file = File.Create(path))
        using (var writer = new BinaryWriter(file, Encoding.ASCII))
        {
            const int rate = 8000, samples = 800;
            writer.Write("RIFF"u8); writer.Write(36 + samples * 2); writer.Write("WAVEfmt "u8); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16); writer.Write("data"u8); writer.Write(samples * 2);
            for (var i = 0; i < samples; i++) writer.Write((short)(Math.Sin(i * Math.PI * 2 * 440 / rate) * 1000));
        }
        try
        {
            var result = await new AlertSoundSink().SendAsync(new(Guid.NewGuid(), "Explicit native audio test", path, MonitorEventKind.OutageStarted), TestContext.Current.CancellationToken);
            Assert.False(string.IsNullOrWhiteSpace(result.Status));
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { result.RequestAccepted, result.Status, audible_delivery_verified = false, os = Environment.OSVersion.ToString() }));
            if (result.RequestAccepted) Assert.Contains("Audible delivery is not verified", result.Status);
            else Assert.Contains("In-app", result.Status);
        }
        finally { File.Delete(path); }
    }
}
