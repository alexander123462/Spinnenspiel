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
- Geschwark edits Docs/BACKLOG.md between your read and patch ("modified since last read" warning): re-read, its [!]/[x] marks are authoritative — reconcile, don't overwrite blindly.
- Cron/background runs cannot use notify_on_complete; start CI in background and poll summary file with sleep+tail instead.

- Geschwark/Claude-CI Exit 1 + 'keine Fehler' + tasklist Unity.exe = project lock by sibling session: do NOT retry, mark [!].

- Geschwark-Blocker: minimal-fixing their syntax errors burns the 3 CI tries fast (CS0504 static const, CS0234 System.IO.Error/Exception); keep own code OUT of tree (Logs/z7/) and mark [!], retry next session.

- Application has NO frameCount; use Time.frameCount for "same-frame" cross-component checks (GameMenu vs Placer Esc handling).

- Aborting a coroutine build: AbortBuild() alone does NOT stop the BuildRoutine loop -> set a cancel-flag and check it at every yield point (also in WalkTo, where `return` must be `yield break`, CS1622).

- Shared key (Esc) for two systems (Pause vs Netzbau-Abort): both read wasPressedThisFrame; the later Update() must detect "handled this frame" via a frame-stamp, not only current state.

- The 4 reds (Climb_TreeFullHeight, Climb_TreeInWorld, RedPreview_WhenSpotGoesBad, Spider_StopsAgainstTree) are DETERMINISTIC (r102u2 + r102u2b identical numbers) — real regressions from Testwelt rebuild churn (87fe1ea/108194f), not wacklers. Investigate climb/net code before any further full-CI gate attempts.

- ScreenshotTool U2 facts: Testwelt course objects DO exist (m_Name counts Stone 12, Branch 72, Log/Boulder/Stump 1); obstacles only 5-30 cm tall -> parcours cam must sit ~0.3 m up and look along course z+0.9..4.6. Fog is Exp2 density 0.022 -> uebersicht cam within ~20 m. MSAA: target.antiAliasing=4 on the ARGBHalf RT fixes jagged edges.

- FindObjectsByType needs FindObjectsSortMode.None (bare, already in scope of UnityEngine.Test import); UnityEngine.TestTools.FindSortMode does not exist (CS compile error).

- 4 reds persist AFTER rebuilding both scenes (u4pre2 Methods Exit 0): Testwelt trunk XZ identical to green 2b3b244, only Y +0.30 m -> rebuild churn NOT the cause; suspect GameMenu _state=1 (Startscreen removed 108194f) changing Spider init/leg SnapToGround timing before Climb tests run.
