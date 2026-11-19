using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Text_Grab;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;
using static Tests.EditTextWindowTestHost;

namespace Tests;

public class EditTextWindowSpeechTests
{
    [WpfFact]
    public Task SpeechMenuReadsSelectionOrDocumentAndStopsPlayback() => WithSpeechService(async (service, engine) =>
    {
        EditTextWindow window = CreateWindow(" \t\r\n");
        TextBox editor = RawTextBox(window);
        RoutedCommand read = GetMenuCommand(window, "ReadAloudMenuItem");
        RoutedCommand stop = GetMenuCommand(window, "StopSpeakingMenuItem");

        Assert.False(read.CanExecute(null, editor));
        Assert.False(stop.CanExecute(null, editor));

        editor.Text = "first second";
        editor.Select("first ".Length, "second".Length);
        Assert.True(read.CanExecute(null, editor));
        read.Execute(null, editor);
        Assert.Equal("second", await engine.ReadRequestAsync());
        Assert.Equal("first second", editor.Text);
        Assert.Equal("second", editor.SelectedText);

        Assert.True(stop.CanExecute(null, editor));
        stop.Execute(null, editor);
        TaskCompletionSource idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
        service.RunWhenIdle(() => idle.SetResult());
        await idle.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(stop.CanExecute(null, editor));

        editor.Select(0, 0);
        read.Execute(null, editor);
        Assert.Equal("first second", await engine.ReadRequestAsync());
    });

    [WpfFact]
    public Task ReadAloudReadsRenderedMarkdownSelection() => WithSpeechService(async (_, engine) =>
    {
        EditTextWindow window = CreateWindow("first\r\n\r\n**second**");
        SwitchMode(window, EtwEditorMode.Markdown);
        RichTextBox editor = MarkdownEditor(window);
        Paragraph lastParagraph = Assert.IsType<Paragraph>(editor.Document.Blocks.LastBlock);
        editor.Selection.Select(lastParagraph.ContentStart, lastParagraph.ContentEnd);

        GetMenuCommand(window, "ReadAloudMenuItem").Execute(null, editor);

        Assert.Equal("second", (await engine.ReadRequestAsync()).Trim());
    });

    [WpfFact]
    public Task ReadAloudReadsOnlySelectedSpreadsheetCells() => WithSpeechService(async (_, engine) =>
    {
        EditTextWindow window = CreateWindow("first\tsecond\r\nthird\tfourth");
        SwitchMode(window, EtwEditorMode.Spreadsheet);
        DataGrid grid = Grid(window);
        grid.SelectedCells.Clear();
        grid.SelectedCells.Add(new DataGridCellInfo(grid.Items[0], grid.Columns[1]));
        grid.SelectedCells.Add(new DataGridCellInfo(grid.Items[1], grid.Columns[1]));

        GetMenuCommand(window, "ReadAloudMenuItem").Execute(null, grid);

        Assert.Equal($"second{Environment.NewLine}fourth", await engine.ReadRequestAsync());
    });

    private static RoutedCommand GetMenuCommand(EditTextWindow window, string name)
        => Assert.IsType<RoutedCommand>(Assert.IsType<MenuItem>(window.FindName(name)).Command);

    private static async Task WithSpeechService(Func<TtsService, RecordingSpeechEngine, Task> test)
    {
        RecordingSpeechEngine engine = new();
        TtsService service = new() { Engine = engine };
        SpeechServiceProvider provider = new(service);
        ServiceAccess.SetProvider(() => provider);
        try
        {
            await test(service, engine);
        }
        finally
        {
            ServiceAccessInitializer.Initialize();
            service.Stop();
        }
    }

    private sealed class SpeechServiceProvider(TtsService service) : IServiceProvider
    {
        public object? GetService(Type type)
            => type == typeof(TtsService) ? service : AppServices.Provider.GetService(type);
    }

    private sealed class RecordingSpeechEngine : ITtsEngine
    {
        private readonly Channel<string> requests = Channel.CreateUnbounded<string>();

        public Task<string> ReadRequestAsync()
            => requests.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        public async Task SpeakAsync(string text, CancellationToken ct)
        {
            await requests.Writer.WriteAsync(text, ct);
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
    }
}
