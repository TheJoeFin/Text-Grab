using Text_Grab.Core.Abstractions;
using Text_Grab.Services;

namespace Text_Grab.Tests.Core.Windows;

/// <summary>
/// <see cref="TextFileWriter"/> (v5 F4) against a fresh temp folder per test.
/// </summary>
public sealed class TextFileWriterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tg-text-writer-{Guid.NewGuid():N}");
    private readonly ITextFileWriter _writer = new TextFileWriter();

    public TextFileWriterTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Write_CreatesTheFileAsUtf8WithoutBom()
    {
        string path = Path.Combine(_root, "new.txt");

        bool written = await _writer.WriteAsync(path, "grabbed ✓ text", ct: TestContext.Current.CancellationToken);

        Assert.True(written);
        byte[] bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "file starts with a UTF-8 BOM");
        Assert.Equal("grabbed ✓ text", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Write_ReplacesTheWholeOfAnExistingFile()
    {
        string path = Path.Combine(_root, "existing.txt");
        await File.WriteAllTextAsync(path, "a much longer earlier version of the file", TestContext.Current.CancellationToken);

        bool written = await _writer.WriteAsync(path, "short", ct: TestContext.Current.CancellationToken);

        Assert.True(written);
        Assert.Equal("short", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Append_AddsToTheEnd_AndCreatesTheFileIfMissing()
    {
        string path = Path.Combine(_root, "log.txt");

        Assert.True(await _writer.WriteAsync(path, "first\r\n", append: true, ct: TestContext.Current.CancellationToken));
        Assert.True(await _writer.WriteAsync(path, "second\r\n", append: true, ct: TestContext.Current.CancellationToken));

        Assert.Equal("first\r\nsecond\r\n", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Write_CreatesMissingDirectories()
    {
        string path = Path.Combine(_root, "a", "b", "c", "deep.txt");

        bool written = await _writer.WriteAsync(path, "deep", ct: TestContext.Current.CancellationToken);

        Assert.True(written);
        Assert.Equal("deep", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Write_ReturnsFalseWhenTheFileSystemRefuses()
    {
        // The target is an existing directory, which cannot be opened as a file.
        string path = Path.Combine(_root, "occupied");
        Directory.CreateDirectory(path);

        bool written = await _writer.WriteAsync(path, "nope", ct: TestContext.Current.CancellationToken);

        Assert.False(written);
    }

    [Theory]
    [InlineData("relative.txt")]
    [InlineData(@"folder\relative.txt")]
    [InlineData(@"\rooted-but-no-drive.txt")]
    public async Task Write_RejectsAPathThatIsNotAbsolute(string path)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _writer.WriteAsync(path, "x", ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Write_HonoursAnAlreadyCancelledToken()
    {
        string path = Path.Combine(_root, "cancelled.txt");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _writer.WriteAsync(path, "x", ct: new CancellationToken(canceled: true)));
        Assert.False(File.Exists(path));
    }
}
