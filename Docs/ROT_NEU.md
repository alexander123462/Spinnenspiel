# Von Hermes gemeldete rote Tests (unbestaetigt). Werden vor neuen Punkten repariert. Nur Claude nimmt sie in die Basislinie Docs/ROT.md auf.

- Spinnenspiel.Tests.UrwaldTests.Urwald_Nahkampfscharf — Nahaufnahme SharpFrac 8,9%<10% im vollen CI r123 (gruen 52,6% in isoliert class-run r122-f8; DoF/TAA-proofshot-Pfad des Tests nachweislich falsch, Try-1..3 gemehren, im volle CI noch smearing).
- Spinnenspiel.Tests.UrwaldTests.Urwald_GeneratesWorldFromSeed — G6 spinne SharpFrac 9,9%<10%, rot seit r116 (gleiche Ursache wie E1: Nebel/Proofshot).

- Spinnenspiel.Tests.UrwaldTests.Urwald_GeneratesWorldFromSeed — G6 spinne SharpFrac 3,4 %..9,9 % <10 %, rot seit r116 auf HEAD, gleiche Ursache wie E1 (Overseer 20.10.).
- Spinnenspiel.Tests.UrwaldTests.Urwald_Nahkampfscharf — SharpFrac wackelt je Lauf (3,9 %..8,9 % voll, gruen isoliert), rot seit r116 auf HEAD ohne Look-Aenderung (Overseer 20.10.).
- Spinnenspiel.Tests.UrwaldTests.Urwald_GeneratesWorldFromSeed — G6 spinne SharpFrac 3,4 %..9,9 % <10 %, rot seit r116 auf HEAD (gleiche Ursache wie E1).
- Spinnenspiel.Tests.UrwaldTests.Urwald_Nahkampfscharf — SharpFrac wackelt je Lauf (3,7 %..8,9 % voll, gruen isoliert), rot seit r116 auf HEAD (Nebel).
- Spinnenspiel.Tests.DayNightTests.J5_TagAndNacht_LightAndInsectsFollowCycle — flackert zwischen 'Day Mittel 260 > 200 (Blendout)' und 'Night Mittel 5 < 10'; ohne Klee-Aenderungen identisch rot (Overseer-Check 2026-10-23, Auto-Exposure/Look); gleiche Wurzel wie die einfarbig grauen Offscreen-Beweisbilder (Mittel=164).
- Spinnenspiel.Tests.DayNightTests.J5_TagAndNacht_LightAndInsectsFollowCycle — flackert zwischen 'Day Mittel 260 > 200' und 'Night Mittel 5 < 10'; ohne Klee-Aenderungen identisch rot (Overseer 2026-10-23, Auto-Exposure/Look). Aus ROT_NEU.md uebernommen (KLEE1b).
- Spinnenspiel.Tests.DayNightTests.J5_TagAndNacht_LightAndInsectsFollowCycle — Vorab-Rot (HEAD): Offscreen-Rahmen weiss/Nacht nicht dunkel; wird in FARBE1a repariert (Overseer 2026-10-24).
- Spinnenspiel.Tests.UrwaldTests.E1_WaterProofImageIsSharp — SharpFrac knapp unter 0.10 bzw. 0 (lastabhaengig/Offscreen-Bild; Overseer 2026-10-28, MODELL17: rot auch ohne Bezug zu Steinen, Backlog-Hinweis 'E1 lastabhaengig').
- Spinnenspiel.Tests.MorgennebelTests.T7_Morgennebel_FollowsDayProgress — StDev noon 12,27 < dense 54,33 (Diff -42,06): Tagesbild fast schwarz, Bildkette schwarzer Screenshots seit LICHT1c-Look 825c61e. NICHT von STAMM1: r104 mit/ohne stash identisch. Backlog-Punkt ROTE1-NEBEL1.
- Spinnenspiel.Tests.SpiderLegTests.Legs_GaitMeasurement_WalkAndSprint — Sprint Bein Leg_L1 vorneZmin 0.067..0.070 < 0.07, am Grenzwert wackelnd (in .hermes.md als Wackler gelistet). NICHT von STAMM1: r104 mit/ohne stash beide rot. Backlog-Punkt ROTE1-GANG1.
- Spinnenspiel.Tests.UrwaldTests.E1_WaterProofImageIsSharp — Wasser-Beweisbild SharpFrac 9,84 % < 0.10. NICHT von STAMM1: r104 mit/ohne STAMM1 (git stash) identisch rot; schon r102-f9b und r103-base vor jeder STAMM1-Aenderung rot. Backlog-Punkt ROTE1-WASSER1.
- Spinnenspiel.Tests.UrwaldTests.Urwald_GeneratesWorldFromSeed — G6 spinne SharpFrac < 0.10. NICHT von STAMM1: r104 mit/ohne stash identisch; schon r102-f9b, r103-base, r103-head-j5g6 rot. Backlog-Punkt ROTE1-SCHARFE1.
- Spinnenspiel.Tests.UrwaldTests.Urwald_Nahkampfscharf — Nahaufnahme SharpFrac < 0.10. NICHT von STAMM1: r104 mit/ohne stash identisch; schon r102-f9b und r103-base rot. Backlog-Punkt ROTE1-SCHARFE1.
- Spinnenspiel.Tests.WaldVergleichTests.Messung — Standpoint 0 image is flat (std=0): Logs/ci/wald/wald_1..3.png Mittel RGB 0. NICHT von STAMM1: r104 mit/ohne stash identisch. Backlog-Punkt ROTE1-NEBEL1.
