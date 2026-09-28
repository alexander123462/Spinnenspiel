# Spinnenspiel – Architektur & Bedienung

Unity 6000.3.24f1 · URP 17.3 · Input System 1.20

## Spielen

1. Projekt `C:\Users\vorau\dev\Spinnenspiel` im Unity Hub öffnen.
2. Szene `Assets/_Project/Scenes/Testwelt.unity` öffnen (die große Welt, Milestone 3–6).
   `M1_Testgelaende.unity` ist die einfache Testszene aus Milestone 1/2.
3. Play drücken.

| Taste | Funktion |
|---|---|
| W A S D | Laufen (relativ zur Kamera) |
| Maus | Kamera drehen |
| Mausrad | Zoom |
| Shift (links) | Sprint |
| Esc / Linksklick | Maus freigeben / wieder fangen |

## Welt neu bauen

Die Welt ist vollständig prozedural und deterministisch. Menü **Spinnenspiel → Build → Testwelt (Milestone 3-6)**
baut Gelände, Hindernisse, Vegetation, Wasser, Licht, Audio, Spinne und Kamera neu.
Die Kenngrößen der Welt (Bachverlauf, Teich, Wald/Wiese, Höhenprofil) stehen zentral in `WorldLayout.cs`.

Automatischer Test/Build: `Tools\ci.ps1` (z. B. `-Name x -Method Spinnenspiel.EditorTools.TestWorldBuilder.Build -Tests`).

## Laufzeit-Komponenten (Assets/_Project/Scripts/Runtime)

| Bereich | Komponente | Aufgabe |
|---|---|---|
| Steuerung | `SpiderInput` | Input-System-Aktionen (Move, Look, Sprint, Zoom) |
| Bewegung | `SpiderMovement` | CharacterController, kamerarelativ, Beschleunigung/Abbremsen, weiche Drehung, Schwerkraft, Oberflächen-Tempo |
| Koordination | `SpiderController` | Referenzen, keine eigene Logik |
| Beine | `SpiderLeg` | je Bein: Ruheposition, Bodenerkennung (Raycast + Hindernis-/Reichweitenprüfung), Schrittbogen, 2-Knochen-IK + Tarsus |
| Beine | `LegIK` | analytische Zwei-Knochen-IK (Kosinussatz, Pol-Vektor) |
| Gang | `SpiderGaitController`, `GaitProfile` | Tetrapoden-Gang (L1 R2 L3 R4 / R1 L2 R3 L4), Schrittdauer & -weite aus echter Geschwindigkeit, Profile für Krabbeln/Laufen |
| Körper | `SpiderBodyController` | Körperhöhe und -neigung aus den Fußpositionen (vorne/hinten, links/rechts), Atmung |
| Oberfläche | `SurfaceType`, `SurfaceTag`, `TerrainSurfaceMap`, `SurfaceQuery`, `SpiderSurfaceDetector` | GROUND/GRASS/ROCK/WOOD/WATER/MUD je Fuß und Spinne, Tempo-Faktor, Wasser-Erkennung |
| Kamera | `SpiderCamera` | Orbit, SmoothDamp, Kollision (SphereCast), Zoom, Sprint-FOV, Bodenschutz |
| Welt | `WorldLayout` | deterministische Weltbeschreibung (Höhen, Bach, Teich, Biome) |
| Vegetation | `VegetationSystem` | Chunk-Streaming, GPU-Instancing (RenderMeshInstanced), Frustum-/Distanz-Culling, prozedurale Verteilung |
| Wind | `WindController` | globaler Wind mit Böen → Shader `_SpinnenWind` |
| Wasser | `WaterBody` | Wasserflächen (Kollider mit SurfaceType.Water) |
| Audio | `ProceduralAudio`, `SpiderFootstepAudio`, `AmbientAudio` | Klänge aus Rauschen/Filtern: Tritte je Oberfläche, Wind, Bach, Teich, Vögel |
| Effekte | `SpiderFootEffects` | Wellenringe auf Wasser, Staub beim Sprinten |

## Shader (Assets/_Project/Shaders)

- `Spinnenspiel/Foliage` – Instancing, Wind im Vertex-Shader, Durchscheinen, Alpha-Clip, Schatten
- `Spinnenspiel/Water` – transparent, zwei Normal-Maps, Fresnel, Sonnenglanz, Fließrichtung

## Editor-Builder (Assets/_Project/Scripts/Editor)

`TestWorldBuilder` ruft auf: `WorldTerrainBuilder` → `WorldLightingBuilder` → `EnvironmentBuilder` →
`VegetationBuilder` → `WaterBuilder` → `SpiderBuilder` (+ `SpiderLegBuilder`) → `EffectsBuilder`.
Texturen und Meshes erzeugen `TextureFactory`, `MeshFactory`, `VegetationMeshFactory` und legen sie unter
`Assets/_Project/Art/Generated` ab (Platzhalter-Assets, austauschbar ohne Code-Änderung).

## Tests (Assets/_Project/Tests/PlayMode)

- `SpiderMovementTests` – Landen, Laufen, Abbremsen
- `SpiderLegTests` – Füße am Boden, max. 5 Beine gleichzeitig in der Luft, alle Beine machen Schritte, Körperhöhe
- `WorldTests` – Hindernisparcours (Fuß auf Stein/Ast, kein Fuß im Hindernis), Vegetation gestreamt, Wasser erkannt/bremst/Wellen, Audio

## Erweiterungspunkte

- Neue Gangarten: weitere `GaitProfile` anlegen, `SpiderGaitController.SetProfile`
- Klettern: `SpiderLeg` nutzt bereits die Hoch-Achse des Roots (`restTarget.up`) statt Welt-Oben
- Größere Welt: `VegetationSystem` streamt bereits chunkweise; `WorldLayout` austauschbar gegen Terrain-Kacheln
- Gameplay je Oberfläche: `SpiderSurfaceDetector.SurfaceChanged`, `SpiderLeg.Planted`
