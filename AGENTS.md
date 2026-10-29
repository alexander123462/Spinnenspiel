# AGENTS.md — Projekt-Spehten for all Agents (Overseer, Hermes, Subagents)

## Lektion Vegetation Blender -> Unity (Jacaranda-Fehler 12.10.2026)
1. Nach jedem Blender-Export die Material-Slots prüfen: Reihenfolge und welche Polygone zu welchem Material gehören. Slot-Index = Submesh-Index in Unity. Materialien im Mesh Renderer nach NAMEN abgleichen, nie blind nach Position.
2. Blattflächen = Leaf-Material (Alpha Clipping + Double Sided), Stamm/Äste = Wood-Material (deckend). Blätter braun und eckig oder Stamm fehlt = Materialien vertauscht.
3. Keine Duplikate wie ".002" erzeugen. Original-Objektnamen und Mesh behalten, damit Prefabs und Szenen-Referenzen nicht brechen. Origin an den Stammfuß setzen.
4. Nie die ganze Vegetation auf einmal neu bauen. Erst EIN Asset fertig machen, in Unity per Screenshot mit dem Original vergleichen, dann die nächsten.
5. Nie "fertig" melden ohne Screenshot-Kontrolle im Unity-Editor.
6. Jedem Unteragenten diese Regeln im Auftrag mitgeben.
