using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Text_Grab.Controls;

namespace Text_Grab.Utilities;

internal static class ExternalLinkLauncher
{
    internal static Task<bool> OpenAsync(string? target)
    {
        return OpenAsync(target, info => Process.Start(info), ShowErrorAsync, ConfirmAsync);
    }

    internal static async Task<bool> OpenAsync(
        string? target,
        Action<ProcessStartInfo> startProcess,
        Func<string, Task> showErrorAsync,
        Func<Uri, Task<bool>> confirmAsync)
    {
        if (!ExternalLinkUtilities.TryCreate(target, out Uri? uri))
        {
            Debug.WriteLine("External link blocked: the target is not a supported HTTP, HTTPS, mailto, or tel URI.");
            await showErrorAsync("This link is not supported. Only HTTP, HTTPS, email links with a recipient and optional subject/body, and telephone numbers with an optional extension can be opened.");
            return false;
        }

        if (ExternalLinkUtilities.RequiresConfirmation(uri) && !await confirmAsync(uri))
            return false;

        try
        {
            startProcess(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Debug.WriteLine($"Opening an external link failed ({ex.GetType().Name}, HRESULT: 0x{ex.HResult:X8}).");
            await showErrorAsync("The link could not be opened in its default app.");
            return false;
        }
    }

    internal static MenuItem? CreateMenuItem(string? target, Func<string, Task<bool>>? openAsync = null)
    {
        if (!ExternalLinkUtilities.TryCreate(target, out Uri? uri))
            return null;

        string host = uri.HostNameType == UriHostNameType.IPv6 ? $"[{uri.IdnHost}]" : uri.IdnHost;
        string port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        string absoluteUri = uri.AbsoluteUri;
        string label = uri.Scheme switch
        {
            "mailto" => $"Email: {Uri.UnescapeDataString(absoluteUri["mailto:".Length..].Split('?')[0])}",
            "tel" => $"Call: {uri.AbsolutePath}",
            _ => $"Go to: {uri.Scheme}://{host}{port}"
        };
        MenuItem menuItem = new()
        {
            Header = new TextBlock { Text = label },
            ToolTip = absoluteUri
        };
        AutomationProperties.SetName(menuItem, label);

        openAsync ??= OpenAsync;
        menuItem.Click += async (_, _) => await openAsync(absoluteUri);
        return menuItem;
    }

    internal static Wpf.Ui.Controls.MessageBox CreateConfirmationDialog(Uri uri)
    {
        bool isEmail = uri.Scheme == Uri.UriSchemeMailto;
        Wpf.Ui.Controls.MessageBox dialog = new()
        {
            Title = isEmail ? "Open Email Link?" : "Open Telephone Link?",
            Content = new ExternalLinkConfirmationContent(uri),
            PrimaryButtonText = isEmail ? "Open email app" : "Open calling app",
            CloseButtonText = "Cancel",
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        if (Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive) is Window owner)
            dialog.Owner = owner;

        return dialog;
    }

    private static async Task<bool> ConfirmAsync(Uri uri)
    {
        Wpf.Ui.Controls.MessageBoxResult result = await CreateConfirmationDialog(uri).ShowDialogAsync();
        return result == Wpf.Ui.Controls.MessageBoxResult.Primary;
    }

    private static async Task ShowErrorAsync(string message)
    {
        await new Wpf.Ui.Controls.MessageBox
        {
            Title = "Unable to Open Link",
            Content = message,
            CloseButtonText = "OK"
        }.ShowDialogAsync();
    }
}
