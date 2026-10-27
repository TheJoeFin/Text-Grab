using System.Threading;
using System.Threading.Tasks;

namespace Text_Grab.Interfaces;

public interface ITtsEngine
{
    Task SpeakAsync(string text, CancellationToken ct);
}
