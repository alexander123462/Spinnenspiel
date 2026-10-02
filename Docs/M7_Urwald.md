# Meilenstein 7 – Urwald
Ziel: dichter Urwald mit Sümpfen, Lichtungen und Bächen; jede Partie erzeugt eine neue Welt aus einer Seed-Zahl (angezeigt, wieder ladbar). Die alte Testwelt bleibt für Tests erhalten.
## Leitlinien (Alex)
- Man soll sich so klein wie eine Spinne fühlen: Pflanzen in echter Größe, Spinne ~10 cm. Kamera tief und nah, Makro-Look mit Tiefenunschärfe. Bodendetails in Spinnengröße: Laubstreu aus echten 3D-Blättern, Zweige, Steinchen, Moospolster, Wurzeln, Tautropfen. Texturen müssen auch aus 10 cm Abstand scharf sein. Große Insekten und Tiere dienen als Größenvergleich.
- Netze soll man fast überall spinnen können: Im dichten Urwald gibt es überall Haltepunkte im Abstand 0.3–1.2 m (Blätter, Halme, Zweige, Lianen, Wurzeln, Boden). Alle Pflanzen in der Nähe der Spinne haben Collider, auch instanzierte. Abnahme-Test: An ≥ 80 % zufälliger begehbarer Urwald-Punkte (keine Lichtung, kein Wasser) findet der Placer innerhalb von 1 m einen Netzplatz.
## Schritte
- 10.1 Weltbeschreibung JungleWorld (Seed → Höhen, Sumpf, Wasserspiegel, Dichte, Lichtungen) + Laufzeit-Erzeugung von Gelände, Sumpfwasser, Spinne; Szene „Urwald“; Seed-Anzeige, F5 = neue Welt
- 10.2 Spielsysteme (Wasser-Erkennung, Audio, Vegetation-Streaming) auf eine allgemeine Weltabfrage umstellen, die Testwelt und Urwald beide kennen
- 10.3 Pflanzenarten per Code mit je ≥ 5 Varianten: Urwaldriese mit Brettwurzeln, Palme, Baumfarn, mehrere Buscharten, Riesenblatt-Pflanze, Farne, Lianen, Schilf, Stelzwurzel-Bäume im Sumpf, umgestürzte Stämme, Wurzeln, Laubstreu, Moos; CC0-Fototexturen (Poly Haven, ambientCG)
- 10.4 Dichte & Leistung: Instancing, LOD, Collider-Proxys für alle Pflanzen nahe der Spinne (Netze + Klettern); Ziel ≥ 60 FPS; Netz-Abnahme-Test (≥ 80 %)
- 10.5 Spinnenperspektive: Kamera tief/nah, Makro-Tiefenunschärfe, Bodendetails in Spinnengröße, große Insekten als Größenvergleich
- 10.6 Umstieg auf HDRP + Atmosphäre: Nebel, Lichtstrahlen, feuchte Oberflächen, Regenwald-Geräusche
- Netze und Klettern müssen in jeder Welt funktionieren (eigene Tests pro Schritt)
