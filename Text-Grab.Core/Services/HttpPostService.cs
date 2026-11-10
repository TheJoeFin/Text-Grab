using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Core.Abstractions;

namespace Text_Grab.Services;

/// <summary>
/// <see cref="IHttpPostService"/> over an injected <see cref="HttpClient"/> (v5 F4).
///
/// Guard rails, all enforced here rather than left to the client:
/// <list type="bullet">
/// <item>Only absolute <c>http</c> and <c>https</c> destinations.</item>
/// <item>A timeout per call (<see cref="DefaultTimeout"/> unless one is given), reported as
/// <see cref="TimeoutException"/>, distinct from the caller's own cancellation.</item>
/// <item>At most <see cref="MaxResponseBodyBytes"/> of the response body is read.</item>
/// <item>No redirect to a different host. The client must not follow redirects itself - build it
/// with <see cref="CreateClient"/>. The service re-sends the POST only for a 307 or 308 that stays
/// on the same host and does not downgrade https to http. Any other redirect is returned to the
/// caller as the result.</item>
/// <item>Nothing is logged, so the request body never is.</item>
/// </list>
///
/// The app's shared <c>HttpClient</c> (F1) follows redirects - the Tesseract downloader relies on
/// that - so this service gets its own client from <see cref="CreateClient"/> instead.
/// </summary>
public sealed class HttpPostService : IHttpPostService
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public const int MaxResponseBodyBytes = 64 * 1024;

    internal const int MaxRedirects = 5;

    private readonly HttpClient _client;
    private readonly TimeSpan _timeout;

    public HttpPostService(HttpClient client, TimeSpan? timeout = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _timeout = timeout ?? DefaultTimeout;

        if (_timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), _timeout, "The timeout must be positive.");
    }

    /// <summary>
    /// A client suited to this service: it does not follow redirects, recycles pooled
    /// connections so it sees DNS changes, and leaves timing out to the service.
    /// </summary>
    public static HttpClient CreateClient() => new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    /// <inheritdoc/>
    public async Task<HttpPostResult> PostAsync(
        Uri destination,
        string body,
        string contentType = "text/plain",
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(body);

        if (!IsHttpOrHttps(destination))
            throw new ArgumentException("Only absolute http and https URIs can be posted to.", nameof(destination));

        if (!MediaTypeHeaderValue.TryParse(contentType, out _))
            throw new ArgumentException($"'{contentType}' is not a valid media type.", nameof(contentType));

        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutSource.CancelAfter(_timeout);

        try
        {
            Uri current = destination;
            for (int hop = 0; ; hop++)
            {
                using HttpRequestMessage request = BuildRequest(current, body, contentType, headers);
                using HttpResponseMessage response = await _client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);

                if (hop < MaxRedirects && TryGetFollowableRedirect(current, response, out Uri? next))
                {
                    current = next;
                    continue;
                }

                string? responseBody = await ReadCappedBodyAsync(response.Content, timeoutSource.Token);
                return new HttpPostResult((int)response.StatusCode, response.IsSuccessStatusCode, responseBody);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Our timeout fired, or the client's own Timeout did; either way the caller did not cancel.
            throw new TimeoutException($"No response from {destination.Host} within {_timeout.TotalSeconds:0.#} s.");
        }
    }

    private static bool IsHttpOrHttps(Uri uri)
        => uri.IsAbsoluteUri && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static HttpRequestMessage BuildRequest(
        Uri destination,
        string body,
        string contentType,
        IReadOnlyDictionary<string, string>? headers)
    {
        MediaTypeHeaderValue mediaType = MediaTypeHeaderValue.Parse(contentType);
        mediaType.CharSet ??= "utf-8";

        StringContent content = new(body, Encoding.UTF8, mediaType);
        HttpRequestMessage request = new(HttpMethod.Post, destination) { Content = content };

        if (headers is null)
            return request;

        foreach ((string name, string value) in headers)
        {
            if (request.Headers.TryAddWithoutValidation(name, value))
                continue;

            // Not a request header: a content header (Content-Type, Content-Language, ...)
            // replaces whatever the content already carries.
            content.Headers.Remove(name);
            if (!content.Headers.TryAddWithoutValidation(name, value))
            {
                request.Dispose();
                throw new ArgumentException($"'{name}' cannot be sent as a request header.", nameof(headers));
            }
        }

        return request;
    }

    private static bool TryGetFollowableRedirect(Uri current, HttpResponseMessage response, [NotNullWhen(true)] out Uri? next)
    {
        next = null;

        // 307 and 308 keep the method and body. 301/302/303 would turn the POST into a GET.
        if (response.StatusCode is not (HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect))
            return false;

        if (response.Headers.Location is not Uri location)
            return false;

        Uri target = location.IsAbsoluteUri ? location : new Uri(current, location);

        if (!IsHttpOrHttps(target))
            return false;

        if (!string.Equals(target.IdnHost, current.IdnHost, StringComparison.OrdinalIgnoreCase))
            return false;

        if (current.Scheme == Uri.UriSchemeHttps && target.Scheme == Uri.UriSchemeHttp)
            return false;

        next = target;
        return true;
    }

    private static async Task<string?> ReadCappedBodyAsync(HttpContent content, CancellationToken ct)
    {
        await using Stream stream = await content.ReadAsStreamAsync(ct);

        byte[] buffer = new byte[MaxResponseBodyBytes];
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total), ct);
            if (read == 0)
                break;
            total += read;
        }

        if (total == 0)
            return null;

        Encoding encoding = GetEncoding(content.Headers.ContentType?.CharSet);

        // flush: false holds back a multi-byte character cut in half by the cap instead of
        // emitting a replacement character for it.
        Decoder decoder = encoding.GetDecoder();
        char[] chars = new char[encoding.GetMaxCharCount(total)];
        decoder.Convert(buffer, 0, total, chars, 0, chars.Length, flush: false, out _, out int charsUsed, out _);

        string text = new(chars, 0, charsUsed);
        return text.Length > 0 && text[0] == '﻿' ? text[1..] : text;
    }

    private static Encoding GetEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
            return Encoding.UTF8;

        try
        {
            return Encoding.GetEncoding(charset.Trim('"', ' '));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }
}
