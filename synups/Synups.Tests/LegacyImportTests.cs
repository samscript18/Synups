using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Synups.Core.Monitoring;
using Synups.Core.Persistence;
using Synups.Desktop;
using Xunit;
using static Synups.Tests.TestConfig;

namespace Synups.Tests;

public class LegacyImportTests
{
    internal static byte[] GenuineFixture()
    {
        using var stream = typeof(LegacyImportTests).Assembly.GetManifestResourceStream("Synups.Tests.Fixtures.WirePeep-5.1.2-windows.stgx")!;
        using var bytes = new MemoryStream(); stream.CopyTo(bytes); return bytes.ToArray();
    }
    [Fact]
    public void ActualWindowsFixturePreservesAllObservedGroupsTargetsIdsAndTimings()
    {
        var bytes = GenuineFixture();
        Assert.Equal("37b66a46f1fffaee7912b2733d00d76ef9fb06589635bdb7f8b6e7020d0ed7c1", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        var preview = LegacySettingsReader.Read(bytes, "Original.stgx");
        var group = Assert.Single(preview.Configuration.Groups);
        Assert.Equal(Guid.Parse("3603b3a3-eb28-464e-944f-4aff0480f36d"), group.Id);
        Assert.Equal(15, group.Targets.Count); Assert.True(group.Enabled);
        Assert.Equal(5000, group.PollMilliseconds); Assert.Equal(10000, group.FailMilliseconds); Assert.Equal(200, group.TimeoutMilliseconds);
        Assert.Equal(Guid.Parse("4b406024-64bc-44df-9d41-a98bf9c8acc1"), group.Targets[0].Id); Assert.Equal("8.8.8.8", group.Targets[0].Address);
        Assert.Contains("nothing", preview.Description.ToLowerInvariant());
        Assert.Equal(bytes, GenuineFixture()); // Never rewrite the actual source fixture.
    }
    [Theory]
    [InlineData("missing-reference")]
    [InlineData("duplicate-id")]
    [InlineData("duplicate-setting")]
    [InlineData("negative-time")]
    [InlineData("fractional-millisecond")]
    [InlineData("empty")]
    public void MalformedProfilesFailRatherThanSilentlyDroppingData(string fault)
    {
        using var input = new MemoryStream(GenuineFixture());
        var root = XElement.Load(input);
        var profile = root.Elements("Settings").Single(e => (string?)e.Attribute("Name") == "Profile");
        var groups = profile.Elements("Settings").Single(e => (string?)e.Attribute("Name") == "PeerGroups");
        var targets = profile.Elements("Settings").Single(e => (string?)e.Attribute("Name") == "Locations");
        var group = groups.Elements().First(); var target = targets.Elements().First();
        if (fault == "missing-reference") target.Elements("Setting").First().SetAttributeValue("Value", Guid.NewGuid());
        if (fault == "duplicate-id") targets.Add(new XElement(target));
        if (fault == "duplicate-setting") group.Add(new XElement(group.Elements("Setting").First()));
        if (fault == "negative-time") group.Elements("Setting").Single(e => (string?)e.Attribute("Name") == "Fail").SetAttributeValue("Value", "-00:00:01");
        if (fault == "fractional-millisecond") group.Elements("Setting").Single(e => (string?)e.Attribute("Name") == "Wait").SetAttributeValue("Value", "00:00:00.0000001");
        if (fault == "empty") { groups.RemoveNodes(); targets.RemoveNodes(); }
        Assert.Throws<ArgumentException>(() => LegacySettingsReader.Read(Encoding.UTF8.GetBytes(root.ToString()), "invalid.stgx"));
    }
    [Fact]
    public void XmlEntitiesWrongProductsAndOversizedSourcesAreRejected()
    {
        Assert.Throws<XmlException>(() => LegacySettingsReader.Read(Encoding.UTF8.GetBytes("<!DOCTYPE Settings [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><Settings Name='WirePeep'>&x;</Settings>"), "bad.stgx"));
        Assert.Throws<ArgumentException>(() => LegacySettingsReader.Read(Encoding.UTF8.GetBytes("<Settings Name='Other'/>"), "other.stgx"));
        Assert.Throws<IOException>(() => LegacySettingsReader.Read(new byte[LegacySettingsReader.MaximumBytes + 1], "large.stgx"));
    }
    [AvaloniaFact]
    public async Task PreviewConfirmationBackupAndReloadPreserveOriginalEvidenceWithoutProbing()
    {
        var root = Path.Combine(Path.GetTempPath(), "Synups-actual-import-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        var path = Path.Combine(root, "configuration.json"); var source = Path.Combine(root, "WirePeep.stgx");
        var beforeConfig = Config(Group()); var store = new ConfigurationStore(path); await store.SaveAsync(beforeConfig);
        var before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(source, GenuineFixture(), TestContext.Current.CancellationToken);
        var probe = new ScriptedProbe(); var vm = new MainWindowViewModel(new MonitoringService(probe), store); var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show(); await UntilAsync(() => vm.Message.Contains("Configuration loaded"));
            await vm.PreviewLegacyImportAsync(source); Assert.True(vm.CanImportLegacy);
            Assert.Contains("15 targets", window.FindControl<TextBlock>("LegacyImportPreviewText")!.Text);
            Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            await vm.ImportLegacyAsync(false); Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            Assert.Empty(Directory.GetFiles(root, "*.bak"));
            await vm.ImportLegacyAsync(true);
            var backup = Assert.Single(Directory.GetFiles(root, "*.before-import-*.bak"));
            Assert.Equal(before, await File.ReadAllBytesAsync(backup, TestContext.Current.CancellationToken));
            Assert.Equal(15, Assert.Single((await new ConfigurationStore(path).LoadAsync()).Configuration!.Groups).Targets.Count);
            Assert.Equal(GenuineFixture(), await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken));
            Assert.False(vm.IsRunning); Assert.False(vm.CanImportLegacy); Assert.Empty(probe.Addresses);
            await vm.PreviewLegacyImportAsync(source); Assert.True(vm.CanImportLegacy);
            await File.WriteAllTextAsync(source, "invalid", TestContext.Current.CancellationToken); await vm.PreviewLegacyImportAsync(source);
            Assert.False(vm.CanImportLegacy); // Failed new preview cannot reuse a previous successful one.
        }
        finally { window.ExitConfirmation = () => Task.FromResult(true); window.Close(); await UntilAsync(() => !window.IsVisible); await vm.DisposeAsync(); Directory.Delete(root, true); }
    }
    [Fact]
    public async Task ImportCannotOverwriteCorruptDestination()
    {
        var root = Path.Combine(Path.GetTempPath(), "Synups-corrupt-import-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        var path = Path.Combine(root, "config.json"); await File.WriteAllTextAsync(path, "preserve invalid bytes", TestContext.Current.CancellationToken);
        try
        {
            await Assert.ThrowsAsync<IOException>(() => new ConfigurationStore(path).SaveWithBackupAsync(LegacySettingsReader.Read(GenuineFixture(), "fixture").Configuration));
            Assert.Equal("preserve invalid bytes", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)); Assert.Empty(Directory.GetFiles(root, "*.bak"));
        }
        finally { Directory.Delete(root, true); }
    }
}
