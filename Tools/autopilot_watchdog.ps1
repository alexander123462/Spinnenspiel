# Startet die Autopilot-Schleife neu, falls sie nicht laeuft. Pausieren: Datei Logs\autopilot_pause anlegen.
$root = 'C:\Users\vorau\dev\Spinnenspiel'
$log = "$root\Logs\autopilot_loop.log"
if (Test-Path "$root\Logs\autopilot_pause") { exit 0 }
$laeuft = Get-CimInstance Win32_Process -Filter "Name='bash.exe'" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -match 'autopilot_loop2' }
if ($laeuft) {
  # Haengt Hermes (keine Ausgabe seit 30 Min, z. B. Modell antwortet nicht)? Dann alles beenden und neu starten.
  $out = Get-Item "$root\Logs\autopilot_runde.out" -ErrorAction SilentlyContinue
  if (-not $out -or ((Get-Date) - $out.LastWriteTime).TotalMinutes -lt 30) { exit 0 }
  Add-Content $log ("== Watchdog: Hermes haengt seit " + $out.LastWriteTime.ToString('HH:mm:ss') + ", beende Schleife " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
  Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'hermes.exe chat' -or $_.CommandLine -match 'autopilot_loop2' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
  Start-Sleep 5
}
if (Test-Path "$root\Tools\autopilot_loop2.sh.neu") { Move-Item -Force "$root\Tools\autopilot_loop2.sh.neu" "$root\Tools\autopilot_loop2.sh" }
Remove-Item -Force "$root\Logs\autopilot_stop" -ErrorAction SilentlyContinue
Add-Content $log ("== Watchdog startet Schleife neu " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
Start-Process -FilePath "$root\Tools\autopilot_loop2.bat" -WindowStyle Minimized
