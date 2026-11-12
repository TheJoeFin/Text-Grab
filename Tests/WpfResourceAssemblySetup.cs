using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Tests;

/// <summary>
/// Makes the Text Grab app assembly WPF's resource assembly in this test process, as it is when the
/// app runs, so tests can construct app windows whose XAML loads content files through
/// <c>/Text-Grab;component/...</c> pack URIs (the Edit Text Window's icon is one).
/// </summary>
/// <remarks>
/// WPF treats a <c>;component</c> URI as a loose content file only when its assembly is
/// <see cref="Application.ResourceAssembly"/>, which defaults to the entry assembly (Tests.exe here).
/// The public setter refuses to change it once an entry assembly exists, so the backing fields are set
/// directly. This runs as a module initializer so it happens before WPF caches anything about the
/// resource assembly. Tests.exe has no WPF resources of its own, so nothing depends on the old value.
/// </remarks>
internal static class WpfResourceAssemblySetup
{
    [ModuleInitializer]
    internal static void UseAppAssemblyForWpfResources()
    {
        Assembly appAssembly = typeof(Text_Grab.EditTextWindow).Assembly;

        typeof(Application)
            .GetField("_resourceAssembly", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, appAssembly);

        typeof(UIElement).Assembly
            .GetType("System.Windows.Navigation.BaseUriHelper", throwOnError: true)!
            .GetProperty("ResourceAssembly", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, appAssembly);
    }
}
