param(
    [string]$EvaluatorPath
)

$ErrorActionPreference = 'Stop'
$ScriptRoot = $PSScriptRoot
$ProjectRoot = Split-Path $ScriptRoot -Parent

if ([string]::IsNullOrWhiteSpace($EvaluatorPath)) {
    $EvaluatorPath = Join-Path $ProjectRoot 'HsBattle\Strategy\Hb\HbHeuristicEvaluator.cs'
}

if (-not (Test-Path -LiteralPath $EvaluatorPath)) {
    Write-Error "Evaluator source not found: $EvaluatorPath"
    exit 1
}

$sourceFiles = @(
    (Join-Path $ProjectRoot 'HsBattle\Strategy\StrategyMode.cs')
    (Join-Path $ProjectRoot 'HsBattle\Strategy\StrategyActionKind.cs')
    (Join-Path $ProjectRoot 'HsBattle\Strategy\Hb\HbBattleSnapshot.cs')
    (Join-Path $ProjectRoot 'HsBattle\Strategy\Hb\HbBattleOptionSnapshot.cs')
    (Join-Path $ProjectRoot 'HsBattle\Strategy\Hb\HbBattleTargetSnapshot.cs')
    (Join-Path $ProjectRoot 'HsBattle\Strategy\Hb\HbMulliganSnapshot.cs')
    (Join-Path $ProjectRoot 'HsBattle\Strategy\Hb\HbMulliganCardSnapshot.cs')
    $EvaluatorPath
)

foreach ($sourceFile in $sourceFiles) {
    if (-not (Test-Path -LiteralPath $sourceFile)) {
        Write-Error "Required source file not found: $sourceFile"
        exit 1
    }
}

$binPath = Join-Path $ScriptRoot 'hb-friendly-target-behavior-bin'
New-Item -Path $binPath -ItemType Directory -Force | Out-Null
$assemblyPath = Join-Path $binPath ('hb-friendly-target-' + [Guid]::NewGuid().ToString('N') + '.dll')

try {
    Add-Type -Path $sourceFiles -OutputAssembly $assemblyPath -CompilerOptions '/langversion:latest' -ErrorAction Stop | Out-Null
}
catch {
    Write-Error "Failed to compile HB strategy sources: $($_.Exception.Message)"
    exit 1
}

try {
    $assembly = [System.Reflection.Assembly]::LoadFile($assemblyPath)
}
catch {
    Write-Error "Failed to load compiled assembly: $($_.Exception.Message)"
    exit 1
}

function New-InternalInstance {
    param(
        [System.Reflection.Assembly]$Asm,
        [string]$TypeName
    )

    $type = $Asm.GetType($TypeName, $true)
    return [System.Activator]::CreateInstance($type, $true)
}

function Set-InternalProperty {
    param(
        [object]$Instance,
        [string]$Name,
        [object]$Value
    )

    $bindingFlags = [System.Reflection.BindingFlags]'Instance,Public,NonPublic'
    $property = $Instance.GetType().GetProperty($Name, $bindingFlags)
    if ($null -eq $property) {
        throw "Property '$Name' not found on type '$($Instance.GetType().FullName)'."
    }

    $property.SetValue($Instance, $Value, $null)
}

$option = New-InternalInstance -Asm $assembly -TypeName 'HsBattle.Strategy.Hb.HbBattleOptionSnapshot'
$actionKindType = $assembly.GetType('HsBattle.Strategy.StrategyActionKind', $true)
$attackKind = [System.Enum]::Parse($actionKindType, 'Attack')
Set-InternalProperty -Instance $option -Name 'Kind' -Value $attackKind
Set-InternalProperty -Instance $option -Name 'Attack' -Value 5
Set-InternalProperty -Instance $option -Name 'SourceHealth' -Value 10
Set-InternalProperty -Instance $option -Name 'IsPlayable' -Value $true

$damagedTarget = New-InternalInstance -Asm $assembly -TypeName 'HsBattle.Strategy.Hb.HbBattleTargetSnapshot'
Set-InternalProperty -Instance $damagedTarget -Name 'EntityId' -Value 101
Set-InternalProperty -Instance $damagedTarget -Name 'Attack' -Value 2
Set-InternalProperty -Instance $damagedTarget -Name 'Health' -Value 3
Set-InternalProperty -Instance $damagedTarget -Name 'IsFriendlyCharacter' -Value $true
Set-InternalProperty -Instance $damagedTarget -Name 'IsDamaged' -Value $true
Set-InternalProperty -Instance $damagedTarget -Name 'IsResolved' -Value $true

$undamagedTarget = New-InternalInstance -Asm $assembly -TypeName 'HsBattle.Strategy.Hb.HbBattleTargetSnapshot'
Set-InternalProperty -Instance $undamagedTarget -Name 'EntityId' -Value 202
Set-InternalProperty -Instance $undamagedTarget -Name 'Attack' -Value 2
Set-InternalProperty -Instance $undamagedTarget -Name 'Health' -Value 3
Set-InternalProperty -Instance $undamagedTarget -Name 'IsFriendlyCharacter' -Value $true
Set-InternalProperty -Instance $undamagedTarget -Name 'IsDamaged' -Value $false
Set-InternalProperty -Instance $undamagedTarget -Name 'IsResolved' -Value $true

$evaluator = New-InternalInstance -Asm $assembly -TypeName 'HsBattle.Strategy.Hb.HbHeuristicEvaluator'
$scoreBattleTargetMethod = $evaluator.GetType().GetMethod(
    'ScoreBattleTarget',
    [System.Reflection.BindingFlags]'Instance,Public,NonPublic'
)

if ($null -eq $scoreBattleTargetMethod) {
    Write-Error 'ScoreBattleTarget method not found on HbHeuristicEvaluator.'
    exit 1
}

$damagedScore = [int]$scoreBattleTargetMethod.Invoke($evaluator, @($null, $option, $damagedTarget))
$undamagedScore = [int]$scoreBattleTargetMethod.Invoke($evaluator, @($null, $option, $undamagedTarget))

Write-Host "Damaged target score: $damagedScore"
Write-Host "Undamaged target score: $undamagedScore"

if ($damagedScore -le $undamagedScore) {
    Write-Error 'Damaged friendly target is not preferred over comparable undamaged target.'
    exit 1
}

Write-Host 'Damaged friendly target preferred over comparable undamaged target.'
exit 0
