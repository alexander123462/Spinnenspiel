# Pause-Schalter fuer den Autopiloten: ist er pausiert, wird gestartet; laeuft er, wird pausiert.
# Aufruf ueber die Desktop-Datei "0 AUTOPILOT AN-AUS (Pause-Schalter).bat".
$root = 'C:\Users\vorau\dev\Spinnenspiel'
$pause = "$root\Logs\autopilot_pause"
$log = "$root\Logs\autopilot_loop.log"

function Lauscht($port) { [bool](Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) }
function Warte($port, $sekunden) {
  $t = [Diagnostics.Stopwatch]::StartNew()
  while ($t.Elapsed.TotalSeconds -lt $sekunden) {
    try { Invoke-WebRequest -UseBasicParsing -TimeoutSec 2 "http://127.0.0.1:$port/v1/models" | Out-Null; return $true } catch { Start-Sleep 4 }
  }
  return $false
}

if (Test-Path $pause) {
  Write-Host ''
  Write-Host 'AUTOPILOT STARTEN' -ForegroundColor Cyan
  $editor = Get-Process Unity -ErrorAction SilentlyContinue
  if ($editor) {
    Write-Host 'Der Unity-Editor ist offen. Bitte zuerst schliessen, sonst kann der Autopilot nicht testen.' -ForegroundColor Yellow
    Write-Host 'Nichts gestartet.'
    return
  }
  if (-not (Lauscht 8084)) {
    Write-Host 'Starte Coder-KI (Strata, 5080) ...'
    Start-Process -FilePath 'C:\AI\Strata\.venv\Scripts\python.exe' -ArgumentList '"C:\AI\Strata\serve\server.py"', '--engine', 'strata', '--config', '"C:\AI\Strata\strata-qwen-iq3_s-long.json"', '--port', '8084' -WorkingDirectory 'C:\AI\Strata' -WindowStyle Hidden
    if (Warte 8084 300) { Write-Host '  Coder-KI bereit.' -ForegroundColor Green } else { Write-Host '  Coder-KI antwortet nicht (nach 5 Minuten).' -ForegroundColor Red; return }
  } else { Write-Host 'Coder-KI laeuft schon.' }
  # Bilder liest die Coder-KI selbst (Vision-Encoder auf der CPU, Port 8084); die separate Vision-KI auf der 2070 entfaellt.
  Remove-Item -Force $pause -ErrorAction SilentlyContinue
  Add-Content $log ('== Gestartet per Schalter ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
  & powershell -NoProfile -ExecutionPolicy Bypass -File "$root\Tools\autopilot_watchdog.ps1"
  Write-Host ''
  Write-Host 'AUTOPILOT LAEUFT (Pause beendet). Die Schleife startet innerhalb von Sekunden.' -ForegroundColor Green
} else {
  Write-Host ''
  Write-Host 'AUTOPILOT PAUSIEREN' -ForegroundColor Cyan
  # Komprimiert Hermes gerade? Dann geht die Rechenarbeit (5-9 Min) beim Abbruch verloren und beginnt beim Fortsetzen neu.
  function Komprimiert {
    $f = "$root\Logs\autopilot_runde.out"
    if (-not (Test-Path $f)) { return $false }
    $t = (Get-Content $f -Tail 400 -ErrorAction SilentlyContinue) -join "`n"
    $s = $t.LastIndexOf('context compression started')
    $d = $t.LastIndexOf('context compression done')
    return ($s -ge 0 -and $s -gt $d)
  }
  if (Komprimiert) {
    Write-Host 'ACHTUNG: Hermes komprimiert gerade seinen Kontext (dauert 5-9 Minuten).' -ForegroundColor Yellow
    Write-Host 'Pausierst du jetzt, geht diese Arbeit verloren und beginnt beim Fortsetzen von vorn.'
    $a = Read-Host 'W = warten bis fertig (max. 15 Minuten), J = trotzdem sofort pausieren'
    if ($a -match '^[Ww]') {
      $t = [Diagnostics.Stopwatch]::StartNew()
      while ((Komprimiert) -and $t.Elapsed.TotalMinutes -lt 15) { Write-Host ('  warte ... {0:N0} s' -f $t.Elapsed.TotalSeconds); Start-Sleep 15 }
      Write-Host 'Komprimierung fertig (oder Zeit abgelaufen), pausiere jetzt.'
    }
  }
  New-Item -ItemType File -Force $pause | Out-Null
  New-Item -ItemType File -Force "$root\Logs\autopilot_resume_ok" | Out-Null
  Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'hermes.exe chat' -or $_.CommandLine -match 'autopilot_loop2' -or $_.Name -eq 'tee.exe' -or ($_.Name -eq 'Unity.exe' -and $_.CommandLine -match 'batchmode') } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
  Write-Host 'Schleife, Hermes und Unity-Testlaeufe beendet.'
  Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'Strata\\serve\\server.py' -or $_.Name -match '^strata' -or $_.Name -eq 'llama-server.exe' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
  Write-Host 'Coder-KI beendet (Grafikkarte frei).'
  Add-Content $log ('== Pausiert per Schalter ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
  Write-Host ''
  Write-Host 'AUTOPILOT PAUSIERT. Viel Spass am PC!' -ForegroundColor Green
}
