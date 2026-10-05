# Spinnenspiel

Ein Unity-6-Spiel: eine Vogelspinne mit IK-Beinen, die in einem HDRP-Urwald
klettert und Netze spinnt.

## Voraussetzungen
- Unity Hub + Unity 6000.3.24f1
- Git (oder GitHub Desktop)
- PowerShell
- Blender 5.1 nur für `ArtSource/`

## Einrichtung
1. Repo klonen.
2. Auf Branch `hdrp` umstellen: `git checkout hdrp`
3. Scans laden (Poly Haven, außerhalb von `Assets/`):
   `powershell -ExecutionPolicy Bypass -File Tools/polyhaven_download.ps1`
4. Projekt in Unity Hub öffnen (Editor 6000.3.24f1).
5. Urwald-Szene bauen: Menü `Spinnenspiel/Build/Urwald`
   (alternativ `-executeMethod Spinnenspiel.EditorTools.UrwaldSceneBuilder.Build`).
6. Szene öffnen: `Assets/_Project/Scenes/Urwald.unity`
7. Play.

## Tests
```
powershell -ExecutionPolicy Bypass -File Tools/ci.ps1 -Name <kurz> -Tests
```
- Ergebnis: `Logs/ci/<name>-summary.txt`
- Unity muss dabei geschlossen sein.
- Steht Unity woanders, `UNITY_EXE` setzen, z. B.:
  `$env:UNITY_EXE = "C:\...\Editor\Unity.exe"` (Pfad mit Editor-Version anpassen).

## Arbeitsregeln
- Eigener Branch pro Änderung, Pull Request nach `hdrp`.
- `Urwald.unity` und `Assets/_Project/Art/Generated/` nicht von Hand ändern (Builder).
- Materialien über `Spinnenspiel.Rendering.PipelineMaterials`.
- Keine Collider an Insekten, Netzen, Fäden und Bodendeckern.
- Kommentare auf Deutsch.

## Projektstruktur
- `Assets/_Project/Scripts/Runtime/` – Spielloogik (Beine, Klettern, Netze, Insekten, Rendering)
- `Assets/_Project/Scripts/Editor/` – Builder, die Szenen aufbauen
- `Assets/_Project/Scenes/` – fertige Szenen (z. B. `Urwald.unity`)
- `Assets/_Project/Art/` – generierte Assets (Scans, Pflanzen)
- `AssetSource/polyhaven/` – Poly Haven Quellen (außerhalb `Assets/`, nicht in git)
- `Tools/` – CI- und Download-Skripte
- `Logs/ci/` – CI-Ergebnisse
- `ArtSource/` – Blender-Quellen für prozedurale Assets
