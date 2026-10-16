using System.Text;
using Synups.Core;
using Synups.Desktop;
using Xunit;

namespace Synups.Tests;

public class ProfileTests
{
    [Fact]
    public void OriginalArtifactPreservesAddressesTimingsAndDiscoveryRules()
    {
        var groups = DefaultProfileReader.LoadBundled();
        var internet = Assert.Single(groups, g => g.Name == "Internet");
        Assert.Equal(15, internet.Targets.Count);
        Assert.Equal("8.8.4.4", internet.Targets[1].Address.ToString());
        Assert.Equal("Google #2", internet.Targets[1].Name);
        Assert.Equal(TimeSpan.FromSeconds(10), internet.FailAfter);
        Assert.Equal(TimeSpan.FromSeconds(5), internet.PollInterval);
        Assert.Equal(TimeSpan.FromMilliseconds(200), internet.Timeout);
        Assert.All(groups.Where(g => g.DiscoveryRules > 0), g => Assert.Empty(g.Targets));
        Assert.Equal(3, groups.Sum(g => g.DiscoveryRules));
    }

    [Theory]
    [InlineData("<Other />")]
    [InlineData("<Defaults><PeerGroup Name='Bad' PollSeconds='0'/></Defaults>")]
    [InlineData("<Defaults><PeerGroup Name='Bad' WaitMilliseconds='-1'/></Defaults>")]
    [InlineData("<Defaults><PeerGroup Name='Bad'><Location Name='Bad'><Address>fake</Address></Location></PeerGroup></Defaults>")]
    [InlineData("<Defaults><PeerGroup /></Defaults>")]
    public void InvalidConfigurationIsRejected(string xml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        Assert.Throws<FormatException>(() => DefaultProfileReader.Read(stream));
    }

    [Fact]
    public void ExternalEntityDeclarationsAreRejected()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<!DOCTYPE Defaults [<!ENTITY e SYSTEM 'file:///private/etc/passwd'>]><Defaults>&e;</Defaults>"));
        Assert.Throws<System.Xml.XmlException>(() => DefaultProfileReader.Read(stream));
    }

}
