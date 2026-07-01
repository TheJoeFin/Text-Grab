using System.IO;
using Text_Grab.Utilities;

namespace Tests;

public class DiagnosticsTests
{
    [Fact]
    public async Task GenerateBugReport_ReturnsValidJson()
    {
        // This test may fail on non-Windows platforms but should at least generate JSON
        string bugReport = await DiagnosticsUtilities.GenerateBugReportAsync();

        // Assert - Should not be empty
        Assert.NotEmpty(bugReport);

        // Assert - Should be valid JSON (basic check)
        Assert.StartsWith("{", bugReport.Trim());
        Assert.EndsWith("}", bugReport.Trim());

        // Assert - Should contain expected sections
        Assert.Contains("generatedAt", bugReport);
        Assert.Contains("appVersion", bugReport);
        Assert.Contains("installationType", bugReport);
        Assert.Contains("startupDetails", bugReport);
    }

    [Fact]
    public async Task SaveBugReportToFile_CreatesFileInDocuments()
    {
        // Act
        string filePath = await DiagnosticsUtilities.SaveBugReportToFileAsync();

        // Assert - File should exist
        Assert.True(File.Exists(filePath));

        // Assert - File should be in Documents folder
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        Assert.StartsWith(documentsPath, filePath);

        // Assert - File should have correct naming pattern
        Assert.Contains("TextGrab_BugReport_", Path.GetFileName(filePath));
        Assert.EndsWith(".json", filePath);

        // Cleanup
        try
        {
            File.Delete(filePath);
        }
        catch
        {
            // Ignore cleanup failures in tests
        }
    }

    [Fact(Skip = "because this fails in GitHub Actions")]
    public async Task BugReport_ContainsStartupPathDiagnostics()
    {
        // Act
        string bugReport = await DiagnosticsUtilities.GenerateBugReportAsync();

        // Assert - Should contain startup diagnostics (PII-safe fields only)
        Assert.Contains("startupDetails", bugReport);
        Assert.Contains("executableFileName", bugReport);
        Assert.Contains("registryValueStatus", bugReport);

        // The executable filename (without path) should be present — full paths are redacted
        Assert.Contains("Text-Grab.exe", bugReport);
        // Full paths that would expose the local username must NOT appear
        Assert.DoesNotContain("baseDirectory", bugReport);
        Assert.DoesNotContain("calculatedRegistryValue", bugReport);
        Assert.DoesNotContain("actualRegistryValue", bugReport);
    }

    [Fact]
    public async Task BugReport_IncludesAllRequestedInformation()
    {
        // Act
        string bugReport = await DiagnosticsUtilities.GenerateBugReportAsync();

        // Assert - Should contain all requested information from issue #553
        Assert.Contains("settingsInfo", bugReport);           // Settings
        Assert.Contains("installationType", bugReport);       // Type of install
        Assert.Contains("startupDetails", bugReport);         // Startup location details
        Assert.Contains("windowsVersion", bugReport);         // Windows version
        Assert.Contains("historyInfo", bugReport);            // Amount of history
        Assert.Contains("languageInfo", bugReport);           // Installed languages
        Assert.Contains("tesseractInfo", bugReport);          // Tesseract details
        Assert.Contains("managedSettingsSummary", bugReport); // Post-grab actions, patterns, shortcuts
    }

    [Fact]
    public async Task BugReport_SettingsInfo_ContainsAllKeySettings()
    {
        string bugReport = await DiagnosticsUtilities.GenerateBugReportAsync();

        // Grab behavior
        Assert.Contains("\"tryInsert\"", bugReport);
        Assert.Contains("\"insertDelay\"", bugReport);
        Assert.Contains("\"closeFrameOnGrab\"", bugReport);
        Assert.Contains("\"postGrabStayOpen\"", bugReport);

        // OCR
        Assert.Contains("\"correctErrors\"", bugReport);
        Assert.Contains("\"correctToLatin\"", bugReport);
        Assert.Contains("\"paragraphDetection\"", bugReport);
        Assert.Contains("\"useTesseract\"", bugReport);
        Assert.Contains("\"tesseractPathConfigured\"", bugReport);  // bool only — no path exposed
        Assert.Contains("\"uiAutomationEnabled\"", bugReport);
        Assert.Contains("\"uiAutomationFallbackToOcr\"", bugReport);

        // Display
        Assert.Contains("\"appTheme\"", bugReport);
        Assert.Contains("\"fontSizeSetting\"", bugReport);

        // Edit Text Window
        Assert.Contains("\"editWindowIsWordWrapOn\"", bugReport);
        Assert.Contains("\"etwShowWordCount\"", bugReport);
        Assert.Contains("\"etwUseMargins\"", bugReport);

        // Fullscreen grab
        Assert.Contains("\"fsgDefaultMode\"", bugReport);
        Assert.Contains("\"fsgSelectionStyle\"", bugReport);

        // Grab Frame
        Assert.Contains("\"grabFrameTranslationEnabled\"", bugReport);
        Assert.Contains("\"grabFrameScrollBehavior\"", bugReport);
    }

    [Fact]
    public async Task BugReport_ManagedSettingsSummary_ContainsExpectedFields()
    {
        string bugReport = await DiagnosticsUtilities.GenerateBugReportAsync();

        Assert.Contains("\"regexPatternCount\"", bugReport);
        Assert.Contains("\"regexCustomPatternCount\"", bugReport);
        Assert.Contains("\"regexCustomPatternNames\"", bugReport);
        Assert.Contains("\"postGrabActionCount\"", bugReport);
        Assert.Contains("\"postGrabActionNames\"", bugReport);
        Assert.Contains("\"shortcutKeySetCount\"", bugReport);
        Assert.Contains("\"bottomBarButtonCount\"", bugReport);
        Assert.Contains("\"webSearchUrlCount\"", bugReport);
        Assert.Contains("\"grabTemplateCount\"", bugReport);
    }

    [Fact]
    public async Task BugReport_DoesNotContainPii()
    {
        string bugReport = await DiagnosticsUtilities.GenerateBugReportAsync();

        // Fields removed from StartupDetailsModel (contained full paths with username)
        Assert.DoesNotContain("\"baseDirectory\"", bugReport);
        Assert.DoesNotContain("\"calculatedRegistryValue\"", bugReport);
        Assert.DoesNotContain("\"actualRegistryValue\"", bugReport);

        // No absolute Windows paths should appear anywhere in the report
        Assert.DoesNotContain(@"C:\Users\", bugReport);
        Assert.DoesNotContain(@"C:\Program", bugReport);

        // The old TesseractPath string field must not appear (replaced by bool TesseractPathConfigured)
        Assert.DoesNotContain("\"tesseractPath\"", bugReport);

        // Web search URLs must not appear (only the count is included)
        Assert.DoesNotContain("\"webSearchUrls\"", bugReport);
    }
}
