using System.Text.Json;
using System.IO;

namespace TextGrab.AutomationHost;

public sealed class FixtureStateWriter(string? stateFile)
{
    private readonly string? stateFile = string.IsNullOrWhiteSpace(stateFile) ? null : Path.GetFullPath(stateFile);

    public void Write(FixtureState state)
    {
        if (stateFile is null)
        {
            return;
        }

        string? directory = Path.GetDirectoryName(stateFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(
            stateFile,
            JsonSerializer.Serialize(state, FixtureJsonContext.Default.FixtureState) + Environment.NewLine);
    }
}

public sealed record FixtureState(
    DateTimeOffset TimestampUtc,
    string Event,
    string Surface,
    string DisplayText,
    string ReceivedText,
    string Bounds,
    string Monitor,
    uint Dpi);

[System.Text.Json.Serialization.JsonSerializable(typeof(FixtureState))]
internal sealed partial class FixtureJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
