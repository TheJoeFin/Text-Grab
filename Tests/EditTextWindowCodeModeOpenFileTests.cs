using System.IO;
using Text_Grab;
using Text_Grab.Controls;
using Text_Grab.Models;
using Text_Grab.Utilities;
using static Tests.EditTextWindowTestHost;

namespace Tests;

/// <summary>
/// Opening a file into code mode (issue #645).
/// </summary>
/// <remarks>
/// Opening a file records it in the recent files, so these run in the history service's collection with
/// the profile pointed at a temporary folder.
/// </remarks>
[Collection("History service")]
public sealed class EditTextWindowCodeModeOpenFileTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"text-grab-code-open-{Guid.NewGuid():N}");
    private readonly IDisposable profileScope;

    public EditTextWindowCodeModeOpenFileTests()
    {
        AutomationProfile profile = AutomationProfile.TryCreate(
            ["Text-Grab.exe"],
            name => name == AutomationProfile.ProfileEnvironmentVariable ? root : null)
            ?? throw new InvalidOperationException("Failed to create automation profile.");
        profileScope = AutomationProfile.OverrideCurrentForTests(profile);
        Directory.CreateDirectory(Path.Combine(root, "files"));
    }

    public void Dispose()
    {
        profileScope.Dispose();
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [WpfFact]
    public async Task ReopeningAFileAfterSavingAnEditFromTextMode_KeepsTheSavedText()
    {
        string path = WriteFile("Greeter.cs", "class Greeter { }\r\n");
        EditTextWindow window = CreateWindow(string.Empty);
        await window.OpenPath(path);
        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);

        // Edit and save from text mode, which leaves the code editor holding the text as opened.
        SwitchMode(window, EtwEditorMode.Text);
        const string savedText = "class Greeter { string Name; }\r\n";
        window.Session.Text = savedText;
        File.WriteAllText(path, savedText);

        await window.OpenPath(path);

        Assert.Equal(savedText, window.Session.Text);
        Assert.Equal(savedText, CodeEditor(window).Text);
    }

    [WpfFact]
    public async Task OpeningAFileWithTheTextAlreadyShowing_DoesNotBlankTheWindow()
    {
        const string text = "def greet():\r\n    pass\r\n";
        string path = WriteFile("greet.py", text);
        // Never in code mode, so the code editor is still empty.
        EditTextWindow window = CreateWindow(text);

        await window.OpenPath(path);

        Assert.Equal(text, window.Session.Text);
        Assert.Equal(text, CodeEditor(window).Text);
    }

    [WpfFact]
    public async Task OpeningAFileInCodeMode_LeavesNothingToUndo()
    {
        const string fileText = "{\r\n  \"name\": \"demo\"\r\n}\r\n";
        string path = WriteFile("demo.json", fileText);
        EditTextWindow window = CreateWindow("previous document");
        SwitchMode(window, EtwEditorMode.Code);
        CodeEditor editor = CodeEditor(window);
        editor.Document.Insert(0, "edited ");
        DoEvents();
        Assert.True(editor.CanUndo);

        await window.OpenPath(path);

        // Undoing would bring the previous document back into the newly opened file.
        Assert.False(editor.CanUndo);
        Assert.Equal(fileText, editor.Text);
        Assert.Equal(fileText, window.Session.Text);
    }

    private string WriteFile(string name, string text)
    {
        string path = Path.Combine(root, "files", name);
        File.WriteAllText(path, text);
        return path;
    }
}
