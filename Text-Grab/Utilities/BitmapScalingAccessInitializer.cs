using System.Runtime.CompilerServices;
using Text_Grab.Services;

namespace Text_Grab.Utilities;

/// <summary>
/// Points Text-Grab.Core.Windows' <see cref="BitmapScalingAccess"/> at the app's WPF scaler, so
/// OCR in the app scales exactly as it did before the engine dispatch moved out of the app.
/// </summary>
internal static class BitmapScalingAccessInitializer
{
    /// <summary>
    /// Same reasoning as <see cref="SettingsAccessInitializer"/>: a module initializer rather than
    /// a call in App.appStartup, so the Tests host is covered too. It only stores a delegate.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize() => BitmapScalingAccess.SetScaler(ImageMethods.ScaleBitmapUniform);
}
