using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Text_Grab.Properties;
using Windows.UI.ViewManagement;

namespace Text_Grab.Services;

internal sealed class MotionPreferences : INotifyPropertyChanged, IDisposable
{
    private readonly Settings _settings;
    private readonly Dispatcher _dispatcher;
    private readonly Func<bool> _systemAnimationsEnabled;
    private readonly UISettings? _uiSettings;
    private bool _lastNotifiedReduceMotion;
    private bool _disposed;

    public static MotionPreferences Instance => AppServices.Provider.GetRequiredService<MotionPreferences>();

    internal MotionPreferences(Settings settings, Dispatcher dispatcher)
        : this(settings, dispatcher, new UISettings())
    {
    }

    private MotionPreferences(Settings settings, Dispatcher dispatcher, UISettings uiSettings)
        : this(settings, dispatcher, () => uiSettings.AnimationsEnabled)
    {
        _uiSettings = uiSettings;
        _uiSettings.AnimationsEnabledChanged += AnimationsEnabledChanged;
        SystemParameters.StaticPropertyChanged += SystemParametersChanged;
    }

    internal MotionPreferences(Settings settings, Dispatcher dispatcher, Func<bool> systemAnimationsEnabled)
    {
        _settings = settings;
        _dispatcher = dispatcher;
        _systemAnimationsEnabled = systemAnimationsEnabled;
        _lastNotifiedReduceMotion = ReduceMotion;
        _settings.PropertyChanged += SettingsChanged;
        _dispatcher.ShutdownStarted += DispatcherShutdownStarted;
    }

    // WPF caches ClientAreaAnimation. Read the WinRT setting so animation callers don't
    // have to wait for WPF to invalidate that cache or for this service to notify bindings.
    public bool ReduceMotion => _settings.ReduceMotion || !_systemAnimationsEnabled();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(Settings.ReduceMotion))
            Refresh();
    }

    private void SystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation))
            _dispatcher.InvokeAsync(Refresh);
    }

    private void AnimationsEnabledChanged(UISettings sender, UISettingsAnimationsEnabledChangedEventArgs args) =>
        _dispatcher.InvokeAsync(Refresh);

    internal void Refresh()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.InvokeAsync(Refresh);
            return;
        }

        if (_disposed)
            return;

        bool reduceMotion = ReduceMotion;
        if (reduceMotion == _lastNotifiedReduceMotion)
            return;

        _lastNotifiedReduceMotion = reduceMotion;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReduceMotion)));
    }

    private void DispatcherShutdownStarted(object? sender, EventArgs e) => Dispose();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _dispatcher.ShutdownStarted -= DispatcherShutdownStarted;
        _settings.PropertyChanged -= SettingsChanged;
        if (_uiSettings is not null)
        {
            SystemParameters.StaticPropertyChanged -= SystemParametersChanged;
            _uiSettings.AnimationsEnabledChanged -= AnimationsEnabledChanged;
        }
    }
}
