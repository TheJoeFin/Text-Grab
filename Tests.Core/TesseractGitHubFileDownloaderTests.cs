using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core;

public class TesseractGitHubFileDownloaderTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? UserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3]),
            });
        }
    }

    [Fact]
    public async Task DownloadFileAsync_WithPassedInClient_SendsUserAgentPerRequestAndLeavesClientUnchanged()
    {
        RecordingHandler handler = new();
        using HttpClient sharedClient = new(handler);
        string destination = Path.Combine(Path.GetTempPath(), $"TextGrab_TessDownload_{Guid.NewGuid():N}.traineddata");

        try
        {
            TesseractDownloadResult result = await new TesseractGitHubFileDownloader(sharedClient) { MinimumPlausibleBytes = 1 }
                .DownloadFileAsync("eng.traineddata", destination, cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.Equal(
                "https://raw.githubusercontent.com/tesseract-ocr/tessdata/main/eng.traineddata",
                handler.Request?.RequestUri?.ToString());
            Assert.Equal(HttpMethod.Get, handler.Request?.Method);
            Assert.Equal("Text Grab settings language downloader", handler.UserAgent);
            Assert.Empty(sharedClient.DefaultRequestHeaders.UserAgent);
            Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(destination);
        }
    }

    private sealed class FixedHandler(HttpStatusCode status, byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
    }

    [Fact]
    public async Task DownloadFileAsync_HttpError_ReportsFailureAndWritesNothing()
    {
        using HttpClient client = new(new FixedHandler(HttpStatusCode.NotFound, [1, 2, 3]));
        string destination = Path.Combine(Path.GetTempPath(), $"TextGrab_TessDownload_{Guid.NewGuid():N}.traineddata");

        TesseractDownloadResult result = await new TesseractGitHubFileDownloader(client)
            .DownloadFileAsync("xyz.traineddata", destination, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("404", result.Error);
        Assert.False(File.Exists(destination));
        Assert.False(File.Exists(destination + ".tmp"));
    }

    [Fact]
    public async Task DownloadFileAsync_ImplausiblySmallFile_IsRejectedAndKeepsExistingModel()
    {
        using HttpClient client = new(new FixedHandler(HttpStatusCode.OK, [1, 2, 3]));
        string destination = Path.Combine(Path.GetTempPath(), $"TextGrab_TessDownload_{Guid.NewGuid():N}.traineddata");
        await File.WriteAllBytesAsync(destination, [9, 9, 9], TestContext.Current.CancellationToken);

        try
        {
            TesseractDownloadResult result = await new TesseractGitHubFileDownloader(client)
                .DownloadFileAsync("eng.traineddata", destination, cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Equal([9, 9, 9], await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(destination + ".tmp"));
        }
        finally
        {
            File.Delete(destination);
        }
    }

    [Fact]
    public async Task DownloadFileAsync_Cancelled_ThrowsAndLeavesNoFiles()
    {
        using HttpClient client = new(new FixedHandler(HttpStatusCode.OK, new byte[200_000]));
        string destination = Path.Combine(Path.GetTempPath(), $"TextGrab_TessDownload_{Guid.NewGuid():N}.traineddata");
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new TesseractGitHubFileDownloader(client).DownloadFileAsync("eng.traineddata", destination, cancellationToken: cts.Token));

        Assert.False(File.Exists(destination));
        Assert.False(File.Exists(destination + ".tmp"));
    }

    [Fact]
    public void Constructor_RejectsNullClient()
    {
        Assert.Throws<ArgumentNullException>(() => new TesseractGitHubFileDownloader(null!));
    }
}
