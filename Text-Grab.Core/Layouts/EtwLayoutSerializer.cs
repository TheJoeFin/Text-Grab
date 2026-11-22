using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Text_Grab.Core.Layouts;

/// <summary>The result of reading a layout: the layout, or why there is none.</summary>
public sealed record LayoutReadResult(EtwLayoutDefinition? Layout, string? Error)
{
    public static LayoutReadResult Failed(string error) => new(null, error);

    public bool Succeeded => Layout is not null;
}

/// <summary>Reads and writes <see cref="EtwLayoutDefinition"/>s as JSON.</summary>
/// <remarks>
/// A layout file is user-editable input. <see cref="Read"/> never throws: a file it cannot use comes
/// back as a <see cref="LayoutReadResult"/> with a reason a person can read.
/// </remarks>
public static class EtwLayoutSerializer
{
    /// <summary>The schema version this build writes.</summary>
    public const string CurrentSchemaVersion = "1.0";

    /// <summary>The newest major schema version this build reads.</summary>
    public const int SupportedMajorVersion = 1;

    /// <summary>
    /// How deep the JSON may nest. A dock tree adds about three levels per split, so this allows
    /// around fifteen nested splits, far more than any window can show.
    /// </summary>
    public const int MaxJsonDepth = 48;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowOutOfOrderMetadataProperties = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        MaxDepth = MaxJsonDepth,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(EtwLayoutDefinition layout)
        => JsonSerializer.Serialize(layout, Options);

    public static LayoutReadResult Read(string json)
    {
        EtwLayoutDefinition? layout;
        try
        {
            layout = JsonSerializer.Deserialize<EtwLayoutDefinition>(json, Options);
        }
        catch (JsonException ex)
        {
            return LayoutReadResult.Failed($"The layout file is not valid: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            // An unknown node "type" lands here.
            return LayoutReadResult.Failed($"The layout file is not valid: {ex.Message}");
        }

        if (layout is null)
            return LayoutReadResult.Failed("The layout file is empty.");

        if (!TryGetMajorVersion(layout.SchemaVersion, out int major))
            return LayoutReadResult.Failed($"The layout file has no usable schema version (\"{layout.SchemaVersion}\").");

        if (major > SupportedMajorVersion)
            return LayoutReadResult.Failed($"The layout was made by a newer Text Grab (schema {layout.SchemaVersion}).");

        // Required members System.Text.Json leaves null when a file omits them.
        if (string.IsNullOrWhiteSpace(layout.Id))
            return LayoutReadResult.Failed("The layout file has no ID.");

        if (layout.Root is null)
            return LayoutReadResult.Failed("The layout file has no dock tree (\"root\").");

        if (FindMissingMember(layout.Root) is string missing)
            return LayoutReadResult.Failed($"The layout file is not valid: {missing}");

        return new LayoutReadResult(layout, null);
    }

    private static bool TryGetMajorVersion(string? schemaVersion, out int major)
    {
        major = 0;
        if (string.IsNullOrWhiteSpace(schemaVersion))
            return false;

        string majorPart = schemaVersion.Split('.')[0];
        return int.TryParse(majorPart, NumberStyles.None, CultureInfo.InvariantCulture, out major);
    }

    // A record's non-nullable members are only as good as the file: a missing "children" or "paneId"
    // deserializes as null.
    private static string? FindMissingMember(DockNode node) => node switch
    {
        SplitNode split when split.Children is null => "a split has no \"children\".",
        SplitNode split => FindMissingMember(split),
        PaneNode pane when string.IsNullOrWhiteSpace(pane.PaneId) => "a pane has no \"paneId\".",
        TabGroupNode tabs when tabs.Tabs is null => "a tab group has no \"tabs\".",
        TabGroupNode tabs => FindMissingTabMember(tabs),
        _ => null,
    };

    private static string? FindMissingMember(SplitNode split)
    {
        foreach (DockChild? child in split.Children)
        {
            if (child?.Node is null)
                return "a split child has no \"node\".";

            if (FindMissingMember(child.Node) is string missing)
                return missing;
        }

        return null;
    }

    private static string? FindMissingTabMember(TabGroupNode tabs)
    {
        foreach (PaneNode? tab in tabs.Tabs)
        {
            if (tab is null || string.IsNullOrWhiteSpace(tab.PaneId))
                return "a tab has no \"paneId\".";
        }

        return null;
    }
}
