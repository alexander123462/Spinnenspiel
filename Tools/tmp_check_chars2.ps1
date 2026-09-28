# Temporäre Diagnose 2: BOM- und FEFF-Position in ScreenshotTool.cs
$f = "C:\Users\vorau\dev\Spinnenspiel\Assets\_Project\Scripts\Editor\ScreenshotTool.cs"
$b = [System.IO.File]::ReadAllBytes($f)
"Bytes 0..15: $(($b[0..15] | ForEach-Object { $_ }) -join ' ')"
$t = [System.IO.File]::ReadAllText($f)
"String laenge: $($t.Length)"
"Erste 10 Zeichen (Codepoints): $(($t.ToCharArray()[0..9] | ForEach-Object { [int]$_ }) -join ' ')"
"FEFF-Index im String: $($t.IndexOf([char]0xFEFF))"
"StartsWith-FEFF: $($t.StartsWith([char]0xFEFF))"
$enc = [System.Text.Encoding]::UTF8
$utf8bom = [byte[]](0xEF,0xBB,0xBF)
if ($b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF) { "Datei hat UTF-8 BOM" } else { "Datei ohne UTF-8 BOM" }
