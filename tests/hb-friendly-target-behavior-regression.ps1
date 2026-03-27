$ScriptRoot = $PSScriptRoot
$ProjectRoot = Split-Path $ScriptRoot -Parent

$sourceFiles = @(
    "$ScriptRoot\hb-friendly-target-harness-types.cs",
    "$ProjectRoot\HsBattle\Strategy\Hb\HbHeuristicEvaluator.cs",
    "$ScriptRoot\hb-friendly-target-helper.cs"
)

try {
    Add-Type -Path $sourceFiles -ErrorAction Stop | Out-Null
}
catch {
    Write-Error "Failed to compile HB strategy sources: $($_.Exception.Message)"
    exit 1
}

$option = [HsBattle.Strategy.Hb.HbBattleOptionSnapshot]::new()
$option.Kind = [HsBattle.Strategy.StrategyActionKind]::Attack
$option.Attack = 5
$option.SourceHealth = 10
$option.IsPlayable = $true

$damagedTarget = [HsBattle.Strategy.Hb.HbBattleTargetSnapshot]::new()
$damagedTarget.EntityId = 101
$damagedTarget.Attack = 2
$damagedTarget.Health = 3
$damagedTarget.IsFriendlyCharacter = $true
$damagedTarget.IsDamaged = $true
$damagedTarget.IsResolved = $true

$undamagedTarget = [HsBattle.Strategy.Hb.HbBattleTargetSnapshot]::new()
$undamagedTarget.EntityId = 202
$undamagedTarget.Attack = 2
$undamagedTarget.Health = 5
$undamagedTarget.IsFriendlyCharacter = $true
$undamagedTarget.IsDamaged = $false
$undamagedTarget.IsResolved = $true

$damagedScore = [HbFriendlyTargetBehavior.EvaluatorProxy]::ScoreTarget($option, $damagedTarget)
$undamagedScore = [HbFriendlyTargetBehavior.EvaluatorProxy]::ScoreTarget($option, $undamagedTarget)

Write-Host "Damaged target score: $damagedScore"
Write-Host "Undamaged target score: $undamagedScore"

if ($damagedScore -le $undamagedScore) {
    Write-Error 'Damaged friendly target is not preferred.'
    exit 1
}

Write-Host 'Damaged friendly target preferred.'
exit 0
