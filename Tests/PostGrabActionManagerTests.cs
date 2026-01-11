using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Tests;

public class PostGrabActionManagerTests
{
    [Fact]
    public void GetDefaultPostGrabActions_ReturnsExpectedCount()
    {
        // Arrange & Act
        List<ButtonInfo> actions = PostGrabActionManager.GetDefaultPostGrabActions();

        // Assert
        Assert.NotNull(actions);
        Assert.Equal(6, actions.Count); // Should have 6 default actions
    }

    [Fact]
    public void GetDefaultPostGrabActions_ContainsExpectedActions()
    {
        // Arrange & Act
        List<ButtonInfo> actions = PostGrabActionManager.GetDefaultPostGrabActions();

        // Assert
        Assert.Contains(actions, a => a.ButtonText == "Fix GUIDs");
        Assert.Contains(actions, a => a.ButtonText == "Trim each line");
        Assert.Contains(actions, a => a.ButtonText == "Remove duplicate lines");
        Assert.Contains(actions, a => a.ButtonText == "Web Search");
        Assert.Contains(actions, a => a.ButtonText == "Try to insert text");
        Assert.Contains(actions, a => a.ButtonText == "Translate to system language");
    }

    [Fact]
    public void GetDefaultPostGrabActions_AllHaveClickEvents()
    {
        // Arrange & Act
        List<ButtonInfo> actions = PostGrabActionManager.GetDefaultPostGrabActions();

        // Assert
        Assert.All(actions, action =>
        {
            Assert.False(string.IsNullOrEmpty(action.ClickEvent));
        });
    }

    [Fact]
    public void GetDefaultPostGrabActions_AllHaveSymbols()
    {
        // Arrange & Act
        List<ButtonInfo> actions = PostGrabActionManager.GetDefaultPostGrabActions();

        // Assert
        Assert.All(actions, action =>
        {
            Assert.True(action.IsSymbol);
        });
    }

    [Fact]
    public void GetDefaultPostGrabActions_AllMarkedForFullscreenGrab()
    {
        // Arrange & Act
        List<ButtonInfo> actions = PostGrabActionManager.GetDefaultPostGrabActions();

        // Assert
        Assert.All(actions, action =>
        {
            Assert.True(action.IsRelevantForFullscreenGrab);
            Assert.False(action.IsRelevantForEditWindow);
        });
    }

    [Fact]
    public void GetDefaultPostGrabActions_AllHaveInputGestureText()
    {
        // Arrange & Act
        List<ButtonInfo> actions = PostGrabActionManager.GetDefaultPostGrabActions();

        // Assert
        Assert.All(actions, action =>
        {
            Assert.False(string.IsNullOrEmpty(action.InputGestureText));
            Assert.StartsWith("CTRL +", action.InputGestureText);
        });
    }

    [Fact]
    public async Task ExecutePostGrabAction_CorrectGuid_TransformsText()
    {
        // Arrange
        ButtonInfo action = PostGrabActionManager.GetDefaultPostGrabActions()
            .First(a => a.ClickEvent == "CorrectGuid_Click");
        string input = "123e4567-e89b-12d3-a456-426614174OOO"; // Has O's instead of 0's

        // Act
        string result = await PostGrabActionManager.ExecutePostGrabAction(action, input);

        // Assert
        Assert.Contains("000", result); // Should have corrected O's to 0's
    }

    [Fact]
    public async System.Threading.Tasks.Task ExecutePostGrabAction_RemoveDuplicateLines_RemovesDuplicates()
    {
        // Arrange
        ButtonInfo action = PostGrabActionManager.GetDefaultPostGrabActions()
            .First(a => a.ClickEvent == "RemoveDuplicateLines_Click");
        string input = $"Line 1{Environment.NewLine}Line 2{Environment.NewLine}Line 1{Environment.NewLine}Line 3";

        // Act
        string result = await PostGrabActionManager.ExecutePostGrabAction(action, input);

        // Assert
        string[] lines = result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.Single(lines, l => l == "Line 1");
    }

    [Fact]
    public void GetCheckState_DefaultOff_ReturnsFalse()
    {
        // Arrange
        ButtonInfo action = new("Test", "Test_Click", Wpf.Ui.Controls.SymbolRegular.Apps24, DefaultCheckState.Off);

        // Act
        bool result = PostGrabActionManager.GetCheckState(action);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetCheckState_DefaultOn_ReturnsTrue()
    {
        // Arrange
        ButtonInfo action = new("Test", "Test_Click", Wpf.Ui.Controls.SymbolRegular.Apps24, DefaultCheckState.On);

        // Act
        bool result = PostGrabActionManager.GetCheckState(action);

        // Assert
        Assert.True(result);
    }
}
