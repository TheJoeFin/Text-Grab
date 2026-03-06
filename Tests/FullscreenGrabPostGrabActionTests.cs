using System.Windows.Controls;
using Text_Grab.Models;
using Text_Grab.Views;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Tests;

public class FullscreenGrabPostGrabActionTests
{
    [Fact]
    public void GetPostGrabActionKey_UsesTemplateIdForTemplateActions()
    {
        ButtonInfo action = new("Template Action", "ApplyTemplate_Click", SymbolRegular.Apps24, DefaultCheckState.Off)
        {
            TemplateId = "template-123"
        };

        string key = FullscreenGrab.GetPostGrabActionKey(action);

        Assert.Equal("template:template-123", key);
    }

    [Fact]
    public void GetPostGrabActionKey_FallsBackToButtonTextWhenClickEventMissing()
    {
        ButtonInfo action = new()
        {
            ButtonText = "Custom action"
        };

        string key = FullscreenGrab.GetPostGrabActionKey(action);

        Assert.Equal("text:Custom action", key);
    }

    [WpfFact]
    public void GetActionablePostGrabMenuItems_ExcludesUtilityEntriesAndPreservesOrder()
    {
        ContextMenu contextMenu = new();
        MenuItem firstAction = new()
        {
            Header = "First action",
            Tag = new ButtonInfo("First action", "First_Click", SymbolRegular.Apps24, DefaultCheckState.Off)
        };
        MenuItem utilityItem = new()
        {
            Header = "Customize",
            Tag = "EditPostGrabActions"
        };
        MenuItem secondAction = new()
        {
            Header = "Second action",
            Tag = new ButtonInfo("Second action", "Second_Click", SymbolRegular.Apps24, DefaultCheckState.Off)
        };

        contextMenu.Items.Add(firstAction);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(utilityItem);
        contextMenu.Items.Add(secondAction);
        contextMenu.Items.Add(new MenuItem
        {
            Header = "Close this menu",
            Tag = "ClosePostGrabMenu"
        });

        List<MenuItem> actionableItems = FullscreenGrab.GetActionablePostGrabMenuItems(contextMenu);

        Assert.Collection(actionableItems,
            item => Assert.Same(firstAction, item),
            item => Assert.Same(secondAction, item));
    }

    [WpfFact]
    public void BuildPostGrabActionSnapshot_KeepsChangedTemplateCheckedAndUnchecksOthers()
    {
        ButtonInfo regularAction = new("Trim each line", "TrimEachLine_Click", SymbolRegular.Apps24, DefaultCheckState.Off);
        ButtonInfo firstTemplate = new("Template A", "ApplyTemplate_Click", SymbolRegular.Apps24, DefaultCheckState.Off)
        {
            TemplateId = "template-a"
        };
        ButtonInfo secondTemplate = new("Template B", "ApplyTemplate_Click", SymbolRegular.Apps24, DefaultCheckState.Off)
        {
            TemplateId = "template-b"
        };

        Dictionary<string, bool> snapshot = FullscreenGrab.BuildPostGrabActionSnapshot(
            [
                new MenuItem { Tag = regularAction, IsCheckable = true, IsChecked = true },
                new MenuItem { Tag = firstTemplate, IsCheckable = true, IsChecked = true },
                new MenuItem { Tag = secondTemplate, IsCheckable = true, IsChecked = false }
            ],
            FullscreenGrab.GetPostGrabActionKey(secondTemplate),
            true);

        Assert.True(snapshot[FullscreenGrab.GetPostGrabActionKey(regularAction)]);
        Assert.False(snapshot[FullscreenGrab.GetPostGrabActionKey(firstTemplate)]);
        Assert.True(snapshot[FullscreenGrab.GetPostGrabActionKey(secondTemplate)]);
    }

    [Fact]
    public void ShouldPersistLastUsedState_ForForcedSourceAction_ReturnsTrue()
    {
        ButtonInfo lastUsedAction = new("Remove duplicate lines", "RemoveDuplicateLines_Click", SymbolRegular.Apps24, DefaultCheckState.LastUsed);

        bool shouldPersist = FullscreenGrab.ShouldPersistLastUsedState(
            lastUsedAction,
            previousChecked: true,
            isChecked: true,
            forcePersistActionKey: FullscreenGrab.GetPostGrabActionKey(lastUsedAction));

        Assert.True(shouldPersist);
    }

    [Fact]
    public void ShouldPersistLastUsedState_DoesNotPersistUnchangedNonSourceAction()
    {
        ButtonInfo lastUsedAction = new("Remove duplicate lines", "RemoveDuplicateLines_Click", SymbolRegular.Apps24, DefaultCheckState.LastUsed);

        bool shouldPersist = FullscreenGrab.ShouldPersistLastUsedState(
            lastUsedAction,
            previousChecked: true,
            isChecked: true);

        Assert.False(shouldPersist);
    }
}
