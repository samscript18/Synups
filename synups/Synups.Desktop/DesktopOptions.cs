using Synups.Core.Persistence;

namespace Synups.Desktop;

/// <summary>Explicit portable-data and installation-diagnostic options. Ordinary startup remains passive.</summary>
public sealed record DesktopOptions(string DataDirectory, string? VerificationDirectory = null,
    string VerificationStage = "exercise", int ObserveSeconds = 2)
{
    public static DesktopOptions Parse(string[] args)
    {
        string data = SynupsPaths.DataDirectory; string? report = null;
        var stage = "exercise"; var seconds = 2;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (!seen.Add(key) || i + 1 >= args.Length) throw new ArgumentException("Every option requires one value and may occur once.");
            var value = args[++i];
            switch (key)
            {
                case "--data-directory": data = Absolute(value); break;
                case "--verify-installation": report = Absolute(value); break;
                case "--verification-stage": stage = value; break;
                case "--observe-seconds":
                    if (!int.TryParse(value, out seconds) || seconds is < 1 or > 300) throw new ArgumentException("Observation must be 1–300 seconds.");
                    break;
                default: throw new ArgumentException($"Unknown option: {key}. Use --help.");
            }
        }
        if (stage is not ("exercise" or "restart" or "interruption" or "recover" or "storage-error" or "storage-recovered")) throw new ArgumentException("Unknown installation verification stage.");
        if (report is null && (seen.Contains("--verification-stage") || seen.Contains("--observe-seconds")))
            throw new ArgumentException("Verification options require --verify-installation.");
        if (report is not null && !seen.Contains("--data-directory")) throw new ArgumentException("Installation verification requires an explicit isolated --data-directory.");
        return new(Absolute(data), report, stage, seconds);
    }
    private static string Absolute(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value)) throw new ArgumentException("Directories must be absolute local paths.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
    }
    public const string Help = """
        Synups — local network reliability monitor
        Open without arguments for normal desktop use. No probes run until Start or an explicitly saved launch preference.
        --data-directory ABSOLUTE_PATH       Use a separate configuration/preferences/history directory.
        --verify-installation REPORT_FOLDER Run an explicit real-loopback installation check in an isolated data directory.
        --verification-stage STAGE          exercise | restart | interruption | recover | storage-error | storage-recovered.
        --observe-seconds SECONDS           1–300 real observation seconds during installation checking.
        --help                              Show these options.
        --version                           Print product/runtime identity.
        Installation checks use real controls and storage, not physical OS input automation. They never access normal user settings.
        """;
}
