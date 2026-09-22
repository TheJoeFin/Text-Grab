using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Text_Grab.Utilities;

public sealed class AutomationSettingsProvider : LocalFileSettingsProvider, IApplicationSettingsProvider
{
    /// <summary>
    /// Set by <c>SettingsService</c> once it has determined that file-backed settings storage
    /// (or Fully Portable mode) is authoritative for this run, so the JSON sidecar - not
    /// <c>user.config</c> - is the single source of truth going forward. Once set, this provider
    /// stops writing to (or reading previous-version data from) the real per-user AppData store;
    /// <c>Properties.Settings</c> keeps working purely in memory, and every existing
    /// <c>Settings.Default.Save()</c> call site becomes a silent no-op on the classic store
    /// without needing to change any of those call sites.
    ///
    /// Only takes effect when no <see cref="AutomationProfile"/> is active - that path already
    /// fully redirects to its own isolated JSON file regardless of this flag.
    /// </summary>
    public static bool SuppressClassicPersistence { get; set; }

    public override SettingsPropertyValueCollection GetPropertyValues(
        SettingsContext context,
        SettingsPropertyCollection collection)
    {
        AutomationProfile? profile = AutomationProfile.Current;
        if (profile is null)
            return base.GetPropertyValues(context, collection);

        Dictionary<string, string> storedValues = ReadValues(profile.ClassicSettingsFilePath);
        SettingsPropertyValueCollection values = [];
        foreach (SettingsProperty property in collection)
        {
            storedValues.TryGetValue(property.Name, out string? value);
            values.Add(new SettingsPropertyValue(property)
            {
                SerializedValue = value ?? property.DefaultValue,
                IsDirty = false
            });
        }

        return values;
    }

    public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection collection)
    {
        AutomationProfile? profile = AutomationProfile.Current;
        if (profile is null)
        {
            if (SuppressClassicPersistence)
                return;

            base.SetPropertyValues(context, collection);
            return;
        }

        Dictionary<string, string> storedValues = ReadValues(profile.ClassicSettingsFilePath);
        foreach (SettingsPropertyValue propertyValue in collection)
            storedValues[propertyValue.Name] = ConvertToInvariantString(propertyValue);

        Directory.CreateDirectory(profile.SettingsDirectory);
        File.WriteAllText(
            profile.ClassicSettingsFilePath,
            JsonSerializer.Serialize(storedValues, new JsonSerializerOptions { WriteIndented = true }));
    }

    // Upgrade/Reset/GetPreviousVersion reach into the legacy per-user user.config. Under
    // an automation profile the classic store lives entirely in the profile directory, so
    // these must be no-ops; likewise once file-backed settings storage is authoritative,
    // since neither should touch the real AppData store anymore. Otherwise defer to the
    // LocalFileSettingsProvider base behavior.
    void IApplicationSettingsProvider.Reset(SettingsContext context)
    {
        if (AutomationProfile.Current is not null || SuppressClassicPersistence)
            return;

        base.Reset(context);
    }

    void IApplicationSettingsProvider.Upgrade(SettingsContext context, SettingsPropertyCollection properties)
    {
        if (AutomationProfile.Current is not null || SuppressClassicPersistence)
            return;

        base.Upgrade(context, properties);
    }

    SettingsPropertyValue IApplicationSettingsProvider.GetPreviousVersion(SettingsContext context, SettingsProperty property)
    {
        if (AutomationProfile.Current is not null)
            return new SettingsPropertyValue(property) { PropertyValue = null, IsDirty = false };

        return base.GetPreviousVersion(context, property);
    }

    private static Dictionary<string, string> ReadValues(string path)
    {
        if (!File.Exists(path))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static string ConvertToInvariantString(SettingsPropertyValue propertyValue)
    {
        object? value = propertyValue.PropertyValue;
        if (value is null)
            return string.Empty;

        TypeConverter converter = TypeDescriptor.GetConverter(propertyValue.Property.PropertyType);
        if (converter.CanConvertTo(typeof(string)))
            return converter.ConvertToInvariantString(value) ?? string.Empty;

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
