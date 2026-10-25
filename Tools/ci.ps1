# Spinnenspiel CI: optional Editor-Methode ausfuehren, kompilieren, PlayMode-Tests laufen lassen.
# Aufruf: powershell -ExecutionPolicy Bypass -File <Projekt>\Tools\ci.ps1 -Name m2 -Method Spinnenspiel.EditorTools.Milestone1SceneBuilder.Build -Tests
# Optional: -Filter "Spinnenspiel.Tests.Climb*" nur Teilmenge der Tests (schnelle Einzellaeufe; vor Commits immer ohne Filter).
param(
    [string]$Name = "ci",
    [string[]]$Method = @(),
    [switch]$Tests,
    [string]$Filter = ""
)

# Eigenes Konsolenfenster verstecken (sonst poppt es bei jedem CI-Lauf kurz auf, wenn Hermes den Aufruf startet).
try { Add-Type -Name W -Namespace CiWin -MemberDefinition '[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c); [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();'; [void][CiWin.W]::ShowWindow([CiWin.W]::GetConsoleWindow(), 0) } catch {}
$ErrorActionPreference = 'Continue'
# Bei Aufruf ueber -File kommen Listen als ein String "a,b" an -> aufteilen.
$Method = @($Method | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$Root = Split-Path -Parent $PSScriptRoot
$project = $Root
$UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe' }
$logDir = "$project\Logs\ci"
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

function Invoke-Unity([string[]]$UnityArgs, [string]$Log) {
    # RAM-Schutz (Overseer 2026-10-25): Blender und Unity nie gleichzeitig neben Strata (sonst stirbt die Strata-Engine, RAM voll). Blender wird vor dem Unity-Lauf beendet; Modelle sind dann schon exportiert.
    $bl = Get-Process blender -ErrorAction SilentlyContinue; if ($bl) { $bl | Stop-Process -Force -ErrorAction SilentlyContinue; Get-Process blender-mcp -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue }
    $all = @('-batchmode', '-projectPath', "`"$project`"", '-logFile', "`"$Log`"") + $UnityArgs
    # Nur auf Unity selbst warten (Start-Process -Wait wartet auch auf Hintergrund-Kindprozesse wie den Lizenz-Client).
    $p = Start-Process -FilePath $UnityExe -ArgumentList $all -PassThru -WindowStyle Hidden
    try { $p.PriorityClass = 'BelowNormal' } catch {}  # die lokale KI (Strata) hat Vorrang vor Builds und Tests
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
    $testArgs = @('-runTests', '-testPlatform', 'PlayMode', '-testResults', "`"$xml`"")
    if ($Filter) { $testArgs += @('-testFilter', $Filter) }
    $code = Invoke-Unity $testArgs $log
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
