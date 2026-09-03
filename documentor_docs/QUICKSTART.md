# Quickstart Guide

This guide details the configuration, command-line arguments, environment variables, and directory structure used when executing and configuring `Text-Grab.exe`.

> **Note:** Explicit build or installation steps (such as package managers or compilation prerequisites) are not defined in the source files provided. Execution relies on running `Text-Grab.exe`.

---

## Command-Line Arguments

You can pass the following command-line flags and arguments to `Text-Grab.exe`:

* **Automation Flags:**
  * `--automation-profile <path>` or `--automation-profile=<path>`  
    Specifies the root directory path for an automation profile.
  * `--automation-system-integration`  
    Enables system integration for the automation profile.
  * `--automation-disposable-registration`  
    Requests persistent registration capabilities (requires specific environment variable opt-ins).

* **Startup & Execution Flags:**
  * `--windowless`  
    Runs the application in quiet mode.
  * `--GRABFRAME <path>`  
    Opens the target file path directly in GrabFrame (case-insensitive flag).
  * `Settings`  
    Primary argument used to launch into settings.

---

## Environment Variables

You can configure automation options and profile behavior using the following environment variables:

| Environment Variable | Description |
| :--- | :--- |
| `TEXT_GRAB_AUTOMATION_PROFILE` | Sets the file path to the root directory for the automation profile. |
| `TEXT_GRAB_AUTOMATION_SYSTEM_INTEGRATION` | Set to `true` or `1` to enable system integration. |
| `TEXT_GRAB_AUTOMATION_DISPOSABLE_REGISTRATION` | Set to `true` or `1` to request persistent registration. |
| `TEXT_GRAB_DISPOSABLE_VM` | Set to `true` or `1` to allow persistent registration (requires both system integration and disposable registration enabled). |

---

## Automation Profile Directory Structure

When an automation profile is specified via argument or environment variable, paths inside the profile root directory resolve as follows:

```text
<AutomationProfileRoot>/
├── seed.json                           # Optional JSON file to override default settings
├── GrabTemplates.json                  # Templates configuration file
├── settings/
│   └── classic-settings.json           # Classic application settings file
├── settings-data/                      # Managed settings directory
├── template-images/                    # Directory for template images
├── history/                             # History files directory
├── data/                               # Data directory
├── output/                             # Output files directory
├── lookup/
│   └── QuickSimpleLookup.csv           # Lookup file location
├── temp/                               # Temporary processing directory
└── diagnostics/
    ├── events.jsonl                    # Diagnostic logs
    └── failure.json                    # Failure sentinel log
```

### Seeding Profile Configuration (`seed.json`)

To apply customized configuration overrides on startup, place a `seed.json` file inside the profile's root directory. The file can contain key-value pairs mapping directly to application setting properties or nested under a `"settings"` object:

```json
{
  "settings": {
    "ShowToast": false,
    "LastUsedLang": "en-US",
    "UseTesseract": false
  }
}
```