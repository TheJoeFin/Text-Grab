namespace Text_Grab.Core.Abstractions;

/// <summary>
/// Shows a Windows toast notification. Display only: clicking the toast does whatever the app
/// already does for a toast without arguments.
/// </summary>
public interface IToastService
{
    /// <summary>
    /// Shows a toast. Never throws: a host that cannot show toasts (no package identity or
    /// registered app ID) simply shows nothing.
    /// </summary>
    void Show(string title, string body);
}
