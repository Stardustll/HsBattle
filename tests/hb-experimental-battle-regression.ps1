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

function Add-Failure {
    param(
        [string]$Name,
        [string]$Path,
        [string]$Detail,
        [System.Collections.Generic.List[string]]$Failures
    )

    $Failures.Add("File '$Name' at path '$Path': $Detail")
}

function Get-NormalizedFileContent {
    param(
        [string]$Name,
        [string]$Path,
        [System.Collections.Generic.List[string]]$Failures
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        Add-Failure -Name $Name -Path $Path -Detail 'not found.' -Failures $Failures
        return $null
    }

    try {
        $bytes = [System.IO.File]::ReadAllBytes($Path)
        $utf8 = New-Object System.Text.UTF8Encoding($false, $true)
        $text = $utf8.GetString($bytes)
    }
    catch {
        Add-Failure -Name $Name -Path $Path -Detail ('could not be read: ' + $_.Exception.Message) -Failures $Failures
        return $null
    }

    return Normalize-SearchText -Text $text
}

function Test-TargetPatterns {
    param(
        [pscustomobject]$Target,
        [System.Collections.Generic.List[string]]$Failures
    )

    $content = Get-NormalizedFileContent -Name $Target.Name -Path $Target.Path -Failures $Failures
    if ($null -eq $content) {
        return
    }

    foreach ($pattern in $Target.Patterns) {
        try {
            $normalizedPattern = Normalize-SearchText -Text $pattern
        }
        catch {
            Add-Failure -Name $Target.Name -Path $Target.Path -Detail ('has invalid pattern: ' + $_.Exception.Message) -Failures $Failures
            continue
        }

        if ([string]::IsNullOrEmpty($normalizedPattern)) {
            Add-Failure -Name $Target.Name -Path $Target.Path -Detail 'has invalid pattern: normalized pattern is empty.' -Failures $Failures
            continue
        }

        if ($content.IndexOf($normalizedPattern, [System.StringComparison]::Ordinal) -lt 0) {
            Add-Failure -Name $Target.Name -Path $Target.Path -Detail ('is missing pattern: ' + $pattern) -Failures $Failures
        }
    }
}

function Test-ExecutableBattleTargetHeuristics {
    param(
        [System.Collections.Generic.List[string]]$Failures
    )

    $pwsh = (Get-Command pwsh -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source)
    if ([string]::IsNullOrEmpty($pwsh)) {
        Add-Failure -Name 'HbHeuristicEvaluator behavioral regression' -Path (Join-Path $ScriptRoot '..\HsBattle\Strategy\Hb\HbHeuristicEvaluator.cs') -Detail 'pwsh was not found, so the executable heuristic regression could not run.' -Failures $Failures
        return
    }

    $repoRoot = [System.IO.Path]::GetFullPath((Join-Path $ScriptRoot '..'))
    $tempPath = [System.IO.Path]::ChangeExtension([System.IO.Path]::GetTempFileName(), '.ps1')
    $tempSource = @'
param(
    [string]$RepoRoot
)

$sourceFiles = @(
    [System.IO.Path]::Combine($RepoRoot, 'HsBattle', 'Strategy', 'StrategyMode.cs'),
    [System.IO.Path]::Combine($RepoRoot, 'HsBattle', 'Strategy', 'StrategyActionKind.cs'),
    [System.IO.Path]::Combine($RepoRoot, 'HsBattle', 'Strategy', 'Hb', 'HbBattleSnapshot.cs'),
    [System.IO.Path]::Combine($RepoRoot, 'HsBattle', 'Strategy', 'Hb', 'HbBattleOptionSnapshot.cs'),
    [System.IO.Path]::Combine($RepoRoot, 'HsBattle', 'Strategy', 'Hb', 'HbBattleTargetSnapshot.cs'),
    [System.IO.Path]::Combine($RepoRoot, 'HsBattle', 'Strategy', 'Hb', 'HbMulliganSnapshot.cs'),
    [System.IO.Path]::Combine($RepoRoot, 'HsBattle', 'Strategy', 'Hb', 'HbMulliganCardSnapshot.cs'),
    [System.IO.Path]::Combine($RepoRoot, 'HsBattle', 'Strategy', 'Hb', 'HbHeuristicEvaluator.cs')
)

Add-Type -Path $sourceFiles -IgnoreWarnings
$assembly = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object {
    $_.GetType('HsBattle.Strategy.Hb.HbHeuristicEvaluator', $false, $false) -ne $null
} | Select-Object -First 1

if ($null -eq $assembly) {
    throw 'Could not resolve compiled evaluator type.'
}

$evaluatorType = $assembly.GetType('HsBattle.Strategy.Hb.HbHeuristicEvaluator', $true)
$optionType = $assembly.GetType('HsBattle.Strategy.Hb.HbBattleOptionSnapshot', $true)
$targetType = $assembly.GetType('HsBattle.Strategy.Hb.HbBattleTargetSnapshot', $true)
$kindType = $assembly.GetType('HsBattle.Strategy.StrategyActionKind', $true)
$scoreMethod = $evaluatorType.GetMethod('ScoreBattleTarget')

$option = [Activator]::CreateInstance($optionType, $true)
$option.Kind = [Enum]::Parse($kindType, 'HeroPower')

$damagedFriendly = [Activator]::CreateInstance($targetType, $true)
$damagedFriendly.IsResolved = $true
$damagedFriendly.IsFriendlyCharacter = $true
$damagedFriendly.IsDamaged = $true
$damagedFriendly.Attack = 0

$healthyFriendly = [Activator]::CreateInstance($targetType, $true)
$healthyFriendly.IsResolved = $true
$healthyFriendly.IsFriendlyCharacter = $true
$healthyFriendly.IsDamaged = $false
$healthyFriendly.Attack = 0

$evaluator = [Activator]::CreateInstance($evaluatorType, $true)
$damagedScore = [int]$scoreMethod.Invoke($evaluator, @($null, $option, $damagedFriendly))
$healthyScore = [int]$scoreMethod.Invoke($evaluator, @($null, $option, $healthyFriendly))
$scoreDelta = $damagedScore - $healthyScore

if ($scoreDelta -ne 40) {
    throw \"expected damaged friendly target score delta to be 40, but damaged=$damagedScore healthy=$healthyScore delta=$scoreDelta.\"
}
'@

    try {
        Set-Content -LiteralPath $tempPath -Value $tempSource -Encoding UTF8
        $output = & $pwsh -NoProfile -File $tempPath -RepoRoot $repoRoot 2>&1
        if ($LASTEXITCODE -ne 0) {
            Add-Failure -Name 'HbHeuristicEvaluator behavioral regression' -Path (Join-Path $ScriptRoot '..\HsBattle\Strategy\Hb\HbHeuristicEvaluator.cs') -Detail ('executable heuristic regression failed: ' + (($output | ForEach-Object { $_.ToString().Trim() }) -join ' ')) -Failures $Failures
        }
    }
    catch {
        Add-Failure -Name 'HbHeuristicEvaluator behavioral regression' -Path (Join-Path $ScriptRoot '..\HsBattle\Strategy\Hb\HbHeuristicEvaluator.cs') -Detail ('could not execute heuristic scoring harness: ' + $_.Exception.Message) -Failures $Failures
    }
    finally {
        if (Test-Path -LiteralPath $tempPath) {
            Remove-Item -LiteralPath $tempPath -Force
        }
    }
}

function New-Target {
    param(
        [string]$Name,
        [string]$RelativePath,
        [string[]]$Patterns
    )

    return [pscustomobject]@{
        Name = $Name
        Path = Join-Path $ScriptRoot $RelativePath
        Patterns = $Patterns
    }
}

$targets = @(
    (New-Target -Name 'HbStrategyEngine.cs' -RelativePath '..\HsBattle\Strategy\HbStrategyEngine.cs' -Patterns @(
        'private readonly HbSnapshotAdapter _snapshotAdapter = new HbSnapshotAdapter();',
        'private readonly HbBattleDecisionService _battleDecisionService = new HbBattleDecisionService();',
        'HbBattleSnapshot snapshot = _snapshotAdapter.CreateBattleSnapshot(context);',
        'StrategyActionPlan plan = _battleDecisionService.Decide(snapshot);',
        'return plan != null ? StrategyEngineResult.Success(plan) : StrategyEngineResult.Fallback'
    )),
    (New-Target -Name 'HbBattleDecisionService.cs' -RelativePath '..\HsBattle\Strategy\Hb\HbBattleDecisionService.cs' -Patterns @(
        'internal sealed class HbBattleDecisionService',
        'public StrategyActionPlan Decide(HbBattleSnapshot snapshot)',
        'int score = _evaluator.ScoreBattleOption(snapshot, option);',
        'foreach (HbBattleTargetSnapshot target in option.Targets)',
        'int targetScore = _evaluator.ScoreBattleTarget(snapshot, option, target);',
        'TargetId = bestTarget.EntityId,',
        'bestScore',
        'return bestPlan;'
    )),
    (New-Target -Name 'HbHeuristicEvaluator.cs' -RelativePath '..\HsBattle\Strategy\Hb\HbHeuristicEvaluator.cs' -Patterns @(
        'internal sealed class HbHeuristicEvaluator',
        'public int ScoreBattleOption(HbBattleSnapshot snapshot, HbBattleOptionSnapshot option)',
        'public int ScoreBattleTarget(HbBattleSnapshot snapshot, HbBattleOptionSnapshot option, HbBattleTargetSnapshot target)',
        'public int ScoreMulliganCard(HbMulliganSnapshot snapshot, HbMulliganCardSnapshot card)',
        'if (option.CanLethal)',
        'if (target.IsEnemyHero)',
        'if (target.IsEnemyCharacter)',
        'if (target.IsFriendlyHero && target.IsDamaged)',
        'if (target.IsFriendlyCharacter && target.IsDamaged)',
        'if (target.IsFriendlyCharacter && !target.IsDamaged && target.Attack > 0)',
        'if (option.Kind == StrategyActionKind.Attack && option.SourceHealth > 0 && target.Attack > 0)',
        'if (option.Kind == StrategyActionKind.Attack && option.Attack > 0 && target.Health > option.Attack && option.SourceHealth <= target.Attack)',
        'if (snapshot != null && snapshot.IsEnemyHeroHealthKnown && snapshot.EnemyHeroHealth <= 12)',
        'if (option.Targets.Exists(delegate (HbBattleTargetSnapshot item) { return item.IsEnemyCharacter; }))',
        'if (option.SourceHealth > target.Attack)',
        'if (option.SourceHealth <= target.Attack)',
        'if (card.Cost >= 5)'
    )),
    (New-Target -Name 'HbBattleOptionSnapshot.cs' -RelativePath '..\HsBattle\Strategy\Hb\HbBattleOptionSnapshot.cs' -Patterns @(
        'using System.Collections.Generic;',
        'public int SourceHealth { get; set; } = -1;',
        'public List<HbBattleTargetSnapshot> Targets { get; }'
    )),
    (New-Target -Name 'HbBattleTargetSnapshot.cs' -RelativePath '..\HsBattle\Strategy\Hb\HbBattleTargetSnapshot.cs' -Patterns @(
        'internal sealed class HbBattleTargetSnapshot',
        'public int EntityId { get; set; } = -1;',
        'public int MaxHealth { get; set; } = -1;',
        'public int MissingHealth { get; set; }',
        'public bool IsDamaged { get; set; }',
        'public bool IsEnemyHero { get; set; }',
        'public bool IsEnemyCharacter { get; set; }'
    )),
    (New-Target -Name 'HbSnapshotAdapter.cs' -RelativePath '..\HsBattle\Strategy\Hb\HbSnapshotAdapter.cs' -Patterns @(
        'SourceHealth = entity != null ? entity.GetCurrentHealth() : -1,',
        'foreach (HbBattleTargetSnapshot target in CreateBattleTargetSnapshots(gameState, option.Main.Targets))',
        'battleOption.Targets.Add(target);',
        'private static IEnumerable<HbBattleTargetSnapshot> CreateBattleTargetSnapshots(GameState gameState, List<Network.Options.Option.TargetOption> targetOptions)',
        'MaxHealth = targetEntity != null ? targetEntity.GetDefHealth() : -1,',
        'MissingHealth = targetEntity != null ? Math.Max(0, targetEntity.GetDefHealth() - targetEntity.GetCurrentHealth()) : 0,',
        'IsDamaged = targetEntity != null && targetEntity.GetCurrentHealth() < targetEntity.GetDefHealth()'
    ))
)

$failures = New-Object 'System.Collections.Generic.List[string]'
foreach ($target in $targets) {
    Test-TargetPatterns -Target $target -Failures $failures
}
Test-ExecutableBattleTargetHeuristics -Failures $failures

if ($failures.Count -gt 0) {
    Write-Host 'HB experimental battle regression checks failed:'
    foreach ($failure in $failures) {
        Write-Host ('  ' + $failure)
    }
    exit 1
}

Write-Host 'HB experimental battle regression checks passed.'
