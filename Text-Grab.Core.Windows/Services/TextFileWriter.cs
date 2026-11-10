using System;
using System.IO;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Core.Abstractions;

namespace Text_Grab.Services;

/// <summary>
/// <see cref="ITextFileWriter"/> over System.IO (v5 F4).
///
/// UTF-8 without a byte-order mark, which is what <c>FileUtilities.SaveTextFile</c> writes. That
/// method stays the way to save into Text Grab's own folders, where packaged and unpackaged storage
/// differ; this writer is for absolute paths a user or workflow chose, which a full-trust app reaches
/// with System.IO either way, and it supports appending, which SaveTextFile does not.
/// </summary>
public sealed class TextFileWriter : ITextFileWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="absolutePath"/> is not a fully qualified path.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    public async Task<bool> WriteAsync(string absolutePath, string text, bool append = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        ArgumentNullException.ThrowIfNull(text);

        if (!Path.IsPathFullyQualified(absolutePath))
            throw new ArgumentException("The path must be absolute.", nameof(absolutePath));

        ct.ThrowIfCancellationRequested();

        try
        {
            if (Path.GetDirectoryName(absolutePath) is { Length: > 0 } directory)
                Directory.CreateDirectory(directory);

            if (append)
                await File.AppendAllTextAsync(absolutePath, text, Utf8NoBom, ct);
            else
                await File.WriteAllTextAsync(absolutePath, text, Utf8NoBom, ct);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or SecurityException)
        {
            return false;
        }
    }
}
