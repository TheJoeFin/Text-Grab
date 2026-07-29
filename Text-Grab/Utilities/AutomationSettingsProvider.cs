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
    // these must be no-ops; otherwise defer to the LocalFileSettingsProvider base behavior.
    void IApplicationSettingsProvider.Reset(SettingsContext context)
    {
        if (AutomationProfile.Current is not null)
            return;

        base.Reset(context);
    }

    void IApplicationSettingsProvider.Upgrade(SettingsContext context, SettingsPropertyCollection properties)
    {
        if (AutomationProfile.Current is not null)
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
