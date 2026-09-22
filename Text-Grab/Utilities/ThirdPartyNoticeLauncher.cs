using System.Diagnostics;
using System.IO;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

/// <summary>
/// Impure half of the third-party notice utilities that stayed split out of Core in batch 2c:
/// resolving notice/license file paths against the running executable's location
/// (<see cref="FileUtilities.GetExePath()"/>) and opening them. The pure package catalog
/// (<see cref="ThirdPartyNoticeUtilities.Packages"/>) lives in Text-Grab.Core under the
/// original name.
/// </summary>
public static class ThirdPartyNoticeLauncher
{
    public static string? GetBuiltWithFilePath()
    {
        string? executableDirectory = Path.GetDirectoryName(FileUtilities.GetExePath());
        return string.IsNullOrWhiteSpace(executableDirectory)
            ? null
            : Path.Combine(executableDirectory, ThirdPartyNoticeUtilities.BuiltWithFileName);
    }

    public static string? GetNoticesDirectoryPath()
    {
        string? executableDirectory = Path.GetDirectoryName(FileUtilities.GetExePath());
        return string.IsNullOrWhiteSpace(executableDirectory)
            ? null
            : Path.Combine(executableDirectory, ThirdPartyNoticeUtilities.NoticesDirectoryName);
    }

    public static string? GetNoticeTarget(ThirdPartyPackageInfo package)
    {
        if (!package.NoticeIsLocal)
            return package.NoticeTarget;

        string? executableDirectory = Path.GetDirectoryName(FileUtilities.GetExePath());
        return string.IsNullOrWhiteSpace(executableDirectory)
            ? null
            : Path.Combine(executableDirectory, package.NoticeTarget);
    }

    public static void OpenBuiltWithFile() => OpenTarget(GetBuiltWithFilePath());

    public static void OpenNoticesDirectory() => OpenTarget(GetNoticesDirectoryPath());

    public static void OpenNoticeFile(ThirdPartyPackageInfo package) => OpenTarget(GetNoticeTarget(package));

    public static void OpenProjectUrl(ThirdPartyPackageInfo package) => OpenTarget(package.ProjectUrl);

    private static void OpenTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return;

        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }
}
