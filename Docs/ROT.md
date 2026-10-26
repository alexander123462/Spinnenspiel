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
- Spinnenspiel.Tests.UrwaldTests.Performance_Z9CostBreakdownPerSystem — Base frame time 17.5 ms > 16.7 ms nur, wenn die lokale KI (Strata/Hermes) die GPU mitbenutzt (Overseer 2026-10-24, FARBE1b); ohne laufende KI gruen.
- Spinnenspiel.Tests.B4cRealStateTests.B4c_OnRope_SpaceLetsGoAndLands — lastabhaengiger Wackler (rot im vollen Lauf r103, einzeln gruen in ov6a; Overseer 2026-10-26, MODELL6); Seil/Space, kein Bezug zu Vegetation.
