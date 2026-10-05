# Auftrag 3.0: Poly-Haven-Katalog abfragen (nur Listen, nichts herunterladen)
$ErrorActionPreference = 'Stop'
$UA   = 'Spinnenspiel/1.0 (Alex, lokal)'
$Root = Split-Path -Parent $PSScriptRoot
$out  = Join-Path $Root 'Logs\assets'
New-Item -ItemType Directory -Force -Path $out | Out-Null

function Get-Ph([string]$url) {
  return Invoke-RestMethod -Uri $url -UserAgent $UA
}

# Schreibt Assets als "id | name | categories | tags", sortiert nach id.
# $wanted leer -> alle; sonst nur Assets, deren categories eine der wanted-Kategorien (kleingeschrieben) enthalten.
function Write-PhFile($obj, [string]$path, $wanted) {
  $lines = foreach ($id in ($obj.PSObject.Properties.Name | Sort-Object)) {
    $v    = $obj.$id
    $cats = @($v.categories | ForEach-Object { $_.ToString().ToLower() })
    if ($wanted -and -not (@($wanted | Where-Object { $cats -contains $_ }).Count)) { continue }
    "$id | $($v.name) | $(@($v.categories) -join ', ') | $(@($v.tags) -join ', ')"
  }
  $arr = @($lines)
  if ($arr.Count -eq 0) { $arr = @('') }
  Set-Content -LiteralPath $path -Value $arr -Encoding utf8
  return $arr.Count
}

$models   = Get-Ph 'https://api.polyhaven.com/assets?t=models'
$textures = Get-Ph 'https://api.polyhaven.com/assets?t=textures'
$skies    = Get-Ph 'https://api.polyhaven.com/assets?t=hdris&c=skies'

$wantedModels   = @('plants','nature','rocks','ground cover')
$wantedTextures = @('terrain','natural','rock','wood','bark','ground','soil','grass','moss')

$n1 = Write-PhFile $models   "$out\ph_models.txt"   $wantedModels
$n2 = Write-PhFile $textures "$out\ph_textures.txt" $wantedTextures
$n3 = Write-PhFile $skies    "$out\ph_skies.txt"    $null

Write-Output "ph_models.txt   : $n1 Zeilen"
Write-Output "ph_textures.txt : $n2 Zeilen"
Write-Output "ph_skies.txt    : $n3 Zeilen"
