using System.Globalization;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using Synups.Core.Monitoring;

namespace Synups.Core.Persistence;

public sealed record LegacyProfilePreview(MonitorConfiguration Configuration, string SourceSha256, string SourceName)
{
    public string Description => $"Read-only WirePeep profile preview: {Configuration.Groups.Count} groups, {Configuration.Groups.Sum(g => g.Targets.Count)} targets.\n" +
        $"Source: {SourceName}\nSHA-256: {SourceSha256}\n" +
        "Import replaces the Synups target/group configuration after confirmation and backs up its existing bytes. Original IDs and timings are retained. " +
        "WirePeep's file, Synups history, desktop preferences and OS startup settings are not modified. Nothing is probed or started.\n\n" +
        string.Join("\n", Configuration.Groups.Select(g => $"{g.Name} [{g.Id}] · poll {g.PollMilliseconds}ms · grace {g.FailMilliseconds}ms · timeout {g.TimeoutMilliseconds}ms\n" +
            string.Join("\n", g.Targets.Select(t => $"  {t.Name}: {t.Address} [{t.Id}]"))));
}

/// <summary>Profile-only reader for the actual Menees 5.1.2 .stgx schema captured from untouched WirePeep on Windows.</summary>
public static class LegacySettingsReader
{
    public const int MaximumBytes = 2_000_000;
    public static LegacyProfilePreview ReadFile(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > MaximumBytes) throw new IOException("WirePeep settings exceed 2MB. Source preserved.");
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = file.Read(buffer)) != 0)
        {
            if (bytes.Length + count > MaximumBytes) throw new IOException("WirePeep settings exceed 2MB. Source preserved.");
            bytes.Write(buffer, 0, count);
        }
        return Read(bytes.ToArray(), Path.GetFileName(path));
    }
    public static LegacyProfilePreview Read(byte[] bytes, string sourceName)
    {
        if (bytes.Length > MaximumBytes) throw new IOException("WirePeep settings exceed 2MB.");
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumBytes });
        var root = XElement.Load(reader);
        if (root.Name != "Settings" || (string?)root.Attribute("Name") != "WirePeep")
            throw new ArgumentException("Choose actual WirePeep Menees .stgx settings. Other formats are unsupported.");
        static XElement Node(XElement parent, string name)
        {
            var matches = parent.Elements("Settings").Where(n => (string?)n.Attribute("Name") == name).ToArray();
            return matches.Length == 1 ? matches[0] : throw new ArgumentException($"Expected exactly one '{name}' settings node; source preserved.");
        }
        static string Value(XElement node, string name)
        {
            var matches = node.Elements("Setting").Where(n => (string?)n.Attribute("Name") == name).ToArray();
            return matches.Length == 1 && matches[0].Attribute("Value") is { } value ? value.Value : throw new ArgumentException($"Missing or duplicate '{name}' value.");
        }
        static Guid Id(string? value) => Guid.TryParse(value, out var id) && id != Guid.Empty ? id : throw new ArgumentException("Invalid legacy identity.");
        static int Milliseconds(string text)
        {
            if (!TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, out var value) || value.Ticks % TimeSpan.TicksPerMillisecond != 0 || value.TotalMilliseconds < 1 || value.TotalMilliseconds > int.MaxValue)
                throw new ArgumentException("Unsupported legacy timing; use positive invariant TimeSpan values in whole milliseconds.");
            return checked((int)value.TotalMilliseconds);
        }
        var profile = Node(root, "Profile");
        var groupsNode = Node(profile, "PeerGroups");
        var targetsNode = Node(profile, "Locations");
        var groups = groupsNode.Elements("Settings").Select(n => new GroupConfiguration(Id((string?)n.Attribute("Name")), Value(n, "Name"), false,
            Milliseconds(Value(n, "Poll")), Milliseconds(Value(n, "Fail")), Milliseconds(Value(n, "Wait")), [])).ToArray();
        if (groups.Length > 64) throw new ArgumentException("At most 64 legacy groups can be imported.");
        var targets = targetsNode.Elements("Settings").Select(n => (Group: Id(Value(n, "PeerGroupId")), Target: new TargetConfiguration(Id((string?)n.Attribute("Name")), Value(n, "Name"), Value(n, "Address")))).ToArray();
        if (groups.Length == 0 || targets.Length == 0) throw new ArgumentException("Empty legacy profiles are not imported; use explicit configuration editing instead.");
        if (targets.Length > 512 || targets.Any(t => !groups.Any(g => g.Id == t.Group)))
            throw new ArgumentException("Too many legacy targets or a missing group reference; nothing was discarded or imported.");
        var config = new MonitorConfiguration(1, groups.Select(g => g with
        { Targets = targets.Where(t => t.Group == g.Id).Select(t => t.Target).ToArray(), Enabled = targets.Any(t => t.Group == g.Id) }).ToArray()).ValidatedCopy();
        return new(config, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), sourceName);
    }
}
