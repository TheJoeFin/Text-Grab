Set-StrictMode -Version Latest

<#
    The v5 E4a parity check: the editor area and calc pane rendered from classic.json through AvalonDock,
    against the hand-written window's fixtures (UiTests/Fixtures/EditTextUia, written by
    UiTests/Scripts/Export-EditTextUiaFixtures.ps1).

    Accepted, because AvalonDock adds them: each pane's container, a TabControl that UI Automation reports
    as a Tab, whose children are the pane's own elements. They are unwrapped before comparing, and an empty
    one (the calc pane's place while it is hidden) is dropped. The resize bar keeps the old splitter's
    AutomationId (TextBoxSplitter) but is a different class, so the splitter's class name is not compared.

    Intentional differences (docs/v5/E4-Layout-Engine.md, "Intentional differences"), applied to the
    fixtures before comparing:
      - A hidden calc pane is out of the tree. The hand-written grid kept its Copy All and Info buttons in
        a column 0 wide, still in the tree and in the Tab order.
      - The resize bar sits between the editor and the calc pane, in the tree and in the Tab order, where it
        is on screen. The hand-written splitter was declared after the pane and drawn over its edge.

    Not compared, on either side: the unnamed text block inside an empty spreadsheet cell. The data grid
    realizes those as it lays out, and the editor area is 5 px narrower with the resize bar beside the calc
    pane rather than over it, so which empty cells near the edge have one varies. They carry no name.

    Anything else that differs fails the check.
#>

$script:CalcButtonIds = @('EditText.CalculatorCopyAllButton', 'EditText.CalculatorInfoButton')

function ConvertFrom-UiaTreeLines {
    param([AllowEmptyCollection()][string[]]$Lines)

    $root = [pscustomobject]@{ Line = '(root)'; Children = [System.Collections.Generic.List[object]]::new() }
    $stack = [System.Collections.Generic.List[object]]::new()
    [void]$stack.Add($root)
    foreach ($line in $Lines) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $depth = ($line.Length - $line.TrimStart(' ').Length) / 2
        $node = [pscustomobject]@{ Line = $line.Trim(); Children = [System.Collections.Generic.List[object]]::new() }
        while ($stack.Count -gt $depth + 1) { $stack.RemoveAt($stack.Count - 1) }
        [void]$stack[$stack.Count - 1].Children.Add($node)
        [void]$stack.Add($node)
    }
    return $root
}

function ConvertTo-UiaTreeLines {
    param([Parameter(Mandatory)]$Node, [int]$Level = 0)

    foreach ($child in $Node.Children) {
        # An empty spreadsheet cell's unnamed text block (see the module's notes).
        if ($Node.Line -like '*(DataGridCell)' -and $child.Line -eq 'Text (TextBlock)') { continue }
        ('  ' * $Level) + $child.Line
        ConvertTo-UiaTreeLines -Node $child -Level ($Level + 1)
    }
}

function Test-UiaLineHasId {
    param([string]$Line, [string[]]$AutomationIds)

    foreach ($id in $AutomationIds) {
        if ($Line -match "#$([regex]::Escape($id))(\s|$)") { return $true }
    }
    return $false
}

function ConvertTo-ComparableUiaLine {
    param([string]$Line)

    # The resize bar replaces the GridSplitter: same AutomationId, different class.
    if ($Line -match '^Thumb #TextBoxSplitter\b') { return 'Thumb #TextBoxSplitter' }
    return $Line
}

function Get-PaneLayoutUiaActual {
    <# The pane layout's tree with AvalonDock's pane containers unwrapped. #>
    param([AllowEmptyCollection()][string[]]$Lines)

    $tree = ConvertFrom-UiaTreeLines -Lines $Lines
    function Unwrap($node) {
        $children = [System.Collections.Generic.List[object]]::new()
        foreach ($child in $node.Children) {
            Unwrap $child
            if ($child.Line -eq 'Tab (TabControl)') {
                foreach ($grandchild in $child.Children) { [void]$children.Add($grandchild) }
            }
            else {
                [void]$children.Add($child)
            }
        }
        $node.Children = $children
    }
    Unwrap $tree
    return @(ConvertTo-UiaTreeLines -Node $tree | ForEach-Object { ConvertTo-ComparableUiaLine $_ })
}

function Get-PaneLayoutUiaExpected {
    <# The hand-written fixture with the intentional differences applied. #>
    param([AllowEmptyCollection()][string[]]$Lines, [bool]$CalcOpen)

    $tree = ConvertFrom-UiaTreeLines -Lines $Lines
    $top = [System.Collections.Generic.List[object]]::new()
    foreach ($node in $tree.Children) { [void]$top.Add($node) }

    if (-not $CalcOpen) {
        $top = [System.Collections.Generic.List[object]]@($top | Where-Object { -not (Test-UiaLineHasId $_.Line $script:CalcButtonIds) })
    }

    $splitter = @($top | Where-Object { $_.Line -match '^Thumb #TextBoxSplitter\b' })
    if ($splitter.Count -eq 1) {
        [void]$top.Remove($splitter[0])
        $results = @($top | Where-Object { Test-UiaLineHasId $_.Line @('EditText.CalculatorResults') })
        $index = if ($results.Count -eq 1) { $top.IndexOf($results[0]) } else { 1 }
        $top.Insert($index, $splitter[0])
    }

    $tree.Children = $top
    return @(ConvertTo-UiaTreeLines -Node $tree | ForEach-Object { ConvertTo-ComparableUiaLine $_ })
}

function Get-PaneLayoutTabExpected {
    <# A fixture's focus stops with the intentional differences applied, per section. #>
    param([string[]]$Lines, [bool]$CalcOpen)

    $sections = [ordered]@{}
    $current = $null
    foreach ($line in $Lines) {
        if ($line.StartsWith('#')) { $current = $line; $sections[$current] = [System.Collections.Generic.List[string]]::new(); continue }
        $stop = ConvertTo-ComparableUiaLine $line
        if (-not $CalcOpen -and (Test-UiaLineHasId $stop $script:CalcButtonIds)) { continue }
        [void]$sections[$current].Add($stop)
    }

    if ($CalcOpen) {
        foreach ($key in @($sections.Keys)) {
            $stops = $sections[$key]
            $splitterIndex = $stops.IndexOf('Thumb #TextBoxSplitter')
            $resultsIndex = -1
            for ($i = 0; $i -lt $stops.Count; $i++) { if (Test-UiaLineHasId $stops[$i] @('EditText.CalculatorResults')) { $resultsIndex = $i; break } }
            if ($splitterIndex -gt $resultsIndex -and $resultsIndex -ge 0) {
                $stops.RemoveAt($splitterIndex)
                $stops.Insert($resultsIndex, 'Thumb #TextBoxSplitter')
            }
        }
    }
    return $sections
}

function Get-TabSections {
    param([string[]]$Lines)

    $sections = [ordered]@{}
    $current = $null
    foreach ($line in $Lines) {
        if ($line.StartsWith('#')) { $current = $line; $sections[$current] = [System.Collections.Generic.List[string]]::new(); continue }
        [void]$sections[$current].Add((ConvertTo-ComparableUiaLine $line))
    }
    return $sections
}

function Compare-EditTextUiaParity {
    <#
    .SYNOPSIS
    Compares a pane-layout capture with the hand-written fixtures. Returns one message per difference;
    none means parity. Focus orders compare as a prefix: each capture presses the key a fixed number of
    times, and where the hand-written window had stops the pane layout drops, the capture runs on further.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$FixtureDirectory,
        [Parameter(Mandatory)][string]$ActualDirectory
    )

    $problems = [System.Collections.Generic.List[string]]::new()
    $fixtures = @(Get-ChildItem -LiteralPath $FixtureDirectory -Filter '*.txt' -File)
    if ($fixtures.Count -eq 0) { [void]$problems.Add("No fixtures in $FixtureDirectory."); return $problems }

    foreach ($fixture in $fixtures) {
        $actualPath = Join-Path $ActualDirectory $fixture.Name
        if (-not (Test-Path -LiteralPath $actualPath)) { [void]$problems.Add("$($fixture.Name): not captured."); continue }
        $calcOpen = $fixture.Name -like '*.calc-open.*'
        $expectedLines = @(Get-Content -LiteralPath $fixture.FullName)
        $actualLines = @(Get-Content -LiteralPath $actualPath)

        if ($fixture.Name -like '*.uia.txt') {
            $expected = @(Get-PaneLayoutUiaExpected -Lines $expectedLines -CalcOpen $calcOpen)
            $actual = @(Get-PaneLayoutUiaActual -Lines $actualLines)
            if (($expected -join "`n") -cne ($actual -join "`n")) {
                [void]$problems.Add("$($fixture.Name): tree differs.`n--- expected`n$($expected -join "`n")`n--- actual`n$($actual -join "`n")")
            }
            continue
        }

        $expectedSections = Get-PaneLayoutTabExpected -Lines $expectedLines -CalcOpen $calcOpen
        $actualSections = Get-TabSections -Lines $actualLines
        foreach ($section in $expectedSections.Keys) {
            $expectedStops = @($expectedSections[$section])
            $actualStops = if ($actualSections.Contains($section)) { @($actualSections[$section]) } else { @() }
            # Only the stops inside the window's editor area and calc pane are compared: up to the first stop
            # outside it (the window itself, where Ctrl+Tab wraps), which both orders must reach.
            $cut = [Math]::Min($expectedStops.Count, $actualStops.Count)
            for ($i = 0; $i -lt $expectedStops.Count; $i++) { if ($expectedStops[$i] -like 'Window *') { $cut = [Math]::Min($cut, $i + 1); break } }
            $expectedPrefix = @($expectedStops | Select-Object -First $cut)
            $actualPrefix = @($actualStops | Select-Object -First $cut)
            if (($expectedPrefix -join "`n") -cne ($actualPrefix -join "`n")) {
                [void]$problems.Add("$($fixture.Name) [$section]: focus order differs.`n--- expected`n$($expectedPrefix -join "`n")`n--- actual`n$($actualPrefix -join "`n")")
            }
        }
    }
    return $problems
}

Export-ModuleMember -Function @('Compare-EditTextUiaParity', 'Get-PaneLayoutUiaActual', 'Get-PaneLayoutUiaExpected')
