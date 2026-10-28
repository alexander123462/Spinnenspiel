## T6 (Overseer) 04:42: Ursache J5_TagAndNacht rot schon ohne T6 (Day Mittel 260>200), vorbestehend. J5 in ROT.md, T6 committet (057640f), Backlog [x]. Offen: J5 als eigener Backlog-Punkt.

## 2026-10-19 T8 (Overseer)
Ursache: Hermes sah 3 neue rote Tests und blockierte; Basislauf ohne T8 zeigt dieselben 3 rot (T7-Nebel vermutet). Getan: 3 Tests in ROT.md, T8 committet, Punkt [x]. Offen: eigener Punkt fuer Nebel-Regression.

## 2026-10-20 00:51 B4 (Overseer)
Ursache: Hermes' Test trieb das echte OS-Keyboard in -batchmode per QueueStateEvent + manuellem InputSystem.Update() -> Action feuert nie (Harness-Problem, kein Beleg fuer Spielfehler). Spielcode-Pfad JumpPressed->TryTriggerJump schluessig, Physik ok (0,47 m). Hermes' Release/Jump-Zusammenlegung unbegruendet. Getan: Hermes-Aenderungen in git stash "Hermes B4" gesichert; B4 ersetzt durch B4a (InputTestFixture-Harness), B4b (Sprung per echter Leertaste), B4c (Zustand aus Alex' Antwort); Fragen in Docs/ALEX-FRAGEN.md. Kein CI-Lauf, kein Code-Commit.

## 2026-10-20 Overseer WALD1
Ursache: Hermes' WaldVergleichTests.cs hatte mehrere Compile-Fehler (nicht nur Zeile 107: Parametername `out`, Math.max/min, Environment.LineFeed, PixelFormat, RGBtoHSB, LoadScene-Namespace, fehlendes UnityTest-Using, `.Count` auf Arrays). Danach: Ordner Logs/ci/wald fehlte, Standort in Richtung 240 grad ohne trockenen Punkt (Abstand 8->4->0 m), Lauf 1 wich von Lauf 2 ab (Aufwaermlauf, Sonne eingefroren, Toleranz 3 %). Selbst behoben, Commit 0e20a09, WALD1 [x]. Zwei Urwald-Reds in ROT.md ergaenzt.

- 2026-10-20 Overseer WALD2: Ursache Exposure-Fixed=EV100 (falsche Annahme Multiplikator) + zu enger Test-Determinismus (3 %). Test-Toleranz committet (767c674); Look-Aenderungen gestasht, weil globale Belichtung J5+T7 rot macht; Punkt in WALD2a (Farbe) und WALD2b (Belichtung) zerlegt.

- 2026-10-20 Overseer WALD2a: Ursache: Hermes aenderte Nebel+Sonne+Ambient+Saettigung gleichzeitig -> Flachbild (StdDev 7,9, Hellanteil 90,8 %), Schuldiger nicht erkennbar. Die "Regression" Nahkampfscharf/GeneratesWorldFromSeed ist ein Wackler der Basislinie (auf HEAD seit r116 rot, siehe ROT_NEU), jetzt in ROT.md. Builder-Aenderung gestasht, WALD2a in WALD2a1 (Saettigung+Ambient), a2 (Nebelfarbe), a3 (Sonne) zerlegt. Unity lief nicht, keine CI.

- 2026-10-21 Overseer WALD7: Ursache: Hermes hatte die Moos-Implementierung nie kompiliert (Zeitbudget): 4 triviale Compilefehler (colNode.transform.TransformPoint, Mathf.Smoothstep, Math->Mathf, go.transform.localScale). Behoben, Wald7MoosTests 2/2 gruen (356 Shells, 20 Karten, 1 Material, Hoehe 1,5 m). Voller CI 251/258: J1_NetsCatchPrey + Sting_stun flaky (einzeln gruen), E1/G6/Nahkampf = Baseline, G6+Nahkampf jetzt in ROT.md. Committed, WALD7 [x]. Unity lief nicht.

- 2026-10-21 WALD8 [!] (Overseer): Hermes' Ursache (Forward+ ignoriert Directional-Cookie / enableVolumetrics=0) ist unbelegt; die 0-Werte stehen in m_ObsoleteFrameSettings, supportVolumetrics=1. Cookie hatte null Wirkung (StdDev 49,5 = Baseline). Zerlegt in WALD8a (Diagnose: Cookie zur Laufzeit gesetzt/ueberschrieben?), WALD8b (Cookie wirksam), WALD8c (Volumetric-Nebel). Hermes' uncommittete Dateien bleiben im Baum (im Punkt WALD8a benannt). Keine CI (Unity lief nicht, aber Aufwand zu gross), kein Code-Commit.
- 2026-10-21 WALD8a (Overseer): Diagnose war fertig, [!] nur wegen Gate (rote Folgetests). Ursache: rohes Texture2D-Cookie aus TextureFactory.Canopy() macht Bild grau (StdDev 0), weisses Cookie normal. Punkt auf [x], Ursache und Loesungshinweis in WALD8b; kein Code-Commit.

- 2026-10-21 WALD8b [!] (Overseer): Cookie funktioniert (Import-Fix wirkt, Bild nicht mehr grau, Diff mit/weiss 5,6 %). Blockade war ein schlechtes Kriterium (StdDev unteres Drittel +25 % bei dichtem, schon kontrastreichem Waldboden mit Auto-Exposure). Zerlegt in WALD8b1 (Import-Fix committen, Mass: nicht flach, Diff >= 4 %) und WALD8b2 (Sichtbarkeit per mittlerer Pixeldifferenz). Code bleibt im Baum fuer WALD8b1; keine CI (kein Code geaendert).

## 2026-10-23 KLEE1 (Overseer)
Ursache: kein Spielcode-Fehler; Beweisbild grau wegen Aufnahme-Pipeline (auch Wald7 grau). Versuche: Fog/TAA/MB-Override wie Galerie, RenderForCamera, Kamera an Spinne -> weiter uniform 164. J5 per Stash-Gegenprobe auch ohne Klee rot (260,35). Getan: Klee committed, J5 in ROT.md, KLEE1b angelegt.

2026-10-24 KLEE1b (Overseer): Ursache asynchrone HDRP-Shader-Kompilierung im ersten Test + volumetrischer Nebel -> einfarbiges Beweisbild. Getan: Nebel-Aus + WarmUp-Schleife in KleeTests/Wald7MoosTests, voll getestet, J5 und Legs_Gait in ROT.md, Punkt [x].

2026-10-24 FARBE1 (Overseer): Ursache J5 = Capture-Setup (Nebel + Nacht-Rahmen nicht dunkel), Mond-Anhebung war falsch. 3 gezielte DayNight-Laeufe (o-farbe1, 1b, 1c), kein Commit. Look/Test-Aenderungen in Stash 'Hermes FARBE1'; Punkt in FARBE1a/FARBE1b zerlegt.
2026-10-24 FARBE1a (Overseer): Hermes-Aenderungen korrekt, Rest = Nacht zu dunkel. moonLux 16000->50000, gezielt+voll gruen, Commit d33a34d.

- 2026-10-25 FOTO1 blockiert (Zeitbudget, Fix ungebaut). Overseer: Player gebaut, Foto-Check 0.05 bei gleicher Pose (1/3 Ausreisser durch Spinnenpose), Pose-Log ergaenzt, volle CI gruen, Commit 92079ea.

- 2026-10-25 02:45 LICHT1 (Overseer): blockiert wegen Messaufbau (Foto zeigt Spinne statt Boden). Player gebaut, Foto nachher gemessen (Std-Faktor 0,9). Zerlegt in LICHT1a/LICHT1b, Code-Aenderung gestasht. Nichts Gameplay-seitig committet.

2026-10-26 TEXTUR2 (Overseer): [!] war Zeitbudget-Abbruch, kein Fehler. Foto und gezielte Tests geprueft (alle gruen ausser Vorab-Rot E1), Farn-Dateien committet, Punkt [x].

2026-10-26 MODELL6 (Overseer): [!] wegen B4c (Seil/Space) Wackler, einzeln gruen -> ROT.md, Farn-Arbeit committet, Punkt [x].

- 2026-10-27 Overseer MODELL15: Hermes-Runde ohne Zeit (Blender-Export offen, Atlas S 0.78 ueber Ziel). Zu gross fuer einen Eingriff -> in MODELL15a (Atlas), 15b (Blender/FBX), 15c (Unity-Abnahme) zerlegt; Atlas-PNGs gestasht, Skripte bleiben im Arbeitsbaum.

## 2026-10-28 MODELL17 (Overseer)
Ursache: Hermes brach ab, weil HDRP-Lit die Vertexfarbe (Moos) nicht zeigt und Blender-PNG-Save schwarz lieferte; Geometrie/Tests waren fertig. Aktion: gezielte Tests geprueft (M17 gruen; Q1/Vegetation/Urwald gruen ausser Z9, E1 lastabhaengig), Hermes-Dateien committet, Moos als MODELL17b neu geplant, E1 in ROT.md.

## 2026-10-28 LICHT1b (Overseer)
Ursache: Testannahme falsch - Dach-Dichte kann den Std-Faktor nicht auf 1,25 heben, Direktlicht nur ~9 % der Bodenhelligkeit. Aktion: kein Code committet (Urwald_GeneratesWorldFromSeed waere rot), Code gestasht, Punkt als LICHT1c (Look-Entscheidung Alex) neu geplant, Frage in ALEX-FRAGEN.md.
