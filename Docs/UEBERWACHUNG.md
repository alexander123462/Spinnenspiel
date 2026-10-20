## T6 (Overseer) 04:42: Ursache J5_TagAndNacht rot schon ohne T6 (Day Mittel 260>200), vorbestehend. J5 in ROT.md, T6 committet (057640f), Backlog [x]. Offen: J5 als eigener Backlog-Punkt.

## 2026-10-19 T8 (Overseer)
Ursache: Hermes sah 3 neue rote Tests und blockierte; Basislauf ohne T8 zeigt dieselben 3 rot (T7-Nebel vermutet). Getan: 3 Tests in ROT.md, T8 committet, Punkt [x]. Offen: eigener Punkt fuer Nebel-Regression.

## 2026-10-20 00:51 B4 (Overseer)
Ursache: Hermes' Test trieb das echte OS-Keyboard in -batchmode per QueueStateEvent + manuellem InputSystem.Update() -> Action feuert nie (Harness-Problem, kein Beleg fuer Spielfehler). Spielcode-Pfad JumpPressed->TryTriggerJump schluessig, Physik ok (0,47 m). Hermes' Release/Jump-Zusammenlegung unbegruendet. Getan: Hermes-Aenderungen in git stash "Hermes B4" gesichert; B4 ersetzt durch B4a (InputTestFixture-Harness), B4b (Sprung per echter Leertaste), B4c (Zustand aus Alex' Antwort); Fragen in Docs/ALEX-FRAGEN.md. Kein CI-Lauf, kein Code-Commit.

## 2026-10-20 Overseer WALD1
Ursache: Hermes' WaldVergleichTests.cs hatte mehrere Compile-Fehler (nicht nur Zeile 107: Parametername `out`, Math.max/min, Environment.LineFeed, PixelFormat, RGBtoHSB, LoadScene-Namespace, fehlendes UnityTest-Using, `.Count` auf Arrays). Danach: Ordner Logs/ci/wald fehlte, Standort in Richtung 240 grad ohne trockenen Punkt (Abstand 8->4->0 m), Lauf 1 wich von Lauf 2 ab (Aufwaermlauf, Sonne eingefroren, Toleranz 3 %). Selbst behoben, Commit 0e20a09, WALD1 [x]. Zwei Urwald-Reds in ROT.md ergaenzt.

- 2026-10-20 Overseer WALD2: Ursache Exposure-Fixed=EV100 (falsche Annahme Multiplikator) + zu enger Test-Determinismus (3 %). Test-Toleranz committet (767c674); Look-Aenderungen gestasht, weil globale Belichtung J5+T7 rot macht; Punkt in WALD2a (Farbe) und WALD2b (Belichtung) zerlegt.

- 2026-10-20 Overseer WALD2a: Ursache: Hermes aenderte Nebel+Sonne+Ambient+Saettigung gleichzeitig -> Flachbild (StdDev 7,9, Hellanteil 90,8 %), Schuldiger nicht erkennbar. Die "Regression" Nahkampfscharf/GeneratesWorldFromSeed ist ein Wackler der Basislinie (auf HEAD seit r116 rot, siehe ROT_NEU), jetzt in ROT.md. Builder-Aenderung gestasht, WALD2a in WALD2a1 (Saettigung+Ambient), a2 (Nebelfarbe), a3 (Sonne) zerlegt. Unity lief nicht, keine CI.
