using System;

namespace Text_Grab.Models;

/// <summary>
/// Represents a stored regular expression pattern with name and pattern
/// </summary>
public class StoredRegex
{
    /// <summary>
    /// Unique identifier for the stored regex
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Display name for the regex pattern
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The actual regex pattern
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a default (built-in) pattern
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// Description or notes about the pattern
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Date/time when this pattern was created
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;

    /// <summary>
    /// Date/time when this pattern was last used
    /// </summary>
    public DateTimeOffset? LastUsedDate { get; set; }

    public StoredRegex()
    {
    }

    public StoredRegex(string name, string pattern, bool isDefault = false, string description = "")
    {
        Name = name;
        Pattern = pattern;
        IsDefault = isDefault;
        Description = description;
    }

    /// <summary>
    /// Gets the default regex patterns that come with Text Grab.
    ///
    /// This list is intentionally limited to formats that the built-in
    /// <see cref="BuiltInRecognizer"/> catalog does not already cover. Emails, phone
    /// numbers, URLs, IP addresses, GUIDs, dates, times, currency, and plain numbers
    /// are all handled better by the culture-aware Smart Patterns (recognizers), so
    /// they are not duplicated here.
    /// </summary>
    public static StoredRegex[] GetDefaultPatterns()
    {
        return
        [
            new StoredRegex("Credit Card", @"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b", true, "Matches credit card numbers"),
            new StoredRegex("Hex Color", @"#[0-9a-fA-F]{6}\b", true, "Matches hex color codes like #FFFFFF"),
            new StoredRegex("Social Security Number", @"\b\d{3}-\d{2}-\d{4}\b", true, "Matches SSN format XXX-XX-XXXX"),
            new StoredRegex("Zip Code (US)", @"\b\d{5}(-\d{4})?\b", true, "Matches US zip codes (5 or 9 digit)"),
        ];
    }
}
