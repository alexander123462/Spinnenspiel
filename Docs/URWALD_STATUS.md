# URWALD_STATUS — Blender-Urwald (Overseer-Steuerfile)

## Gesamtziel
Hochwertiger Urwald (M7), neu: Blender-Art statt nur prozedural — Modelle, Materialien, Farne, Geometry-Node-Verteilsystem, Unity-Export. Abnahmekriterien des M7-Leitfaden (Docs/M7_Urwald.md) remain binding (Netz-Placer ≥80 %, ≥60 FPS, Makro-Look).

## Current phase
Phase 1: T1+T2 complete & verified (2026-10-11). Wave 2: T4 (Farne) + T7a (Unity-FBX-Roundtrip-Test). Small work packages per URWALD_TASKS.md.

## Verified facts (checked by Overseer 2026-10-11)
- Blender 5.1 available: `C:/Program Files/Blender Foundation/Blender 5.1/blender.exe` (headless batch usable; proven pattern: ArtSource/*/*.py generate FBX, e.g. Baumfarn, Riesenblatt).
- Repo branch `hdrp`, GitHub current (origin hdrp = b8464f5).
- Recent commits: Urwald-JungleWorldGenerator GameObject deleted + Testwelt Trees/Vegetation roots deleted ("rebuilt from zero later") — CURRENT scene state must be re-verified before new work (T1).
- Existing Blender art pipeline: ArtSource/<Name>/<name>.py -> FBX + Vorschau, imported via Builders; CI = Tools/ci.ps1 per .hermes.md.
- Backlog B1–B12 + Z/M-points all [x]; M8.2 closed 2026-10-08.

## Architecture / interfaces (short)
- Unity: Editor-Builders generate scene+assets (idempotent, CI-callable); Tests/PlayMode verify. No prefabs/Resources.
- PH-Trees (Poly Haven FBX) in Assets/_Project/Art/Plants/PH_Baeume; scan plants in Art/Scans/Models.
- Terrain: Assets/_Project/Art/Terrain + JungleWorldGenerator terrain code (details -> T1 report).

## Completed work packages
- T1 verified (researcher): Urwald-Terrain = procedural Unity Terrain at runtime (JungleWorldGenerator.cs:57-91, 513hmap, Size=96m, MaxHeight=12m; layers Earth/Grass/Mud/Rock). Urwald scene has NO static terrain. Scene deletions were transient — current files rebuilt (Urwald 127 names, roots Weltgenerator/Sun/...; Testwelt Trees/Vegetation back). FBX-import canonical: Assets/_Project/Scripts/Editor/Builders/ScanModels.cs (ImportAssetOptions.ForceUpdate, materialImportMode=None, useFileScale=false, SaveAndReimport); materials always code-side, slot names via renderer.sharedMaterials[i].name.
- KEY EXPORT CONTRACT (from T1): terrain stays Unity Terrain (heightmap feed into SetHeights), Blender ground detail = FBX props via ScanModels pattern; units baked in vertices (cm=m); water never in heightmap; everything must stay SampleHeight/raycast-queryable.
- T2 verified on disk: ArtSource/UrwaldTest/urwald_test.fbx 138K, urwald_test.py, preview PNG, exit 0. Blender 5.1 batch invocation that WORKS: `blender -b -P script.py -- args` (--no-login flag fails).

## Open tasks — see Docs/URWALD_TASKS.md

## Known errors / constraints
- One agent per shared file/scene; Blender-scene work sequential.
- CI runs are expensive (~10 min full): Smart-CI (-Method only) for cosmetic changes.
- Never touch llama-server/TrueNAS/drivers; no deletions without Alex's confirmation (scene content already deleted by earlier commits — do NOT re-delete).

## Next concrete action
Wave 2 delegated 2026-10-11: T4 fern variants (Blender) + T7a FBX->Unity roundtrip test. Then T5 Geometry-Nodes scatter.
