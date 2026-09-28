$ErrorActionPreference = 'Stop'
$ua = 'Spinnenspiel/1.0 (Alex, lokal)'
function Show([string]$id) {
  $o = Invoke-RestMethod -Uri ("https://api.polyhaven.com/files/" + $id) -UserAgent $ua
  Write-Output ("=== " + $id + " ===")
  foreach ($p in $o.PSObject.Properties) {
    $v = $p.Value
    if ($v -is [System.Collections.IDictionary]) {
      $inner = foreach ($k in $v.PSObject.Properties.Name) {
        $vv = $v[$k]
        if ($vv -is [System.Collections.IDictionary]) { $k + " {" + (($vv.PSObject.Properties.Name) -join ",") + "}" }
        else { $k + ": " + $vv }
      }
      Write-Output ($p.Name + " { " + ($inner -join ", ") + " }")
    }
    else { Write-Output ($p.Name + ": " + $v) }
  }
}
Show 'fern_02'
Show 'forest_ground_04'
Show 'kloofendal_48d_partly_cloudy_puresky'
