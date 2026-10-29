# Spielziel und Umfang (Entwurf, Stand 2026-10-27)

Zum Streichen und Ergaenzen. Nichts davon ist im Backlog, bis Alex es freigibt.

## Kern in einem Satz
Eine Spinne im Urwald: jagen, Netze bauen, wachsen, ueberleben.

## Stufe 1: Kreislauf (klein, zuerst)
1. Hunger und Energie; Beute fangen fuellt sie, ohne Beute wird die Spinne langsamer.
2. Beutearten mit Werten: Motte gross und traege, Mücke klein und schnell, Fliege dazwischen.
3. Haeutung: nach genug Beute waechst die Spinne eine Stufe.
4. Atmosphaere: Geraeusche, Wetter, Tag und Nacht, Licht (HDRP-Basis ist da).

## Stufe 2: Netzbau (mittel, das Besondere)
5. Netz zwischen Aesten/Blaettern spannen (nutzt Seile und Lianen).
6. Beute faengt sich im Netz; Vibrationssicht zeigt Treffer.
7. Netz geht kaputt bei Regen, Wind oder grosser Beute.

## Stufe 3: Gefahr und Ziele (mittel)
8. Raeuber (Vogel, Eidechse, groessere Spinne): verstecken oder fliehen.
9. Kleine Auftraege ("fang 5 Motten", "Netz ueber dem Wasser").
10. Tagebuch: entdeckte Tiere, Pflanzen, Orte.
11. Abschluss: Nest oder Paarung als Spielende.

## Entscheidungen von Alex (2026-10-27)
- Offenes Spiel, kein festes Ende (Punkt 11 entfaellt oder wird optional).
- Koop-Mehrspieler (2 bis 4 Spinnen im selben Wald).
- Schwierigkeitsgrade (z.B. Entspannt / Normal / Hart): steuern Hungerrate, Raeuber-Dichte und -Staerke, Wetterhaerte, Beute-Menge.
- Die lokale KI (Hermes) probiert ALLES, auch Netz und Raeuber (5 bis 8). Claude prueft jedes Ergebnis und uebernimmt, wenn es scheitert.

## Stufe 0: Mehrspieler-Fundament (vor allem anderen entscheiden)
Netzwerk laesst sich schwer nachruesten. Darum zuerst, bevor Stufe 1 bis 3 wachsen:
- 0a. Technik waehlen: Netcode for GameObjects (Unity-Standard, Host + Clients, per Direktverbindung/Relay). Entscheidung und Architekturskizze: Claude.
- 0b. Zustand sauber trennen: was ist Spielzustand (Hunger, Wachstum, Netze, Beute, Raeuber) und was nur Darstellung. Der Host ist die Wahrheit.
- 0c. Zweite Spinne in der Testwelt, lokal mit zwei Instanzen testbar (Tests/Tools).
- 0d. Schwierigkeit als eine ScriptableObject-Konfiguration, vom Host vorgegeben.

## Wer baut was
- Hermes (alles zuerst versuchen): Stufe 1, 9, 10, 0c, 0d, und danach Netz (5 bis 7) und Raeuber (8) in kleinen Schritten mit Tests.
- Claude: 0a/0b (Architektur), Review aller Hermes-Ergebnisse, Eskalationen. Scheitert Hermes zweimal an einem Punkt, baue ich ihn.

## Risiken (ehrlich)
- Koop verdoppelt Testaufwand und Fehlerarten (Latenz, Desync). Physik-Klettern der Spinne ueber das Netz ist die haerteste Stelle.
- Wald-Assets sind schwer fuer Netz-Start: Welt per festem Seed bei allen erzeugen, nicht uebertragen.

## Beantwortet (Alex, 2026-10-27)
- Bis 4 Spieler, online (Freunde ueber Internet), erstmal kein PvP.
- Folge: Host + Relay (Unity Relay/Lobby), kein eigener Server noetig. Test erst lokal mit 2 bis 4 Instanzen, Online-Verbindung spaeter.

## Noch offen
- Host ist ein Spieler (Listen-Server) oder spaeter dedizierter Server? Vorschlag: Spieler-Host.
- Was passiert beim Tod/Hunger im Koop: Wiederbelebung durch Mitspieler?
