# F1 — DI composition root

| | |
|---|---|
| Track | Foundation |
| Size / risk | Medium / **High** (startup ordering, packaged vs unpackaged, test host) |
| Recommended model | Opus |
| Depends on | — |
| Unblocks | F4, F5, A1, and constructor injection for all new components |
| Branch | `v5/f1-di` |

Read [README.md](README.md) first.

## Goal

Introduce `Microsoft.Extensions.DependencyInjection` with one container built by the app, and make the existing `Singleton<T>` resolve through it, without editing its call sites and without changing when any service is constructed.

## Current state (verify)

- No DI package is referenced directly. (Corrected 2026-11-09, `dev` @ `6edfc7a`: `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.14 was already a transitive dependency of Core, via `NCalc.Core` 7.4.0 → `Microsoft.Extensions.Logging.Abstractions` 10.0.14, and via `Microsoft.Recognizers.Text` → `Microsoft.Extensions.Caching.Memory` 2.2.0. So adding it directly to Core changed no resolved version.)
- `Text-Grab.Core/Utilities/Singleton.cs` (11 lines) is a `ConcurrentDictionary`-backed lazy `new T()`. It lives in **Core**, so it cannot see the app.
- 77 uses in product code: `Singleton<HistoryService>` 52, `<TtsService>` 10, `<LanguageService>` 8, `<WebSearchUrlCatalog>` 6, `<SettingsService>` 1. (`Tests/LanguageServiceTests.cs` adds 4 more `Singleton<LanguageService>` uses.)
- Four delegate seams in `Text-Grab.Core/Services/`: `SettingsAccess`, `UiThreadAccess`, `InputLanguageAccess`, `TtsEngineAccess`. Each is registered by a `[ModuleInitializer]` in `Text-Grab/Utilities/*Initializer.cs`.
- `Text-Grab/App.xaml.cs`: constructor at ~line 50 reads a static `_defaultSettings` to set the render mode; `appStartup` at ~line 610. `App` exposes `HotKeyIds`, `NumberOfRunningInstances`, `TextGrabIcon`, reached by `(App)App.Current` casts in `NotifyIconUtilities.cs` (3), `ImplementAppOptions.cs`, `WindowUtilities.cs`, `SettingsWindow.xaml.cs` (2), `KeysSettings.xaml.cs`. (Clarified 2026-11-09: the last three use the `App.Current is App app` pattern. The `SettingsWindow` and `KeysSettings` casts exist only to pass `app` to `NotifyIconUtilities.RegisterHotKeys(App)` / `UnregisterHotkeys(App)`, whose only use of it is `HotKeyIds`. Removing those casts therefore means changing the two signatures, which also touches `App.appExit`.)
- `HttpClient`s today: `TesseractGitHubFileDownloader` (Core) and `SvgImageLoader` (app, deliberately locked down).
- The `Tests` project loads the app assembly and never raises WPF Startup. See `Tests/TestHostInitializer.cs`, `Tests/SettingsIsolationCollection.cs`, `Tests/StartupTests.cs`.

## Design

**Core** (`Text-Grab.Core`, add `Microsoft.Extensions.DependencyInjection.Abstractions`):

```csharp
namespace Text_Grab.Services;
public static class ServiceAccess
{
    public static void SetProvider(Func<IServiceProvider?> resolver);
    public static void ClearProvider();
    public static bool IsConfigured { get; }
    public static T? TryGet<T>() where T : class;      // null when no provider or not registered
    public static T GetRequired<T>() where T : class;  // throws with a clear message
}
```

**`Singleton<T>`** keeps its public surface. It gains an internal fallback instance, and `Instance` asks the container first:

```csharp
public static T Instance => ServiceAccess.TryGet<T>() ?? FallbackInstance;
internal static T FallbackInstance => /* the existing GetOrAdd */;
```

**One instance, whatever the order.** The container registers each `Singleton<T>`-backed type with a factory that returns `Singleton<T>.FallbackInstance`. Then code that ran before the container existed and code that runs after it see the same object. This is the property that makes the migration safe; test it directly.

**App** (`Text-Grab/Services/AppServices.cs`, add `Microsoft.Extensions.DependencyInjection`):

- `static IServiceProvider Provider` backed by a `Lazy<ServiceProvider>`.
- A `[ModuleInitializer]` calls `ServiceAccess.SetProvider(() => AppServices.Provider)`. The initializer must only store the delegate. It must not build the container or construct any service.
- Registrations: `SettingsService`, `HistoryService`, `TtsService`, `LanguageService`, `WebSearchUrlCatalog` (all through the fallback-instance factory); `ITextGrabSettings` (factory → `SettingsAccess.Current`); one shared `HttpClient`; `AppRuntimeState`.
- `AppRuntimeState` (app-side class) owns `HotKeyIds`, `NumberOfRunningInstances`, `TextGrabIcon`. `App`'s properties forward to it so nothing else has to change in the same commit; then replace the `(App)App.Current` casts with `AppServices.Provider.GetRequiredService<AppRuntimeState>()`.

**The four existing seams stay as they are.** Do not rewrite them onto the container in this phase. Registering `ITextGrabSettings` in the container is enough for new code to inject it.

## Steps

1. Read `App.xaml.cs` start to finish and write down, in your notes, the order in which settings, theme, toast activation, `--windowless`, hotkeys and the tray icon are touched today. That order must not change.
2. Add `ServiceAccess` to Core with unit tests in `Tests.Core` (no provider → `TryGet` null, `GetRequired` throws; fake provider → resolves).
3. Add `FallbackInstance` to `Singleton<T>` and route `Instance` through `ServiceAccess`. Test in `Tests.Core`: with no provider, behavior is unchanged.
4. Add `AppServices` and its module initializer in the app. Add tests in `Tests`: container resolves each registered service; `Singleton<X>.Instance` and `Provider.GetRequiredService<X>()` are the same reference, in both resolution orders.
5. Add `AppRuntimeState`; forward `App`'s three properties; replace the casts one file at a time.
6. Point `TesseractGitHubFileDownloader` at an injected or passed-in `HttpClient` only if it is a small change; otherwise leave it and note it for F4. Leave `SvgImageLoader` alone.
7. Run every gate. Then smoke-test by hand (below).

## Out of scope

- Migrating any of the 77 `Singleton<T>` call sites to constructor injection.
- Converting windows to be DI-constructed. Windows are still created with `new`.
- Rewriting the four delegate seams.
- Any new service interface (F4).

## Verification

- All three test executables pass, with the new tests included.
- `Tests.Core.Windows/TierBoundaryTests` still passes (Core must not gain a Windows or WPF reference from the DI package).
- Manual, on an unpackaged Debug build: normal launch; launch with `--windowless`; launch with `--automation-profile <path>`; a global hotkey opens its window; the tray icon and tray flyout work; a toast click re-activates the app; Settings opens and saves.
- Report that the packaged (MSIX) build and first-run migration still need a check in Visual Studio.

## Done when

- `ServiceAccess` and `AppServices` exist, the container resolves the five services plus `HttpClient`, `ITextGrabSettings` and `AppRuntimeState`.
- No `(App)App.Current` cast remains for `HotKeyIds`, `TextGrabIcon` or `NumberOfRunningInstances`.
- No `Singleton<T>` call site was edited.

## Traps

- `SettingsService` construction touches `ApplicationData.Current` when packaged and runs first-run migration. If the container constructs it earlier or later than today, first run breaks. The fallback-instance factory keeps construction lazy; do not add eager resolution "to warm up".
- A module initializer that builds the container will run during test discovery and during design-time builds. Store a delegate only.
- Tests swap settings through `SettingsAccess.SetResolver` and the settings-isolation collection. Check that container singletons do not cache a settings object across those tests.
- `Singleton<T>` has no `class` constraint and `ServiceAccess.TryGet<T>` does. Add the constraint to `Singleton<T>` only if every current use satisfies it (they do today); otherwise bridge with a cast.
