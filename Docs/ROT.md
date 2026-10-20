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
- Spinnenspiel.Tests.UrwaldTests.Urwald_Nahkampfscharf — SharpFrac wackelt je Lauf (3,9 %..8,9 % voll, gruen isoliert), rot seit r116 auf HEAD ohne Look-Aenderung (Overseer 20.10.).
- Spinnenspiel.Tests.UrwaldTests.Urwald_GeneratesWorldFromSeed — G6 spinne SharpFrac 3,4 %..9,9 % <10 %, rot seit r116 auf HEAD, gleiche Ursache wie E1 (Overseer 20.10.).
