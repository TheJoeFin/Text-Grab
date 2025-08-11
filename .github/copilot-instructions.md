# Text Grab - GitHub Copilot Instructions

Text Grab is a Windows-specific .NET 9.0 WPF OCR (Optical Character Recognition) application that extracts text from images using Windows APIs. It provides multiple modes for text capture including full-screen grab, grab frame, edit text window, and quick lookup.

**ALWAYS reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.**

## CRITICAL: Windows-Only Application

**DO NOT attempt to fully build or run this application on non-Windows platforms.** The application:
- Requires Windows-specific APIs (Windows.Media.Ocr, WPF, WinForms)
- Uses Windows 10 SDK 22621.0 and WindowsAppSDK
- Build packaging fails on Linux/macOS due to Windows-specific MSBuild tasks
- Only dependency restoration and partial compilation validation possible on non-Windows

## Working Effectively

### Prerequisites (Windows Only)
For full development on Windows:
- Windows 10/11 with Windows 10 SDK 22621.0
- Visual Studio 2019/2022 with workloads:
  - "Universal Windows Platform Development" 
  - ".NET desktop development"
  - ".NET cross-platform development"
- **OR** .NET 9.0 SDK: https://dotnet.microsoft.com/download/dotnet/9.0

### Cross-Platform Dependency Validation
For non-Windows environments (validation only):
- Install .NET 9.0: `curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version 9.0.101`
- Add to PATH: `export PATH="$HOME/.dotnet:$PATH"`

### Build Commands

#### Windows Full Build
- Restore dependencies: `dotnet restore Text-Grab.sln` -- takes 30 seconds initial, 2 seconds cached. NEVER CANCEL. Set timeout to 60+ seconds.
- Build main project: `dotnet build Text-Grab/Text-Grab.csproj -c Release` -- takes 45 seconds. NEVER CANCEL. Set timeout to 90+ seconds.
- Build packaged version: `dotnet build Text-Grab-Package/Text-Grab-Package.wapproj -c Release` -- takes 60 seconds. NEVER CANCEL. Set timeout to 120+ seconds.
- Run tests: `dotnet test Tests/Tests.csproj` -- takes 30 seconds. NEVER CANCEL. Set timeout to 60+ seconds.

#### Production Build (Windows Only)
- Use PowerShell script: `.\build-unpackaged.ps1` -- takes 180 seconds (3 minutes). NEVER CANCEL. Set timeout to 300+ seconds.
- Creates multiple architecture builds (x64, ARM64) with framework-dependent and self-contained variants

#### Cross-Platform Validation (Limited)
- **CRITICAL: Always use `-p:EnableWindowsTargeting=true` flag on non-Windows platforms**
- Restore solution: `dotnet restore Text-Grab.sln -p:EnableWindowsTargeting=true` -- takes 30 seconds initial, 2 seconds cached. NEVER CANCEL. Set timeout to 60+ seconds.
- Restore individual project: `dotnet restore Text-Grab/Text-Grab.csproj -p:EnableWindowsTargeting=true` -- takes 2-30 seconds. NEVER CANCEL. Set timeout to 60+ seconds.
- **DO NOT attempt full build** - will fail with Windows packaging error: `Microsoft.Build.Packaging.Pri.Tasks.ExpandPriContent task could not be loaded`

### Running the Application (Windows Only)
- Debug in Visual Studio: Set Text-Grab-Package as startup project, press F5
- Command line debug: `dotnet run --project Text-Grab/Text-Grab.csproj`
- Production executable: `Text-Grab/bin/Release/net9.0-windows10.0.22621.0/Text-Grab.exe`

### CLI Usage (Windows Only)
The application supports command-line arguments:
- `Text-Grab.exe Fullscreen` - Launch fullscreen grab mode
- `Text-Grab.exe GrabFrame` - Launch grab frame
- `Text-Grab.exe EditText` - Launch edit text window
- `Text-Grab.exe Settings` - Open settings
- `Text-Grab.exe "image.png"` - OCR an image file
- `Text-Grab.exe "C:\folder"` - OCR all images in folder

## Validation Scenarios

**ALWAYS run these validation steps after making changes (Windows only):**

### Core OCR Functionality
1. **Image OCR Test**: Run `dotnet test Tests/Tests.csproj --filter "OcrFontSampleImage"` - validates basic OCR engine
2. **QR Code Reading**: Run `dotnet test Tests/Tests.csproj --filter "ReadQrCode"` - validates barcode/QR functionality  
3. **Table Analysis**: Run `dotnet test Tests/Tests.csproj --filter "AnalyzeTable"` - validates structured data extraction

### Manual Application Testing (Windows Only)
1. **Full-Screen Mode**: Launch app → Press Windows+Shift+T → Select screen region → Verify text copied to clipboard
2. **Grab Frame Mode**: Launch app → Create grab frame → Position over text → Click Grab → Verify OCR results
3. **Edit Text Window**: Launch app → Open Edit Text → Test text manipulation tools (find/replace, regex, etc.)
4. **Quick Lookup**: Press Windows+Shift+Q → Test custom text storage and retrieval

### Build Validation
- Always run `dotnet test Tests/Tests.csproj` before committing changes
- Use `.\build-unpackaged.ps1` to verify production builds work correctly
- Check CI/CD status: `.github/workflows/buildDev.yml` runs on Windows-latest only

## Key Project Structure

### Primary Components
- **Text-Grab/**: Main WPF application (.NET 9.0)
- **Text-Grab-Package/**: Windows application packaging project (.wapproj)
- **Tests/**: XUnit test suite with WPF support
- **.github/workflows/buildDev.yml**: CI/CD pipeline (Windows-only)

### Important Subdirectories
- **Text-Grab/Views/**: Main UI windows (FullscreenGrab, GrabFrame, EditTextWindow)
- **Text-Grab/Utilities/**: Core functionality (OcrUtilities, ClipboardUtilities, etc.)
- **Text-Grab/Services/**: Background services and system integration
- **Text-Grab/Models/**: Data models and language support

### Configuration Files
- **.editorconfig**: Code formatting rules (4-space indents, CRLF line endings)
- **build-unpackaged.ps1**: Production build script for multiple architectures
- **app.manifest**: Windows application manifest with DPI awareness

## Common Validation Errors

### Known Issues to Document (Not Fix)
- `warning CS0162: Unreachable code detected` in WindowsAiUtilities.cs - platform-specific code paths
- `warning CS8604: Possible null reference` in OCR utilities - acceptable for performance
- `warning WFO0003: Remove high DPI settings from app.manifest` - legacy compatibility requirement

### Build Failures on Non-Windows
- `error NETSDK1100: To build a project targeting Windows on this operating system, set the EnableWindowsTargeting property to true` - **EXPECTED** without `-p:EnableWindowsTargeting=true` flag
- `error MSB4062: Microsoft.Build.Packaging.Pri.Tasks.ExpandPriContent task could not be loaded` - **EXPECTED** on Linux/macOS during build attempts
- Document as: "Full build requires Windows - packaging tasks not available on other platforms. Use only for dependency restoration and validation."

## Timing Expectations

**NEVER CANCEL builds or long-running operations. Always set appropriate timeouts:**

- Dependency restore (clean): 30 seconds (timeout: 60+ seconds)
- Dependency restore (cached): 2 seconds (timeout: 10+ seconds)
- Incremental build: 45 seconds (timeout: 90+ seconds) 
- Full production build: 180 seconds (timeout: 300+ seconds)
- Test suite: 30 seconds (timeout: 60+ seconds)
- CI/CD pipeline: 300 seconds (timeout: 600+ seconds)

**Cross-platform validation (non-Windows):**
- Restore with EnableWindowsTargeting: 2-30 seconds (timeout: 60+ seconds)
- Build attempts will fail after ~10 seconds with packaging errors - this is expected

## Dependency Information

### NuGet Packages
Key dependencies (see Text-Grab.csproj for complete list):
- **Microsoft.WindowsAppSDK**: Windows runtime APIs
- **WPF-UI**: Modern Fluent Design System components
- **ZXing.Net**: Barcode and QR code scanning
- **Magick.NET**: Advanced image processing
- **Microsoft.Toolkit.Uwp.Notifications**: Toast notifications

### External Tools
- **Tesseract OCR** (optional): Enhanced OCR engine download
- **Windows OCR API**: Primary OCR engine (Windows 10+ built-in)

## Quick Reference Commands

```bash
# Windows Development Setup
git clone https://github.com/TheJoeFin/Text-Grab.git
cd Text-Grab

# Restore dependencies (30s initial, 2s cached)
dotnet restore Text-Grab.sln

# Build and test (75s total)
dotnet build Text-Grab/Text-Grab.csproj -c Release
dotnet test Tests/Tests.csproj

# Production build (180s)
.\build-unpackaged.ps1

# Non-Windows Validation Only (ALWAYS include -p:EnableWindowsTargeting=true)
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version 9.0.101
export PATH="$HOME/.dotnet:$PATH"
dotnet restore Text-Grab.sln -p:EnableWindowsTargeting=true
# Note: Full build will fail - only restore and dependency validation possible
```

## Development Notes

- **Always test OCR functionality** when modifying OCR-related code
- **Language support** is critical - test with multiple languages when changing language utilities
- **DPI awareness** is essential for accurate screen capture - avoid modifying DPI-related code
- **Clipboard integration** requires testing on actual Windows systems
- **Performance** matters for OCR operations - profile changes that affect image processing

Remember: This is a Windows-native application leveraging platform-specific APIs. Development and testing should primarily occur on Windows systems.