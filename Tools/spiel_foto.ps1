# Startet den gebauten Player (Builds\Windows64) im Foto-Modus: der Player speichert das
# Foto selbst (ScreenCapture) und beendet sich dann. Wartet auf die Datei, hoechstens 120 s.
# Fehlt die Datei nach 120 s, macht die PowerShell ein Fenster-Foto (altes Verhalten) und killt den Player.
# Aufruf: powershell -ExecutionPolicy Bypass -File Tools\spiel_foto.ps1 -Out Logs\ci\spiel_foto.png [-Seed 4242] [-Pitch 0] [-Warte 25] [-Boden]
param(
    [string]$Out = "Logs\ci\spiel_foto.png",
    [int]$Seed = 4242,
    [double]$Pitch = 0,
    [int]$Warte = 25,
    [string]$Near = "",
    [double]$Zoom = 0,
    [switch]$Boden,
    [switch]$Riesen,  # MODELL12: Riesenblaetter trotz WALD6 ins Player-Foto (-fotoriesen)
    [switch]$Baumfarn,  # MODELL15: Baumfarne trotz WALD6 ins Player-Foto (-fotobaumfarn)
    [switch]$Immer   # Nutzer-Wache ueberspringen (nur fuer Overseer-Tests)
)
# Nutzer-Wache (Alex 2026-10-26: ein sichtbares Spielfenster stoert beim Zocken): nur starten, wenn seit mindestens 180 s keine Eingabe an Tastatur/Maus kam.
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public class LeerlaufWache { [StructLayout(LayoutKind.Sequential)] struct LII { public uint cb; public uint t; }
 [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LII p);
 public static double Sekunden() { LII l = new LII(); l.cb = (uint)Marshal.SizeOf(typeof(LII)); GetLastInputInfo(ref l); return ((uint)Environment.TickCount - l.t) / 1000.0; } }
'@
if (-not $Immer) {
    $idle = [LeerlaufWache]::Sekunden()
    if ($idle -lt 180) {
        Write-Output ("NUTZER AKTIV (letzte Eingabe vor {0:N0} s): Foto verschoben, in 5 Minuten erneut versuchen (Start-Sleep 300), hoechstens 6 Mal; danach Foto-Schritt zurueckstellen." -f $idle)
        exit 3
    }
}
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public class WSF { [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r); [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; } }
'@
$root = Split-Path -Parent $PSScriptRoot
$full = if ([System.IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $root $Out }
New-Item -ItemType Directory -Path (Split-Path -Parent $full) -Force | Out-Null
Remove-Item $full -ErrorAction SilentlyContinue

$pArgs = @(
    '-screen-fullscreen','0','-screen-width','1280','-screen-height','720',
    '-fotopfad', "`"$full`"", '-fotoseed', "$Seed", '-fotopitch', "$Pitch", '-fotowarte', "$Warte"
)
if ($Boden) { $pArgs += '-fotoboden' }
if ($Riesen) { $pArgs += '-fotoriesen' }
if ($Baumfarn) { $pArgs += '-fotobaumfarn' }
if ($Near) { $pArgs += @('-fotonear', "`"$Near`"") }
if ($Zoom -gt 0) { $pArgs += @('-fotozoom', "$Zoom") }
$p = Start-Process "$root\Builds\Windows64\Spinnenspiel.exe" -ArgumentList $pArgs -PassThru

# Auf die Foto-Datei warten: sie gilt als fertig, wenn sie ueber 100 KB hat und ihre
# Groesse ueber mehrere Abfragen stabil ist. Hoechstens 120 s.
$deadline = (Get-Date).AddSeconds(120)
$lastSize = -1; $stable = 0
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
    if (Test-Path $full) {
        $size = (Get-Item $full).Length
        if ($size -gt 100KB -and $size -eq $lastSize) { $stable++ } else { $stable = 0 }
        if ($stable -ge 2) { break }
        $lastSize = $size
    }
}

if (-not (Test-Path $full)) {
    # Fallback: altes Fenster-Foto, falls der Player das Foto nicht selbst schreibt.
    Start-Sleep $Warte
    $p.Refresh(); $h = $p.MainWindowHandle
    $r = New-Object WSF+RECT; [void][WSF]::GetWindowRect($h, [ref]$r)
    $bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
    $g = [System.Drawing.Graphics]::FromImage($bmp); $dc = $g.GetHdc(); [void][WSF]::PrintWindow($h, $dc, 2); $g.ReleaseHdc($dc)
    $bmp.Save($full)
    Write-Output "Foto (Fallback Fenster): $full"
} else {
    Write-Output ("Foto (Player): $full ({0:N0} Bytes)" -f (Get-Item $full).Length)
}

if (-not $p.HasExited) { Stop-Process $p -Force }
