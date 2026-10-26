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
