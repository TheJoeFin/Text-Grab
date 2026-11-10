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
            await new TesseractGitHubFileDownloader(sharedClient).DownloadFileAsync("eng.traineddata", destination);

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

    [Fact]
    public void Constructor_RejectsNullClient()
    {
        Assert.Throws<ArgumentNullException>(() => new TesseractGitHubFileDownloader(null!));
    }
}
