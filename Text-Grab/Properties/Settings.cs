using System.Configuration;
using Text_Grab.Interfaces;
using Text_Grab.Utilities;

namespace Text_Grab.Properties;

// The generated Settings class (Settings.Designer.cs) carries no SettingsProvider
// attribute, so it defaults to LocalFileSettingsProvider and always reads/writes the
// per-user user.config. Attaching AutomationSettingsProvider here redirects classic
// settings into the active automation profile's isolated directory; when no profile
// is active the provider transparently defers to its LocalFileSettingsProvider base,
// so normal runs are unaffected. This lives in a hand-written partial so it survives
// SettingsSingleFileGenerator regenerating Settings.Designer.cs.
//
// This partial also declares ITextGrabSettings, which is how Text-Grab.Core reads settings
// without depending on the app. Every member of that interface is already implemented by the
// generated properties (and by ApplicationSettingsBase.Save), so there is nothing to write here -
// declaring the interface is the whole implementation. If a build breaks on a newly added
// interface member, the fix belongs in Settings.settings, not in a forwarding property here.
[SettingsProvider(typeof(AutomationSettingsProvider))]
internal sealed partial class Settings : ITextGrabSettings
{
}
