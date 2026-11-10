using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Text_Grab.Core.Abstractions;
using Text_Grab.Services;
using Text_Grab.Views;

namespace Text_Grab.Utilities;

public class OutputUtilities
{
    public static void HandleTextFromOcr(string grabbedText, bool isSingleLine, bool isTable, TextBox? destinationTextBox = null)
    {
        if (isSingleLine && !isTable)
            grabbedText = grabbedText.MakeStringSingleLine();

        if (destinationTextBox is not null)
        {
            // A Spreadsheet-mode window's text box is hidden and its selection/cursor doesn't
            // track the DataGrid's current cell, so route through the structured table model
            // instead of splicing raw text at a stale cursor position (which corrupts whatever
            // row happens to sit there). Applies regardless of whether table mode produced
            // real column structure or a single plain-text value.
            if (Window.GetWindow(destinationTextBox) is EditTextWindow { IsSpreadsheetMode: true } destinationEtw
                && destinationEtw.TryInsertGrabbedTextIntoSpreadsheet(grabbedText))
            {
                destinationTextBox.Focus();
                return;
            }

            // Do it this way instead of append text because it inserts the text at the cursor
            // Then puts the cursor at the end of the newly added text
            // AppendText() just adds the text to the end no matter what.
            destinationTextBox.SelectedText = grabbedText;
            destinationTextBox.Select(destinationTextBox.SelectionStart + grabbedText.Length, 0);
            destinationTextBox.Focus();
            return;
        }

        if (!AppUtilities.TextGrabSettings.NeverAutoUseClipboard)
            _ = CopyToClipboardAsync(grabbedText);

        if (AppUtilities.TextGrabSettings.ShowToast)
            NotificationUtilities.ShowToast(grabbedText);

        WindowUtilities.ShouldShutDown();
    }

    /// <summary>
    /// Puts the grab on the clipboard through <see cref="IClipboardService"/> (v5 F4). Callers are
    /// on the UI thread, an STA thread, where the service runs the write and any retries before
    /// returning, so the write is finished before <see cref="WindowUtilities.ShouldShutDown"/> can
    /// exit the app. Failures are swallowed, as they were around the WPF write this replaced.
    /// </summary>
    private static async Task CopyToClipboardAsync(string text)
    {
        try
        {
            await AppServices.Provider.GetRequiredService<IClipboardService>().SetTextAsync(text);
        }
        catch
        {
        }
    }
}
