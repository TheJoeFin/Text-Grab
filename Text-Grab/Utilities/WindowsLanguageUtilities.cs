namespace Text_Grab.Utilities;
public class WindowsLanguageUtilities
{
    public static string PowerShellCommandForInstallingWithTag(string languageTag)
    {
        // $Capability = Get-WindowsCapability -Online | Where-Object { $_.Name -Like 'Language.OCR*en-US*' }
        return $"$Capability = Get-WindowsCapability -Online | Where-Object {{ $_.Name -Like 'Language.OCR*{languageTag}*' }}; $Capability | Add-WindowsCapability -Online";
    }

    public static string DismLanguageCommand(string languageTag)
    {
        return $"Language.OCR~~~{languageTag}";
    }

    public static string PowerShellCommandForUninstallingWithTag(string languageTag)
    {
        // $Capability = Get-WindowsCapability -Online | Where-Object { $_.Name -Like 'Language.OCR*en-US*' }
        return $"$Capability = Get-WindowsCapability -Online | Where-Object {{ $_.Name -Like 'Language.OCR*{languageTag}*' }}; $Capability | Remove-WindowsCapability -Online";
    }

    public static readonly string[] AllLanguages = [
        "ar-SA",
        "bg-BG",
        "bs-LATN-BA",
        "cs-CZ",
        "da-DK",
        "de-DE",
        "el-GR",
        "en-GB",
        "en-US",
        "es-ES",
        "es-MX",
        "fi-FI",
        "fr-CA",
        "fr-FR",
        "hr-HR",
        "hu-HU",
        "it-IT",
        "ja-JP",
        "ko-KR",
        "nb-NO",
        "nl-NL",
        "pl-PL",
        "pt-BR",
        "pt-PT",
        "ro-RO",
        "ru-RU",
        "sk-SK",
        "sl-SI",
        "sr-CYRL-RS",
        "sr-LATN-RS",
        "sv-SE",
        "tr-TR",
        "zh-CN",
        "zh-HK",
        "zh-TW",
        ];
}
