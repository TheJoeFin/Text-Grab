namespace TextGrab.AutomationHost;

public sealed record FixtureOptions(string Surface, string? StateFile, string? DisplayText)
{
    public static FixtureOptions Parse(IEnumerable<string> arguments)
    {
        string surface = "KnownText";
        string? stateFile = Environment.GetEnvironmentVariable("TEXT_GRAB_AUTOMATION_HOST_STATE_FILE");
        string? displayText = null;
        string[] values = arguments.ToArray();

        for (int index = 0; index < values.Length; index++)
        {
            string argument = values[index];
            if (TryGetValue(argument, "--surface", out string value))
            {
                surface = value;
            }
            else if (TryGetValue(argument, "--state-file", out value))
            {
                stateFile = value;
            }
            else if (TryGetValue(argument, "--text", out value))
            {
                displayText = value;
            }
            else if (argument is "--surface" or "--state-file" or "--text")
            {
                if (index + 1 < values.Length)
                {
                    value = values[++index];
                    if (argument == "--surface")
                    {
                        surface = value;
                    }
                    else if (argument == "--state-file")
                    {
                        stateFile = value;
                    }
                    else
                    {
                        displayText = value;
                    }
                }
            }
        }

        return new FixtureOptions(surface, stateFile, displayText);
    }

    private static bool TryGetValue(string argument, string option, out string value)
    {
        string prefix = $"{option}=";
        if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = argument[prefix.Length..];
            return true;
        }

        value = string.Empty;
        return false;
    }
}
