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
5. Input-Actions generieren: Menü `Spinnenspiel/Build/Input`
   (`-executeMethod Spinnenspiel.EditorTools.CreateInputActions.Build`;
   produce `SpiderControls.inputactions` + Wrapperclass).
6. Urwald-Szene bauen: Menü `Spinnenspiel/Build/Urwald`
   (alternativ `-executeMethod Spinnenspiel.EditorTools.UrwaldSceneBuilder.Build`).
7. Szene öffnen: `Assets/_Project/Scenes/Urwald.unity`
8. Play.

## Steuerung
- Move: WASD / Gamepad Left-Stick (Deadzone 0.1)
- Look: Mouse-Delta / Gamepad Right-Stick
- Sprint: Left-Shift / Gamepad Left-Stick-Press
- Zoom: Mouse-Scroll-Rad
- Silk (Faden): F / RMB / Gamepad Right-Shoulder
- Release: Space / Gamepad Button-South (A/Cross)
- Pause: Esc (GameMenu) — Sensitivity in Options (PlayerPrefs)
- F1: Help-Overlay on/off (auto-hide after 15 s)

## Build
Windows-Player-Build (Scene Urwald → `Builds/Windows/Spinnenspiel.exe`):
```
powershell -ExecutionPolicy Bypass -File Tools/ci.ps1 -Name build -Method Spinnenspiel.EditorTools.BuildWindowsTool.BuildWindows
```
- Output-Verzeichnis `Builds/` not in git (gitignore).
- Unity muss closed sein während Build.

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
