using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
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

    [Fact]
    public async Task BugReport_ContainsStartupPathDiagnostics()
    {
        // Act
        string bugReport = await DiagnosticsUtilities.GenerateBugReportAsync();
        
        // Assert - Should contain startup diagnostics to verify the fix
        Assert.Contains("startupDetails", bugReport);
        Assert.Contains("calculatedRegistryValue", bugReport);
        Assert.Contains("actualRegistryValue", bugReport);
        Assert.Contains("baseDirectory", bugReport);
        
        // The bug report should help verify that startup path fix is working
        Assert.Contains("Text-Grab.exe", bugReport);
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
    }
}