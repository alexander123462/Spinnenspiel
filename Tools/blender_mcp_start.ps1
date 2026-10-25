# Startet Blender (sichtbar) mit dem MCP-Server auf Port 9876, falls er nicht schon laeuft. Wartet bis der Port offen ist.
# Aufruf: powershell -ExecutionPolicy Bypass -File Tools\blender_mcp_start.ps1
$root = Split-Path -Parent $PSScriptRoot
function Lauscht { [bool](Get-NetTCPConnection -LocalPort 9876 -State Listen -ErrorAction SilentlyContinue) }
# RAM-Schutz: nicht neben einem laufenden Unity starten (Unity-Batchmode oder Editor), hoechstens 25 Minuten warten.
for ($w = 0; $w -lt 150 -and (Get-Process Unity -ErrorAction SilentlyContinue); $w++) { Start-Sleep 10 }
if (Lauscht) { Write-Output 'Blender-MCP laeuft bereits (Port 9876).'; return }
$exe = 'C:\Program Files\Blender Foundation\Blender 5.1\blender.exe'
Start-Process $exe -ArgumentList '--python', "$root\Tools\blender_mcp_start.py" | Out-Null
for ($i = 0; $i -lt 40; $i++) { Start-Sleep 2; if (Lauscht) { Write-Output 'Blender-MCP bereit (Port 9876).'; return } }
Write-Output 'FEHLER: Blender-MCP antwortet nicht auf Port 9876.'
exit 1
