using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Text_Grab.Controls;

public partial class ExternalLinkConfirmationContent : UserControl
{
    public ExternalLinkConfirmationContent(Uri uri)
    {
        InitializeComponent();
        bool isEmail = uri.Scheme == Uri.UriSchemeMailto;
        ActionDescription.Text = isEmail
            ? "This opens your email app. Review all recipients and message details before sending."
            : "This opens your calling app and may start a call. Charges may apply.";
        TargetTextBox.Text = Uri.UnescapeDataString(uri.AbsoluteUri);
        TargetTextBox.ToolTip = uri.AbsoluteUri;
    }

    private void WarningButton_Click(object sender, RoutedEventArgs e)
    {
        WarningPopup.IsOpen = !WarningPopup.IsOpen;
    }

    private void Content_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && WarningPopup.IsOpen)
        {
            WarningPopup.IsOpen = false;
            e.Handled = true;
        }
    }

    private void Content_Unloaded(object sender, RoutedEventArgs e)
    {
        WarningPopup.IsOpen = false;
    }
}
