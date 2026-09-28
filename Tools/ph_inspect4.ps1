$ErrorActionPreference = 'Stop'
$ua = 'Spinnenspiel/1.0 (Alex, lokal)'
$o  = Invoke-RestMethod -Uri 'https://api.polyhaven.com/files/fern_02' -UserAgent $ua
Write-Output "=== fbx ==="
Write-Output ($o.fbx | ConvertTo-Json -Depth 6 -Compress)
Write-Output "=== gltf (gekürzt) ==="
Write-Output (($o.gltf | ConvertTo-Json -Depth 6 -Compress).Substring(0, 400))
