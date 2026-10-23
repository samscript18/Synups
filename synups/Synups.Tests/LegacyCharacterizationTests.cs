// Collaborators only. State machines are linked unmodified from Bill Menees' MIT source.
using System.Net;
using Xunit;

namespace Menees { } // Original state source has a using, but needs no Menees API here.

namespace WirePeep
{
    public sealed record PeerGroup(string Name, TimeSpan Fail, TimeSpan Poll, TimeSpan Wait)
    {
        // Verbatim legacy predicate, copyright Bill Menees, MIT.
        public bool CanPoll(DateTime now, DateTime last) => (now - last) > (Poll - TimeSpan.FromMilliseconds(20));
    }
    public sealed record Location(PeerGroup PeerGroup, string Name, IPAddress Address);
    public sealed class Pinger(TimeSpan wait) : IDisposable
    {
        public bool? TryPing(IPAddress address, out TimeSpan latency)
        { latency = TimeSpan.Zero; throw new InvalidOperationException($"Test must use legacy simulation ({wait})."); }
        public void Dispose() { }
    }
}

namespace Synups.Tests
{
    public class LegacyCharacterizationTests
    {
        private static readonly DateTime Epoch = new(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);
        private static (WirePeep.PeerGroupState Group, WirePeep.LocationState[] Targets) Create(int count = 1)
        {
            var config = new WirePeep.PeerGroup("Legacy", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(200));
            return (new(config), Enumerable.Range(0, count).Select(i => new WirePeep.LocationState(new(config, i.ToString(), IPAddress.Loopback))).ToArray());
        }

        [Fact]
        public void GraceBoundaryAndOneRecoveryTransitionMatchOriginal()
        {
            var (g, targets) = Create();
            g.Update(Epoch, targets, WirePeep.ConnectionState.Disconnected);
            Assert.False(g.IsFailed);
            g.Update(Epoch.AddSeconds(9), targets, WirePeep.ConnectionState.Disconnected);
            Assert.False(g.IsFailed);
            g.Update(Epoch.AddSeconds(10), targets, WirePeep.ConnectionState.Disconnected);
            Assert.True(g.IsFailed);
            Assert.Equal(Epoch.AddSeconds(10), g.IsFailedChanged);
            g.Update(Epoch.AddSeconds(11), targets, WirePeep.ConnectionState.Disconnected);
            Assert.Equal(Epoch.AddSeconds(10), g.IsFailedChanged);
            g.Update(Epoch.AddSeconds(12), targets, WirePeep.ConnectionState.Connected);
            Assert.False(g.IsFailed);
            Assert.Equal(Epoch.AddSeconds(12), g.IsFailedChanged);
        }

        [Fact]
        public void RecoveryBeforeThresholdDoesNotCreateFailure()
        {
            var (g, targets) = Create();
            g.Update(Epoch, targets, WirePeep.ConnectionState.Disconnected);
            g.Update(Epoch.AddSeconds(1), targets, WirePeep.ConnectionState.Connected);
            Assert.False(g.IsFailed);
            Assert.Null(g.IsFailedChanged);
        }

        [Fact]
        public void OriginalUnavailableClearsConfirmedOutageWithoutSuccess()
        {
            var (g, targets) = Create();
            g.Update(Epoch, targets, WirePeep.ConnectionState.Disconnected);
            g.Update(Epoch.AddSeconds(10), targets, WirePeep.ConnectionState.Disconnected);
            Assert.True(g.IsFailed);
            g.Update(Epoch.AddSeconds(11), targets, WirePeep.ConnectionState.Unavailable);
            Assert.False(g.IsFailed); // Deliberately improved in migrated engine.
        }

        [Fact]
        public void RotationStopsOnSuccessAndMarksOtherSamplesStale()
        {
            var (g, targets) = Create(3);
            g.Update(Epoch, targets, WirePeep.ConnectionState.Connected);
            Assert.Equal(0, targets[0].UpdateCounter);
            Assert.Equal(-2, targets[1].UpdateCounter);
            g.Update(Epoch.AddSeconds(1), targets, WirePeep.ConnectionState.Connected);
            Assert.Equal(1, targets[1].UpdateCounter);
            Assert.NotEqual(g.UpdateCounter, targets[0].UpdateCounter);
            g.Update(Epoch.AddSeconds(2), targets, WirePeep.ConnectionState.Disconnected);
            Assert.All(targets, t => Assert.Equal(g.UpdateCounter, t.UpdateCounter));
        }

        [Fact]
        public void PollToleranceIsStrictAndSkippedCallsDoNotRotate()
        {
            var (g, targets) = Create();
            g.Update(Epoch, targets, WirePeep.ConnectionState.Connected);
            g.Update(Epoch.AddMilliseconds(980), targets, WirePeep.ConnectionState.Disconnected);
            Assert.Equal(0, g.UpdateCounter);
            g.Update(Epoch.AddMilliseconds(981), targets, WirePeep.ConnectionState.Disconnected);
            Assert.Equal(1, g.UpdateCounter);
        }
    }
}
