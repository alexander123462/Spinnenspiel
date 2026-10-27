# MODELL17: prozedurale Steine in Blender (per MCP gebaut, hier als Quelle dokumentiert).
# Aufruf in Blender: exec(open(...).read()); dann build_all().
import bpy, bmesh, math, random
from mathutils import Vector, noise

def make_rock(name, seed, size):
    rnd = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=0.5)  # 1280 Dreiecke
    sx = size*(0.85+rnd.random()*0.30); sy = size*(0.45+rnd.random()*0.25); sz = size*(0.85+rnd.random()*0.30)
    for v in bm.verts:
        v.co.x*=sx; v.co.y*=sy; v.co.z*=sz
    def facet(n_sites, blend, zbias, only_above=None):
        dirs = []
        for _ in range(n_sites):
            d = Vector((rnd.random()*2-1, rnd.random()*2-1, rnd.random()*zbias - 0.2))
            if d.length < 0.05: d = Vector((0,0,1))
            dirs.append(d.normalized())
        offs = [rnd.uniform(-0.06, 0.06)*size for _ in dirs]
        for v in bm.verts:
            if only_above is not None and v.co.z < only_above: continue
            p = v.co
            pn = p.normalized() if p.length > 1e-6 else Vector((0,0,1))
            bi, bd = 0, 1e9
            for i, d in enumerate(dirs):
                dd = (pn-d).length
                if dd < bd: bd, bi = dd, i
            d = dirs[bi]
            po = offs[bi] + size*0.5*(1.0-abs(d.z)*0.4)
            proj = p + d*(p.dot(d)-po)
            v.co = v.co.lerp(proj, blend)
    # mehrfach gebrochene Kanten: drei Facettendurchlaeufe (unten/mitte/oben)
    facet(rnd.randint(10, 14), rnd.uniform(0.72, 0.85), 1.4)
    facet(rnd.randint(4, 6), 0.55, 1.8)
    facet(rnd.randint(5, 7), 0.6, 2.4, only_above=0.15*size)
    # Schichtung: Stufen mit Phasen-Jitter (keine Box-Treppen)
    tilt = rnd.uniform(-0.12, 0.12)
    layer = size*rnd.uniform(0.12, 0.18)
    for v in bm.verts:
        p = v.co
        jit = noise.noise(Vector((p.x*3.1, p.y*3.1, 4.7)))*0.45
        zq = round((p.z - p.x*tilt)/layer + jit)*layer + p.x*tilt
        v.co.z = p.z*(1-0.5) + zq*0.5
    # zwei echte Schichtkanten (dynamitische Schnitte)
    for _ in range(2):
        zc = rnd.uniform(0.2, 0.62)*size
        no = Vector((math.sin(rnd.uniform(-0.3,0.3)), rnd.uniform(-0.1,0.1), 0.98)).normalized()
        r = bmesh.ops.bisect_plane(bm, geom=bm.verts[:]+bm.edges[:]+bm.faces[:],
            plane_co=Vector((0,0,zc)), plane_no=no, clear_inner=False, clear_outer=False)
        ne = [e for e in r['geom_cut'] if isinstance(e, bmesh.types.BMEdge)]
        bmesh.ops.holes_fill(bm, edges=ne)
    # Mikrorelief
    for v in bm.verts:
        p = v.co
        n = noise.noise(p*3.2/size)*0.6 + noise.noise(p*9.0/size)*0.3 + noise.noise(p*21.0/size)*0.1
        v.co += p.normalized()*n*size*0.028
    # sauberer flacher Fuss, Normierung auf Zielgroesse, Basis-Taper
    zb = min(v.co.z for v in bm.verts) + size*0.05
    r = bmesh.ops.bisect_plane(bm, geom=bm.verts[:]+bm.edges[:]+bm.faces[:],
        plane_co=Vector((0,0,zb)), plane_no=Vector((0,0,1)), clear_inner=True, clear_outer=False)
    ne = [e for e in r['geom_cut'] if isinstance(e, bmesh.types.BMEdge)]
    bmesh.ops.holes_fill(bm, edges=ne)
    dz = min(v.co.z for v in bm.verts)
    for v in bm.verts: v.co.z -= dz
    xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]; zs2 = [v.co.z for v in bm.verts]
    ext = max(max(xs)-min(xs), max(ys)-min(ys), max(zs2)-min(zs2))
    if ext > 1e-6:
        s = size/ext
        cx = (max(xs)+min(xs))*0.5; cy = (max(ys)+min(ys))*0.5
        for v in bm.verts:
            v.co.x = (v.co.x-cx)*s; v.co.y = (v.co.y-cy)*s; v.co.z = (v.co.z-min(zs2))*s
    h = max(v.co.z for v in bm.verts) or 1.0
    for v in bm.verts:
        if v.co.z < h*0.12:
            t = v.co.z/(h*0.12); f = 0.88 + 0.12*t
            v.co.x *= f; v.co.y *= f
    bmesh.ops.dissolve_degenerate(bm, dist=0.00002, edges=bm.edges[:])
    loose = [v for v in bm.verts if not v.link_faces] + [e for e in bm.edges if not e.link_faces]
    if loose: bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bmesh.ops.holes_fill(bm, edges=bm.edges[:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for p in me.polygons: p.use_smooth = False
    return ob

def paint_rock(ob, seed, size):
    # Vertexfarben: graues Gestein mit Schichtbaendern + Rostflecken, Moos nur oben.
    me = ob.data
    for ca in list(me.color_attributes): me.color_attributes.remove(ca)
    col = me.color_attributes.new(name='Col', type='BYTE_COLOR', domain='CORNER')
    rnd = random.Random(seed)
    rust = rnd.uniform(0.10, 0.25)
    layer = size*rnd.uniform(0.12, 0.18)
    tilt = rnd.uniform(-0.12, 0.12)
    mxz = max(v.co.z for v in me.vertices) or 1.0
    for p in me.polygons:
        n = p.normal; c = p.center; z = c.z
        up = max(0.0, (n.z - 0.35)/0.65)
        hfac = 0.45 + 0.55*max(0.0, (z - mxz*0.15)/(mxz*0.85))
        patch = noise.noise(c*7.5 + Vector((seed, 0, 0))) * 0.5 + 0.5
        moss = min(1.0, up*hfac*(0.45 + 0.75*patch))
        band = (math.floor((z - c.x*tilt)/layer) % 2) * 0.05 - 0.025
        band += noise.noise(c*2.2)*0.02
        g = 0.36 + noise.noise(c*3.1)*0.07 + band
        r = g*1.04; b = g*0.95
        rf = noise.noise(c*4.7 + Vector((0, seed*3.1, 0)))
        if rf > 1.0 - rust:
            t = (rf - (1.0-rust))/max(rust, 0.001)
            r += 0.15*t; g += 0.04*t; b -= 0.05*t
        mr, mg, mb = 0.13, 0.33, 0.11
        cr = min(1,max(0, r*(1-moss)+mr*moss)); cg = min(1,max(0, g*(1-moss)+mg*moss)); cb = min(1,max(0, b*(1-moss)+mb*moss))
        for li in p.loop_indices:
            col.data[li].color = (cr, cg, cb, 1.0)

VAR = [('Stein_1',1701,0.62),('Stein_2',5150,0.58),('Stein_3',9021,0.66),('Stein_4',3377,0.60),('Stein_5',7734,0.70)]

def build_all(mat_name='M_Stein'):
    mat = bpy.data.materials.get(mat_name) or bpy.data.materials.new(mat_name)
    for nm, seed, size in VAR:
        ob = make_rock(nm, seed, size)
        ob.data.materials.clear(); ob.data.materials.append(mat)
        paint_rock(ob, seed, size)
        bpy.ops.object.select_all(action='DESELECT')
        ob.select_set(True); bpy.context.view_layer.objects.active = ob
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=0.9, island_margin=0.02)
        bpy.ops.object.mode_set(mode='OBJECT')

def export_fbx(dirs):
    for nm, _, _ in VAR:
        ob = bpy.data.objects[nm]
        ob.location=(0,0,0); ob.rotation_euler=(0,0,0); ob.scale=(1,1,1)
        bpy.ops.object.select_all(action='DESELECT')
        ob.select_set(True); bpy.context.view_layer.objects.active = ob
        for d in dirs:
            bpy.ops.export_scene.fbx(filepath=d + '/' + nm + '.fbx', use_selection=True,
                object_types={'MESH'}, add_leaf_bones=False, use_mesh_modifiers=True,
                apply_scale_options='FBX_SCALE_ALL', path_mode='COPY', embed_textures=False)
