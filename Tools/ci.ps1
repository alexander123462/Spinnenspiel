# Spinnenspiel CI: optional Editor-Methode ausfuehren, kompilieren, PlayMode-Tests laufen lassen.
# Aufruf: powershell -ExecutionPolicy Bypass -File C:\Users\vorau\dev\Spinnenspiel\Tools\ci.ps1 -Name m2 -Method Spinnenspiel.EditorTools.Milestone1SceneBuilder.Build -Tests
param(
    [string]$Name = "ci",
    [string[]]$Method = @(),
    [switch]$Tests
)

$ErrorActionPreference = 'Continue'
# Bei Aufruf ueber -File kommen Listen als ein String "a,b" an -> aufteilen.
$Method = @($Method | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$project = 'C:\Users\vorau\dev\Spinnenspiel'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe'
$logDir = "$project\Logs\ci"
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

function Invoke-Unity([string[]]$UnityArgs, [string]$Log) {
    $all = @('-batchmode', '-projectPath', "`"$project`"", '-logFile', "`"$Log`"") + $UnityArgs
    # Nur auf Unity selbst warten (Start-Process -Wait wartet auch auf Hintergrund-Kindprozesse wie den Lizenz-Client).
    $p = Start-Process -FilePath $unity -ArgumentList $all -PassThru -WindowStyle Hidden
    $p.WaitForExit()
    return $p.ExitCode
}

function Show-LogIssues([string]$Log) {
    if (-not (Test-Path $Log)) { "  (kein Log)"; return }
    $issues = Select-String $Log -Pattern 'error CS', 'warning CS', 'Shader error', 'Exception:', 'NullReferenceException', '\[Spinnenspiel\]' |
        ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique | Select-Object -First 25
    if ($issues) { $issues | ForEach-Object { "  $_" } } else { "  keine Fehler/Warnungen/Meldungen" }
}

$summary = @()
$step = 0
foreach ($m in $Method) {
    $step++
    $log = "$logDir\$Name-method$step.log"
    $code = Invoke-Unity @('-quit', '-executeMethod', $m) $log
    $summary += "METHOD $m -> Exit $code"
    $summary += Show-LogIssues $log
}

if ($Method.Count -eq 0) {
    $log = "$logDir\$Name-compile.log"
    $code = Invoke-Unity @('-quit') $log
    $summary += "COMPILE -> Exit $code"
    $summary += Show-LogIssues $log
}

if ($Tests) {
    $log = "$logDir\$Name-tests.log"
    $xml = "$logDir\$Name-tests.xml"
    Remove-Item $xml -ErrorAction SilentlyContinue
    $code = Invoke-Unity @('-runTests', '-testPlatform', 'PlayMode', '-testResults', "`"$xml`"") $log
    $summary += "TESTS -> Exit $code"
    if (Test-Path $xml) {
        [xml]$r = Get-Content $xml
        $run = $r.'test-run'
        $summary += "  Ergebnis: $($run.result) | gesamt $($run.total) | bestanden $($run.passed) | fehlgeschlagen $($run.failed)"
        foreach ($tc in $r.SelectNodes('//test-case')) {
            $msg = ''
            if ($tc.failure) { $msg = ($tc.failure.message.InnerText -replace '\s+', ' ').Trim() }
            $summary += "  $($tc.name): $($tc.result) $msg"
        }
    } else {
        $summary += "  Keine Test-XML erzeugt"
        $summary += Show-LogIssues $log
    }
}

$out = "$logDir\$Name-summary.txt"
$summary | Set-Content -Path $out -Encoding UTF8
$summary
