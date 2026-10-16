using System.Globalization;
using System.Net;
using System.Xml;
using System.Xml.Linq;

namespace Synups.Core;

public sealed record TargetDefinition(string Name, IPAddress Address);

public sealed record GroupDefinition(
    string Name, TimeSpan FailAfter, TimeSpan PollInterval, TimeSpan Timeout,
    IReadOnlyList<TargetDefinition> Targets, int DiscoveryRules);

/// <summary>
/// Reads WirePeep's actual configuration, without Profile's synchronous device discovery.
/// This is configuration inspection, not a measurement or an imported user profile.
/// Original artifact copyright (c) 2020 Bill Menees, MIT; see repository LICENSE.
/// </summary>
public static class DefaultProfileReader
{
    public static IReadOnlyList<GroupDefinition> LoadBundled()
    {
        using var stream = typeof(DefaultProfileReader).Assembly
            .GetManifestResourceStream("Synups.DefaultProfile.xml")
            ?? throw new InvalidOperationException("The original default profile is missing.");
        return Read(stream);
    }

    public static IReadOnlyList<GroupDefinition> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 1_000_000,
            CloseInput = false,
        });
        var root = XElement.Load(reader);
        if (root.Name != "Defaults")
            throw new FormatException("Expected a WirePeep Defaults profile.");
        var groups = new List<GroupDefinition>();
        foreach (var group in root.Elements("PeerGroup"))
        {
            var targets = new List<TargetDefinition>();
            foreach (var location in group.Elements("Location"))
            {
                var name = RequiredName(location);
                var ordinal = 0;
                foreach (var address in location.Elements("Address"))
                {
                    if (!IPAddress.TryParse(address.Value.Trim(), out var ip))
                        throw new FormatException($"Invalid IP address for {name}.");
                    ordinal++;
                    // Preserve WirePeep's multiple-address name suffix convention.
                    targets.Add(new TargetDefinition(ordinal == 1 ? name : $"{name} #{ordinal}", ip));
                }
            }
            groups.Add(new GroupDefinition(RequiredName(group),
                TimeSpan.FromSeconds(Positive(group, "FailSeconds", 10)),
                TimeSpan.FromSeconds(Positive(group, "PollSeconds", 5)),
                TimeSpan.FromMilliseconds(Positive(group, "WaitMilliseconds", 200)),
                targets.AsReadOnly(), group.Elements("Find").Count()));
        }
        return groups.AsReadOnly();
    }

    private static string RequiredName(XElement element) =>
        !string.IsNullOrWhiteSpace((string?)element.Attribute("Name"))
            ? ((string)element.Attribute("Name")!).Trim()
            : throw new FormatException("A group or target name is missing.");

    private static int Positive(XElement element, string attribute, int fallback)
    {
        var text = (string?)element.Attribute(attribute);
        if (text is null) return fallback;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
            throw new FormatException($"{attribute} must be a positive integer.");
        return value;
    }
}
