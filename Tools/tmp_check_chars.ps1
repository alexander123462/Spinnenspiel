# Temporäre Diagnose: fremde Zeichen + Zeilenumbrueche in ScreenshotTool.cs
$f = "C:\Users\vorau\dev\Spinnenspiel\Assets\_Project\Scripts\Editor\ScreenshotTool.cs"
$t = [System.IO.File]::ReadAllText($f)
$bad = @()
foreach ($ch in $t.ToCharArray()) {
    $c = [int]$ch
    if (($c -lt 9) -or ($c -gt 13 -and $c -lt 32)) { $bad += $c }
}
"Anomal Zeichen (dezimal): $(($bad | Select-Object -Unique) -join ', ')"
"LF: $(([regex]::Matches($t, [char]10)).Count)  CR: $(([regex]::Matches($t, [char]13)).Count)  BOM: $([bool]($t.StartsWith([char]0xFEFF)))"
"Zeilen: $((Get-Content $f).Count)"
