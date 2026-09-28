$ErrorActionPreference = 'Stop'
$ua = 'Spinnenspiel/1.0 (Alex, lokal)'
$o = Invoke-RestMethod -Uri 'https://api.polyhaven.com/files/kloofendal_48d_partly_cloudy_puresky' -UserAgent $ua
Write-Output (($o.hdri.'4k' | ConvertTo-Json -Depth 5 -Compress).Substring(0, 300))
Write-Output "----- forest_ground_04 Displacement 2k -----"
$t = Invoke-RestMethod -Uri 'https://api.polyhaven.com/files/forest_ground_04' -UserAgent $ua
foreach ($k in @('Displacement','nor_gl','arm')) {
  if ($t.PSObject.Properties.Name -contains $k) {
    $node = $t.$k
    $fmts = $node.'2k'.PSObject.Properties.Name
    Write-Output ($k + " 2k formats: " + ($fmts -join ', '))
  } else { Write-Output ($k + ": NICHT VERFUEGBAR") }
}
