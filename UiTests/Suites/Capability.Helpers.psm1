Set-StrictMode -Version Latest
Import-Module (Join-Path (Split-Path -Parent $PSScriptRoot) 'Capability.Preflight.psm1') -Force -DisableNameChecking

function Require-UiTestCapabilities {
    param([Parameter(Mandatory)]$Context, [Parameter(Mandatory)][string[]]$Capability)

    $missing = @(Get-UiTestMissingCapabilities -Capabilities $Context.Capabilities.capabilities -RequiredCapabilities $Capability)
    if ($missing.Count -gt 0) {
        Skip-UiTest ("Required capability unavailable: " + ($missing -join ' | '))
    }
}

function Start-CapabilityFixture {
    param([Parameter(Mandatory)]$Context, [Parameter(Mandatory)][string]$Surface)

    $fixturePath = (Get-Process -Id $Context.Fixture.ProcessId -ErrorAction Stop).Path
    $state = Join-Path $Context.LogsDirectory "fixture-$Surface-state.jsonl"
    Start-UiTestProcess -Context $Context -FilePath $fixturePath -Arguments @('--surface', $Surface, '--state-file', $state) -Kind Fixture -WindowTitle 'Text Grab Automation Fixture Host'
}

function Get-CapabilityFixtureState {
    param([Parameter(Mandatory)][string]$Path)

    @(Get-Content -LiteralPath $Path -ErrorAction Stop | ForEach-Object { $_ | ConvertFrom-Json } | Select-Object -Last 1)[0]
}

function Assert-CapabilityContains {
    param([Parameter(Mandatory)][string]$Actual, [Parameter(Mandatory)][string]$Expected, [string]$Description = 'text')

    if ($Actual -notlike "*$Expected*") {
        throw "Expected $Description to contain '$Expected', but it was '$Actual'."
    }
}

function Move-CapabilityWindow {
    param([Parameter(Mandatory)][long]$WindowHandle, [Parameter(Mandatory)][int]$X, [Parameter(Mandatory)][int]$Y)

    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class UiTestWindowPlacement {
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint flags);
}
'@ -ErrorAction SilentlyContinue
    if (-not [UiTestWindowPlacement]::SetWindowPos([IntPtr]$WindowHandle, [IntPtr]::Zero, $X, $Y, 0, 0, 0x0001 -bor 0x0004)) {
        throw "SetWindowPos failed with Win32 error $([Runtime.InteropServices.Marshal]::GetLastWin32Error())."
    }
}

function Invoke-CapabilityUnitTests {
    param([Parameter(Mandatory)][string]$Filter)

    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) '.'))
    $project = Join-Path $repositoryRoot 'Tests\Tests.csproj'
    $output = & dotnet test $project --no-restore --filter $Filter --nologo 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "Targeted capability unit tests failed for '$Filter': $output"
    }
}

Export-ModuleMember -Function @(
    'Require-UiTestCapabilities',
    'Start-CapabilityFixture',
    'Get-CapabilityFixtureState',
    'Assert-CapabilityContains',
    'Move-CapabilityWindow',
    'Invoke-CapabilityUnitTests'
)
