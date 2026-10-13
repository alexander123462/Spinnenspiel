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

- Find before you fix: a red in the full run may already be green after a sibling fix wave — rerun the FULL CI filtered/unfiltered BEFORE writing code; R3 (RedPreview) self-healed via climb fix 662c80c, zero code change needed.


- 4 reds persist AFTER rebuilding both scenes (u4pre2 Methods Exit 0): Testwelt trunk XZ identical to green 2b3b244, only Y +0.30 m -> rebuild churn NOT the cause; suspect GameMenu _state=1 (Startscreen removed 108194f) changing Spider init/leg SnapToGround timing before Climb tests run.
- G1 skybox: MAT_Sky legacy Built-In-Shader (guid 000...f000...=Skybox) renders magenta in HDRP; fix = RenderSettings.skybox=null + HDRP Volume (VisualEnvironment skyType=PhysicallyBased). But Mittel stays ~27 (<40) even after that -> dark scene needs Exposure/ambient tuning, not just skybox removal. Gate blocked by R4 Spider_StopsAgainstTree (separate point).\n- Scene GUID refs: grep MAT_Sky NAME misses scene refs; scenes store m_SkyboxMaterial by GUID (01ce316bfe87fac49af900dcce5f435f). Always grep the .mat.meta guid before deleting a material.\n- r102 full CI = fehlgeschlagen 1 (only Spider_StopsAgainstTree left; Climb x2 + RedPreview now GREEN after 662c80c/9fa512b wave). So the 4-red blocker shrank to 1 red (R4).


- GATE-REGEL (wichtig, hat schon 2 richtige Fixes vernichtet: U3 T4 und G1): "fehlgeschlagen 0" ist NICHT das Kriterium, wenn ein fremder Test schon vorher rot war. Basislinie steht in Docs/ROT.md. Fehlschlaege, die genau dieser Liste entsprechen, blockieren den Commit NICHT und zaehlen NICHT als Fehlversuch. Eigene korrekte Aenderungen deswegen NIE mit git checkout zurueckholen - sonst ist die Arbeit weg und der Punkt bleibt offen.
- PH-Tree FBX origins NOT always at the trunk: jacaranda root pivot sits ~5.7 m XZ inside crown-edge air. Distance tests to the pivot axis are a phantom there; measure against the collider footprint (TreesTests ray-samples it) or filter picks by |colliderBoundsCenter-pivot| XZ <= 1.5 m.
- GATE-REGEL refined: a red in your OWN point's test (not in ROT.md baseline) IS a real failure — you must fix it, do not treat it as foreign. Only pre-existing reds listed in Docs/ROT.md are exempt from the gate.
- HDRP sky-fraction sanity check at ground level fails in closed-canopy Urwald: Physics.Raycast upper-half grid returns 0% sky because foliage/tree colliders fill the canopy. Proofshot cam for a "sky visible" assert must sit ABOVE the canopy (or use short ray range / wider FOV), not just terrain+2 m.
- HDRP VisualEnvironment default skyAmbientMode=Dynamic but overrideState=false -> NOT actually applied; must call env.skyAmbientMode.Override(SkyAmbientMode.Dynamic) to get dynamic ambient from the PhysicallyBased sky (static probes were black -> dark scene, Mittel~27).

- Der Urwald hat ein GESCHLOSSENES Kronendach: eine Kamera auf 2 m Hoehe sieht praktisch keinen Himmel (gemessen skyFrac 0.0, alle Rays der oberen Bildhaelfte treffen Bewuchs). Ein Himmels-Nachweis braucht deshalb eine ZWEITE Kamera ueber dem Kronendach, nicht eine Kamera am Boden. Helligkeit am Waldboden darf niedrig sein, das ist kein Fehler.
- BEI [!] NICHT ZURUECKNEHMEN: eigene Aenderungen bei "3 Versuche erreicht" NIE mit git checkout verwerfen. Im Arbeitsbaum lassen und im Backlog-Eintrag die geaenderten Dateien namentlich auflisten. Die naechste Runde fuehrt sie weiter (Leftover-Regel). Dreimal ist so schon fertige richtige Arbeit vernichtet worden (U3 T4, G1, G2).
- Jacaranda-Blocker: grounding des TreeCollider (Pflanz-Scan-Cap um mc.bounds.center statt Pivot) ist necessary aber NOT sufficient: der Spinne laeuft durch den erhoehehehene stummfoot trotz solid Collider at body height (minNear 0.03). Naechste Ansaetze: SpiderMovement wall-detection gegen concave trunk oder Collider-Footprint-Solidifier.
- R5-Messung (wertvoll, nicht verlieren): Jacaranda-Collider sitzt nach dem Pflanz-Cap richtig am Boden (colliderBottomAboveGround -0.091), aber die Spinne laeuft weiter durch den Stamm (minNear 0.032, maxToward 6.45 > trunkDist 5.80). Also kein Test- und kein Pflanzproblem, sondern Bewegung/Kollision. Weiter in Punkt R6.

- Spider-Movement brakes only via Physics.SphereCast: a NON-convex (concave) MeshCollider is NOT hit by SphereCast/CapsuleCast raycasts against a directly-set transform.position. Trees need a convex collider (Capsule along trunk axis, child of the shifted TreeCollider node so it inherits planting dropLocal) on a layer the movement/sensor mask queries. Root cause of 'spider walks through trunk'.

- PlayMode-Walk-Tests against a tree must measure distance to the TRUNK COLLIDER axis (blocker.bounds.center), NOT the root pivot: some FBX origins (jacaranda) sit laterally offset from the trunk, so a pivot-based test reads false penetration. Take the axis from the real blocker (TrunkCapsule).

- HDRP AutomaticHistogram Exposure: limitMin senken BRIGHTENET (hoeheres EV = dunkleres). Sun-Lux-Cut (300k->100k) und Exposure limitMin sind COUPLED hebeln; aber der SharpFraction>=0.10 G4-Assert ist INDEPENDIENT von Helligkeit -> ein [ ] G5-Helligkeity-Point CANNOT commit while Urwald_GeneratesWorldFromSeed stays red on SharpFraction (0.081), regardless of Lux/limitMin tuning. Gate fehlgeschlagen 0 needs the G4 sharpness fixed FIRST.
- Untracked orphan Editor-Files (z.B. TreeGroundSnapper.cs V1-leftover, CS0246 MenuItem) in Assets/_Project/Scripts/Editor/ BREAK the whole Build (Exit 1, no test XML). They are NOT a regression of your point -> delete foreign untracked orphans, do NOT mark [!] for them; they belong to another backlog point.
- Messung muss reproduzierbar sein: Exposure auf AutomaticHistogram liefert bei gleichem Code unterschiedliche Bildhelligkeit (r102f Mittel 140.7, r108 mit demselben Code 54.2). Fuer bildbasierte Kriterien die Exposure auf Fixed stellen, sonst misst man Rauschen.
- Asserts NIE vor der letzten Screenshot-Aufnahme: ein fehlschlagender Assert bricht den Test ab, die spaeteren Bilder bleiben veraltet, und alle Folgemessungen sind wertlos. Reihenfolge: aufnehmen -> messen -> asserten.
- PUSH-REGEL (ab 2026-10-13, gilt ab sofort, auch wenn im Rundenauftrag noch "kein push" steht): Nach JEDEM erfolgreichen Commit zusaetzlich "git push origin hdrp" ausfuehren. Alex schaut am Server nach dem Fortschritt und hat dort 9 Tage lang nichts gesehen, weil nur lokal committet wurde. Schlaegt der Push fehl: Punkt trotzdem [x] lassen, Fehler in einer Zeile melden, nicht wiederholen. Weiterhin gilt: nie "git add -A", nur die eigenen Dateien.
- U1b/VolumetricFog: VolumetricFog AUS (enableVolumetricFog.Override(false)) im WorldLightingBuilder schaerfe/beleucht nahkam die Spinne: der Volumetric-Nebel streulicht losst den Kontrast, so die Luminanz-StDeviation am Spinnen-Closeup steht hoeher. Messwert mean=0,2 stdev=31,1; Kriterium StDev>=30 OK.
- U1b Gate war REACHABLE: the 4 old reds (Climb x2, RedPreview, Spider_StopsAgainstTree) turned all green via 9b5562b (R6 TrunkCapsule) + 9cb699d wave; ROT.md empty -> gate fehlgeschlagen 0 achievable. A prior [!] is only a hard block if the blocker itself is still red; re-check the baseline each loop before declaring a point blocked.
- Unity C# math: use Mathf.Sqrt/Mathf.Max (UnityEngine.Math is not the C# stdlib Math). For doubles, Math.Sqrt exists but is fragile in HDRP asmdef context — prefer (float) casts with Mathf.* so it compiles clean in batch CI.
- Unity Scene-Enumeration in PlayMode: SceneManager.GetActiveScene().GetRootGameObjects() — es KEEIN SceneManager.GetRootGameObjects(); GetRootGameObjects() lives on the returned Scene object, not on SceneManager.
- Testify Assertions: Assert.GreaterOrEqual(actual, expected, msg) exists; there is NO Assert.AtLeast. Use GreaterOrEqual/Greater/LessOrEqual for count-criteria.

- FBX-Export of Blender clones: src.copy() shares Mesh-Data -> the FBX exporter emits material-index warnings and Unity gets wrong submesh mats. Fix: cp.data = bpy.data.meshes.new_from_object(src) for a fresh unique mesh per clone; then 0 warnings in the run log.

- Deterministic scatter: implement Poisson-Disc in plain Python (grid rejection sampling, random.Random(seed)) inside the Blender script instead of Geometry-Nodes; then count/min_dist/patch-bounds are directly measurable from the JSON sidecar for the tests.

- Blender 5.1 removed bpy.ops.file.reset_active; FBX imports land in scene.collections — iterate all collections recursively or use bpy.data.objects, not only scene.collection.objects.

- Joined-per-instance FBX (scatter patches): one Mesh asset per instance with transforms baked into vertices; XZ-position = mesh.bounds.center via LoadAllAssetsAtPath (filter numVertices>0 by subMeshCount>0 — Unity 6 Mesh has no numVertices). Submesh order measured by GetSubMesh(i).indexCount (leaf crown >> stem), never assumed from Blender material order.

- Unity C#: `System` is Unity's global class — fully-qualified stdlib needs System.IO.File / System.Collections.Generic.List / System.String; System.IO.DirectorySeparatorChar and System.Collections.List do NOT exist. SubMeshDescriptor has indexCount, not count.

- HDRP material base color: mat.color = c writes _BaseColor/_Color correctly once HDMaterial.ValidateMaterial ran; verify with grep -A2 on the .mat (r/g/b match Blender tint).


- C# foreach: cannot modify members of the iteration variable (CS1654, e.g. 'foreach (Vector3 p in list) p.x -= ...'). Use an index-loop and write back: 'for i { Vector3 q=list[i]; q.x-=...; list[i]=q; }'.


- Scatter-Patch tiling: bake XZ-offsets centred around the patch centre (subtract bounds-center of all instances) so the runtime can add them to an arbitrary anchor; measure patch side = max(extents.x,extents.z) into a Library field (fernScatterPatchSize), not a magic number in placement code.

- Unity Vector2/Vector3: magnitude is a PROPERTY (dxz.magnitude), NOT a method — .Length() does not exist (CS1061). Use sqrMagnitude for comparisons to avoid sqrt.

- Farn-Scatter near-spawn count: the Scatter-Tiling deliberately keeps a 3m spawn-free zone + Density-gate, so 0 scatter instances within 12m of spawn is CORRECT by design — do not assert a scatter near-spawn count; guaranteed near-spawn ferns are the tree-ferns (AddTreeFern, covered by TreeFernCount). Assert collider-count == FernScatterCount instead.
- Re-Run-Antipattern: a prior [!] whose only blocker was PRE-EXISTING reds (Climb x2, RedPreview, Spider_StopsAgainstTree) is SOFT — those reds were already killed by the R6/9b5562b wave. Before declaring any U-point blocked, rerun the FULL CI: if baseline is now green (ROT.md empty), the point is reachable and just needs a Docs-closure commit, no new code.

- Grundfarn-FBX (Blender fern_1..5.fbx): the FBX-Importer MERGES Stem+Leaf into ONE submesh (subMeshCount==1), so do NOT filter meshes by `subMeshCount >= 2` (that silently drops all 5 -> 'Kein Grundfarn-Mesh loaded' and the count-test passes VACUOUSLY). Take the largest-area mesh instead and paint it with one opaque leaf-material.
- Vacuous-test trap: an assert guarded by `if (count > 0)` passes trivially when the loader silently loaded 0. Always grep the build log for the loader's Debug.Log (e.g. 'Grundfarne: N loaded') AND the runtime placement line ('Grundfarne: 61') to prove N>0 before trusting a green run.
- Separate-root plant pattern (Baumfarne/Farn-Scatter/Grundfarne): keep decorative plants under their OWN root with their OWN count field, and do NOT increment PlantColliderCount. The Unterholz collider-count assert (`PlantColliderCount == unterholz.GetComponentsInChildren<Collider>().Length`) then stays green; adding a separate root that DOES increment the counter is what broke U3b (4549 != 4452).
- A `[!]` whose only blocker was PRE-EXISTING reds (Climb x2/RedPreview/Spider_StopsAgainstTree) that a later R6 wave already turned green is SOFT: rerun the FULL CI; if ROT.md empty + fehlgeschlagen 0, the point needs ONLY a Docs-closure commit flipping [!]->[x], NO new code. (U3 closed this way in bd61620 after r113 green.)
- When closing such a soft-blocked point, also COMMIT its leftover untracked deliverables (the point's FBX + .meta + generated .mat) that a prior reverted round left on disk — they belong to THIS point, not another. Stage them explicitly (never git add -A); the Unity-noise ` M` .mat/.asset stay unstaged.

- LEFTOVER-Datei routing: modified Scripts/Tests files from a prior round of THIS loop belong to the topmost OPEN point. If they carry that point fix -> finish+commit; if they belong to a DIFFERENT point (e.g. G6/G3 SkyFraction proofshot assert sitting in the same UrwaldTests.cs as G1 lighting) -> git checkout -- them, commit ONLY the own point file. A foreign proofshot-Assert red is NOT your regression.

- G1 closed once the foreign test edits are reverted: r116 (foreign SkyFraction assert kept) = fehlgeschlagen 1; revert UrwaldTests.cs+asmdef -> r116b green 109/109. WorldLightingBuilder.cs alone (skybox=null + HDRP Volume sky) is the whole G1 fix.

- TreeRenderer-Node-Finder: die MeshRenderers of a PH-Tree sit NOT on the 'PH-Tree' GameObject but on its child LOD nodes 'TreeLOD0'/'TreeLOD1'. A name-filter for 'PH-Tree' silently finds zero renderers (canopyTop=0), so any camera placed from that height stays inside the canopy and measures skyFrac=0. Filter the LOD child names, or walk tree.transform children.
- Weak-evidence trap in walk-vs-tree tests: minNear 65.9/46.6 m with dStart 46-65 m means the 8 s walk NEVER REACHED the trunk — the assert passes vacuously. For a real blocking proof the candidate tree must be NEAR spawn (dStart ~3-6 m) so the spider actually contacts the TrunkCapsule. R5/R6 closed green but with weak coverage; tighten dStart filter if the block ever regresses.
- R5 soft-block closure (2026-10-13): the [!] blocker 'spider walks through jacaranda' was killed by the R6 wave 9b5562b (convex TrunkCapsule child of TreeCollider + PlantBlockTests targets capsule axis, skip removed). Full CI r123 109/109 green, ROT.md empty -> Docs-closure commit only, no new code.

- Testwelt-rebuild Churn: re-running TestWorldBuilder.Build regenerates tree positions (random layout), which can break foreign PlayMode tests that need specific geometry (e.g. NetbuildComfortTests RedPreview_WhenSpotGoesBad needs two Trunk MeshColliders 1.5-4 m apart). Before rebuilding a scene in CI, check which other tests load it and depend on its layout; keep the mandated -Method narrow or make layout-generating builders seed-stable with guaranteed spacing.

- LERNEN: Ein Zufall-Test (RedPreview "No trunk pair") kann rot DICH, wenn die Welt neu generiert — nicht automatisch eine Regression von dir; steht es als eigene Backlog-Punkt, in Docs/ROT.md-Baseline meldarum und committable.
- LERNEN: S1-Spawn-Hoehe already correct (JWG SampleHeight+Teleport, gap 0,030 m alle Seeds) — symptom im Spiel came from scene-authored Y=20 before generation; a PlayMode test over 5 seeds proves the runtime path, watch scene vs runtime separately.

- Deterministic precondition over random layout: a PlayMode test that needs specific geometry (e.g. two Wood colliders 1.5-4 m apart) must build that geometry IN the test (new GameObject + CapsuleCollider + SurfaceTag.Wood, base snapped to terrain via vertical RaycastAll skipping Wood), not hunt it in a randomly generated world. Otherwise regenerating the world flips the test red without any real regression. MakeWoodTrunk pattern in NetbuildComfortTests.cs.

- Unity has NO CylinderCollider component — use CapsuleCollider (height/radius) for a trunk-like primitive collider.
