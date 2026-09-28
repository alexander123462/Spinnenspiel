$ErrorActionPreference = 'Stop'
$ua = 'Spinnenspiel/1.0 (Alex, lokal)'
$o  = Invoke-RestMethod -Uri 'https://api.polyhaven.com/files/fern_02' -UserAgent $ua
$l  = $o.fbx.'2k'
Write-Output "Keys: $($l.PSObject.Properties.Name -join ', ')"
Write-Output "size: $($l.size)"
Write-Output "url:  $($l.url)"
Write-Output "md5:  $($l.md5)"
# Struktur von /info/ mitpruefen
$i = Invoke-RestMethod -Uri 'https://api.polyhaven.com/info/fern_02' -UserAgent $ua
Write-Output "INFO Keys: $($i.PSObject.Properties.Name -join ', ')"
if ($i.PSObject.Properties.Name -contains 'poly_count') { Write-Output "poly_count: $($i.poly_count)" }
