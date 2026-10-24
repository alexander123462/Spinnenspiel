# Spielplan: Spinnenspiel soll wirklich Spass machen

Stand 17.10.2026, geschrieben von Claude nach dem Blick auf Code, Tests, Galerie-Bilder und Backlog.
Ziel: Ein Spiel, das man gern 30 bis 60 Minuten am Stueck spielt und danach "noch eine Runde" will.

## 1. Wo wir stehen (ehrlich)

Schon da und getestet: Zufallswelt (Seed) mit Sumpf, Bach, Lichtungen; Klettern an Waenden und Decken; Netzbau; Fliegen und Motten, die ins Netz gehen, zappeln und sich befreien; Jagen und Fressen; Energie und Tod; Beute flieht; Voegel als Gefahr; Tag und Nacht; Regen; Speichern; Springen (L2); Wachstum durch Fressen; Statistik; Menue und Einstellungen.
Es ist also viel Mechanik vorhanden. Es fehlt das, was aus Mechanik ein Spiel macht: ein spuerbarer Rhythmus, Ziele, Rueckmeldung (Ton, Effekte, Gefuehl), Abwechslung und ein sauberes Bild.

Bekannte Schwaechen (aus Galerie und Spieltest):
- Steuerung/Bewegung: Spinne haengt auf Aesten fest (L1), Ast-zu-Ast-Springen klappt nicht (L3).
- Optik: Baumrinde verschmiert, Blaetter von hinten schwarz, Schilf ausgebleicht, Wasser flach und kantig, Fliege/Motte sehr primitiv, Totholz eckig, Spinne dunkel und schwer lesbar.
- Alle bisherigen automatischen Beweisbilder waren durch Bewegungsunschaerfe verschmiert (inzwischen behoben, Galerie-Test Q1).
- Es gibt kein Spielziel ueber das reine Ueberleben hinaus, keine Anleitung, wenig Ton.

## 2. Was macht es spassig? (Leitgedanken)

1. **Eine klare Schleife, die sich gut anfuehlt:** Netz bauen -> warten/erkunden -> Beute faengt sich -> zur Beute klettern -> einwickeln und fressen -> Energie, Wachstum. Jeder Schritt muss Rueckmeldung geben (Ton, Vibration, Kamera, Anzeige).
2. **Risiko und Belohnung:** Draussen im Freien jagen ist riskant (Vogel), im Dickicht sicher aber karg. Gutes Netz an gutem Ort ist die Belohnung.
3. **Ziele auf drei Ebenen:** in 10 Sekunden (die naechste Fliege), in 5 Minuten (satt werden, Nacht ueberstehen), in 30 Minuten (wachsen, Revier einnehmen).
4. **Abwechslung:** verschiedene Beute, verschiedene Gefahren, verschiedene Orte, Tageszeit und Wetter wirken auf das Spiel.
5. **Es sieht gut aus und liest sich klar:** die Spinne ist immer erkennbar, die Welt wirkt lebendig, nichts sieht kaputt aus.
6. **Faire Steuerung:** nie festhaengen, nie ohne Grund sterben, Fehler sind erklaerbar.

## 3. Phasen

### Phase 0 - Fundament sauber machen (zuerst, ca. 1 bis 2 Wochen Autopilot)
Ziel: nichts sieht kaputt aus, nichts haengt, alles ist messbar.
- L1 Ast-Haenger beheben (mit Test), L3 Ast-zu-Ast-Sprung.
- Galerie-Befunde abarbeiten: Q1b Baumbilder, Q2 schwarze Blaetter, Q3 Rinde, Q4 Schilf (Ursache vermutlich Farbton-Ueberschreibung aus F1), Q5 Wasser.
- Fliege und Motte neu modellieren (Blender, mit Vision-Kontrolle): Koerperform, Beine, Fluegel, Facettenaugen, Fluegelschlag.
- Spinne besser lesbar: Rim-Licht oder leichte Aufhellung, Kontrast zum Boden, Beine/Koerper-Modell pruefen (M1d).
- Leistung absichern: Frametime-Budget auch in dichtem Wald (Test K8 erweitern).
- Unity-Rauschen (K2a) beenden, damit Aenderungen sichtbar bleiben.
Abnahme: Galerie-Sheets aller Typen von Claude durchgesehen und "gut"; B1, B2, Climb-Tests, Frametime gruen; Alex spielt 15 Minuten ohne Festhaengen.

### Phase 1 - Die Kernschleife spuerbar machen (ca. 2 Wochen)
Ziel: Netz bauen, fangen, fressen fuehlt sich gut an.
- **Netzbau-Erlebnis:** Faeden mit Spannung und Wackeln, Ton beim Spinnen, Vorschau klar, Fertigstellung mit kleinem Effekt. Netz glitzert im Licht (Tautropfen morgens).
- **Fang-Erlebnis:** Netz vibriert bei Treffer (Kamera leicht, Ton), Beute zappelt sichtbar, Hinweis-Pfeil (K3) im Stil des Spiels. Kleine Wartezeit-Beschaeftigung (erkunden, Nachbesserung, Faeden pruefen).
- **Fress-Erlebnis:** Einwickeln und Fressen mit kurzer Animation, Energie-Leiste fuellt sich sichtbar, Ton.
- **Beute-Vielfalt:** Fliege (leicht), Muecke (klein, schnell), Falter (nachts), Kaefer (hart, mehr Energie, bricht Netz?), Libelle (stark, riskant), Wespe (gefaehrlich, sticht). Jede mit eigener Flugbahn und eigenem Netzverhalten.
- **Steuerung ueberarbeiten:** Klettern, Springen, Abseilen auf einer Tastenlogik, die sich fluessig anfuehlt; optional Zielhilfe beim Springen.
Abnahme: 10-Minuten-Spieltest, bei dem Alex ohne Anleitung fangen und fressen kann und sagt, es macht Laune.

### Phase 2 - Ziele, Fortschritt, Gefahr (ca. 2 bis 3 Wochen)
Ziel: Es gibt immer einen Grund weiterzuspielen.
- **Tagesrhythmus als Struktur:** Tag = jagen, Abend = Netz bereit machen, Nacht = Gefahr und Falter, Morgen = Bilanz. Kurze Zusammenfassung am Morgen (gefressen, Netze, Wachstum).
- **Wachstum und Haeutung:** nach genug Fressen Haeutungsszene (kurz, verletzlich), danach groesser/staerker; neue Faehigkeiten durch Stufen (staerkere Faeden, weiterer Sprung, besserer Biss).
- **Netz-Pflege:** Netze nutzen sich ab (Regen, Wind, grosse Beute), Reparatur kostet Energie. Das gibt Entscheidungen.
- **Gefahren mit Gegenmitteln:** Vogel (Deckung), Wespe/Libelle (Netz-Falle oder Flucht), Frosch/Echse am Boden (Hoehe), Regenflut (Aufstieg), Hunger (Zeitdruck). Jede Gefahr hat eine erkennbare Vorwarnung und eine faire Antwort.
- **Missionen/Ziele:** 3 kleine Tagesziele (z. B. "fange 3 Falter", "baue ein Netz in der Krone") mit Belohnung; Langzeitziel: bestes Revier und ausgewachsene Spinne.
- **Schwierigkeit:** drei Stufen (entspannt, normal, hart) ueber Hungerrate, Gefahrenhaeufigkeit, Beutemenge.
Abnahme: Eine 30-Minuten-Partie hat einen spuerbaren Verlauf (Anfang, Spannungsbogen, Ende/neuer Anfang). Mindestens 80 Prozent der Spieltests wollen noch einmal spielen.

### Phase 3 - Welt, Entdecken, Wiederspielwert (ca. 2 bis 3 Wochen)
- Orte mit Charakter: Lichtung, Sumpf, Bachufer, Baumkrone, hohler Stamm, Felsen - je mit eigener Beute und eigenem Risiko.
- Senkrecht spielen: Wurzeln -> Stamm -> Krone -> Lianen. Jede Hoehe hat eigene Beute, Wind und Gefahren.
- Entdeckungen: Verstecke, Abkuerzungen, seltene Beute, Fundstuecke (Sammelstuecke, Tagebuch der Spinne).
- Zufallswelt mit Wiedererkennung: Seed teilen, "Tagesseed" fuer alle gleich.
- Vegetationsvielfalt und Details (Pilze, Moos, Pfuetzen, Tau), damit nichts leer wirkt.

### Phase 4 - Praesentation (ca. 2 Wochen, teils parallel)
- **Ton:** Raumklang, Schritte je Untergrund, Faden-Zwitschern, Vogelruf als Warnung, ruhige dynamische Musik (leise am Tag, spannend bei Gefahr).
- **Bild:** Beleuchtung und Farbstimmung (Morgen-Nebel, Abendlicht, Mondlicht), Tiefenunschaerfe nur wo es hilft, klares Bild.
- **UI:** schlichte, einheitliche Anzeige (Energie, Tageszeit, Ziel), Hinweise nur bei Bedarf.
- **Anleitung:** 3-Minuten-Einstieg im Spiel (Bewegen, Klettern, Netz bauen, fressen), spaeter einblendbar. Hilfe-Karte (vorhanden) ausbauen.
- **Menues:** Start, Pause, Optionen, Steuerung, Statistik (vorhanden), Spielstand (vorhanden).

### Phase 5 - Spieltests und Feinschliff (fortlaufend, ab Phase 1)
- Jede Woche: Alex spielt 20 Minuten, schreibt in 5 Stichpunkten auf, was nervt und was Spass macht. Daraus werden Auftraege.
- Messwerte im Spiel (anonym im Log): Zeit bis zum ersten Fang, Tode und Ursachen, Netze pro Spielstunde, Zeit im Freien. So sehen wir, wo es hakt.
- Feinschliff: Zahlen anpassen (Hunger, Beute, Gefahr) per Konfigurationsdatei, damit man ohne Codeaenderung testen kann.

## 4. Arbeitsweise (damit das mit dem Autopiloten klappt)

- **Hermes (lokal, Autopilot):** mechanische, kleine, messbare Punkte (Parameter, einzelne Tests, Materialien, kleine Funktionen). Jeder Punkt hat Kriterien "Wert -> Kriterium -> OK/NOK" und einen Beweis (Test oder Galerie-Bild).
- **Claude:** Entwurf, schwierige Fehlersuche (Bewegung, Physik), Bildbeurteilung der Galerien, Zerlegung in kleine Punkte, Kontrolle der Ergebnisse alle 12 Stunden.
- **Alex:** Spieltests, Geschmack, Entscheidungen (Ton, Tempo, Schwierigkeit), Modellkunst-Freigabe.
- **Vision-KI (2070):** beurteilt Blender-Renderings und Galerie-Bilder vor, danach schaut Claude drauf.
- **Qualitaetstore vor jedem Commit:** Tests gruen, Frametime im Budget, sichtbare Aenderung hat ein sauberes Galerie-Vorher/Nachher.
- **Rhythmus:** alle 12 Stunden Zwischenstand (Backlog, Galerie, Spielbarkeit), danach neue Auftraege.

## 5. Entscheidungen (von Alex, 17.10.2026)

1. **Ton:** spannender Ueberlebenskampf (Gefahren, Hunger, Nacht als Risiko).
2. **Struktur:** endloses Ueberleben mit Wachstum (Tage und Naechte, Spinne wird groesser und staerker).
3. **Schwierigkeit:** drei waehlbare Stufen (Entspannt, Normal, Hart) ueber Hunger, Gefahr und Beutemenge.
Noch offen: Spinnenart (bestimmt Netzbau, Sprung, Jagdstil), Zielplattform/Hardware-Untergrenze.

## 6. Naechste konkrete Schritte
1. Phase 0 weiter abarbeiten lassen (Q1b bis Q5, L1, L3, K2a).
2. Qwen3.5-9B als Vision-KI wieder einrichten und Blender-Render-Ablauf (4 Ansichten) bauen.
3. Fliege und Motte neu modellieren (erster Blender-Auftrag).
4. Phase 1 ist als Backlog-Punkte S1 bis S9 eingetragen (Schwierigkeit, Muecke, Fang-/Fress-Rueckmeldung, Tagesbilanz, Netz-Verschleiss, Wespe, Tagesziele, Einstieg).
