# Bekannte rote Tests (Basislinie)

Diese Tests sind VOR deiner Aenderung schon rot. Sie sind eigene Backlog-Punkte und
blockieren den Commit eines anderen Punktes NICHT.

Regel: Nach dem CI-Lauf die Namen der fehlgeschlagenen Tests aus
Logs/ci/<name>-tests.log herausziehen. Stehen ALLE davon in dieser Liste und ist der
Test des eigenen Punktes gruen -> das gilt als gruen, committen. Taucht ein Test auf,
der NICHT in dieser Liste steht -> das ist eine echte Regression von dir, nicht
committen. Wird ein Punkt dieser Liste gruen, Zeile hier entfernen und im selben
Commit mitnehmen.

(Baseline currently empty: RedPreview_WhenSpotGoesBad made deterministic and green in R7.)

- Spinnenspiel.Tests.UrwaldTests.Performance_Z9CostBreakdownPerSystem — Base frame time 17.5 ms > 16.7 ms nur, wenn die lokale KI (Strata/Hermes) die GPU mitbenutzt (Overseer 2026-10-24, FARBE1b); ohne laufende KI gruen.
- Spinnenspiel.Tests.UrwaldTests.E1_WaterProofImageIsSharp — Wasser-Beweisbild SharpFrac 9,84 % < 0.10. NICHT von STAMM1: r104 mit/ohne STAMM1 (git stash) identisch rot; schon r102-f9b und r103-base vor jeder STAMM1-Aenderung rot. Backlog-Punkt ROTE1-WASSER1.
- Spinnenspiel.Tests.UrwaldTests.Urwald_GeneratesWorldFromSeed — G6 spinne SharpFrac < 0.10. NICHT von STAMM1: r104 mit/ohne stash identisch; schon r102-f9b, r103-base, r103-head-j5g6 rot. Backlog-Punkt ROTE1-SCHARFE1.
- Spinnenspiel.Tests.UrwaldTests.Urwald_Nahkampfscharf — Nahaufnahme SharpFrac < 0.10. NICHT von STAMM1: r104 mit/ohne stash identisch; schon r102-f9b und r103-base rot. Backlog-Punkt ROTE1-SCHARFE1.
- Spinnenspiel.Tests.MorgennebelTests.T7_Morgennebel_FollowsDayProgress — StDev noon 12,27 < dense 54,33 (Diff -42,06): Tagesbild fast schwarz, Bildkette schwarzer Screenshots seit LICHT1c-Look 825c61e. NICHT von STAMM1: r104 mit/ohne stash identisch. Backlog-Punkt ROTE1-NEBEL1.
- Spinnenspiel.Tests.WaldVergleichTests.Messung — Standpoint 0 image is flat (std=0): Logs/ci/wald/wald_1..3.png Mittel RGB 0. NICHT von STAMM1: r104 mit/ohne stash identisch. Backlog-Punkt ROTE1-NEBEL1.
- Spinnenspiel.Tests.SpiderLegTests.Legs_GaitMeasurement_WalkAndSprint — Sprint Bein Leg_L1 vorneZmin 0.067..0.070 < 0.07, am Grenzwert wackelnd (in .hermes.md als Wackler gelistet). NICHT von STAMM1: r104 mit/ohne stash beide rot. Backlog-Punkt ROTE1-GANG1.
