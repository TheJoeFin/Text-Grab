namespace Tests;

/// <summary>
/// Tests that construct a real <see cref="Text_Grab.EditTextWindow"/>. Not run in parallel with
/// anything else: constructing one briefly sets a stand-in <see cref="System.Windows.Application.Current"/>,
/// which other tests read from their own threads.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class EditTextWindowInstancesCollection
{
    public const string Name = "Edit Text Window instances";
}
