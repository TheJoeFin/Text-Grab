using Microsoft.Win32;
using System;

namespace Text_Grab.Utilities;

public class SystemThemeUtility
{
    public const string themeKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";

    public static bool IsLightTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(themeKeyPath);
            if (key is null)
                return false;

            Object? o = key.GetValue("SystemUsesLightTheme");
            if (o is null)
                return false;

            if (o.ToString() == "1")
                return true;

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
