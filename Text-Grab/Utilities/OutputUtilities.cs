using System.Windows;
using System.Windows.Controls;

namespace Text_Grab.Utilities;

public class OutputUtilities
{
    public static void HandleTextFromOcr(string grabbedText, bool isSingleLine, bool isTable, TextBox? destinationTextBox = null)
    {
        if (isSingleLine && !isTable)
            grabbedText = grabbedText.MakeStringSingleLine();

        if (destinationTextBox is not null)
        {
            // Do it this way instead of append text because it inserts the text at the cursor
            // Then puts the cursor at the end of the newly added text
            // AppendText() just adds the text to the end no matter what.
            destinationTextBox.SelectedText = grabbedText;
            destinationTextBox.Select(destinationTextBox.SelectionStart + grabbedText.Length, 0);
            destinationTextBox.Focus();
            return;
        }

        if (!AppUtilities.TextGrabSettings.NeverAutoUseClipboard)
            try { Clipboard.SetDataObject(grabbedText, true); } catch { }

        if (AppUtilities.TextGrabSettings.ShowToast)
            NotificationUtilities.ShowToast(grabbedText);

        WindowUtilities.ShouldShutDown();
    }
}
