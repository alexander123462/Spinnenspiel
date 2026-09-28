$ErrorActionPreference = 'Stop'
$ua = 'Spinnenspiel/1.0 (Alex, lokal)'
$o  = Invoke-WebRequest -Uri 'https://api.polyhaven.com/files/fern_02' -UserAgent $ua -UseBasicParsing
Write-Output ("RAW LEN: " + $o.Content.Length)
Write-Output ($o.Content.Substring(0, [Math]::Min(3000, $o.Content.Length)))
Write-Output "----- INFO polycount -----"
$i = Invoke-RestMethod -Uri 'https://api.polyhaven.com/info/fern_02' -UserAgent $ua
Write-Output ("polycount: " + $i.polycount + " | type: " + $i.type)
