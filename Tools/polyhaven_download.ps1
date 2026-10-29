# polyhaven_download.ps1 – Poly Haven Assets nach AssetSource\polyhaven (außerhalb von Assets)
# Wiederverwendbar: vorhandene Dateien (gleiche Größe + MD5) werden übersprungen.
# Einzelne Fehler werden gemeldet, das Skript bricht nicht ab.
$ErrorActionPreference = 'Stop'
$UA   = 'Spinnenspiel/1.0 (Alex, lokal)'
$Root = Split-Path -Parent $PSScriptRoot
$base = Join-Path $Root 'AssetSource\polyhaven'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

# ---------- Listen ----------
$models1k = @(
  'grass_medium_01','grass_medium_02','fern_02','moss_01','nettle_plant','dandelion_01',
  'celandine_01','weed_plant_02','periwinkle_plant','dry_branches_medium_01','pine_roots',
  'root_cluster_01','stone_01'
)
$models2k = @(
  'shrub_02','shrub_03','shrub_04','fir_tree_01','pine_tree_01','fir_sapling',
  'pine_sapling_small','pine_sapling_medium','dead_tree_trunk','dead_tree_trunk_02',
  'tree_stump_01','tree_stump_02','rock_moss_set_01','rock_moss_set_02','rock_07','rock_09','boulder_01',
  'island_tree_01','island_tree_02','island_tree_03','jacaranda_tree','tree_small_02'
)
$textures2k = @(
  'forest_ground_04','sparse_grass','brown_mud_leaves_01','mud_forest','rocky_trail',
  'mossy_rock','river_small_rocks','forest_leaves_04','pine_bark'
)
$hdri4k = @(
  'kloofendal_48d_partly_cloudy_puresky'
)

# ---------- Infrastruktur ----------
$wc = New-Object System.Net.WebClient
$wc.Headers.Add('User-Agent', $UA)

$manifest   = [System.Collections.Generic.List[object]]::new()
$failures   = [System.Collections.Generic.List[string]]::new()
$manifestPath = if (Test-Path (Join-Path $PSScriptRoot 'polyhaven_manifest.csv')) { Join-Path $PSScriptRoot 'polyhaven_manifest.csv' } else { Join-Path $base 'manifest.csv' }
New-Item -ItemType Directory -Force -Path $base | Out-Null

function Get-Ph([string]$path) {
  return Invoke-RestMethod -Uri ('https://api.polyhaven.com/' + $path) -UserAgent $UA
}

function Get-FileMd5([string]$path) {
  $md5 = [System.Security.Cryptography.MD5]::Create()
  $fs = [System.IO.File]::OpenRead($path)
  try { $h = $md5.ComputeHash($fs) } finally { $fs.Dispose() }
  return (([System.BitConverter]::ToString($h) -replace '-','').ToLower())
}

function Get-Basename([string]$url) {
  return [uri]::UnescapeDataString(($url -split '/')[-1])
}

function Add-ManifestRow([string]$id, [string]$typ, [int]$files, [long]$bytes, [string]$poly) {
  $manifest.Add([pscustomobject]@{ id=$id; typ=$typ; dateien=$files; bytes=$bytes; polycount=$poly })
}

function Get-Polycount([string]$id) {
  try {
    $i = Get-Ph ('info/' + $id)
    if ($i.PSObject.Properties.Name -contains 'polycount') { return ([string]$i.polycount) }
  } catch { }
  return ''
}

# Speichert eine Datei; überspringt bei gleicher Größe + MD5. Lädt sonst neu (via .part, danach rename).
function Invoke-SaveFile([string]$url, [string]$dest, $size, $md5) {
  if (Test-Path -LiteralPath $dest) {
    $len = (Get-Item -LiteralPath $dest).Length
    if ($null -ne $size -and $len -eq [long]$size) {
      if ($null -eq $md5 -or (Get-FileMd5 $dest) -eq $md5.ToLower()) {
        Write-Output ("    SKIP: " + (Split-Path $dest -Leaf))
        return $true
      }
      Write-Output ("    MD5-Abweichung, neu laden: " + (Split-Path $dest -Leaf))
    } else {
      Write-Output ("    Größenabweichung, neu laden: " + (Split-Path $dest -Leaf))
    }
  }
  $dir = Split-Path $dest -Parent
  if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
  $tmp = $dest + '.part'
  $wc.DownloadFile($url, $tmp)
  if (Test-Path -LiteralPath $dest) { Remove-Item -LiteralPath $dest -Force }
  Move-Item -LiteralPath $tmp -Destination $dest
  $b = (Get-Item -LiteralPath $dest).Length
  Write-Output ("    OK (" + [math]::Round($b/1MB, 2) + " MB): " + (Split-Path $dest -Leaf))
  return $true
}

function Record-File([string]$dest, [ref]$files, [ref]$bytes) {
  $files.Value++
  $bytes.Value += (Get-Item -LiteralPath $dest).Length
}

# ---------- Modelle: FBX + include (Texturen) ----------
foreach ($id in @($models1k + $models2k)) {
  $res = if ($id -in $models1k) { '1k' } else { '2k' }
  $dir = Join-Path $base ('models\' + $id)
  try {
    $o = Get-Ph ('files/' + $id)
    $m = $o.fbx.($res).fbx
    $files = 0; $bytes = [long]0

    $dest = Join-Path $dir (Get-Basename $m.url)
    if (Invoke-SaveFile $m.url $dest $m.size $m.md5) { Record-File $dest ([ref]$files) ([ref]$bytes) }

    foreach ($rel in $m.include.PSObject.Properties.Name) {
      $e = $m.include.$rel
      $fdest = Join-Path $dir ($rel -replace '/', '\')
      if (Invoke-SaveFile $e.url $fdest $e.size $e.md5) { Record-File $fdest ([ref]$files) ([ref]$bytes) }
    }

    $poly = Get-Polycount $id
    Add-ManifestRow $id 'model' $files $bytes $poly
    Write-Output ("[model] " + $id + " (" + $res + "): " + $files + " Dateien, " + [math]::Round($bytes/1MB, 2) + " MB, polycount " + $poly)
  } catch {
    Write-Output ("[FEHLER] model " + $id + ": " + $_.Exception.Message)
    $failures.Add('model ' + $id + ': ' + $_.Exception.Message)
    Add-ManifestRow $id 'model' 0 0 ''
  }
}

# ---------- Texturen: Diffuse, nor_gl, arm, Displacement in 2k ----------
$texMaps = @(
  @{ key='Diffuse';      fmts=@('jpg') },
  @{ key='nor_gl';       fmts=@('png','jpg') },
  @{ key='arm';          fmts=@('jpg') },
  @{ key='Displacement'; fmts=@('png','jpg') }
)
foreach ($id in $textures2k) {
  $dir = Join-Path $base ('textures\' + $id)
  try {
    $o = Get-Ph ('files/' + $id)
    $files = 0; $bytes = [long]0
    foreach ($m in $texMaps) {
      $node = $null
      if ($o.PSObject.Properties.Name -contains $m.key) { $node = $o.($m.key) }
      if (-not $node -or -not ($node.PSObject.Properties.Name -contains '2k')) {
        Write-Output ("[WARN] " + $id + ": Map " + $m.key + " nicht verfügbar")
        continue
      }
      $leaf = $null
      foreach ($f in $m.fmts) {
        if ($node.'2k'.PSObject.Properties.Name -contains $f) { $leaf = $node.'2k'.$f; break }
      }
      if (-not $leaf) {
        Write-Output ("[WARN] " + $id + ": Map " + $m.key + " ohne passendes Format")
        continue
      }
      $dest = Join-Path $dir (Get-Basename $leaf.url)
      if (Invoke-SaveFile $leaf.url $dest $leaf.size $leaf.md5) { Record-File $dest ([ref]$files) ([ref]$bytes) }
    }
    $poly = Get-Polycount $id
    Add-ManifestRow $id 'texture' $files $bytes $poly
    Write-Output ("[texture] " + $id + ": " + $files + " Dateien, " + [math]::Round($bytes/1MB, 2) + " MB")
  } catch {
    Write-Output ("[FEHLER] texture " + $id + ": " + $_.Exception.Message)
    $failures.Add('texture ' + $id + ': ' + $_.Exception.Message)
    Add-ManifestRow $id 'texture' 0 0 ''
  }
}

# ---------- HDRI: 4k .hdr ----------
foreach ($id in $hdri4k) {
  $dir = Join-Path $base 'hdri'
  try {
    $o = Get-Ph ('files/' + $id)
    $leaf = $o.hdri.'4k'.hdr
    $files = 0; $bytes = [long]0
    $dest = Join-Path $dir (Get-Basename $leaf.url)
    if (Invoke-SaveFile $leaf.url $dest $leaf.size $leaf.md5) { Record-File $dest ([ref]$files) ([ref]$bytes) }
    $poly = Get-Polycount $id
    Add-ManifestRow $id 'hdri' $files $bytes $poly
    Write-Output ("[hdri] " + $id + ": " + $files + " Dateien, " + [math]::Round($bytes/1MB, 2) + " MB")
  } catch {
    Write-Output ("[FEHLER] hdri " + $id + ": " + $_.Exception.Message)
    $failures.Add('hdri ' + $id + ': ' + $_.Exception.Message)
    Add-ManifestRow $id 'hdri' 0 0 ''
  }
}

# ---------- Manifest ----------
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('id;typ;dateien;bytes;polycount')
foreach ($r in $manifest) {
  $lines.Add(('{0};{1};{2};{3};{4}' -f $r.id, $r.typ, $r.dateien, $r.bytes, $r.polycount))
}
Set-Content -LiteralPath $manifestPath -Value $lines -Encoding utf8

# ---------- Zusammenfassung ----------
$all = Get-ChildItem -Path $base -Recurse -File | Where-Object { $_.Name -ne 'manifest.csv' -and $_.Name -ne '.part' }
$sum = ($all | Measure-Object -Property Length -Sum).Sum
Write-Output "==== ZUSAMMENFASSUNG ===="
Write-Output ("Dateien: " + $all.Count)
Write-Output ("Gesamtgröße: " + [math]::Round($sum/1MB, 1) + " MB")
if ($failures.Count -gt 0) {
  Write-Output "FEHLGESCHLAGENE ASSETS:"
  foreach ($f in $failures) { Write-Output ("  - " + $f) }
} else {
  Write-Output "Fehlgeschlagene Assets: keine"
}
