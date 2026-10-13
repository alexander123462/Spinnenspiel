
2026-10-06 | B1 Beine | BLOCKED: Unity offen (Hub installing Editor 6000.3.24f1, unity.exe PID 37460 lauft) -> CI not run, nothing committed | no hash

2026-10-06 | B1 Beine | GRUEN: CS0136 fixed, FBX imported, Legs_UseBlenderMeshes passed (size.y 2.0 all 24), full CI 81 passed / 0 failed | cb50b7a

2026-10-06 | B2 Koerper | GRUEN: Body_UsesScanMeshesAndDoubleSided passed, full CI 82 passed / 0 failed | 424da30

2026-10-06 21:00 | B3.2 JungleAssetLibrary PH-Tree-Fields | grün | 3cead3b
2026-10-06 22:35 | B3.3 PH-Baumes in UrwaldSceneBuilder | gruuen (CI b3full 83/0) | 8584a5c

2026-10-06 | B3.4 CreateTree PH-Trees (LOD0/1, MeshCollider LOD1, SurfaceTag Wood) | gruen (Build Exit 0) | f0d87d9

2026-10-07 00:30 | B3.5 Trees_HaveCollidersAndBlockSpider | blockiert (Unity offen, Geschwark-Session b35f/b35h lauft; TreesTests.cs untracked) | -

2026-10-07 01:25 | B3.5 Trees_HaveCollidersAndBlockSpider | blockiert (Geschwark-Session owns TreesTests.cs; CI b3test3 red Tree1 hits=0; baseDrop-Patch left uncommitted) | -

2026-10-07 02:00 | B3.5/B3.6 | blockiert: Unity.exe (PID 11768) offen, Geschwark-Session owns TreesTests.cs; kein CI, kein Commit

2026-10-07 03:07 | B3.6 Full CI + commit | blockiert (Unity.exe open, Geschwark-Session owns repo; ci b3full2 Exit 1 lock) | -

2026-10-07 | B3.6 full CI + close-out | gruen (b36: Build Exit 0, Tests Exit 0, fehlgeschlagen 0) | d4ff92f

2026-10-07 | B4 Lianen (per-Tree, capsule-chain, LianenTests) | gruen (b4full8 Build Exit 0, Tests Exit 0, fehlgeschlagen 0) | f482032

2026-10-07 | B5 PlantBlockTests (rays vs colliders, Spider stops vs tree) | gruen (b5full6 Build Exit 0, Tests Exit 0, fehlgeschlagen 0) | 84e3e13

2026-10-07 | B6 Hunger-Test + Spielschleife check | gruen (b6full1 Build Exit 0, Tests Exit 0, fehlgeschlagen 0) | 0f2d2ad

2026-10-07 | B7 ControlsHelp auto-hide/Toggle-Test | gruen (b7full5 Build Exit 0, Tests Exit 0, fehlgeschlagen 0) | dc25b59

2026-10-07 | B8 GameMenu + Test | gruen (b8full6 Build Exit 0, Tests Exit 0, fehlgeschlagen 0) | e5012fb

2026-10-07 | Z1 bald-star Abdomen (pole_skip 0.15) + FBX re-export | gruen (z1full Build Exit 0, Tests Exit 0, fehlgeschlagen 0, 91/92) | 8d48556

2026-10-07 | Z2 own-code Warnings in CI log = 0 (grep) | gruen (no code change needed) | 21695df

2026-10-07 | Z6 Netzbau-Komfort | blocked (Geschwark Z5 GameSaveData.cs "var out" breaks full compile, CI r4q rot; own Z6 code written uncommitted) | -

2026-10-07 Z7 Beute (Motte): BLOCKED — CI r5d rot due to Geschwark uncommitted SaveLoadManager/GameSaveData; own code in Logs/z7/, no commit.

2026-10-07 | Z5 Speichern | GREEN | Commit 352210c (SaveGame JSON in persistentDataPath, GameMenu Q-Quit/Starting hooks, SaveGameTests roundtrip; full CI r102 Build Exit 0 / Tests Exit 0 / fehlgeschlagen 0, 94/95 Passed)

2026-10-07 | Z6 Netzbau-Komfort | GREEN | Commit 5ea1204 (SpiderWebBuilder.CancelBuild + cancel-flag in BuildRoutine/WalkTo; SpiderWebPlacer Esc->CancelBuild + red ShowBlockedPreview/PreviewBlocked; GameMenu EscIsBuildAbort skips Pause; NetbuildComfortTests CancelBuild_StopsRunningBuild + RedPreview_WhenSpotGoesBad green; full CI r103 Build Exit 0 / Tests Exit 0 / fehlgeschlagen 0, 96/97 Passed +1 Skipped)

2026-10-08 | Z9 Leistung 2: Cost-Breakdown-Test + Insects-Optimization (throttled casts) | gruen (z9opt1 Build Exit 0, Tests Exit 0, fehlgeschlagen 0, 98/99 +1 Skipped; base 6.57 ms, Insects ~5.88 ms)

2026-10-08 | M8.1 Windows-Build neustand | DONE: CI m8build1 BuildWindowsTool.BuildWindows Exit 0, Spinnenspiel_Data rebuilt 12:49 (false-positive BLOCKED resolved: unity.exe PID was licensing client, batch CI unaffected) | m8build1

2026-10-08 | M8.2 Abschluss | FAERTER: B1-B12 + Z1-Z10 + M7.x + M8.1 alle [x]; Windows-exe exists (Builds/ gitignored, rebuilt m8build1); full CI green m7finalchk 101/0; game state finished | ec836b3

2026-10-12 | HANDOUT | Status: Commit 108194f pushed hdrp. DONE: T7a FBX ok (29 Meshes, 8x4.361x8 m); Startscreen removed (GameMenu _state=1, WorldTests adapted, full suite green nomenu-full); ScreenshotTool HDR ARGBHalf + ACES/Auto-Exposure, debug removed. Testwelt rebuilt (twrebuild1), Volume -> Testwelt_VolumeProfile.asset GUID 826061331138218489e89e320b230f32 (old M1_VolumeProfile was empty = root cause). OPEN: spinne_nah/spielkamera still near-uniform fog frames (std 5-7, raw HDR white min ~0.75-0.9 at camera, moving views did not help); parcours/uebersicht lack course structures + uebersicht blurred; next: check Fog/VolumetricFog values and whether obstacles exist in generated Testwelt.
2026-10-12 | U1 BLOCKED | own fix compiled+green (r101c Urwald 7/7 incl. new Urwald_Nahkampfscharf luma-stdev>30 test; VolumetricFog off in WorldLightingBuilder), but full CI r101b fehlgeschlagen 4 = 4 PRE-EXISTING reds already in nomenu-full (2026-10-11): Climb_TreeFullHeight, Climb_TreeInWorld, RedPreview_WhenSpotGoesBad, Spider_StopsAgainstTree. Gate fehlgeschlagen 0 unreachable; own changes reverted. Next: kill those 4 reds first.

2026-10-12 | U2 BLOCKED | own fix green in r102u2 (Methods Exit 0, new Course_StructuresExistNearSpawn Passed, reshoots written) but full CI fehlgeschlagen 4 = same 4 pre-existing reds as r101b/nomenu-full, deterministic (r102u2b filtered rerun identical). Gate fehlgeschlagen 0 unreachable; own changes reverted. Next: kill the 4 reds first.

2026-10-12 | U4 BLOCKED | T5 Scatter not attempted: full CI u4pre2 (TestWorldBuilder+UrwaldSceneBuilder both Exit 0, rebuilt scenes) still fehlgeschlagen 4 = same deterministic pre-existing reds (Climb x2, RedPreview_WhenSpotGoesBad, Spider_StopsAgainstTree); gate fehlgeschlagen 0 unreachable; trunk XZ unchanged vs green 2b3b244, only Y +0.30 m / terrain churn -> rebuild is not the cause. No own code changes made this loop.
2026-10-12 | R3 RedPreview_WhenSpotGoesBad | GRUEN ohne Codechange: r3a/r3b filtered Passed + r3full full run Passed (only red left = R4 Spider_StopsAgainstTree). Root cause = climb regression fixed in 662c80c; trunk pair 1.5-4 m intact. Docs-only commit.
2026-10-12 | G1 Himmel/Beleuchtung | BLOCKED: own fix correct (legacy Skybox/Panoramic+Procedural path removed in WorldLightingBuilder.cs, RenderSettings.skybox=null; MAT_Sky guid still referenced in Testwelt.unity RenderSettings -> NOT deleted) but full CI r102 fehlgeschlagen 1 = Spider_StopsAgainstTree (=R4, separate open point, pre-existing deterministic red); AND Schritt-4 Mittel 27.5 <40 (NOK), Magenta 0 (OK). Gate fehlgeschlagen 0 + both criteria unreachable; own change reverted per rule. Next: fix R4, then redo G1 with exposure/ambient tuning.
2026-10-12 | R4 Spider_StopsAgainstTree | GRUEN commit 9cb699d: jacaranda-FBX-origin ~5,7 m XZ off trunk -> test walked to empty pivot, phantom-axis minNear 0.145. Test-only fix: pick needs collider footprint centred on pivot (<=1.5 m). Full CI r103 Build Exit 0 + 107/107 green (minNear 0.843).

2026-10-12 | G2 Himmel/Helligkeit | BLOCKED: own fix compiled+ran (WorldLightingBuilder skybox=null + env.skyAmbientMode=Dynamic + UrwaldTests proofshot-cam ground+2m/look-10deg/near0.3 + new SkyFractionUpperHalf>=0.15 assert). Full CI r102 Build Exit 0, Tests 106/107, fehlgeschlagen 1 = OWN Urwald_GeneratesWorldFromSeed skyFrac=0.0f (closed-canopy Urwald: all upper-half rays hit foliage/tree colliders at ground level -> Step-0 cam setup wrong for this forest). 3 Versuche reached; own files reverted per rule. No commit.
R5 Spider_StopsAgainstTree: BLOCKED 2026-10-12 (r5diag3): Test-Target=woodSub-Axis + Planting-Cap mc.bounds.center grounded the jacaranda collider (-0.723 -> -0.091 OK) but spider still walks through trunk (minNear 0.032 < 0.25, maxToward 6.45 > trunkDist 5.80); cause movement/geometry; own changes reverted, marked [!].

- 2026-10-13 G3 r102f: Himmel ueber Crown PROVEN (skyFrac 1.00, urwald_himmel.png Mittel 140.7/Max 210/Magenta 0); Waldloor Max>150 NOT yet verified -> [!] left in tree, not committed; foreign TreeGroundSnapper.cs removed (V1 compile-breaker).

- R6 Spinne laeuft durch Baumstaemme: FIX convex TrunkCapsule-Blocker per CreateTree (Schatten-Node) + Spider_StopsAgainstTree now targets trunk XZ-axis via TrunkCapsule, removed false pivot-skip. CI r103 107/107 green, Spider_StopsAgainstTree Passed. Commit 9b5562b.

- 2026-10-13 G3 r108: Himmel re-applied aus Arbeitsbaum (HEAD still old Panoramic). Build Exit 0, Tests 107/107 green. SkyFraction assert pass. BUT Bildkriterium NOK in eigener Reconstruction: urwald_himmel.png Mittel 54.2 (-> 60..200 -> NOK), Max 103 (-> >200 -> NOK), Magenta 0 (-> =0 -> OK); urwald_spinne.png Mittel 18.6 (-> >=25 -> NOK). Step-5 Hebel (Exposure limitMin 0..2, Sun Lux) exhausted without reaching gate; PBS-Sky-Himmel rendere duuster as r102f. NOT committed. Changed files in tree: WorldLightingBuilder.cs, UrwaldTests.cs.

- G4 Beweisbild urwald_spinne.png: BLOCKED nach 3 Versuche (SharpFraction 0.10-Threshold). Restpose+PhysSync+Standoff~2m+ZweirerFrame raised Metric 0.045->0.079, still soft; no HDRP Volume/DoF API in PlayMode asmdef. Test red NOT in ROT.md -> Regression, NOT committed; UrwaldTests.cs left in tree.
- U1b Nahkampfscharfe: DONE commit 2565cc4. VolumetricFog off in WorldLightingBuilder.cs + Urwald_Nahkampfscharf luma-StDev test (>=30). CI r102b Build Exit 0, Tests 108/108 green, fehlgeschlagen 0. Measured [U1-Nahcam] mean=0,2 stdev=31,1 -> 31,1 >=30 OK. U1 (Zeile 64) und U1b auf [x].
- U2b Screenshots parcours/uebersicht: DONE commit 4284022. ScreenshotTool cams fixed (parcours s+(-0.30,0.30,-0.50) along course z+2.2, uebersicht 9 m in fog range, MSAA=4); WorldTests Course_StructuresExistNearSpawn green (Stone 12/Branch 2/Log+Boulder+Stump je 1). Full CI r104b Build Exit 0, Tests 109/109, fehlgeschlagen 0. Reshoots fresh 07:40; parcours sharp low-angle OK (vision), uebersicht legible with scene-DoF blur (not a smear/whiteout). U2+U2b [x].

- 2026-10-13 U4b/U4.1: scatter.py (deterministic Poisson-Disc, seed 17) executed in Blender -> fern_scatter.fbx + fern_scatter.json, 245 ferns over 8x8 m patch, min_d 0.42. Full CI r106 Build Exit 0 + Tests 109/109 green. Commit d747a8e.
