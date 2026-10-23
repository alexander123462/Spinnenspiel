# Startet den gebauten Player (Builds\Windows64), wartet, macht ein Foto des Fensters und beendet ihn.
# Aufruf: powershell -ExecutionPolicy Bypass -File Tools\spiel_foto.ps1 -Out Logs\ci\spiel_foto.png [-Warte 60]
param([string]$Out = "Logs\ci\spiel_foto.png", [int]$Warte = 60)
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public class WSF { [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r); [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; } }
'@
$root = Split-Path -Parent $PSScriptRoot
$p = Start-Process "$root\Builds\Windows64\Spinnenspiel.exe" -ArgumentList '-screen-fullscreen','0','-screen-width','1280','-screen-height','720' -PassThru
Start-Sleep $Warte
$p.Refresh(); $h = $p.MainWindowHandle
$r = New-Object WSF+RECT; [void][WSF]::GetWindowRect($h, [ref]$r)
$bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
$g = [System.Drawing.Graphics]::FromImage($bmp); $dc = $g.GetHdc(); [void][WSF]::PrintWindow($h, $dc, 2); $g.ReleaseHdc($dc)
$full = if ([System.IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $root $Out }
$bmp.Save($full)
Stop-Process $p -Force
Write-Output "Foto: $full"
