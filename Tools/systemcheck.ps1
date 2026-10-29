# Systemcheck fuer Overseer/Alex: prueft, ob KI-Kette und Autopilot richtig eingestellt sind. Nur lesen, aendert nichts.
# Aufruf: powershell -ExecutionPolicy Bypass -File Tools\systemcheck.ps1
$root = Split-Path -Parent $PSScriptRoot
$ok = 0; $warn = 0
function OK($t)   { Write-Output "  [OK]   $t"; $script:ok++ }
function WARN($t) { Write-Output "  [FEHLT] $t"; $script:warn++ }

Write-Output "== Strata"
$st = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'strata.exe' })
$srv = @(Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'serve\\server.py' -and $_.Name -match 'python' })
$bw = (Get-Content "C:\AI\Strata\engine\BUILD.json" -Raw | ConvertFrom-Json).version
if ($bw -ge '0.1.50') { OK "Engine $bw (aktuell)" } else { WARN "Engine $bw (alt, Update 0.1.50 ausstehend)" }
if ($st.Count -eq 0) { Write-Output "  [aus]  Strata laeuft nicht (normal, wenn pausiert)" }
elseif ($st.Count -eq 1) { OK "genau 1 strata.exe" } else { WARN "$($st.Count) strata.exe gleichzeitig (RAM-Gefahr)" }
if ($st | Where-Object { $_.CommandLine -match 'iq2_xs' }) { WARN "IQ2_XS laeuft (sollte nicht, nur IQ3_S)" }
try { $m = (Invoke-RestMethod http://127.0.0.1:8084/v1/models -TimeoutSec 4).data[0]; if ($m.id -eq 'qwen3.8-flash-next-iq3_s') { OK "Port 8084: $($m.id), Kontext $($m.meta.n_ctx)" } else { WARN "Port 8084 meldet $($m.id)" } } catch { Write-Output "  [aus]  Port 8084 antwortet nicht" }
if (Get-NetTCPConnection -LocalPort 8080 -State Listen -ErrorAction SilentlyContinue) { WARN "Port 8080 belegt (alter Server?)" }
$cfg = Get-Content "C:\AI\Strata\strata-qwen-iq3_s-long.json" -Raw
if ($cfg -match 'conversation-cache-min-free-mib') { OK "Strata-Konfig: Cache-Reserve gesetzt" } else { WARN "Strata-Konfig: Cache-Reserve fehlt" }

Write-Output "== RAM / GPU"
$o = Get-CimInstance Win32_OperatingSystem; $frei = [math]::Round($o.FreePhysicalMemory / 1MB, 1)
if ($frei -ge 10 -or $st.Count -eq 0) { OK "RAM frei $frei GB" } else { WARN "RAM frei nur $frei GB (unter 10)" }
Write-Output ("  GPU: " + ((nvidia-smi --query-gpu=name,memory.used,memory.total --format=csv,noheader) -join ' | '))
$bl = @(Get-Process blender -ErrorAction SilentlyContinue).Count; $un = @(Get-Process Unity -ErrorAction SilentlyContinue).Count
if ($bl -gt 0 -and $un -gt 0) { WARN "Blender und Unity laufen gleichzeitig" } else { OK "Blender/Unity nicht gleichzeitig (Blender $bl, Unity $un)" }

Write-Output "== Hermes"
$hc = 'C:\Users\vorau\AppData\Local\hermes\config.yaml'
$t = Get-Content $hc -Raw
if (Test-Path 'C:\Users\vorau\AppData\Local\hermes\bin\hermes.exe') { OK "hermes.exe (stabiler Pfad) vorhanden" } else { WARN "hermes.exe fehlt" }
if ($t -match 'threshold: 0\.45') { OK "Komprimierung 0.45" } else { WARN "Komprimierung nicht 0.45" }
if ($t -match 'reasoning_effort: "medium"') { OK "Denklevel medium" } else { WARN "Denklevel nicht medium" }
$aux = [regex]::Match($t, '(?s)\nauxiliary:.*?\nplatforms:').Value
if ($aux -match '8082') { WARN "Hermes-Bildanalyse (auxiliary.vision) zeigt auf toten Port 8082" } else { OK "Bildanalyse (auxiliary.vision) zeigt auf Strata 8084" }
if (Select-String -Path "$root\Tools\autopilot_loop2.sh" -Pattern 'PROVIDER="custom:strata_iq3"; MODELL="qwen3.8-flash-next-iq3_s"' -Quiet) { OK "Schleife nutzt strata_iq3" } else { WARN "Schleife nutzt nicht strata_iq3" }
if (Select-String -Path "$root\Tools\bild_fragen.py" -Pattern '^PORT = 8084' -Quiet) { OK "bild_fragen -> Port 8084" } else { WARN "bild_fragen zeigt nicht auf 8084" }
if (Select-String -Path "$root\Tools\bild_fragen.py" -Pattern 'Popen' -Quiet) { WARN "bild_fragen kann Server starten (Popen)" } else { OK "bild_fragen startet keinen Server" }
$mcp = (& 'C:\Users\vorau\AppData\Local\hermes\bin\hermes.exe' mcp list 2>&1 | Out-String)
if ($mcp -match 'blender' -and $mcp -match '4 selected') { OK "Blender-MCP: 4 Werkzeuge" } else { WARN "Blender-MCP fehlt/falsche Werkzeugzahl" }

Write-Output "== Autopilot"
$pause = Test-Path "$root\Logs\autopilot_pause"
if ($pause) { Write-Output "  [Pause] Autopilot ist pausiert (Desktop-Schalter 0 startet ihn)" } else { Write-Output "  [an]   Autopilot nicht pausiert" }
$loop = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'bash.exe' -and $_.CommandLine -match 'autopilot_loop2' }).Count
Write-Output "  Schleifenprozesse: $loop, hermes.exe: $(@(Get-Process hermes -ErrorAction SilentlyContinue).Count)"
$ll = ((Get-Content "$root\Logs\autopilot_loop.log" -Tail 2) -join ' // ')
Write-Output ("  Letzte Zeilen Schleife: " + $ll.Substring(0, [Math]::Min(200, $ll.Length)))
$nxt = (Select-String -Path "$root\Docs\BACKLOG.md" -Pattern '^- \[ \]' | Select-Object -First 1).Line
Write-Output ("  Naechster offener Punkt: " + $nxt.Substring(0, [Math]::Min(80, $nxt.Length)))
if (Test-Path "$root\Logs\no_claude") { WARN "Logs\no_claude gesetzt: Eskalation aus" } else { OK "Eskalation an (Limit 4 pro Tag)" }

Write-Output "== Git"
Write-Output ("  HEAD: " + (git -C $root log -1 --format='%h %s').Substring(0, 70))
Write-Output ("  Unbestaetigte Dateien in Scripts/Tests: " + @(git -C $root status --short -- Assets/_Project/Scripts Assets/_Project/Tests).Count)
Write-Output ""
Write-Output "Ergebnis: $ok OK, $warn Abweichungen"
