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

- Mesh-Vertices are NOT runtime-readable for PH-FBX trees (isReadable=false): lod0.vertices spams errors. For the TRUE visual bottom use the MeshCollider world-space AABB mc.bounds.min.y — always valid, unlike MeshRenderer.bounds which is STALE at creation time inside a builder (measured floats +2.5 m unmeasured).

- Raycast-foot tree anchoring fails on slopes/map edges: SampleHeight clamps off-terrain (sank -14.9 m) and crown columns on slopes grab branches; anchor at the root XZ with mc.bounds.min.y instead, and shift CHILDREN (visual+collider localPosition), never the root (VegetationContactTest pins root at SampleHeight+0.15).

- End-of-abseil/thread landings set transform.position = hit.point or anchorB/anchorA WITHOUT a terrain-height check, so the collider bottom (center - halfExtent) ends BELOW Terrain.SampleHeight and the spider sinks in. Fix once with a shared helper SettleOnTerrain(landing): halfToBottom = center.y - collider.bounds.min.y; targetCenterY = SampleHeight+terrainOriginY+halfToBottom+clearance; only RAISE (never lower). Apply at every landing site, not per-site copies.

- Cache the body Collider in Awake as GetComponent<Collider>() (field _bodyCollider); collider.bounds is world-space AABB valid after any transform move. Do NOT recompute halfExtent from local extents — bounds.min.y already accounts for current rotation/scale, so halfToBottom = position.y - bounds.min.y is robust.

- Per-instance color variation WITHOUT new material instances: one MaterialPropertyBlock per MeshRenderer via r.SetPropertyBlock(mp) with mp.SetColor("_BaseColor", c) (Unity-6/HDRP API is SetPropertyBlock, NOT SetShaderProperties which fails CS1061). Keeps Static Batching / Draw Calls in den Grueben. A MaterialPropertyBlock does NOT break batching the way cloned per-object materials do.
- Near-white MULTIPLICATIVE tint (all channels ~0.72..1.10) preserves Albedo texture AND overall saturation; an ABSOLUTE HSV anchor darkens/washes non-green plants (flowers, bark). To raise hue SPREAD without washing: jitter hue per instance, blend toward chroma only ~0..20%, brightness +/-0.10. Test the math as a public static ComputeTint(hRaw,sRaw,vRaw) pure function in EditMode/PlayMode without the scene.

## Lehren (A1, r108)
- PlayMode-Ko routine: SceneManager.LoadScene(...) defert das Szenen-Switch zum NAechsten Frame. Ist der Generator spawned in Awake, muss 'yield return null;' RIGHT after LoadScene befoer FindFirstObjectByType im WaitReady-Loop — ohne geht die Loop erstem Frame still old scene, FindFirst null und Timeout. Passing UrwaldTests do this; kopie pattern.
- Vegetation-Varianten: pro gepflanzter Instanz Zufall-Scale+Rotation deterministisch from JungleWorldGenerator.NextSeed (hash of world pos), KEINE new Material-Instanzen per Object (Draw Calls explode) — MaterialPropertyBlock for tint, shared material for geometry.
- New scan IDs (nettle/periwinkle/shrub_03/04) need matching ScanModels.IsAvailable+LoadMeshes entries AND generated HDRP .mat (MAT_<id>) committed; check ls AssetSource/polyhaven + Art/Scans/Models before referencing an ID in the generator, else Null-Return at runtime.

## Lehren (A2, r109)
- Spider-Spawn is ~9 m above ground and falls during walking: a proof-shot camera placed at spawn.transform.position films SKY (all-white PNG, mean 255). Take the screenshot only AFTER the walk / physics-rest, from the in-game near-camera.
- Web_WalkInWeb_StaysOnWeb is FLAKY (sometimes 'Nur 4 von 8 Beinen geerdet'): it passed in all prior runs; on a single failure rerun the CI once before calling it a regression.
- Verify proof-shots by pixel stats (PIL convert('L') mean/stdev), NOT the local vision model: white-out = mean ~255 / stdev <1; good scene shot ~ mean 57 / stdev 30. Vision model called both 'white' ambiguously.

## Lehren (G6, r108)
- Offscreen cam.Render() SKIPS the HDRP post-processing pipeline: a plain ARGB32 ReadPixels clamps to [0,1] and renders dark/soft (Max ~172). For bright/sharp proofshots use an ARGBHalf HDR RT + manual auto-exposure (geometric-mean, target ~0.55) + ACES tonemap — the proven ScreenshotTool path. ACES rolls off highlights, so raise the exposure TARGET to push Max past 200 while keeping Mittel <200.
- SkyFractionUpperHalf needs the proofshot camera ABOVE the canopy (canopyTop + ~6m) AND a short sky-ray maxDist (~30m); at ground or canopyTop+1.5 the upper-half rays all hit tree crowns -> 0.0 and the assert aborts before any capture.
- Capture ALL frames first, THEN measure/assert AFTER (a SharpFraction/SkyFraction assert that fires BEFORE SaveFrame leaves a STALE png and makes later measurements worthless).
- G3-closure (2026-10-14): der naechste "offene" BACKLOG-entry kann sein ein [-]-entry, das sagt "(abgeloest durch G6)" — then a full CI green + empty ROT.md closes it with a Docs-commit ONLY, no code. Check the G6-criteria in the summary log BEFORE flipping [-] to [x].
- G4-closure (2026-10-14): der [!] "SharpFraction 0.10 not reached" war SOFT — G6 HDR-RT path (ARGBHalf+AutoHist+ACES statt ARGB32 cam.Render()) already made urwald_spinne.png scharf. Measure on the FRESH r115 png, not a stale one: PIL numpy diffmax>8 -> SharpFrac 0.554 (close-up), himmel 0.009 (sky is smooth, expected low). A soft [!] whose fix already shipped needs only re-measure + Docs-commit, no code.
- Measure-before-close on proofshot-criteria: a [-] entry referencing a Logs/ci/*.png is only closable if THAT image is fresh from the current CI run (check mtime vs run time) and its pixel stats pass the named criterion; ls -l the png + recompute mean/SharpFrac in numpy, never trust an old number.
- G5-CLOSURE (2026-10-14): a [-] "abgeloest durch G6" entry whose own blocker was the foreign SharpFraction/SkyFraction regression is SOFT once G6 (e417086) turned Urwald_GeneratesWorldFromSeed green. Full CI r116 118/118 fehlgeschlagen 0 + ROT.md empty -> Docs-closure commit only, no new code. The sun-Lux/exposure levers themselves never needed to pass on their own — G6's HDR-RT path is what closed the gate.

- Soft-[!]-closure: a [!] entry can be STALE — a LATER commit (here 9ebf04d) already fixed it, so the [!] is not a live blocker. Before flipping [!] to [x]: git log --oneline for the fix-hash + re-run full CI and confirm the named test green in the summary + ROT.md empty. Then Docs-commit only, no new code. Distinguish a HARD [!] (real unresolved blocker) from a SOFT one (fix already shipped) by checking whether the fix commit exists and its test is now green.

- W1 Creek: carve the Bach LAST but NOT within the outer EdgeWidth border band (use TRUE distBorder=min(x,z,Size-x,Size-z), world spans [0,Size], NOT Mathf.Abs which breaks interior points). Carving the rim floats liana/tree anchors -> Lianen_HangFromCrowns red.

- W1 M7 invariant World_HasSwampsAndClearings: a dry point (SwampFactor<0.01) must NOT be below WaterLevel+0.3. A carved creek bed IS wetland -> fold creek into public SwampFactor (Max with creekWet ramp), so creek points are not counted as dry-violations. HeightAt keeps its own private SwampFactorAt, unaffected.

- W1 Creek present-test: assert bed under WaterLevel by sampling segment MIDPOINTS (IsUnderWater(mid)) and banks dry at mid+perp offset; path must span x=0..Size. Determinism via PlacementHash(seed) FNV over quantised Height/Density grid, same seed -> equal, diff seed -> unequal.

- N1 Silk: ShootThread already does Raycast+CanAnchor; a PlayMode test for anchor-geometry must respect CanAnchor thresholds (minThreadLength=0.5, rule b dot(dir,ownNormal)<-0.1, rule c coplanar within samePlaneTolerance=0.15). Shoot the Ueberhang underside from the GROUND (dist>0.5), never 0.4m straight up -> rejected.

- N1 Silk: SilkThread.Length is exactly Vector3.Distance(anchorA,anchorB) so the +-0.1m length-check holds trivially; the real sag-check is droop=max over t of chord.y-PointAt(t).y with PointAt parabola down sag*4*t*(1-t), max at t=0.5 = sag. Set sag=0.02*dist (0.1 at 5m) -> droop<=sag<=0.3 OK.

- N1 Silk test: set silk.autoAttach=false before shooting so the spider does not climb the thread; shoot aim from silk.spinnerets.position (not transform.position) since that is the raycast origin. For a Block side-face anchor keep vantage near the face mid-height so dot(dir,up)>-0.1 (rule b).
- Unity/VBA: 'base' ist a reserved keyword in Unity — a local named 'base' compiles in plain C# but Unity batchmode fails CS1001 at the line. Never use 'base', 'mf' etc. as locals; prefix them (baseIdx, waterMf).
- Full-world water Quad replaced by Flood-Fill-Mesh: iterate a 1-m grid, emit a quad where Height < WaterLevel, build verts/tris. Gives realistic shorelines for Sumpf+Bach and lets MeshCollider exactly mark the flooded area for wading.
- Depth clamp for flat swamp water: return Mathf.Max(h, WaterLevel - MaxSwampDepth) in the flooded-area height function so no area is deeper than MaxSwampDepth under the waterline; testable via world.IsUnderWater + WaterLevel-Height.

- Unity: Application.logMessageReceived is an EVENT, not a method -> register with 'Application.logMessageReceived += Handler' and unregister '-=' (handler signature void(string,string,LogType)). .Add()/.Remove() do NOT compile (CS0079/CS7036). Count LogType.Error + LogType.Exception for Konsolenfehler=0 tests.

- LODGroup.GetRenderers has NO C# accessor (CS1061). To check a renderer is under LOD, walk up the transform chain: a renderer is covered if it or any ancestor carries an LODGroup component. Trees put LODGroup on root, renderers on children; plants/lianas put it on the same node -> ancestor-walk covers both.

- Transform.GetChild(i) returns Transform (NOT GameObject); iterate children via childCount+GetChild (Transform->Transform), there is no Transform.GetChildren(). GetComponent<LODGroup>() works on a Transform.

- Unity C# events: 'public event System.Action X' is NOT valid (CS1061, no .invoke() on a plain type). Use 'public UnityEvent X' (needs 'using UnityEngine.Events;') and call 'X.Invoke()'. Never 'X?.invoke()'.

- Pure-logic state machines: put the timing/state in a plain non-MonoBehaviour class (e.g. BirdThreatModel) with an explicit Tick(bool,dt); the MonoBehaviour only feeds it physics/web state + Time.deltaTime. Tests then drive the model deterministically with fixed dt — no wall-clock/waitFor frames needed.

- Float accumulation in tests: summing N*dt (80*0.1f) can land just under 8.0 so a '>= warnTime' trigger never fires. Drive until the state flips ('while(!model.Warned ...)') instead of hard-coding the exact tick count.

- Directional sun below the horizon lights NOTHING: a night scene with only a down-facing sun stays near-black (night Mittel 4). At night mirror the light above the horizon (elevLight=-elev, azLight=az+180 = a MOON) so ambient+moon gives a measurable brightness floor.

- Proof-image magenta/hole detector: Unity hole colour is exactly (1,0,1); a loose threshold (R>200,G<80,B>200) miscounts real pink flowers in the Urwald. Use near-pure (r>0.96,g<0.06,b>0.96).

- Day/night species: give the spawner a DayNightCycle ref + activeAtNight flag; target=round(count*ActiveShare), remove excess (retreat) then top up. Fixed exposure E for reproducible Mittel across day/night (G6-Lehre).

- Save-versioning: put an int version in the payload head; on load compare and warn+return on mismatch (JsonUtility.FromJson of bad JSON gives defaults version=0, so unknown version never crashes). Queue loaded seed via a static NextSeed so the next generated world matches the save.

- JsonUtility roundtrip of flat SavePayload (primitive + Vector3 lists) is exact for float/int/Vector3; keep payload flat, no nested classes. For test-only raw JSON injection give the component a WriteRawSaveJson helper writing to FilePath.
- CS0618 cleanup: replace FindObjectsOfType<T>() with FindObjectsByType<T>(FindObjectsSortMode.None) in test helpers that only iterate; sort order never mattered there, so None is safe and faster. Count warnings per compile pass doubles (each warning printed twice in method+tests logs).

## Lehren (K3, r113)

- Legacy IMGUI (OnGUI/GUI) has NO transform stack: GUI.PushTransform/Translate/Rotate/PopTransform do not exist (CS0117). Draw HUD markers as axis-aligned rects via GUI.DrawTexture(rect, whiteTexture) tinted with GUI.color; a rotated arrow needs a pre-rendered sprite, not GUI transforms.

- Mathf constant is Mathf.Rad2Deg, NOT RadToDeg (CS0117). And FindObjectsSortMode: the distance value is not named SortByDistanceToCamera; for plain iteration use FindObjectsSortMode.None. Never guess enum members — grep Library/PackageCache or the compile error names them.

- Pure-Math + MonoBehaviour split (PreyHintModel static math vs PreyHint OnGUI) lets an EditMode-style test assert direction/distance without pixels, and a PlayMode proofshot paints the model-projected marker over cam.Render() via ReadPixels->EncodeToPNG (ARGB32 RT, ARGB32/RGB24 tex).

## Lehren (K6, r119)
- PlayMode-Test: AddComponent in ARBEIT-Order (Hunger-Ausgabe AFTER DeathStats.Awake ran) -> cached _hunger null, Tod never fires. Fix: lazy re-acquire in Update (GetComponent only while ref still missing) OR add components BEFORE first yield.
- OnGUI does NOT render into a RenderTexture: proofshot must paint panel+ticks manually via PaintRect (GUI y-down -> tex.height-1-y), like K3/K6 pattern. Vision model miscounts small ticks -> verify counts by pixel-run-analysis per row (PIL).
- Bests persisted via SaveGame fields + payload bestTime/bestEaten/bestWebs; DeathStats.Update mirrors save-bests into model while alive so they survive app restart without new code.

## Lehren (K7, r120)

- File-API: with 'using System.IO;' use bare File.Exists/ReadAllText/WriteAllText and Path.Combine. Writing 'Path.File.x' is CS0117 ('Path' has no member 'File'); fully-qualified 'System.IO.File.x' also compiles. Copy the SaveGame.cs pattern (bare File./Path.) exactly.

- Persistence that must not break an existing test: add a SEPARATE MonoBehaviour (GameSettings) instead of extending GameMenu, so the old PlayerPrefs test stays green while new file-based persistence is tested independently.

- 'Survives between partien' PlayMode test without scene reload: comp A sets values (writes file), DestroyImmediate(A), create comp B (Awake loads), expose public LoadSettingsFromDisk() so the test forces a fresh read; assert values roundtrip. Corrupt-file test: WriteRawJson helper with unknown version -> assert defaults kept, no console error.


## Lehren (K8, r121)
- 'Simultan' PlayMode-Perf-Test: must PROVE the conditions hold during measurement, not just set them at t0. WeatherController.Rain01 is time-based (dry90/rain30); at WaitReady Time.time is low -> rain01=0. Fix: fast-forward loop `while Rain01<0.8 && Time.time<deadline yield 0.5s` THEN measure, and Assert.GreaterOrEqual(rainNow,0.8) so a dry-window run fails instead of silently passing.
- Tag+Regen together is impossible at default dayLengthMinutes=20 (noon drifts to night during the ~105s rain window -> day-species count drops). Fix: raise cycle.dayLengthMinutes (e.g. 200) + SetDayProgress(0.5) so dayFactor stays 1.00 through the rain window; day-spawner.count=6, night-spawner.count=0 gives exactly 6 flying while raining.
- CS1002 '; expected' on `Vector3 base = ...`: Unity compiler choked on the identifier/ternary combo. Fix: rename local (spiderPos) and replace ternary with if/else block. Cheap, no logic change.
