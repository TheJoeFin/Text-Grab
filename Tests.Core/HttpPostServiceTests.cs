using System.Net;
using System.Net.Http;
using System.Text;
using Text_Grab.Core.Abstractions;
using Text_Grab.Services;

namespace Text_Grab.Tests.Core;

/// <summary>
/// <see cref="HttpPostService"/> (v5 F4) against a fake handler. Nothing here touches the network.
/// </summary>
public class HttpPostServiceTests
{
    private static readonly Uri Endpoint = new("https://hooks.example.test/ingest");

    [Fact]
    public async Task Success_ReturnsStatusAndBody_AndSendsTheBodyAsUtf8Text()
    {
        FakeHandler handler = new(_ => Respond(HttpStatusCode.OK, "accepted"));
        IHttpPostService service = new HttpPostService(new HttpClient(handler));

        HttpPostResult result = await service.PostAsync(Endpoint, "héllo wörld", ct: TestContext.Current.CancellationToken);

        Assert.Equal(new HttpPostResult(200, true, "accepted"), result);
        RecordedRequest sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal(Endpoint, sent.Uri);
        Assert.Equal("héllo wörld", sent.Body);
        Assert.Equal("text/plain; charset=utf-8", sent.ContentType);
    }

    [Fact]
    public async Task NonSuccessStatus_IsReturnedNotThrown()
    {
        FakeHandler handler = new(_ => Respond(HttpStatusCode.InternalServerError, "boom"));
        IHttpPostService service = new HttpPostService(new HttpClient(handler));

        HttpPostResult result = await service.PostAsync(Endpoint, "x", ct: TestContext.Current.CancellationToken);

        Assert.Equal(new HttpPostResult(500, false, "boom"), result);
    }

    [Fact]
    public async Task EmptyResponseBody_IsNull()
    {
        FakeHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        IHttpPostService service = new HttpPostService(new HttpClient(handler));

        HttpPostResult result = await service.PostAsync(Endpoint, "x", ct: TestContext.Current.CancellationToken);

        Assert.Equal(new HttpPostResult(204, true, null), result);
    }

    [Fact]
    public async Task Timeout_ThrowsTimeoutException()
    {
        FakeHandler handler = new(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Respond(HttpStatusCode.OK, "too late");
        });
        IHttpPostService service = new HttpPostService(new HttpClient(handler), TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<TimeoutException>(
            () => service.PostAsync(Endpoint, "x", ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CallerCancellation_ThrowsOperationCanceled_NotTimeout()
    {
        using CancellationTokenSource cts = new();
        FakeHandler handler = new(async (_, ct) =>
        {
            cts.Cancel();
            await Task.Delay(Timeout.Infinite, ct);
            return Respond(HttpStatusCode.OK, "too late");
        });
        IHttpPostService service = new HttpPostService(new HttpClient(handler));

        Exception ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.PostAsync(Endpoint, "x", ct: cts.Token));
        Assert.IsNotType<TimeoutException>(ex);
    }

    [Theory]
    [InlineData("ftp://files.example.test/drop")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("ws://socket.example.test/")]
    public async Task NonHttpScheme_IsRejectedBeforeSending(string uri)
    {
        FakeHandler handler = new(_ => Respond(HttpStatusCode.OK, "should not be reached"));
        IHttpPostService service = new HttpPostService(new HttpClient(handler));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.PostAsync(new Uri(uri), "x", ct: TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task RelativeUri_IsRejected()
    {
        IHttpPostService service = new HttpPostService(new HttpClient(new FakeHandler(_ => Respond(HttpStatusCode.OK, ""))));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.PostAsync(new Uri("/relative", UriKind.Relative), "x", ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResponseBody_IsCappedAt64KB()
    {
        string huge = new('a', HttpPostService.MaxResponseBodyBytes * 3);
        FakeHandler handler = new(_ => Respond(HttpStatusCode.OK, huge));
        IHttpPostService service = new HttpPostService(new HttpClient(handler));

        HttpPostResult result = await service.PostAsync(Endpoint, "x", ct: TestContext.Current.CancellationToken);

        Assert.Equal(HttpPostService.MaxResponseBodyBytes, result.ResponseBody!.Length);
    }

    [Fact]
    public async Task ResponseBody_CapNeverSplitsAMultiByteCharacter()
    {
        // 'é' is two bytes in UTF-8, so a one-byte prefix puts a character across the cap.
        string body = "x" + new string('é', HttpPostService.MaxResponseBodyBytes);
        FakeHandler handler = new(_ => Respond(HttpStatusCode.OK, body));
        IHttpPostService service = new HttpPostService(new HttpClient(handler));

        HttpPostResult result = await service.PostAsync(Endpoint, "x", ct: TestContext.Current.CancellationToken);

        Assert.DoesNotContain('\uFFFD', result.ResponseBody!);
        Assert.True(Encoding.UTF8.GetByteCount(result.ResponseBody!) <= HttpPostService.MaxResponseBodyBytes);
    }

    [Fact]
    public async Task Headers_AreSent_AndContentHeadersReplaceTheDefaults()
    {
        FakeHandler handler = new(_ => Respond(HttpStatusCode.OK, ""));
        IHttpPostService service = new HttpPostService(new HttpClient(handler));
        Dictionary<string, string> headers = new()
        {
            ["X-Api-Key"] = "secret",
            ["Content-Type"] = "application/json; charset=utf-8",
        };

        await service.PostAsync(Endpoint, "{}", headers: headers, ct: TestContext.Current.CancellationToken);

        RecordedRequest sent = Assert.Single(handler.Requests);
        Assert.Equal("secret", sent.Headers["X-Api-Key"]);
        Assert.Equal("application/json; charset=utf-8", sent.ContentType);
    }

    [Fact]
    public async Task ContentTypeParameter_IsUsed()
    {
        FakeHandler handler = new(_ => Respond(HttpStatusCode.OK, ""));
        IHttpPostService service = new HttpPostService(new HttpClient(handler));

        await service.PostAsync(Endpoint, "{}", "application/json", ct: TestContext.Current.CancellationToken);

        Assert.Equal("application/json; charset=utf-8", Assert.Single(handler.Requests).ContentType);
    }

    [Theory]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    [InlineData(HttpStatusCode.PermanentRedirect)]
    public async Task SameHostRedirectThatKeepsTheMethod_IsFollowedWithTheBody(HttpStatusCode status)
    {
        FakeHandler handler = new(request => request.RequestUri!.AbsolutePath == "/ingest"
            ? Redirect(status, "/v2/ingest")
            : Respond(HttpStatusCode.Created, "moved and stored"));
        IHttpPostService service = new HttpPostService(new HttpClient(handler));

        HttpPostResult result = await service.PostAsync(Endpoint, "payload", ct: TestContext.Current.CancellationToken);

        Assert.Equal(new HttpPostResult(201, true, "moved and stored"), result);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(new Uri("https://hooks.example.test/v2/ingest"), handler.Requests[1].Uri);
        Assert.Equal("payload", handler.Requests[1].Body);
    }

    [Theory]
    [InlineData("https://elsewhere.example.test/ingest")]
    [InlineData("http://hooks.example.test/ingest")]
    public async Task RedirectToAnotherHostOrDowngrade_IsNotFollowed(string location)
    {
        FakeHandler handler = new(_ => Redirect(HttpStatusCode.TemporaryRedirect, location));
        IHttpPostService service = new HttpPostService(new HttpClient(handler));

        HttpPostResult result = await service.PostAsync(Endpoint, "payload", ct: TestContext.Current.CancellationToken);

        Assert.Equal(307, result.StatusCode);
        Assert.False(result.IsSuccess);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.SeeOther)]
    public async Task RedirectThatWouldTurnThePostIntoAGet_IsNotFollowed(HttpStatusCode status)
    {
        FakeHandler handler = new(_ => Redirect(status, "/elsewhere"));
        IHttpPostService service = new HttpPostService(new HttpClient(handler));

        HttpPostResult result = await service.PostAsync(Endpoint, "payload", ct: TestContext.Current.CancellationToken);

        Assert.Equal((int)status, result.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task RedirectLoop_StopsAfterTheLimit()
    {
        FakeHandler handler = new(_ => Redirect(HttpStatusCode.TemporaryRedirect, "/ingest"));
        IHttpPostService service = new HttpPostService(new HttpClient(handler));

        HttpPostResult result = await service.PostAsync(Endpoint, "payload", ct: TestContext.Current.CancellationToken);

        Assert.Equal(307, result.StatusCode);
        Assert.Equal(HttpPostService.MaxRedirects + 1, handler.Requests.Count);
    }

    [Fact]
    public void CreateClient_LeavesTimingOutToTheService()
    {
        // Its handler (AllowAutoRedirect = false) is not observable through HttpClient; the
        // redirect tests above cover the service's own handling with a handler that never redirects.
        using HttpClient client = HttpPostService.CreateClient();

        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }

    private static HttpResponseMessage Respond(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "text/plain") };

    private static HttpResponseMessage Redirect(HttpStatusCode status, string location)
    {
        HttpResponseMessage response = new(status);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    private sealed record RecordedRequest(
        HttpMethod Method, Uri? Uri, string? Body, string? ContentType, Dictionary<string, string> Headers);

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            => _respond = (request, _) => Task.FromResult(respond(request));

        public FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
            => _respond = respond;

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Dictionary<string, string> headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value));
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Content?.Headers.ContentType?.ToString(),
                headers));

            return await _respond(request, cancellationToken);
        }
    }
}
