# URWALD_TASKS — Taskplan (IDs, Status, Deps)

| ID | Task | Deps | Status | Owner |
|----|------|------|--------|-------|
| T1 | (see URWALD_STATUS: report done) | — | DONE 2026-10-11 | researcher |
| T2 | (FBX verified 138K on disk) | — | DONE 2026-10-11 | coder+tester |
| T3 | Bodenmaterial (HDRP, Spinnengröße-scharf) for the test area | T1,T2 | open | — |
| T4 | Farne-Gruppe: real fern, ≥5 variants, FBX + Vorschau into ArtSource/UrwaldTest/fern/ | T2 | open→running | coder |
| T5 | Geometry-Nodes distribution of ferns over terrain patch -> scatter export | T4 | open | — |
| T6 | Tree trunks + roots ergänzung | T4 | open | — |
| T7a | Roundtrip-Test: urwald_test.fbx imported via ScanModels importer pattern, mesh/scale verified in Editor log | T1,T2 | open→running | coder+tester |
| T8 | Import/Kollision/Performance check in Unity (Tests) | T7 | open | tester |

Rules: parallel only without deps AND no shared files; Blender-scene tasks sequential; one agent per file. Each task delivers: Ergebnis, Files, Tests, Problems, Next steps (short).
