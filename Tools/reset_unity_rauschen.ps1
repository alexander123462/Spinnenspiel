# reset_unity_rauschen.ps1 - K2a: Unity-Rauschen automatisch zuruecksetzen.
# Je geaenderten Datei under Assets/_Project/Art/Generated bzw. Assets/_Project/Scenes
# den Diff (git diff -U0) pruefen. Only Rausch-Zeilen -> git checkout back.
# Rausch = Zeile starts with '--- !u!' (fileID/AssetVersion restamp) ODER reine Umordnung
# (removed-content == added-content after removing those lines).
# Output: number reset / number left.

param(
    [string[]]$Roots = @("Assets/_Project/Art/Generated", "Assets/_Project/Scenes")
)

$repo = "C:/Users/vorau/dev/Spinnenspiel"
Set-Location $repo

# Changed tracked files under the roots (modified, not new).
$changed = & git --no-optional-locks ls-files -m -- $Roots

$resetCount = 0
$leftCount = 0
$noiseHeader = '^--- !u!\d+'

foreach ($f in $changed) {
    if (-not $f) { continue }
    $diff = & git --no-optional-locks diff -U0 -- $f
    $removed = @()
    $added = @()
    foreach ($line in $diff) {
        if ($line -match '^(diff |index |\+\+\+|---|@@)') { continue }
        if ($line -match '^-') { $removed += ,($line.Substring(1)) }
        elseif ($line -match '^\+') { $added += ,($line.Substring(1)) }
    }

    # Content lines = changed lines that are NOT a Unity fileID header.
    $removedReal = @($removed | Where-Object { $_ -notmatch $noiseHeader })
    $addedReal = @($added | Where-Object { $_ -notmatch $noiseHeader })

    # 1) Pure fileID restamp: only '--- !u!' lines changed.
    $pureRestamp = ($removedReal.Count -eq 0) -and ($addedReal.Count -eq 0)

    # 2) Pure reordering: content unchanged as multiset, only headers moved.
    $pureReorder = ($removedReal.Count -gt 0) -and
        (($removedReal | Sort-Object) -join "`n") -eq (($addedReal | Sort-Object) -join "`n")

    if ($pureRestamp -or $pureReorder) {
        & git --no-optional-locks checkout -- $f
        Write-Output "reset: $f"
        $resetCount += 1
    } else {
        Write-Output "left: $f"
        $leftCount += 1
    }
}

Write-Output "reset=$resetCount left=$leftCount"
