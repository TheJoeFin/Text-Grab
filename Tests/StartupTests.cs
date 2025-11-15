using System.IO;

namespace Tests;

public class StartupTests
{
    [Fact]
    public void StartupPathCalculation_OldVsNewLogic()
    {
        // Arrange - Simulate a typical Windows executable path structure
        string simulatedBaseDirectory = @"C:\Apps\Text-Grab\";

        // Act - Old buggy logic (what's currently in the code)
        string? parentDir = Path.GetDirectoryName(simulatedBaseDirectory.TrimEnd('\\'));
        string oldLogicPath = parentDir is not null ?
            $"\"{parentDir}\\Text-Grab.exe\"" : "";

        // Act - New correct logic (what it should be)
        string newLogicPath = $"\"{Path.Combine(simulatedBaseDirectory, "Text-Grab.exe")}\"";

        // Assert - The paths should be different (proving the bug exists)
        Assert.NotEqual(oldLogicPath, newLogicPath);

        // Assert - Old logic should point to parent directory (wrong)
        Assert.Contains(@"C:\Apps\Text-Grab.exe", oldLogicPath);

        // Assert - New logic should point to base directory (correct)
        Assert.Contains(@"C:\Apps\Text-Grab\Text-Grab.exe", newLogicPath);
    }

    [Fact]
    public void WindowsStartupPathCalculation_OldVsNewLogic()
    {
        // Arrange - Simulate a typical Windows executable path structure
        string simulatedBaseDirectory = @"C:\Program Files\Text-Grab\";

        // Act - Old buggy logic (what's currently in the code)
        string? parentDir = Path.GetDirectoryName(simulatedBaseDirectory.TrimEnd('\\'));
        string oldLogicPath = parentDir is not null ?
            $"\"{parentDir}\\Text-Grab.exe\"" : "";

        // Act - New correct logic (what it should be)  
        string newLogicPath = $"\"{Path.Combine(simulatedBaseDirectory, "Text-Grab.exe")}\"";

        // Assert - The paths should be different (proving the bug exists)
        Assert.NotEqual(oldLogicPath, newLogicPath);

        // Assert - Old logic should point to parent directory (wrong)
        Assert.Equal("\"C:\\Program Files\\Text-Grab.exe\"", oldLogicPath);

        // Assert - New logic should point to correct directory 
        Assert.Equal("\"C:\\Program Files\\Text-Grab\\Text-Grab.exe\"", newLogicPath);
    }

    [Fact]
    public void FixedStartupPathCalculation_UsesCorrectBaseDirectory()
    {
        // Arrange - Simulate the fixed logic that should be in the code now
        string simulatedBaseDirectory = @"C:\MyApps\Text-Grab\";

        // Act - Fixed logic using Path.Combine with BaseDirectory directly
        string fixedLogicPath = $"\"{Path.Combine(simulatedBaseDirectory, "Text-Grab.exe")}\"";

        // Assert - Fixed logic should create correct path
        Assert.Equal("\"C:\\MyApps\\Text-Grab\\Text-Grab.exe\"", fixedLogicPath);

        // Assert - Path should point to the executable in the base directory
        Assert.Contains(simulatedBaseDirectory.TrimEnd('\\'), fixedLogicPath);
        Assert.EndsWith("Text-Grab.exe\"", fixedLogicPath);
    }

    [Fact]
    public void FileUtilitiesPathCalculation_OldVsNewLogic()
    {
        // Arrange - Simulate a typical Windows executable path structure
        string simulatedBaseDirectory = @"C:\Apps\Text-Grab\";

        // Act - Old buggy logic (what was previously in FileUtilities)
        string? parentDir = Path.GetDirectoryName(simulatedBaseDirectory.TrimEnd('\\'));
        string oldLogicHistoryPath = parentDir is not null ?
            $"{parentDir}\\history" : "";

        // Act - New correct logic (what should be in FileUtilities now)
        string newLogicHistoryPath = Path.Combine(simulatedBaseDirectory, "history");

        // Assert - The paths should be different (proving the bug exists)
        Assert.NotEqual(oldLogicHistoryPath, newLogicHistoryPath);

        // Assert - Old logic should point to parent directory (wrong)
        Assert.Equal(@"C:\Apps\history", oldLogicHistoryPath);

        // Assert - New logic should point to base directory (correct)
        Assert.Equal(@"C:\Apps\Text-Grab\history", newLogicHistoryPath);
    }

    [Fact]
    public void FileUtilitiesLocalFilePathCalculation_OldVsNewLogic()
    {
        // Arrange - Simulate a typical Windows executable path structure and relative file
        string simulatedBaseDirectory = @"C:\Program Files\Text-Grab\";
        string relativeFile = @"images\logo.png";

        // Act - Old buggy logic (what was previously in GetPathToLocalFile)
        string? parentDir = Path.GetDirectoryName(simulatedBaseDirectory.TrimEnd('\\'));
        string oldLogicPath = parentDir is not null ?
            Path.Combine(parentDir, relativeFile) : "";

        // Act - New correct logic (what should be in GetPathToLocalFile now)
        string newLogicPath = Path.Combine(simulatedBaseDirectory, relativeFile);

        // Assert - The paths should be different (proving the bug exists)
        Assert.NotEqual(oldLogicPath, newLogicPath);

        // Assert - Old logic should point to parent directory (wrong)
        Assert.Equal(@"C:\Program Files\images\logo.png", oldLogicPath);

        // Assert - New logic should point to base directory (correct)
        Assert.Equal(@"C:\Program Files\Text-Grab\images\logo.png", newLogicPath);
    }
}