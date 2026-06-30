using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.Choice;
using Microsoft.Recognizers.Text.DateTime;
using Microsoft.Recognizers.Text.Number;
using Microsoft.Recognizers.Text.NumberWithUnit;
using Microsoft.Recognizers.Text.Sequence;

namespace Text_Grab.Models;

/// <summary>
/// Represents one of the built-in, culture-aware recognizers from the
/// Microsoft Recognizers-Text library (numbers, dates, currencies, emails, …).
///
/// Unlike <see cref="StoredRegex"/>, recognizers are a fixed catalog — there is no
/// editor. They are surfaced for selection in Grab Templates (via <c>{r:Name:mode}</c>
/// placeholders), applied in the Edit Text Window, and used for searching.
///
/// Each recognizer wraps one Recognizers-Text "Recognize" method. The recognizer
/// returns matches that carry both the matched <see cref="ModelResult.Text"/> and a
/// normalized resolution (e.g. "next tuesday" → 2026-07-07, "$5" → 5 Dollar) — see
/// <c>RecognizerExecutor</c> for how the resolution is formatted.
/// </summary>
public class BuiltInRecognizer
{
    /// <summary>Stable identifier used in serialized templates (e.g. "datetime").</summary>
    public string Id { get; }

    /// <summary>Display name shown in menus and pickers (e.g. "Date / Time").</summary>
    public string Name { get; }

    /// <summary>Short description of what the recognizer matches.</summary>
    public string Description { get; }

    /// <summary>Invokes the underlying Recognizers-Text method. (text, culture) → matches.</summary>
    public Func<string, string, List<ModelResult>> Recognize { get; }

    private BuiltInRecognizer(string id, string name, string description,
        Func<string, string, List<ModelResult>> recognize)
    {
        Id = id;
        Name = name;
        Description = description;
        Recognize = recognize;
    }

    private static readonly IReadOnlyList<BuiltInRecognizer> All =
    [
        new("number", "Number", "Numbers like 25 or 3.5",
            static (text, culture) => NumberRecognizer.RecognizeNumber(text, culture)),
        new("ordinal", "Ordinal", "Ordinal numbers like 1st, 2nd, 3rd",
            static (text, culture) => NumberRecognizer.RecognizeOrdinal(text, culture)),
        new("percentage", "Percentage", "Percentages like 50%",
            static (text, culture) => NumberRecognizer.RecognizePercentage(text, culture)),
        new("age", "Age", "Ages like 25 years old",
            static (text, culture) => NumberWithUnitRecognizer.RecognizeAge(text, culture)),
        new("currency", "Currency", "Currency amounts like $5 or 10 dollars",
            static (text, culture) => NumberWithUnitRecognizer.RecognizeCurrency(text, culture)),
        new("dimension", "Dimension", "Dimensions like 3 miles or 5 kg",
            static (text, culture) => NumberWithUnitRecognizer.RecognizeDimension(text, culture)),
        new("temperature", "Temperature", "Temperatures like 90 degrees fahrenheit",
            static (text, culture) => NumberWithUnitRecognizer.RecognizeTemperature(text, culture)),
        new("datetime", "Date / Time", "Dates, times, durations and ranges like next tuesday at 3pm",
            static (text, culture) => DateTimeRecognizer.RecognizeDateTime(text, culture)),
        new("phonenumber", "Phone Number", "Phone numbers like (212) 555-0182",
            static (text, culture) => SequenceRecognizer.RecognizePhoneNumber(text, culture)),
        new("email", "Email", "Email addresses",
            static (text, culture) => SequenceRecognizer.RecognizeEmail(text, culture)),
        new("url", "URL", "Web URLs",
            static (text, culture) => SequenceRecognizer.RecognizeURL(text, culture)),
        new("ip", "IP Address", "IPv4 and IPv6 addresses",
            static (text, culture) => SequenceRecognizer.RecognizeIpAddress(text, culture)),
        new("guid", "GUID", "GUIDs / UUIDs",
            static (text, culture) => SequenceRecognizer.RecognizeGUID(text, culture)),
        new("boolean", "Boolean", "Yes / no style boolean values",
            static (text, culture) => ChoiceRecognizer.RecognizeBoolean(text, culture)),
    ];

    /// <summary>Returns the full fixed catalog of recognizers.</summary>
    public static IReadOnlyList<BuiltInRecognizer> GetAll() => All;

    /// <summary>Finds a recognizer by its stable <see cref="Id"/> (case-insensitive). Null if none.</summary>
    public static BuiltInRecognizer? GetById(string id) =>
        All.FirstOrDefault(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Finds a recognizer by its display <see cref="Name"/> (case-insensitive). Null if none.</summary>
    public static BuiltInRecognizer? GetByName(string name) =>
        All.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
