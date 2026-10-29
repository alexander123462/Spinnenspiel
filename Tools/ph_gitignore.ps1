$Root = Split-Path -Parent $PSScriptRoot
$p = Join-Path $Root '.gitignore'
$c = [IO.File]::ReadAllText($p)
if ($c -notmatch '(?m)^/AssetSource/\s*$') {
  if ($c -and -not $c.EndsWith([char]10)) { $c += "`n" }
  [IO.File]::WriteAllText($p, $c + "/AssetSource/`n")
  Write-Output 'ergaenzt'
} else {
  Write-Output 'bereits enthalten'
}
Write-Output '----- neue Datei -----'
Get-Content $p
