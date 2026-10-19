## T6 (Overseer) 04:42: Ursache J5_TagAndNacht rot schon ohne T6 (Day Mittel 260>200), vorbestehend. J5 in ROT.md, T6 committet (057640f), Backlog [x]. Offen: J5 als eigener Backlog-Punkt.

## 2026-10-19 T8 (Overseer)
Ursache: Hermes sah 3 neue rote Tests und blockierte; Basislauf ohne T8 zeigt dieselben 3 rot (T7-Nebel vermutet). Getan: 3 Tests in ROT.md, T8 committet, Punkt [x]. Offen: eigener Punkt fuer Nebel-Regression.

## 2026-10-20 00:51 B4 (Overseer)
Ursache: Hermes' Test trieb das echte OS-Keyboard in -batchmode per QueueStateEvent + manuellem InputSystem.Update() -> Action feuert nie (Harness-Problem, kein Beleg fuer Spielfehler). Spielcode-Pfad JumpPressed->TryTriggerJump schluessig, Physik ok (0,47 m). Hermes' Release/Jump-Zusammenlegung unbegruendet. Getan: Hermes-Aenderungen in git stash "Hermes B4" gesichert; B4 ersetzt durch B4a (InputTestFixture-Harness), B4b (Sprung per echter Leertaste), B4c (Zustand aus Alex' Antwort); Fragen in Docs/ALEX-FRAGEN.md. Kein CI-Lauf, kein Code-Commit.
