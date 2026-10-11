# Poly-Haven-Baum auf Spielgroesse reduzieren: FBX mit <id>_LOD0 / <id>_LOD1 (Unity baut daraus eine LODGroup).
# Aufruf: blender -b -P ph_baum_reduzieren.py -- <fbx> <id> <outdir> [lod0_holz lod0_blatt lod1_holz lod1_blatt]
import bpy, bmesh, sys, os, math, shutil
import numpy as np
from mathutils import Matrix, Vector

A = sys.argv[sys.argv.index("--") + 1:]
fbx, tid, out = os.path.abspath(A[0]), A[1], os.path.abspath(A[2])
B = [int(x) for x in A[3:7]] if len(A) >= 7 else [25000, 60000, 6000, 16000]
os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
sc = bpy.context.scene

# Straigthten: Poly-Haven-Scans can be leaning trees (island_tree_02 ~19° Centerline).
# Rotate whole scan about its base so centerline becomes vertical; threshold 15°.
_v0 = [o for o in sc.objects if o.type == "MESH"]
import numpy as _np
_V = _np.vstack([_np.array([(o.matrix_world @ v.co)[:] for v in o.data.vertices]) for o in _v0]) if _v0 else None
if _V is not None:
    _zmin, _zmax = float(_V[:,2].min()), float(_V[:,2].max()); _hgt = (_zmax - _zmin) or 1.0
    _base = _V[_V[:,2] < _zmin + 0.10*_hgt][:,:2].mean(axis=0)
    _top  = _V[_V[:,2] > _zmax - 0.10*_hgt][:,:2].mean(axis=0)
    _v3 = _np.array([_top[0]-_base[0], _top[1]-_base[1], 0.75*_hgt])
    _phi = float(_np.arctan2(_np.hypot(_v3[0], _v3[1]), _v3[2]))
    print(f"DEBUG straighten: Centerline-Lean {_np.degrees(_phi):.1f}deg", flush=True)
    if _np.degrees(_phi) > 15.0:
        _axis = Vector((0,0,1)).cross(Vector(_v3)).normalized()
        from mathutils import Quaternion as _Q
        _M = Matrix.Translation(Vector((_base[0], _base[1], _zmin))) @ _Q(_axis, _phi).to_matrix().to_4x4() @ Matrix.Translation(-Vector((_base[0], _base[1], _zmin)))
        for o in _v0: o.matrix_world = _M @ o.matrix_world
        print(f"DEBUG straighten: rotated {-_np.degrees(_phi):.1f}deg about base -> upright", flush=True)

def sel(objs, active):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = active

def tris(o): return sum(len(p.vertices) - 2 for p in o.data.polygons)

meshes = [o for o in sc.objects if o.type == "MESH"]
sel(meshes, meshes[0])
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
bpy.ops.object.make_single_user(object=True, obdata=True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in [o for o in sc.objects if o.type != "MESH"]: bpy.data.objects.remove(o)
# Poly-Haven-FBX: ein Objekt mit mehreren Materialien -> nach Material trennen
for o in list(meshes):
    sel([o], o); bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="MATERIAL"); bpy.ops.object.mode_set(mode="OBJECT")
meshes = [o for o in sc.objects if o.type == "MESH" and len(o.data.polygons) > 0]


def is_leaf(o):
    m = o.data.materials[o.data.polygons[0].material_index] if o.data.materials else None
    return bool(m) and "leaves" in m.name.lower()
def join(objs):
    sel(objs, objs[0]); bpy.ops.object.join(); return bpy.context.view_layer.objects.active
wood = [o for o in meshes if not is_leaf(o)]; leafs = [o for o in meshes if is_leaf(o)]
print("Holz-Objekte", [o.name for o in wood], "Blatt-Objekte", [o.name for o in leafs])
W = join(wood); L = join(leafs)
# FBX-Import trennt Vertices an UV-Naehten -> zusammenschweissen, sonst zerfallen Stamm und Zweige in viele "Inseln"
def weld(o):
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4); bm.to_mesh(o.data); bm.free()
weld(W); weld(L)
co_all = np.array([v.co[:] for v in W.data.vertices] + [v.co[:] for v in L.data.vertices])
H0 = float(co_all[:, 2].max() - co_all[:, 2].min())

def copy(o, name):
    c = o.copy(); c.data = o.data.copy(); c.name = name; sc.collection.objects.link(c); return c
def decimate(o, target):
    t = tris(o)
    if t > target:
        m = o.modifiers.new("d", "DECIMATE"); m.ratio = target / t
        sel([o], o); bpy.ops.object.modifier_apply(modifier="d")
    return o

# Blattinseln: Label-Propagation ueber Kanten (numpy)
me = L.data; N = len(me.vertices)
e = np.empty(len(me.edges) * 2, dtype=np.int64); me.edges.foreach_get("vertices", e); e = e.reshape(-1, 2)
lab = np.arange(N)
while True:
    old = lab.copy(); m = np.minimum(lab[e[:, 0]], lab[e[:, 1]])
    np.minimum.at(lab, e[:, 0], m); np.minimum.at(lab, e[:, 1], m); lab = lab[lab]
    if np.array_equal(lab, old): break
uniq, vlab = np.unique(lab, return_inverse=True); n_isl = len(uniq)
lv = np.empty(len(me.loops), dtype=np.int64); me.loops.foreach_get("vertex_index", lv)
ls = np.empty(len(me.polygons), dtype=np.int64); me.polygons.foreach_get("loop_start", ls)
flab = vlab[lv[ls]]
tl = tris(L)

# Jedes Blatt -> eine Karte (Quad) in seiner Ebene; die Form kommt aus der Alpha-Textur.
LEAFMAT = L.data.materials[0]
co_l = np.empty(N * 3); me.vertices.foreach_get("co", co_l); co_l = co_l.reshape(-1, 3)
uvl = np.empty(len(me.loops) * 2); me.uv_layers.active.data.foreach_get("uv", uvl); uvl = uvl.reshape(-1, 2)
uvv = np.zeros((N, 2)); uvv[lv] = uvl
order = np.argsort(vlab, kind="stable"); st = np.concatenate([[0], np.cumsum(np.bincount(vlab, minlength=n_isl))])
CV = np.zeros((n_isl, 4, 3)); CU = np.zeros((n_isl, 4, 2)); CC = np.zeros((n_isl, 3)); OK = np.zeros(n_isl, bool)
for i in range(n_isl):
    idx = order[st[i]:st[i + 1]]
    if len(idx) < 3: continue
    P = co_l[idx]; c = P.mean(0); Q = P - c
    e1, e2 = np.linalg.svd(Q, full_matrices=False)[2][:2]
    a_, b_ = Q @ e1, Q @ e2
    sol = np.linalg.lstsq(np.stack([a_, b_, np.ones_like(a_)], 1), uvv[idx], rcond=None)[0]
    R = np.array([[a_.min(), b_.min()], [a_.max(), b_.min()], [a_.max(), b_.max()], [a_.min(), b_.max()]])
    CV[i] = c + R[:, :1] * e1 + R[:, 1:] * e2; CU[i] = np.c_[R, np.ones(4)] @ sol; CC[i] = c; OK[i] = True
n_ok = int(OK.sum())
# Hoechst + tiefst Blattinsel always keep: otherwise the Quad-Approximation shrinks the Bounds (Hoehe +-5%)
if n_ok:
    zc = CC[:, 2]
    keep_isl = np.zeros(n_isl, bool)
    keep_isl[int(np.argmax(np.where(OK, zc, -1e9)))] = True
    keep_isl[int(np.argmin(np.where(OK, zc, 1e9)))] = True

def leaves(target, maxf, name):
    k = min(1.0, (target / 2) / max(n_ok, 1)); f = min(maxf, 1.0 / math.sqrt(k))
    ids = np.nonzero(OK & ((np.random.default_rng(7).random(n_isl) < k) | keep_isl))[0]
    v = (CC[ids][:, None, :] + (CV[ids] - CC[ids][:, None, :]) * f).reshape(-1, 3)
    m = bpy.data.meshes.new(name)
    m.from_pydata(v.tolist(), [], [(4 * j, 4 * j + 1, 4 * j + 2, 4 * j + 3) for j in range(len(ids))])
    uvl_ = m.uv_layers.new(name="UVMap"); uvl_.data.foreach_set("uv", CU[ids].reshape(-1, 2).ravel())
    m.materials.append(LEAFMAT)
    o = bpy.data.objects.new(name, m); sc.collection.objects.link(o); return o

def islands(o):
    me = o.data; n = len(me.vertices)
    ed = np.empty(len(me.edges) * 2, dtype=np.int64); me.edges.foreach_get("vertices", ed); ed = ed.reshape(-1, 2)
    lb = np.arange(n)
    while True:
        ol = lb.copy(); mm = np.minimum(lb[ed[:, 0]], lb[ed[:, 1]])
        np.minimum.at(lb, ed[:, 0], mm); np.minimum.at(lb, ed[:, 1], mm); lb = lb[lb]
        if np.array_equal(lb, ol): break
    u, vl = np.unique(lb, return_inverse=True)
    lp = np.empty(len(me.loops), dtype=np.int64); me.loops.foreach_get("vertex_index", lp)
    st = np.empty(len(me.polygons), dtype=np.int64); me.polygons.foreach_get("loop_start", st)
    return vl[lp[st]], len(u)

def prune(o, target):
    # kleinste Inseln (Zweige) entfernen, bis die Dreieckszahl <= target ist
    fl, n = islands(o)
    tri = np.array([len(p.vertices) - 2 for p in o.data.polygons])
    size = np.bincount(fl, tri, n); order = np.argsort(-size)
    keep = np.zeros(n, bool); keep[order[np.cumsum(size[order]) <= target]] = True; keep[order[0]] = True
    # Inseln containing the highest/lowest Vertices always keep (Branch-Tips = Hoehe)
    vz = np.array([v.co[2] for v in o.data.vertices])
    flv, _ = islands(o)
    keep[np.unique(flv[[int(np.argmax(vz)), int(np.argmin(vz))]])] = True
    bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in np.nonzero(~keep[fl])[0]], context="FACES")
    bm.to_mesh(o.data); bm.free()
    return o

lods = []
for i, (tw, tlv, mf) in enumerate([(B[0], B[1], 1.6), (B[2], B[3], 2.5)]):
    w = decimate(copy(W, "w%d" % i), tw)
    if tris(w) > 1.3 * tw: w = prune(w, tw)
    l = leaves(tlv, mf, "l%d" % i)
    o = join([w, l]); o.name = o.data.name = "%s_LOD%d" % (tid, i)
    cap = (90000, 25000)[i]
    if tris(o) > cap:  # Join can overshoot the target (leaf Quads not counted in tw) -> final decimate to cap
        m = o.modifiers.new("d", "DECIMATE"); m.ratio = cap / tris(o)
        sel([o], o); bpy.ops.object.modifier_apply(modifier="d")
    lods.append(o)

# Basis auf 0, Stammmitte auf x/y = 0
wc = np.array([v.co[:] for v in W.data.vertices]); zmin = wc[:, 2].min()
low = wc[wc[:, 2] < zmin + 0.05 * H0]; cx, cy = low[:, 0].mean(), low[:, 1].mean()
for o in lods: o.data.transform(Matrix.Translation((-cx, -cy, -zmin)))
_wc = np.array([v.co[:] for v in W.data.vertices]); _lc = np.array([v.co[:] for v in L.data.vertices])
print("DEBUG W_h", _wc[:, 2].max() - _wc[:, 2].min(), "L_h", _lc[:, 2].max() - _lc[:, 2].min(), "H0", H0)
bpy.data.objects.remove(W); bpy.data.objects.remove(L)


def dims(o):
    c = np.array([v.co[:] for v in o.data.vertices])
    return c[:, 2].max() - c[:, 2].min(), max(np.ptp(c[:, 0]), np.ptp(c[:, 1]))
t0, t1 = tris(lods[0]), tris(lods[1]); h, wd = dims(lods[0])
print(f"{tid}: LOD0 {t0}, LOD1 {t1}, Hoehe {h:.2f}, Breite {wd:.2f}, Blaetter {n_isl} (Karten {n_ok})")
print(f"{t0} -> LOD0 <= 90000 -> {'OK' if t0 <= 90000 else 'NOK'}")
print(f"{t1} -> LOD1 <= 25000 -> {'OK' if t1 <= 25000 else 'NOK'}")
print(f"{h:.2f}/{H0:.2f} -> Hoehe +-5% -> {'OK' if abs(h - H0) <= 0.05 * H0 else 'NOK'}")

sel(lods, lods[0])
bpy.ops.export_scene.fbx(filepath=os.path.join(out, tid + ".fbx"), use_selection=True,
    apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
    mesh_smooth_type="FACE", path_mode="STRIP", use_mesh_modifiers=True)

# Vorschau: LOD0 links, LOD1 rechts
lods[1].location.x = 1.2 * wd
wl = bpy.data.worlds.new("W"); sc.world = wl; wl.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.8, 0.85, 1)
sd = bpy.data.lights.new("Sun", "SUN"); sd.energy = 4.0
so = bpy.data.objects.new("Sun", sd); so.rotation_euler = (math.radians(45), 0, math.radians(45)); sc.collection.objects.link(so)
cd = bpy.data.cameras.new("Cam"); cd.type = "ORTHO"; cd.ortho_scale = 1.1 * max(2.2 * wd, 2 * h)
d = 3 * max(h, wd); cd.clip_end = 10 * d
co_ = bpy.data.objects.new("Cam", cd); co_.location = (0.6 * wd, -d, h / 2); co_.rotation_euler = (math.radians(90), 0, 0)
sc.collection.objects.link(co_); sc.camera = co_
try: sc.render.engine = "BLENDER_EEVEE_NEXT"
except Exception:
    sc.render.engine = "CYCLES"; sc.cycles.samples = 16
sc.render.resolution_x, sc.render.resolution_y = 800, 400
sc.render.filepath = os.path.join(out, tid + "_LODs.png")
bpy.ops.render.render(write_still=True)
print("fertig")
