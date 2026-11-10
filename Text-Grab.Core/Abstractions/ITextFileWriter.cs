using System.Threading;
using System.Threading.Tasks;

namespace Text_Grab.Core.Abstractions;

/// <summary>
/// Writes text files to absolute paths.
/// </summary>
public interface ITextFileWriter
{
    /// <summary>
    /// Writes <paramref name="text"/> as UTF-8 to <paramref name="absolutePath"/>, replacing the file
    /// or, with <paramref name="append"/>, adding to its end. A missing parent directory is
    /// created. Returns <c>false</c> when the file system refuses the write.
    /// </summary>
    Task<bool> WriteAsync(string absolutePath, string text, bool append = false, CancellationToken ct = default);
}
