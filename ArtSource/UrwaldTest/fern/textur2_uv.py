# TEXTUR2 Farn_Grund: jede Fieder-Insel in eigene Atlas-Zelle (GxG-Raster,
# V entlang der Mittelrippe wie bisher), Stiel- und Blatt-Objekt zu EINEM Mesh
# merged (Slot 0 = Stiel, Slot 1 = Blatt). Export nach Assets + ArtSource.
# Aufruf in Blender-MCP: exec(open(r'C:/Users/vorau/dev/Spinnenspiel/ArtSource/
# UrwaldTest/fern/textur2_uv.py').read())
import bpy, bmesh, shutil

ROOT = 'C:/Users/vorau/dev/Spinnenspiel'
ART = ROOT + '/Assets/_Project/Art/Plants/UrwaldTestFern/'
SRC = ROOT + '/ArtSource/UrwaldTest/fern/'
G, PAD = 22, 0.03   # Raster; PAD = Rand in Zell-Fraktion (Bluteschutz)


def islands(bm):
    """Zusammenhaengende Flaechen des Meshes als Face-Listen."""
    bm.faces.ensure_lookup_table()
    seen, out = set(), []
    for f in bm.faces:
        if f.index in seen:
            continue
        comp, stack = [], [f]
        seen.add(f.index)
        while stack:
            c = stack.pop()
            comp.append(c)
            for e in c.edges:
                for lf in e.link_faces:
                    if lf.index not in seen:
                        seen.add(lf.index)
                        stack.append(lf)
        out.append(comp)
    return out


def remap(me, row):
    """Inseln auf eigene Zellen legen. row=0: Stiele in Zeile 0; sonst Blaetter ab Zeile 1."""
    bm = bmesh.new()
    bm.from_mesh(me)
    uvl = bm.loops.layers.uv.active or bm.loops.layers.uv.new('UVMap')
    isl = islands(bm)
    for k, comp in enumerate(isl):
        cx, cy = (k % G, 0) if row == 0 else (k % G, 1 + k // G)
        for f in comp:
            for l in f.loops:
                u, v = l[uvl].uv
                l[uvl].uv = ((cx + PAD + u * (1 - 2 * PAD)) / G,
                             (cy + PAD + v * (1 - 2 * PAD)) / G)
    bm.to_mesh(me)
    bm.free()
    return len(isl)


for v in range(1, 6):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.materials):
        if m.users == 0:
            bpy.data.materials.remove(m)
    # Quelle ist immer das gesicherte 2-Objekt-Modell -> Skript ist wiederholbar.
    bpy.ops.import_scene.fbx(filepath=ART + 'fern_' + str(v) + '_alt.fbx.bak')
    stem = [o for o in bpy.data.objects if o.type == 'MESH' and 'stem' in o.name][0]
    leaf = [o for o in bpy.data.objects if o.type == 'MESH' and 'leav' in o.name][0]
    ns, nl = remap(stem.data, 0), remap(leaf.data, 1)
    assert ns <= G and nl + G <= G * G, (ns, nl)
    bpy.ops.object.select_all(action='DESELECT')
    stem.select_set(True)
    leaf.select_set(True)
    bpy.context.view_layer.objects.active = stem   # Slot 0 = Stiel, dann Blatt
    bpy.ops.object.join()
    stem.name = 'fern_' + str(v)
    stem.data.name = 'fern_' + str(v)
    # Saubere Slot-Namen ohne .00x: ungenutzte Material-Klauen entfernen, dann umbenennen.
    used = set(m for m in stem.data.materials if m)
    for m in list(bpy.data.materials):
        if m not in used:
            bpy.data.materials.remove(m)
    stem.data.materials[0].name = 'M_Fern_Stem'
    stem.data.materials[1].name = 'M_Fern_Leaf_' + str(v)
    slots = [m.name for m in stem.data.materials]
    tris = sum(len(p.vertices) - 2 for p in stem.data.polygons)
    # UV-Kontrolle: alle in 0..1, jede Insel in eigener Zelle.
    uvd = stem.data.uv_layers.active.data
    cells = set()
    ok = all(0.0 <= d.uv[0] <= 1.0 and 0.0 <= d.uv[1] <= 1.0 for d in uvd)
    for d in uvd:
        cells.add((min(int(d.uv[0] * G), G - 1), min(int(d.uv[1] * G), G - 1)))
    print('fern_' + str(v), 'uvOK', ok, 'zellen', len(cells), 'erwartet', ns + nl)
    bpy.ops.object.select_all(action='DESELECT')
    stem.select_set(True)
    fp = ART + 'fern_' + str(v) + '.fbx'
    bpy.ops.export_scene.fbx(filepath=fp, use_selection=True,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, mesh_smooth_type='FACE', path_mode='STRIP',
        use_mesh_modifiers=True)
    shutil.copy(fp, SRC + 'fern_' + str(v) + '.fbx')
    print('fern_' + str(v), 'stemIns', ns, 'leafIns', nl, 'slots', slots, 'tris', tris)
