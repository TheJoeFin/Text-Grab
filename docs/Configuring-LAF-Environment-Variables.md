# Configuring LAF environment variables

Text-Grab's on-device text AI — translation, summarize, rewrite, text-to-table, extract RegEx —
runs on Phi Silica through `Microsoft.Windows.AI.Text.LanguageModel`. Microsoft ships that model as
a **Limited Access Feature (LAF)**, so an app has to unlock it before any call will succeed:

```csharp
LimitedAccessFeatures.TryUnlockFeature(
    "com.microsoft.windows.ai.languagemodel",
    token,
    $"{publisherId} has registered their use of com.microsoft.windows.ai.languagemodel with Microsoft and agrees to the terms of use.");
```

Without a valid token the call returns `LimitedAccessFeatureStatus.Unknown` and every AI request
fails with *"Access is denied. Limited Access Feature is not available:
com.microsoft.windows.ai.languagemodel."*

Tokens are issued per publisher ID by Microsoft at <https://aka.ms/laffeatures>. The publisher ID is
the hash half of the package family name (`40087JoeFinApps.TextGrab_<hash>`).

**The token is a secret. It must never be committed to this repository.**

## The two values

| Name | Meaning |
| --- | --- |
| `LAF_TOKEN` | The unlock token Microsoft issued for `com.microsoft.windows.ai.languagemodel`. |
| `LAF_PUBLISHER_ID` | The publisher ID the token was issued against, used to build the usage string. |

Set `LAF_PUBLISHER_ID` explicitly rather than relying on the fallback. `LimitedAccessFeatureUtilities`
derives it from `Package.Current.Id.FamilyName` when it is unset, and a locally sideloaded MSIX
signed with a development certificate has a *different* publisher hash than the Store package — so
the fallback would build a usage string the token was not issued for.

## Local development

Persist both values for your user account once, then restart Visual Studio or your shell so it picks
them up:

```powershell
setx LAF_TOKEN "<token>"
setx LAF_PUBLISHER_ID "<publisher-id>"
```

`Text-Grab.csproj` defaults the `LafToken` / `LafPublisherId` MSBuild properties from these
environment variables, so ordinary `dotnet build` and Visual Studio builds bake the token in without
any extra flags.

## Explicit build-time injection

The properties can also be passed directly, which is what CI does:

```powershell
dotnet build Text-Grab/Text-Grab.csproj -p:LafToken="<token>" -p:LafPublisherId="<publisher-id>"
```

The project maps them into assembly metadata:

| MSBuild property | `AssemblyMetadata` key |
| --- | --- |
| `LafToken` | `LAF_TOKEN` |
| `LafPublisherId` | `LAF_PUBLISHER_ID` |

At runtime `LimitedAccessFeatureUtilities.GetSetting` reads the assembly metadata first and falls
back to the environment variables, so a build with neither still runs — it just reports the feature
as unavailable instead of crashing.

## CI

`Release.yml` and `buildDev.yml` read the repository secrets `LAF_TOKEN` and `LAF_PUBLISHER_ID` and
forward them to every `dotnet publish` step. Add them under **Settings → Secrets and variables →
Actions**. If they are missing the build still succeeds; the published binaries simply ship without
working on-device text AI.
