# LERNEN – nur Kernlehren. HARTE GRENZE: max. 40 Zeilen mit "- " und 8000 Zeichen. Vor dem Anfuegen pruefen, ob es sinngemaess schon dasteht (dann NICHT anfuegen). Wird die Grenze ueberschritten: erst verdichten (Doppeltes zusammenlegen, Unwichtiges nach Docs/LERNEN_ARCHIV.md verschieben, das Archiv wird NIE gelesen). Zeilen mit "- [K]" sind Kernlehren und bleiben stehen. Das Skript schiebt bei Ueberschreitung die aeltesten Nicht-Kern-Zeilen automatisch ins Archiv.

- [K] Unity/C#: ScriptableObject serialisiert KEINE verschachtelten Arrays (Mesh[][]), flache Arrays nutzen. FBX-Meshnamen bekommen ".001"-Suffix: Contains("_LOD0"), nicht EndsWith.
- [K] Unity6: LOD-Konstruktor will MeshRenderer[]; sharedMaterials will Material[]; "out" ist reservierter Name (CS1002); Transform.GetChild liefert Transform; FindObjectsOfType veraltet -> FindObjectsByType(..., FindObjectsSortMode.None); RaycastHit: hit.collider.gameObject; Application hat kein frameCount -> Time.frameCount.
- [K] git: --no-optional-locks nur VOR dem Unterbefehl ("git --no-optional-locks commit").
- [K] Scan-Meshes (PH-Tree) sind isReadable=false: keine vertices zur Laufzeit; MeshCollider braucht LOD0; MeshCollider.worldBound statt bounds. Ursprung liegt nicht am Stammfuss: Fusspunkt per Raycast nach unten messen.
- [K] Coroutine abbrechen: Cancel-Flag an JEDEM yield pruefen, dort "yield break" statt return (CS1622). Geteilte Taste (Esc) fuer zwei Systeme: Frame-Stempel (Time.frameCount), nicht nur Zustand.
- [K] CI: Exit 0 und "fehlgeschlagen 0" = gruen; Skipped/Ignored ist nicht rot. Unity muss geschlossen sein (tasklist | grep -i "^Unity.exe"; Unity Hub, "unity.exe serve" und Licensing-Client zaehlen NICHT).
- [K] CI Exit 1 + "keine Fehler" + Unity.exe laeuft = Projekt gesperrt: Punkt [!] markieren, aufhoeren, Unity nie beenden.
- [K] GATE-REGEL: Basislinie steht in Docs/ROT.md. Nur dort gelistete rote Tests blockieren den Commit nicht. Ein roter Test, der dort NICHT steht, ist eigene Regression: nicht committen.
- [K] Rotes Testergebnis des eigenen Punkts: ZUERST die Annahme des Tests pruefen (Testaufbau, Vorbedingungen, Zaehler; mit aehnlichem gruenem Test vergleichen), dann erst Spielcode umbauen (J1: webAttraction fehlte im Test).
- [K] Find before you fix: vor dem Programmieren die volle CI laufen lassen, der Rote kann schon gruen sein. Beim Entwickeln gezielt mit -Filter testen, voller Lauf nur einmal vor dem Commit.
- [K] HUD (OnGUI/uGUI) laesst sich im Batchmode unter HDRP NICHT per Screenshot belegen: Werttest statt Beweisbild, Alex prueft im Editor.
- [K] Screenshots unter HDRP: genau EIN cam.Render() pro Frame, Motion Blur/DoF/TAA aus, 3 Frames warten, sonst Verschmier-Artefakte (kein Modellfehler!). Exposure fest, sonst nicht reproduzierbar. MAT_Sky (Built-in-Shader) ist in HDRP magenta.
- [K] Material-Slot-Index = Submesh-Index; Materialien nach NAMEN abgleichen, nie nach Position. Keine neuen Materialinstanzen pro Objekt: MaterialPropertyBlock.
- [K] Unity-Rauschen: geaenderte .mat/.asset/.unity ohne eigene Aenderung (HDRP vergibt fileID neu) nicht anfassen und nie committen.
- [K] Pflanzen liegen auf Layer 8 (GameLayers.PlantMask), der Oberflaechensensor der Spinne ignoriert sie. Baumstaemme (TreeCollider/TrunkCapsule) sind kletterbar.
- [K] Spinnenbewegung: nach unten zeigende Flaechen (normal.y < -0.1) nahe Terrain (Terrain-Raycast 1 m) sind KEINE Kletterflaeche (B2, sonst dreht sie sich kopfueber unter Wurzeln). Ein solcher Guard darf nie das Rueckwaertslaufen blockieren (L1).
- [K] Dateien vor dem Patchen neu lesen ("modified since last read"); eigene Aenderungen nicht blind ueberschreiben.
