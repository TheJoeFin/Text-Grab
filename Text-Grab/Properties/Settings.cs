using System.Configuration;
using Text_Grab.Utilities;

namespace Text_Grab.Properties;

// The generated Settings class (Settings.Designer.cs) carries no SettingsProvider
// attribute, so it defaults to LocalFileSettingsProvider and always reads/writes the
// per-user user.config. Attaching AutomationSettingsProvider here redirects classic
// settings into the active automation profile's isolated directory; when no profile
// is active the provider transparently defers to its LocalFileSettingsProvider base,
// so normal runs are unaffected. This lives in a hand-written partial so it survives
// SettingsSingleFileGenerator regenerating Settings.Designer.cs.
[SettingsProvider(typeof(AutomationSettingsProvider))]
internal sealed partial class Settings
{
}
