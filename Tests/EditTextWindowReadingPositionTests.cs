using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Text_Grab;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;
using static Tests.EditTextWindowTestHost;

namespace Tests;

public class EditTextWindowReadingPositionTests
{
    [WpfTheory]
    [InlineData(EtwEditorMode.Text)]
    [InlineData(EtwEditorMode.Markdown)]
    [InlineData(EtwEditorMode.Code)]
    [InlineData(EtwEditorMode.Spreadsheet)]
    public void Bookmark_MatchesTheDestinationThumbAndDisappearsWhenClicked(EtwEditorMode mode)
    {
        EditTextWindow window = CreateLongDocument(mode);
        window.Resources.MergedDictionaries.Add(new ThemesDictionary
        {
            Theme = mode is EtwEditorMode.Text or EtwEditorMode.Code ? ApplicationTheme.Dark : ApplicationTheme.Light,
        });
        window.Resources.MergedDictionaries.Add(new ControlsDictionary());
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/Text-Grab;component/Styles/TextBoxStyles.xaml", UriKind.Relative),
        });
        LayOut(window);
        Control editor = mode switch
        {
            EtwEditorMode.Markdown => MarkdownEditor(window),
            EtwEditorMode.Code => CodeEditor(window),
            EtwEditorMode.Spreadsheet => Grid(window),
            _ => RawTextBox(window),
        };
        if (mode == EtwEditorMode.Spreadsheet)
        {
            // App startup sets the DataGrid default to pixel scrolling for the smooth wheel.
            VirtualizingPanel.SetScrollUnit(editor, ScrollUnit.Pixel);
            LayOut(window);
        }

        ScrollViewer viewer = Assert.IsAssignableFrom<ScrollViewer>(WindowUtilities.GetScrollViewer(editor));
        ScrollBar bar = Assert.IsAssignableFrom<ScrollBar>(viewer.Template.FindName("PART_VerticalScrollBar", viewer));

        viewer.ScrollToVerticalOffset(viewer.ScrollableHeight * 0.6);
        LayOut(window);
        double savedOffset = viewer.VerticalOffset;
        (int position, _) = ((int, bool))typeof(EditTextWindow).GetMethod("GetTopOfViewport", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, null)!;
        Assert.True(position > 0);
        viewer.ScrollToTop();
        LayOut(window);

        Button bookmark = OfferBookmark(window, position);
        var host = (FrameworkElement)window.FindName("ReadingEditorHost");
        Assert.Equal(Visibility.Visible, bookmark.Visibility);
        Assert.InRange(bookmark.ActualWidth, 20, 28);
        Assert.Equal(Visibility.Collapsed, Assert.IsType<ContentPresenter>(bookmark.Template.FindName("BookmarkLabel", bookmark)).Visibility);
        Border border = Assert.IsType<Border>(bookmark.Template.FindName("BookmarkBorder", bookmark));
        Assert.Equal(255, Assert.IsType<SolidColorBrush>(border.Background).Color.A);
        Point marker = bookmark.TranslatePoint(new Point(bookmark.ActualWidth, bookmark.ActualHeight / 2), host);
        Assert.Equal(bar.TranslatePoint(new Point(), host).X, marker.X, precision: 1);
        Assert.InRange(marker.Y, host.ActualHeight * 0.5, host.ActualHeight * 0.7);
        Assert.Equal(0, viewer.VerticalOffset);

        bookmark.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, bookmark));
        LayOut(window);

        Assert.Equal(Visibility.Collapsed, bookmark.Visibility);
        Assert.InRange(viewer.VerticalOffset, savedOffset - viewer.ScrollableHeight * 0.015, savedOffset + viewer.ScrollableHeight * 0.015);
        Thumb thumb = bar.Track.Thumb;
        double thumbCenter = thumb.TranslatePoint(new Point(0, thumb.ActualHeight / 2), host).Y;
        Assert.InRange(thumbCenter - marker.Y, -2.0, 2.0);
    }

    [WpfFact]
    public async Task Bookmark_TimesOutWithoutConsumingTheSavedPosition()
    {
        EditTextWindow window = CreateLongDocument(EtwEditorMode.Text);
        int position = RawTextBox(window).GetCharacterIndexFromLineIndex(150);
        Button bookmark = OfferBookmark(window, position);
        Assert.Equal(Visibility.Visible, bookmark.Visibility);

        DispatcherTimer timer = (DispatcherTimer)typeof(EditTextWindow).GetField("_resumeReadingTimer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(window)!;
        var elapsed = new TaskCompletionSource();
        timer.Tick += (_, _) => elapsed.TrySetResult();
        timer.Interval = TimeSpan.FromMilliseconds(1);
        await elapsed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(Visibility.Collapsed, bookmark.Visibility);
        Assert.False(timer.IsEnabled);
        Assert.Equal(position, typeof(EditTextWindow).GetField("_resumePosition", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(window));
    }

    private static EditTextWindow CreateLongDocument(EtwEditorMode mode)
    {
        string separator = mode == EtwEditorMode.Markdown ? "\r\n\r\n" : "\r\n";
        string text = string.Join(separator, Enumerable.Range(1, 400).Select(i => mode switch
        {
            EtwEditorMode.Code => $"int line{i} = {i};",
            EtwEditorMode.Spreadsheet => $"Row {i}\tValue {i}",
            _ => $"This is line {i} of the document.",
        }));
        EditTextWindow window = CreateWindow(text);
        if (mode != EtwEditorMode.Text)
            SwitchMode(window, mode);
        return window;
    }

    private static Button OfferBookmark(EditTextWindow window, int position)
    {
        typeof(EditTextWindow).GetMethod("OfferResumeReadingBookmark", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, [position]);
        LayOut(window);
        return (Button)window.FindName("ResumeReadingButton");
    }
}
