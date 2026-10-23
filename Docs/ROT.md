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

- Spinnenspiel.Tests.UrwaldTests.E1_WaterProofImageIsSharp — SharpFrac 1.3%<10%, rot auch ohne T8 (t8basefull); Verdacht T7 Nebel.
- Spinnenspiel.Tests.DayNightTests.J5_TagAndNacht_LightAndInsectsFollowCycle — flackert zwischen 'Day Mittel 260 > 200' und 'Night Mittel 5 < 10'; ohne Klee-Aenderungen identisch rot (Overseer 2026-10-23, Auto-Exposure/Look). Aus ROT_NEU.md uebernommen (KLEE1b).
- Spinnenspiel.Tests.SpiderLegTests.Legs_GaitMeasurement_WalkAndSprint — flaky, Sprint Leg_R1 vorneZmin 0.070 < 0.07 (Grenzwert); einzeln gruen (KLEE1b, 2026-10-24).
