using System;
using System.Globalization;
using System.Windows.Controls;

namespace Text_Grab;

// copied from StackOverflow on 4/25/2022
// https://stackoverflow.com/questions/19539492/implement-validation-for-wpf-textboxes?msclkid=ff84958fc27c11ec949ff19deb79c5f4
// Mr. B

public class NumericValidationRule : ValidationRule
{
    public Type? ValidationType { get; set; }
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        string? strValue = Convert.ToString(value);

        if (string.IsNullOrEmpty(strValue))
            return new ValidationResult(false, $"Value cannot be coverted to string.");
        bool canConvert = false;

        if (ValidationType is null)
            return new ValidationResult(false, $"ValidationType was null."); ;

        switch (ValidationType.Name)
        {
            case "Boolean":
                bool boolVal = false;
                canConvert = bool.TryParse(strValue, out boolVal);
                return canConvert ? new ValidationResult(true, null) : new ValidationResult(false, $"Input should be type of boolean");
            case "Int32":
                int intVal = 0;
                canConvert = int.TryParse(strValue, out intVal);
                return canConvert ? new ValidationResult(true, null) : new ValidationResult(false, $"Input should be type of Int32");
            case "Double":
                double doubleVal = 0;
                canConvert = double.TryParse(strValue, out doubleVal);
                return canConvert ? new ValidationResult(true, null) : new ValidationResult(false, $"Input should be type of Double");
            case "Int64":
                long longVal = 0;
                canConvert = long.TryParse(strValue, out longVal);
                return canConvert ? new ValidationResult(true, null) : new ValidationResult(false, $"Input should be type of Int64");
            default:
                throw new InvalidCastException($"{ValidationType.Name} is not supported");
        }
    }
}