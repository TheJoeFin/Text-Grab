using System;
using System.Linq;
using Text_Grab.Core.Actions;

namespace Text_Grab.Tests.Core;

public class CoreActionsTests
{
    /// <summary>
    /// Every action ID Core registers. IDs are persisted in users' layouts, workflows and button
    /// settings, so this list only ever grows: never rename or remove an entry to make this pass.
    /// </summary>
    private static readonly string[] PinnedIds =
    [
    ];

    private static ActionRegistry CreateRegistry()
    {
        ActionRegistry registry = new();
        CoreActions.RegisterAll(registry);
        return registry;
    }

    [Fact]
    public void RegisterAll_RegistersExactlyThePinnedIds()
    {
        string[] registered = [.. CreateRegistry().All.Select(d => d.Id).Order(StringComparer.Ordinal)];

        Assert.Equal(PinnedIds.Order(StringComparer.Ordinal), registered);
    }
}
