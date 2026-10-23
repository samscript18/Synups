// Engineering only: called solely inside the guarded isolated three-hop IPv4 fixture.
using System.Text.Json;
using Synups.Core.Diagnostics;

var output = Path.GetFullPath(args.Single());
var request = new DiagnosticRequest(Guid.NewGuid(), "10.77.3.2", [DiagnosticKind.TraceRoute],
    new() { MaximumHops = 5, ProbesPerHop = 2, ProbeTimeoutMilliseconds = 1000, IntervalMilliseconds = 10, SessionTimeoutSeconds = 20, MaximumProbes = 10 }, new(true));
var result = await new TraceRouteTest().ExecuteAsync(request, null, CancellationToken.None);
await File.WriteAllTextAsync(Path.Combine(output, "trace.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
if (result.Outcome != DiagnosticOutcome.Success || result.Hops is not { Count: 3 } hops)
    throw new InvalidOperationException($"Real three-hop trace failed: {result.Outcome}: {result.Error}");
string[] expected = ["10.77.1.2", "10.77.2.2", "10.77.3.2"];
for (var i = 0; i < expected.Length; i++)
    if (hops[i].Hop != i + 1 || !hops[i].Addresses.Contains(expected[i]) || hops[i].RttMilliseconds.Count == 0)
        throw new InvalidOperationException($"Missing actual responder at TTL {i + 1}");
Console.WriteLine("REAL_ICMP_TWO_ROUTERS_AND_DESTINATION=PASS");
