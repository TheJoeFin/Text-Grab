using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Text_Grab.Core.Actions;

/// <summary>
/// An action that turns one string into another and needs nothing else: no editor, no window and no
/// selection. Menus apply it to the selection or the whole document; workflows apply it to a step's
/// input.
/// </summary>
/// <remarks>
/// <para>
/// <b>Culture.</b> There is no culture parameter. A culture-sensitive transform uses
/// <see cref="System.Globalization.CultureInfo.CurrentCulture"/>, which is what the Edit Text Window
/// uses for these transforms today. An action whose result depends on a culture the caller chooses
/// (such as toggling case in the window's selected language) is not a text transform.
/// </para>
/// <para>
/// <b>Config.</b> <c>config</c> is the JSON form of <see cref="ActionDescriptor.ConfigType"/>.
/// Property names are matched without regard to case and enums may be given by name or number.
/// Null means "use the defaults"; a transform whose config has a required value throws
/// <see cref="System.ArgumentException"/> when it is missing. A transform without a config type
/// ignores <c>config</c>.
/// </para>
/// <para>
/// <b>Failure.</b> A transform never returns an error message in place of a result. It throws:
/// <see cref="System.ArgumentException"/> for missing or invalid config,
/// <see cref="System.InvalidOperationException"/> with a message fit to show the user when it ran but
/// could not produce a result (for example, Windows AI is unavailable), and
/// <see cref="System.OperationCanceledException"/> when <c>ct</c> is cancelled.
/// </para>
/// <para>
/// <b>Threads.</b> <see cref="ApplyAsync"/> may be called from any thread and never touches UI.
/// </para>
/// </remarks>
public interface ITextTransform
{
    /// <summary>The descriptor, whose <see cref="ActionDescriptor.Kind"/> is <see cref="ActionKind.TextTransform"/>.</summary>
    ActionDescriptor Descriptor { get; }

    /// <summary>Transforms <paramref name="input"/>. See the type remarks for config, culture and failure.</summary>
    ValueTask<string> ApplyAsync(string input, JsonElement? config, CancellationToken ct);
}
