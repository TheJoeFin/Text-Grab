using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Text_Grab.Core.Abstractions;

/// <summary>
/// Sends text to an HTTP endpoint with a POST.
/// </summary>
public interface IHttpPostService
{
    /// <summary>
    /// POSTs <paramref name="body"/> to <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">An absolute <c>http</c> or <c>https</c> URI.</param>
    /// <param name="body">The request body, sent as UTF-8.</param>
    /// <param name="contentType">The body's media type.</param>
    /// <param name="headers">Extra request headers. Content headers are applied to the body.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The final response's status and the start of its body.</returns>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is not an absolute http or https URI.</exception>
    /// <exception cref="TimeoutException">No response arrived within the service's timeout.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    /// <exception cref="System.Net.Http.HttpRequestException">The request failed before a response arrived.</exception>
    Task<HttpPostResult> PostAsync(
        Uri destination,
        string body,
        string contentType = "text/plain",
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken ct = default);
}

/// <summary>
/// The outcome of an <see cref="IHttpPostService.PostAsync"/> call.
/// </summary>
/// <param name="StatusCode">The HTTP status code of the final response.</param>
/// <param name="IsSuccess">Whether <paramref name="StatusCode"/> is in the 2xx range.</param>
/// <param name="ResponseBody">The response body decoded as text and capped in length, or <c>null</c> when the response had none.</param>
public sealed record HttpPostResult(int StatusCode, bool IsSuccess, string? ResponseBody);
