using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Text_Grab.Core.Actions;

/// <summary>
/// Reads an action's JSON config into its config record, the same way for every action: property
/// names in any case, enums by name or number, unknown properties ignored. A required value that
/// is missing or null, or a value of the wrong JSON type, is an error.
/// </summary>
internal static class ActionConfig
{
    internal static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>
    /// The config in <paramref name="config"/>, or <paramref name="defaultConfig"/> when there is none
    /// (absent, or JSON <c>null</c>).
    /// </summary>
    /// <exception cref="ArgumentException">There is no config and no default, or the config does not read as <typeparamref name="T"/>.</exception>
    internal static T Read<T>(JsonElement? config, T? defaultConfig, string actionId) where T : class
    {
        if (config is not { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } element)
            return defaultConfig
                ?? throw new ArgumentException($"'{actionId}' needs a {typeof(T).Name} config and none was given.");

        try
        {
            return element.Deserialize<T>(Options)
                ?? throw new ArgumentException($"'{actionId}' needs a {typeof(T).Name} config and none was given.");
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"'{actionId}' has a config that is not a valid {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
