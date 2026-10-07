# LERNEN – Lehres (max. 40 Zeiles)

- ScriptableObject-Fields (Mesh[][], Material[][]) seriisengruen ohne Init; CI-Compile Exit 0 ist genug Verifikation, wenn Builder-Fill in naechst Schritt laeuft.
- CI-Summary: "fehlgeschlagen 0" + alle Exit 0 = gruuen; Skipped-Ignored (Urwald_WebSitesAlmostEverywhere) gruuen, nicht rot.
- Unity-Close pruefen per tasklist | grep -i unity BEFORE ci.ps1 laeuft; Unity offen -> [!] und aufhören.
- Unity NOT serialisiert nested arrays (Mesh[][], Material[][]) in ScriptableObjects -> silently missing in .asset; use flat arrays (phTreeLod0/1, WoodMats/LeafMats).
- FBX-Mesh-Names get ".001/.002"-Suffix per Unity-Import -> match Contains("_LOD0"), NOT EndsWith.
- git: --no-optional-locks ONLY before subcommand ("git --no-optional-locks commit" works, "git commit --no-optional-locks" unknown option).
- Unity6 LODGroup.SetLODs: LOD ctor wants MeshRenderer[] (Renderer[] and List<MeshRenderer> both rejected CS1503); Renderer.sharedMaterials wants Material[] (List<Material> rejected CS0029). Error msg names the expected type -> convert to THAT.
- `out` is a reserved identifier in Unity6 C#: local var named out -> CS1002 cascade. Rename (e.g. matPerSub).
- MeshCollider DOES have worldBound in Unity6 (compiles+runs); mc.bounds is mesh-local. Use worldBound for world-space extents.
- PH-Tree FBX-Scan origins are NOT at mesh bottom (Island ~6 m above origin) -> align via pos - up*(lod0.bounds.min.y*scale) in CreateTree, else colliders float over ground.
- PH-Tree scans: LOD1 lacks lower-trunk triangles -> MeshCollider must use LOD0; scan meshes isReadable=false, so NO lod0.vertices at runtime (throws 'Not allowed to access vertices').
- Trunk XZ offset from root pos by ~3 m AND terrain height differs there -> corner-sampling still floats trunks 3-6 m; fix: measure trunk foot via Physics.RaycastAll downcasts in trunk footprint, then set rootY = footGround + 0.25 - footOffset (one-shot, guard drop<=10 m).
- island_tree_03 scan has NO trunk triangles below ~9 m (measured) -> PlayMode tree-blocking test asserts collider geometry above ground, not spider-body band; log per-tree hits for evidence.
- Geschwark/Claude-Sessions edit the same repo concurrently: if a test file changes between your reads (mtime check!) and kette-hdrp.log shows new "hermes" commits, that session owns the point -> mark [!], do NOT commit its files.
- Physics.Raycast out-RaycastHit: use hit.collider.gameObject, hit.gameObject does not exist.
- Autopilot lock race: if tasklist shows Unity.exe during CI, a sibling session owns the project; ci.ps1 exits 1 with 'keine Fehler' (project lock). Mark [!] and stop, never kill Unity.
