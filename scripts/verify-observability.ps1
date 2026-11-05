param(
    [ValidateSet('Offline', 'Hyperlight', 'Live')][string]$Mode = 'Offline',
    [switch]$Live,
    [switch]$PreflightOnly,
    [ValidateRange(1, 100)][int]$Trials = 1,
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
if ($Live) {
    if ($PSBoundParameters.ContainsKey('Mode') -and $Mode -ne 'Live') { throw '-Live conflicts with -Mode.' }
    $Mode = 'Live'
}
$Live = $Mode -eq 'Live'
$label = if ($Mode -eq 'Offline') { 'OFFLINE-CONTROL' } else { 'LIVE' }
$repository = Split-Path $PSScriptRoot -Parent
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
if (-not $OutputRoot) {
    $OutputRoot = Join-Path (Join-Path $repository 'artifacts') (Join-Path 'observability-validation' ([guid]::NewGuid().ToString('N')))
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
Write-Output "[$label] PASS dotnet available from PATH; mode=$Mode"
if ($Mode -ne 'Offline') {
    if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne [Runtime.InteropServices.Architecture]::X64) { throw '[SKIPPED] Real Hyperlight requires x86_64; no runtime tests executed.' }
    Write-Output '[LIVE] PASS x86_64 architecture'
    if ([Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Linux)) {
        if (-not (Test-Path '/dev/kvm')) { throw '[SKIPPED] /dev/kvm missing; enable nested virtualization.' }
        & test -r /dev/kvm
        if ($LASTEXITCODE -ne 0) { throw '[SKIPPED] /dev/kvm not readable; reconnect after joining kvm group.' }
        & test -w /dev/kvm
        if ($LASTEXITCODE -ne 0) { throw '[SKIPPED] /dev/kvm not writable; reconnect after joining kvm group.' }
        Write-Output '[LIVE] PASS /dev/kvm readable and writable'
    } else { Write-Output '[SKIPPED] KVM is Linux-only; Windows retains WHP, verified by runtime tests.' }
    if (-not $env:HYPERLIGHT_PYTHON_GUEST_PATH -or -not (Test-Path $env:HYPERLIGHT_PYTHON_GUEST_PATH -PathType Leaf)) { throw '[SKIPPED] Set HYPERLIGHT_PYTHON_GUEST_PATH to an existing version-matched guest file.' }
    Write-Output '[LIVE] PASS guest file exists; runtime tests verify compatibility'
}
if ($Live) {
    if (-not $env:AZURE_OPENAI_ENDPOINT -or -not $env:AZURE_OPENAI_DEPLOYMENT_NAME) { throw 'Azure endpoint and deployment must be configured.' }
    $endpoint = [uri]$env:AZURE_OPENAI_ENDPOINT
    if ($endpoint.Scheme -ne 'https' -or $endpoint.Query -or $endpoint.UserInfo) { throw 'Azure endpoint must be HTTPS without credentials or query string.' }
    $headers = @{}
    if ($env:AZURE_OPENAI_API_KEY) { $headers['api-key'] = $env:AZURE_OPENAI_API_KEY } else {
        $az = (Get-Command az -ErrorAction Stop).Source
        $token = & $az account get-access-token --resource https://cognitiveservices.azure.com --query accessToken -o tsv --only-show-errors
        if ($LASTEXITCODE -ne 0 -or -not $token) { throw '[SKIPPED] Azure token unavailable; run az login on the VM.' }
        $headers.Authorization = "Bearer $token"
    }
    $deployment = [uri]::EscapeDataString($env:AZURE_OPENAI_DEPLOYMENT_NAME)
    $uri = $endpoint.AbsoluteUri.TrimEnd('/') + "/openai/deployments/$deployment/chat/completions?api-version=2024-10-21"
    $body = @{ messages = @(@{ role = 'user'; content = 'Reply OK.' }); max_tokens = 1 } | ConvertTo-Json -Depth 5
    try { $null = Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -ContentType 'application/json' -Body $body } catch {
        $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 'transport' }
        throw "[LIVE] Minimal Azure call failed (status $status). Stop: verify auth, deployment/API compatibility, networking and quota. No benchmarks started."
    } finally { $headers.Clear(); $token = $null }
    Write-Output '[LIVE] PASS minimal Azure call; authentication/current request capacity verified, not sustained quota'
} else { Write-Output '[SKIPPED] Azure authentication not required for this mode' }
if ($PreflightOnly) { return }

function Invoke-TestGate([string]$Filter, [int]$Expected, [string]$ResultLabel, [string]$Directory, [string[]]$Projects) {
    $resultsDirectory = Join-Path $Directory ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [guid]::NewGuid().ToString('N'))
    if (Test-Path -LiteralPath $resultsDirectory) { throw "Results directory must be new: $resultsDirectory" }
    $null = New-Item -ItemType Directory -Path $resultsDirectory
    if ($Projects.Count -eq 0) { throw 'No test projects selected.' }
    foreach ($project in $Projects) {
        $name = [IO.Path]::GetFileNameWithoutExtension($project)
        & $dotnet test $project -c Release --no-build --filter $Filter --logger "trx;LogFileName=$name.trx" --results-directory $resultsDirectory --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw "[$ResultLabel] Tests failed in $name. Stop and diagnose before code changes." }
    }
    $files = @(Get-ChildItem -LiteralPath $resultsDirectory -Filter '*.trx' -File -Recurse)
    if ($files.Count -ne $Projects.Count) { throw "[$ResultLabel] Expected $($Projects.Count) TRX files for projects run; found $($files.Count)." }
    $executed = 0; $passed = 0; $total = 0; $failed = 0; $notExecuted = 0
    $breakdown = foreach ($file in $files) {
        $result = $null
        [xml]$result = Get-Content -LiteralPath $file.FullName -Raw -ErrorAction Stop
        $counts = $result.TestRun.ResultSummary.Counters
        foreach ($counter in @('total', 'executed', 'passed', 'failed', 'notExecuted')) {
            if ($null -eq $counts -or $counts.GetAttribute($counter) -notmatch '^\d+$') { throw "Invalid TRX counter $counter in $($file.Name)" }
        }
        $executed += [int]$counts.executed; $passed += [int]$counts.passed; $total += [int]$counts.total
        $failed += [int]$counts.failed; $notExecuted += [int]$counts.notExecuted
        [pscustomobject]@{
            file = $file.Name; modifiedUtc = $file.LastWriteTimeUtc.ToString('o')
            total = [int]$counts.total; executed = [int]$counts.executed; passed = [int]$counts.passed
            failed = [int]$counts.failed; notExecuted = [int]$counts.notExecuted
        }
    }
    $breakdown | Format-Table -AutoSize
    $breakdown | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $resultsDirectory 'counts.json') -Encoding UTF8
    if ($executed -ne $Expected -or $passed -ne $Expected -or $total -ne $Expected -or $failed -ne 0 -or $notExecuted -ne 0) {
        throw "[$ResultLabel] Expected $Expected actual passes; total=$total executed=$executed passed=$passed failed=$failed notExecuted=$notExecuted. Skips are not passes."
    }
    Write-Output "[$ResultLabel] PASS total=$total executed=$executed passed=$passed failed=$failed notExecuted=$notExecuted projects=$($Projects.Count) TRX=$($files.Count); results=$resultsDirectory"
}

$keys = @('AZURE_OPENAI_ENDPOINT', 'HYPERLIGHT_PYTHON_GUEST_PATH', 'DATASET_ROOT', 'ARTIFACTS_ROOT',
    'EXECUTION_LOG_LEVEL', 'EXECUTION_PAYLOAD_CAPTURE', 'BENCHMARK_REQUIRE_LIVE', 'CLASSIC_BATCH_SIZE')
$saved = @{}
foreach ($key in $keys) { $saved[$key] = [Environment]::GetEnvironmentVariable($key) }
Push-Location $repository
try {
    $env:AZURE_OPENAI_ENDPOINT = ''
    $env:HYPERLIGHT_PYTHON_GUEST_PATH = ''
    $env:CLASSIC_BATCH_SIZE = '0'
    $env:BENCHMARK_REQUIRE_LIVE = ([bool]$Live).ToString().ToLowerInvariant()
    $env:DATASET_ROOT = Join-Path $OutputRoot 'data'
    $env:ARTIFACTS_ROOT = $OutputRoot
    $env:EXECUTION_PAYLOAD_CAPTURE = 'full'
    $env:EXECUTION_LOG_LEVEL = 'minimal'
    & $dotnet restore --locked-mode --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    & $dotnet build -c Release --no-restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    $testProjects = @(Get-ChildItem -LiteralPath (Join-Path $repository 'tests') -Filter '*.csproj' -File -Recurse | Sort-Object FullName | ForEach-Object FullName)
    Invoke-TestGate 'Category!=LiveModel&Category!=Hyperlight' 197 'OFFLINE-CONTROL' (Join-Path $OutputRoot 'non-live-tests') $testProjects
    if ($Mode -ne 'Offline') {
        $env:HYPERLIGHT_PYTHON_GUEST_PATH = $saved.HYPERLIGHT_PYTHON_GUEST_PATH
        $hyperlightProject = Join-Path (Join-Path (Join-Path $repository 'tests') 'SterlingVale.CodeAct.IntegrationTests') 'SterlingVale.CodeAct.IntegrationTests.csproj'
        Invoke-TestGate 'Category=Hyperlight' 6 'LIVE' (Join-Path $OutputRoot 'hyperlight-tests') @($hyperlightProject)
        if ($Mode -eq 'Hyperlight') { return }
        $env:AZURE_OPENAI_ENDPOINT = $saved.AZURE_OPENAI_ENDPOINT
    }
    & $dotnet run -c Release --no-build --project (Join-Path 'src' 'SterlingVale.DataGenerator') -- $env:DATASET_ROOT
    if ($LASTEXITCODE -ne 0) { throw 'Dataset generation failed.' }
    $rows = @()
    foreach ($profile in @('small', 'medium', 'large')) {
        & $dotnet run -c Release --no-build --project (Join-Path 'src' 'SterlingVale.Benchmark') -- run --profile $profile --trials $Trials --log-level minimal --payload-capture full
        if ($LASTEXITCODE -ne 0) { throw "[$label] $profile fairness or telemetry failed. Stop before next profile; inspect artifacts before changing code." }
        $file = Get-ChildItem (Join-Path $OutputRoot 'comparisons') -Filter "$profile-*.json" | Sort-Object Name -Descending | Select-Object -First 1
        if (-not $file) { throw "Missing comparison for $profile" }
        $report = Get-Content $file.FullName -Raw | ConvertFrom-Json
        $manifest = Get-Content (Join-Path (Join-Path $env:DATASET_ROOT $profile) 'manifest.json') -Raw | ConvertFrom-Json
        $classic = $report.rawTrials | Where-Object mode -eq 'classic'
        $codeact = $report.rawTrials | Where-Object mode -eq 'codeact'
        $row = [pscustomobject]@{
            dataset = $profile
            resultLabel = $label
            executionKind = $(if ($Live) { 'live-model-real-hyperlight' } else { 'offline-fake-control-no-python-executed' })
            households = ($manifest.files | Where-Object file -eq 'households.json').recordCount
            accounts = ($manifest.files | Where-Object file -eq 'accounts.json').recordCount
            positions = ($manifest.files | Where-Object file -eq 'positions.json').recordCount
            classicStatus = (@($classic | ForEach-Object status) -join ',')
            codeactStatus = (@($codeact | ForEach-Object status) -join ',')
            classicLatencyMs = $report.classic.durationMs.median
            codeactLatencyMs = $report.codeAct.durationMs.median
            latencyReductionPercent = $(if ($null -ne $report.delta.durationDeltaFraction) { -100 * $report.delta.durationDeltaFraction } else { $null })
            classicTokens = $report.classic.totalTokens.median
            codeactTokens = $report.codeAct.totalTokens.median
            tokenReductionPercent = $(if ($null -ne $report.delta.tokenDeltaFraction) { -100 * $report.delta.tokenDeltaFraction } else { $null })
            classicModelTurns = $report.classic.roundTrips.medianModelTurns
            codeactModelTurns = $report.codeAct.roundTrips.medianModelTurns
            classicGranularToolCalls = $report.classic.roundTrips.medianToolCalls
            codeactGranularToolCalls = $report.codeAct.roundTrips.medianToolCalls
            classicCostUsd = $report.classic.cost.median
            codeactCostUsd = $report.codeAct.cost.median
            classicCorrectness = $report.classic.correctness
            codeactCorrectness = $report.codeAct.correctness
            telemetryValid = @($report.rawTrials | Where-Object { -not $_.telemetryValid }).Count -eq 0
            comparisonId = $report.comparisonId
            source = $file.FullName
        }
        $rows += $row
        $rows | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $OutputRoot 'validation-summary.json') -Encoding UTF8
        if (-not $report.fairness.matched -or -not $row.telemetryValid -or @($report.rawTrials).Count -ne (2 * $Trials) -or
            @($report.rawTrials | Where-Object status -ne 'Completed').Count -gt 0 -or
            $row.classicCorrectness.exactCount -ne $Trials -or $row.codeactCorrectness.exactCount -ne $Trials) {
            throw "[$label] $profile completion/oracle/reconciliation failed. Stop before next profile; readiness NOT COMPLETE."
        }
        if ($Live) {
            foreach ($trial in $codeact) {
                $journal = Join-Path (Join-Path (Join-Path $OutputRoot 'runs') $trial.runId) 'events.jsonl'
                $events = @(Get-Content $journal | ForEach-Object { $_ | ConvertFrom-Json })
                if (-not ($events | Where-Object eventType -eq 'CODE_EXECUTION_COMPLETED') -or ($events | Where-Object { $_.eventType -like 'OFFLINE_CODE_TOOL_*' })) {
                    throw "[LIVE] $profile lacks real sandbox completion or contains stand-in execution. Stop."
                }
            }
        }
        Write-Output "[$label] PASS $profile exact oracle and valid telemetry; comparison=$($report.comparisonId)"
    }
    $rows | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $OutputRoot 'validation-summary.json') -Encoding UTF8
    $rows | Select-Object dataset, executionKind, households, accounts, positions, classicStatus, codeactStatus, telemetryValid | Format-Table -AutoSize
    Write-Output "Validation artifacts: $OutputRoot"
    Write-Output "[$label] Review all 15 acceptance criteria before changing readiness; no automatic COMPLETE verdict."
}
finally {
    foreach ($key in $keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key]) }
    Pop-Location
}