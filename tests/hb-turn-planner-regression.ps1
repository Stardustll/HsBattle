$ScriptRoot = $PSScriptRoot

function Normalize-SearchText {
    param(
        [string]$Text
    )

    if ($null -eq $Text) {
        throw (New-Object System.ArgumentNullException -ArgumentList 'Text', 'Search text cannot be null.')
    }

    $normalized = [System.Text.RegularExpressions.Regex]::Replace($Text, '\r\n?|\n', ' ')
    $normalized = [System.Text.RegularExpressions.Regex]::Replace($normalized, '\s+', ' ')
    $normalized = [System.Text.RegularExpressions.Regex]::Replace($normalized, '\s*([().,+])\s*', '$1')
    return $normalized.Trim()
}

function Strip-CSharpComments {
    param(
        [string]$Text
    )

    $withoutBlockComments = [System.Text.RegularExpressions.Regex]::Replace(
        $Text,
        '/\*.*?\*/',
        ' ',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    return [System.Text.RegularExpressions.Regex]::Replace(
        $withoutBlockComments,
        '(^|[^:])//.*?$',
        '$1 ',
        [System.Text.RegularExpressions.RegexOptions]::Multiline)
}

function Assert-FileContains {
    param(
        [string]$Path,
        [string[]]$Patterns,
        [System.Collections.Generic.List[string]]$Failures
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        $Failures.Add("Missing file: $Path")
        return
    }

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $utf8 = New-Object System.Text.UTF8Encoding($false, $true)
    $text = $utf8.GetString($bytes)

    if ([System.IO.Path]::GetExtension($Path).Equals('.cs', [System.StringComparison]::OrdinalIgnoreCase)) {
        $text = Strip-CSharpComments -Text $text
    }

    $content = Normalize-SearchText -Text $text

    foreach ($pattern in $Patterns) {
        $needle = Normalize-SearchText -Text $pattern
        if ($content.IndexOf($needle, [System.StringComparison]::Ordinal) -lt 0) {
            $Failures.Add("File '$Path' is missing pattern: $pattern")
        }
    }
}

$failures = New-Object 'System.Collections.Generic.List[string]'

Assert-FileContains -Path (Join-Path $ScriptRoot '..\HsBattle\Strategy\Hb\HbBattleDecisionService.cs') -Patterns @(
    'private readonly HbTurnPlanner _turnPlanner = new HbTurnPlanner(',
    'HbActionSequencePlan sequence = _turnPlanner.Plan(snapshot);',
    'return sequence != null && sequence.Steps.Count > 0 ? sequence.Steps[0] : TryPickSingleStepFallback(snapshot);'
) -Failures $failures

Assert-FileContains -Path (Join-Path $ScriptRoot '..\HsBattle\Strategy\Hb\HbTurnPlanner.cs') -Patterns @(
    'internal sealed class HbTurnPlanner',
    'public HbActionSequencePlan Plan(HbBattleSnapshot snapshot)',
    'HbSimulatedTurnState initialState = HbSimulatedTurnState.Create(snapshot);',
    'HbActionSupportKind.SupportedExact',
    'return bestSequence;'
) -Failures $failures

Assert-FileContains -Path (Join-Path $ScriptRoot '..\HsBattle\Strategy\Hb\HbActionResolver.cs') -Patterns @(
    'internal sealed class HbActionResolver',
    'public bool TryApply(HbSimulatedTurnState state, HbBattleOptionSnapshot option, HbBattleTargetSnapshot target)',
    'ApplyAttack(state, option, target)',
    'ApplyHeroPower(state, option, target)',
    'ApplyPlayCard(state, option, target)'
) -Failures $failures

if ($failures.Count -gt 0) {
    Write-Host 'HB turn planner regression checks failed:'
    foreach ($failure in $failures) {
        Write-Host ('  ' + $failure)
    }
    exit 1
}

Write-Host 'HB turn planner regression checks passed.'
