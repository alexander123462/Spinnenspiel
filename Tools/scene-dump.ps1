param([string]$Scene = '')
$Root = Split-Path -Parent $PSScriptRoot
if (-not $Scene) { $Scene = Join-Path $Root 'Assets\_Project\Scenes\Klettertest.unity' }
$raw = Get-Content $Scene -Raw
$docs = [regex]::Split($raw, '(?m)^--- ')
$names = @{}   # fileID -> name
$pos = @{}     # goFileID -> "x,y,z"
$scales = @{}
$colliders = @{}  # goFileID -> list

foreach ($doc in $docs) {
    $m = [regex]::Match($doc, '^!u!(\d+) &(\d+)')
    if (-not $m.Success) { continue }
    $type = $m.Groups[1].Value
    $goRef = ''
    $mg = [regex]::Match($doc, 'm_GameObject: \{fileID: (\d+)\}')
    if ($mg.Success) { $goRef = $mg.Groups[1].Value }
    if ($type -eq '1') {
        $mn = [regex]::Match($doc, 'm_Name: (.*)')
        if ($mn.Success) { $names[$m.Groups[2].Value] = $mn.Groups[1].Value }
    }
    if ($type -eq '4') {
        $mp = [regex]::Match($doc, 'm_LocalPosition: \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}')
        $ms = [regex]::Match($doc, 'm_LocalScale: \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}')
        if ($mp.Success -and $goRef) {
            $pos[$goRef] = "$($mp.Groups[1].Value),$($mp.Groups[2].Value),$($mp.Groups[3].Value)"
            if ($ms.Success) { $scales[$goRef] = "$($ms.Groups[1].Value),$($ms.Groups[2].Value),$($ms.Groups[3].Value)" }
        }
    }
    # Colliders: 65 Box, 64 Mesh, 135 Sphere, 136 Capsule, 144 Terrain, 75 Mesh? -> capture size/center
    if ($type -in '65','135','136','64') {
        $mc = [regex]::Match($doc, 'm_Center: \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}')
        $mm = [regex]::Match($doc, 'm_Size: \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}')
        $tname = @{ '65'='BoxCollider'; '135'='SphereCollider'; '136'='CapsuleCollider'; '64'='MeshCollider' }[$type]
        $line = $tname
        if ($mc.Success) { $line += " center=$($mc.Groups[1].Value),$($mc.Groups[2].Value),$($mc.Groups[3].Value)" }
        if ($mm.Success) { $line += " size=$($mm.Groups[1].Value),$($mm.Groups[2].Value),$($mm.Groups[3].Value)" }
        if ($goRef) {
            if (-not $colliders.ContainsKey($goRef)) { $colliders[$goRef] = @() }
            $colliders[$goRef] += , $line
        }
    }
}

foreach ($k in ($names.Keys | Sort-Object)) {
    $nm = $names[$k]
    if ($nm -match '^\s*$') { continue }
    $line = $nm
    if ($pos.ContainsKey($k)) { $line += " pos=" + $pos[$k] }
    if ($scales.ContainsKey($k)) { $line += " scale=" + $scales[$k] }
    if ($colliders.ContainsKey($k)) { $line += " [" + ($colliders[$k] -join ' | ') + "]" }
    Write-Output $line
}
