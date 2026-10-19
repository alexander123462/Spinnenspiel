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

- Spinnenspiel.Tests.DayNightTests.J5_TagAndNacht_LightAndInsectsFollowCycle — rot auch ohne T6 (Overseer-Lauf t6base: "Day Mittel > 200" 260; frueher "Day insects still out at night" 3>2). Eigener Backlog-Punkt noetig.
- Spinnenspiel.Tests.UrwaldTests.Urwald_GeneratesWorldFromSeed — G6 spinne SharpFrac 0.059<0.10, rot auch ohne T8 (Overseer-Lauf t8basefull); Verdacht T7 Morgennebel (dichterer Nebel). Eigener Backlog-Punkt noetig.
- Spinnenspiel.Tests.UrwaldTests.Urwald_Nahkampfscharf — Luma-StDev 28.8<30, rot auch ohne T8 (t8basefull); Verdacht T7 Nebel.
- Spinnenspiel.Tests.UrwaldTests.E1_WaterProofImageIsSharp — SharpFrac 1.3%<10%, rot auch ohne T8 (t8basefull); Verdacht T7 Nebel.
