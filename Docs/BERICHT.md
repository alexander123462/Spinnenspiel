
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
