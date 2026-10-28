# Fragen an Alex

## B4 Leertaste-Sprung (Claude, 2026-10-20)
Im Code ist der Sprung an die Leertaste gebunden und die Sprung-Physik funktioniert im Test. Um den Fehler im echten Spiel nachzustellen:
1. Gespielt im Unity-Editor (Play) oder im gebauten Spiel (Spiel_bauen_und_starten.bat)?
2. Welche Szene (Urwald / Klettertest / Testgelaende)?
3. Wo war die Spinne: ebener Boden, Stamm/Wand, Ast, am Faden oder im Netz?
4. War vorher ein Menue offen (Startmenue/Pause) oder ein Knopf angeklickt?
5. Gar keine Reaktion, oder nur ein ganz kleiner Hopser (Sprunghoehe ist 0.5 m bei einer kleinen Spinne)?

## WALD8b2 Lichtflecks (Hermes, 2026-10-21)
Directional-Cookie im Forward+/auto-exposed HDRP never projects the dapple pattern onto the ground: at any tile size the whole frame collapses to ONE constant value (with-cookie lower third std 0.0, mean 164), while a white cookie renders normally (std 63). Hebel per Backlog (Kachel 20/96/192 m, CellDarkFloor 0.25, Drift-PingPong) cannot change that -> the pattern is NOT applied spatially.
Alex: Ist the directional-light cookie at all expected to work in this HDRP Forward+ setup? Options for Claude:
1) Switch render method to DeferredOnly (cookie reliable there) - but risk: breaks other tests / performance.
2) Fake the light-flecks without a cookie: bake the dapple pattern into the ground/G-buffer or a projected decal / volumetric shaft directly from WaldLook, no directional cookie at all.
3) Keep auto-exposure but disable it only for the test (fixed EV) so the pattern survives - confirm which EV.
Which lever does Claude pick? Then WALD8b2 can be re-attempted on that basis.

## LICHT1c Lichtflecken am Boden (Overseer, 2026-10-28)
Messung: Dach-Schatten wirken kaum (Std-Faktor 0,94 statt 1,25), weil Direktlicht nur ~9 % der Bodenhelligkeit ausmacht (Rest Nebel/Ambient/Exposure). Hebel ist der Look, nicht das Dach.
Alex: Soll der Wald-Look mehr Sonnenlicht bekommen (Direktlicht staerker, Nebel/Ambient niedriger, also kontrastreicher/dunkler im Schatten)? Oder reicht der weiche Dschungel-Look ohne Lichtflecken (dann LICHT1c streichen)?
