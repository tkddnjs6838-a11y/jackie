# Set-dressing kits for the five design maps. Run headless:
#   blender -b --factory-startup -P Art/Blender/map_kits.py -- <Assets/Design/Environment> <Art/Blender> [kit ...]
# Each kit exports one FBX per object into <env>/<MapXX_Name>/Models and saves <Art/Blender>/<Kit>Kit.blend.
# Everything is rebuilt from code with a fixed seed per kit, so re-running gives the same meshes.
# Materials whose name contains "Glow" are made emissive on the Unity side (MapBuilder.Model).
import bpy, bmesh, math, random, sys, os
from mathutils import Matrix, Vector, Euler

args = sys.argv[sys.argv.index("--") + 1:]
ENV_ROOT, BLEND_DIR = args[0], args[1]
KITS = {"meadow": "Map01_Meadow", "canyon": "Map02_Canyon", "lake": "Map03_WaxLake",
        "plateau": "Map04_CrystalPlateau", "crater": "Map05_Crater"}
WANTED = args[2:] or list(KITS)

R = random.uniform


# ---------- helpers ----------

def lin(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]


def mat(name, hexcol, rough=0.85):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    c = lin(hexcol)
    m.diffuse_color = (*c, 1)
    try:
        m.use_nodes = True
    except Exception:
        pass
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*c, 1)
    bsdf.inputs["Roughness"].default_value = rough
    return m


def obj(name, mats):
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    for m in mats:
        me.materials.append(m)
    return ob


def tag(verts, mi):
    for f in {f for v in verts for f in v.link_faces}:
        f.material_index = mi


def place(bm, verts, base=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    m = Matrix.Translation(base) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1))
    bmesh.ops.transform(bm, matrix=m, verts=verts)
    return verts


def jitter(verts, amount):
    for v in verts:
        v.co += Vector((R(-amount, amount), R(-amount, amount), R(-amount, amount)))
    return verts


def box(bm, center, size, mi, rot=(0, 0, 0)):
    """Box with rounded edges: hard 90° corners read as stacked blocks, a small bevel catches the light."""
    verts = bmesh.ops.create_cube(bm, size=1.0)["verts"]
    place(bm, verts, center, rot, size)
    tag(verts, mi)
    edges = list({e for v in verts for e in v.link_edges})
    bmesh.ops.bevel(bm, geom=verts + edges, offset=min(size) * 0.18, segments=2, profile=0.5,
                    affect="EDGES", clamp_overlap=True)


def blob(bm, center, scale, mi, subdiv=2, amount=0.12):
    verts = bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)["verts"]
    for v in verts:
        v.co *= 1 + R(-amount, amount)
    place(bm, verts, center, (0, 0, 0), scale)
    tag(verts, mi)
    return verts


def cyl(bm, r1, r2, h, segs, mi, base=(0, 0, 0), rot=(0, 0, 0)):
    """Cylinder/cone standing on its base point (z = 0 at the bottom)."""
    verts = bmesh.ops.create_cone(bm, cap_ends=True, segments=segs, radius1=r1, radius2=r2, depth=h)["verts"]
    bmesh.ops.translate(bm, verts=verts, vec=Vector((0, 0, h / 2)))
    place(bm, verts, base, rot)
    tag(verts, mi)
    return verts


SHARP = math.radians(40)


def finish(ob, bm, smooth=False, auto=False):
    """smooth shades every face round. auto keeps only creases sharper than SHARP hard, so gentle
    curves (hills, bevels, many-sided cones) shade round while real corners keep an edge.
    Neither: flat faceted shading (rocks and crystals are low-poly on purpose)."""
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if auto:
        for e in bm.edges:
            e.smooth = e.is_manifold and e.calc_face_angle(0) < SHARP
    bm.to_mesh(ob.data)
    bm.free()
    for p in ob.data.polygons:
        p.use_smooth = smooth or auto
    return ob


def backdrop(name, mats, base, scatter, count, rmin=50, rmax=72):
    """Ring of hills from r=46 out to 150 m around the 96 m terrain, plus `count` silhouettes from `scatter`.
    mats[0] near ring, mats[1] far ring, the rest belong to the scatter function."""
    ob = obj(name, mats)
    bm = bmesh.new()
    A = 120
    radii = [46, 54, 64, 78, 96, 120, 150]

    def height(ri, a):
        t = a / A * math.tau
        h = base[ri] + 2.5 * math.sin(t * 3 + ri) + 1.8 * math.sin(t * 7.3 + 1.7 * ri) + 1.2 * math.sin(t * 13.1 + 0.4)
        return h * (1.4 if ri >= 4 else 1.0)

    grid = [[bm.verts.new((math.cos(a / A * math.tau) * r, math.sin(a / A * math.tau) * r, height(ri, a)))
             for a in range(A)] for ri, r in enumerate(radii)]
    for ri in range(len(radii) - 1):
        for a in range(A):
            b = (a + 1) % A
            f = bm.faces.new((grid[ri][a], grid[ri][b], grid[ri + 1][b], grid[ri + 1][a]))
            f.material_index = 1 if ri >= 3 else 0
    for _ in range(count):
        a, r = R(0, math.tau), R(rmin, rmax)
        ri = next(i for i, rr in enumerate(radii) if rr >= r)
        frac = (r - radii[ri - 1]) / (radii[ri] - radii[ri - 1])
        ai = int(a / math.tau * A) % A
        z = height(ri - 1, ai) * (1 - frac) + height(ri, ai) * frac
        scatter(bm, Vector((math.cos(a) * r, math.sin(a) * r, z - 0.5)))
    return finish(ob, bm, auto=True)


# ---------- meadow ----------

def kit_meadow():
    out = []
    ob = obj("Meadow_Bridge", [mat("Wood_Plank", "#c39363"), mat("Wood_Beam", "#7d5634")])
    bm = bmesh.new()
    L, W, n = 11.0, 3.6, 15
    pitch = L / n
    for i in range(n):
        x = -L / 2 + pitch * (i + 0.5)
        box(bm, (x, R(-0.05, 0.05), 0.07 + R(0, 0.025)), (pitch * 0.86, W + R(-0.12, 0.12), 0.12), 0,
            (math.radians(R(-1.2, 1.2)), 0, math.radians(R(-2, 2))))
    for s in (-1, 1):
        box(bm, (0, s * (W / 2 - 0.35), -0.12), (L + 0.4, 0.26, 0.26), 1)
        for i in range(5):
            box(bm, (-L / 2 + 0.3 + i * (L - 0.6) / 4, s * (W / 2 + 0.02), 0.55), (0.2, 0.2, 1.1), 1,
                (math.radians(R(-2, 2)), math.radians(R(-2, 2)), 0))
        box(bm, (0, s * (W / 2 + 0.02), 1.02), (L - 0.2, 0.14, 0.14), 1)
        box(bm, (0, s * (W / 2 + 0.02), 0.6), (L - 0.4, 0.09, 0.09), 1)
    # entry ramps: the deck top (~0.16 m) is taller than the 0.72-scale player's step offset (~0.22 m
    # minus skin), so each end gets a 1.2 m wedge down to the ground
    top, run, hw = 0.16, 1.2, W / 2
    for s in (-1, 1):
        x0, x1 = s * L / 2, s * (L / 2 + run)
        v = [bm.verts.new(c) for c in [(x0, -hw, 0), (x0, hw, 0), (x0, hw, top), (x0, -hw, top), (x1, -hw, 0), (x1, hw, 0)]]
        for face in [(v[0], v[1], v[2], v[3]), (v[3], v[2], v[5], v[4]), (v[0], v[3], v[4]), (v[1], v[5], v[2]), (v[0], v[4], v[5], v[1])]:
            bm.faces.new(face).material_index = 0
    out.append(finish(ob, bm, auto=True))

    # one triangle per blade (9 tris per tuft); the Unity material renders both faces ("Blade" in the name)
    ob = obj("Meadow_GrassTuft", [mat("GrassBlade", "#6cb446", 0.9)])
    bm = bmesh.new()
    for _ in range(9):
        a, r, yaw = R(0, math.tau), R(0, 0.13), R(0, math.tau)
        c = Vector((math.cos(a) * r, math.sin(a) * r, 0))
        side = Vector((math.cos(yaw), math.sin(yaw), 0)) * 0.035
        tip = c + Vector((R(-0.12, 0.12), R(-0.12, 0.12), R(0.3, 0.55)))
        bm.faces.new((bm.verts.new(c + side), bm.verts.new(c - side), bm.verts.new(tip)))
    bm.to_mesh(ob.data)
    bm.free()
    out.append(ob)

    for k, stone_col in enumerate(["#8e979c", "#a2a9a5", "#7d8890"]):
        ob = obj(f"Meadow_Stone{k}", [mat(f"Stone{k}", stone_col, 0.7), mat("Moss", "#6f8a5a")])
        bm = bmesh.new()
        sx, sy, sz = R(0.7, 1.0), R(0.55, 0.85), R(0.35, 0.5)
        blob(bm, (0, 0, sz * 0.7), (sx, sy, sz), 0)
        for f in bm.faces:
            if f.normal.z > 0.75 and f.calc_center_median().z > sz * 1.05:
                f.material_index = 1
        out.append(finish(ob, bm, smooth=True))

    # low-poly on purpose (~160 tris): star-shaped petal plate + 4-sided core + 3-sided stem, flat leaf triangles
    for k, petal in enumerate(["#ff8fb1", "#ffd95e", "#fff3e6"]):
        ob = obj(f"Meadow_Flowers{k}", [mat("Stem", "#4c8f37"), mat(f"Petal{k}", petal, 0.6), mat("FlowerCore", "#ffb627", 0.6)])
        bm = bmesh.new()
        for _ in range(3):
            a = R(0, math.tau)
            d = Vector((math.cos(a), math.sin(a), 0))
            side = Vector((-d.y, d.x, 0)) * 0.05
            f = bm.faces.new((bm.verts.new(side), bm.verts.new(-side), bm.verts.new(d * 0.3 + Vector((0, 0, 0.08)))))
            f.material_index = 0
            f.normal_update()
            if f.normal.z < 0:
                f.normal_flip()
        for _ in range(7):
            a, r, h = R(0, math.tau), R(0, 0.28), R(0.22, 0.45)
            x, y = math.cos(a) * r, math.sin(a) * r
            cyl(bm, 0.012, 0.01, h, 3, 0, (x, y, 0))
            spin = R(0, math.tau)
            star = [bm.verts.new((x + math.cos(spin + i * math.pi / 5) * (0.07 if i % 2 == 0 else 0.03),
                                  y + math.sin(spin + i * math.pi / 5) * (0.07 if i % 2 == 0 else 0.03), h + 0.005))
                    for i in range(10)]
            petal = bm.faces.new(star)  # counter-clockwise from above, so it faces up
            petal.material_index = 1
            cyl(bm, 0.022, 0.0, 0.025, 4, 2, (x, y, h))
        # no finish(): recalc_face_normals can flip the open single-sided plates downward
        bm.to_mesh(ob.data)
        bm.free()
        out.append(ob)

    def pine(bm, p):
        h = R(4, 7.5)
        cyl(bm, 0.28, 0.22, 1.6, 6, 3, p)
        r = R(1.4, 2.1)
        for start, width, height in ((0.0, 1.0, 0.55), (0.3, 0.78, 0.45), (0.58, 0.55, 0.42)):  # stacked tiers
            cyl(bm, r * width, 0, h * height, 10, 2, p + Vector((0, 0, 1.0 + h * start)))

    out.append(backdrop("Meadow_Backdrop", [mat("Hill", "#5f9f45"), mat("Ridge", "#6e9b84"), mat("PineLeaf", "#3c7a3a"), mat("PineTrunk", "#6b4a2f")],
                        [-1, 5, 10, 16, 24, 28, 18], pine, 140))
    return out


# ---------- canyon: strata rocks, mesas, cacti, dry shrubs ----------

def kit_canyon():
    out = []
    strata = [mat("RockA", "#a96d45"), mat("RockB", "#c58a5a"), mat("RockTop", "#d9a873")]

    def mesa(bm, p, radius, height, layers=4, off=0):
        z = 0.0
        for i in range(layers):
            lh = height / layers * R(0.8, 1.2)
            r = radius * (1 - i * 0.07)
            v = cyl(bm, r * R(1.0, 1.08), r, lh, 9, off + i % 2, p + Vector((0, 0, z)))
            jitter(v, radius * 0.06)
            z += lh
        cyl(bm, radius * 0.75, radius * 0.7, 0.25, 9, off + 2, p + Vector((0, 0, z)))

    for k in range(3):
        ob = obj(f"Canyon_Rock{k}", strata)
        bm = bmesh.new()
        for _ in range(random.randint(2, 4)):
            v = blob(bm, (R(-0.8, 0.8), R(-0.8, 0.8), R(0.4, 0.8)), (R(0.8, 1.4), R(0.7, 1.2), R(0.6, 1.1)), k % 2, 1, 0.18)
        out.append(finish(ob, bm))

    for k in range(2):
        ob = obj(f"Canyon_Pillar{k}", strata)
        bm = bmesh.new()
        mesa(bm, Vector((0, 0, -0.3)), R(1.2, 1.8), R(4, 7), 5)
        out.append(finish(ob, bm))

    cactus_mats = [mat("Cactus", "#5b9a4a"), mat("CactusFlower", "#ff7aa8", 0.6)]
    for k in range(2):
        ob = obj(f"Canyon_Cactus{k}", cactus_mats)
        bm = bmesh.new()
        h = R(2.2, 3.2)
        cyl(bm, 0.32, 0.3, h, 8, 0)
        blob(bm, (0, 0, h), (0.3, 0.3, 0.22), 0, 1, 0.02)
        for s in ([-1, 1] if k == 0 else [1]):
            ah, al, up = R(0.9, 1.6), R(0.45, 0.65), R(0.6, 1.1)
            cyl(bm, 0.18, 0.18, al, 8, 0, (0, 0, ah), (0, s * math.pi / 2, 0))
            cyl(bm, 0.18, 0.17, up, 8, 0, (s * al, 0, ah - 0.1))
            blob(bm, (s * al, 0, ah - 0.1 + up), (0.17, 0.17, 0.13), 0, 1, 0.02)
        blob(bm, (0, 0, h + 0.2), (0.12, 0.12, 0.08), 1, 1, 0.05)
        out.append(finish(ob, bm, smooth=True))

    ob = obj("Canyon_Shrub", [mat("DryShrub", "#8f8a4c"), mat("DryShrubDark", "#6d6a3a")])
    bm = bmesh.new()
    for i in range(7):
        blob(bm, (R(-0.5, 0.5), R(-0.5, 0.5), R(0.2, 0.5)), (R(0.25, 0.45),) * 2 + (R(0.2, 0.35),), i % 2, 1, 0.2)
    out.append(finish(ob, bm))

    out.append(backdrop("Canyon_Backdrop", [mat("DuneNear", "#c48a5c"), mat("DuneFar", "#d6a67c"), *strata],
                        [-1, 8, 13, 17, 21, 24, 18],
                        lambda bm, p: mesa(bm, p, R(3, 7), R(8, 18), 4, 2), 46))
    return out


# ---------- wax lake: giant melted candles, floating wax pads ----------

def candle(bm, p, radius, height, body_mi, flame_mi, wick_mi, drips=6):
    v = cyl(bm, radius, radius * 0.97, height, 16, body_mi, p)
    jitter(v, radius * 0.02)
    for i in range(drips):
        a = i / drips * math.tau + R(-0.3, 0.3)
        length = R(0.25, 0.7) * height
        dr = radius * R(0.1, 0.17)
        x, y = math.cos(a) * radius * 0.97, math.sin(a) * radius * 0.97
        cyl(bm, dr, dr * 0.9, length, 6, body_mi, p + Vector((x, y, height - length)))
        blob(bm, p + Vector((x, y, height - length)), (dr * 1.3,) * 3, body_mi, 1, 0.05)
    blob(bm, p + Vector((0, 0, height)), (radius * 1.02, radius * 1.02, radius * 0.18), body_mi, 2, 0.04)
    cyl(bm, radius * 0.05, radius * 0.04, radius * 0.35, 5, wick_mi, p + Vector((0, 0, height + radius * 0.1)))
    blob(bm, p + Vector((0, 0, height + radius * 0.55)), (radius * 0.14, radius * 0.14, radius * 0.32), flame_mi, 1, 0.02)


def kit_lake():
    out = []
    flame, wick = mat("FlameGlow", "#ffb43a", 0.5), mat("Wick", "#2b2220")
    for k, col in enumerate(["#ffb3c7", "#b8ead6", "#fff0a8"]):
        ob = obj(f"Lake_Candle{k}", [mat(f"Wax{k}", col, 0.45), flame, wick])
        bm = bmesh.new()
        candle(bm, Vector((0, 0, -0.4)), 1.0, [5.5, 3.8, 2.4][k], 0, 1, 2)
        out.append(finish(ob, bm, smooth=True))

    for k, col in enumerate(["#ffc9d8", "#d9c8ff"]):
        ob = obj(f"Lake_WaxPad{k}", [mat(f"PadWax{k}", col, 0.4)])
        bm = bmesh.new()
        blob(bm, (0, 0, 0), (R(1.6, 2.2), R(1.3, 1.8), 0.18), 0, 2, 0.08)
        for _ in range(3):
            blob(bm, (R(-1.8, 1.8), R(-1.5, 1.5), 0), (R(0.25, 0.45),) * 2 + (0.1,), 0, 1, 0.1)
        out.append(finish(ob, bm, smooth=True))

    wax_cols = [mat("BigWax0", "#ffb3c7", 0.45), mat("BigWax1", "#b8ead6", 0.45), mat("BigWax2", "#fff0a8", 0.45), mat("BigWax3", "#d9c8ff", 0.45)]

    def giant(bm, p):
        candle(bm, p, R(1.6, 3.2), R(8, 22), random.randrange(2, 6), 6, 7, 5)

    out.append(backdrop("Lake_Backdrop", [mat("ShoreNear", "#a5d68f"), mat("ShoreFar", "#cdb6dd"), *wax_cols, flame, wick],
                        [-1, 3, 6, 9, 13, 15, 11], giant, 44))
    return out


# ---------- crystal plateau: crystal clusters, ruined columns, spires ----------

def crystal(bm, p, radius, height, tilt, mi):
    verts = cyl(bm, radius, radius * 0.92, height, 6, mi) + cyl(bm, radius * 0.92, 0, radius * 1.8, 6, mi, (0, 0, height))
    place(bm, verts, p, (tilt[0], tilt[1], R(0, math.tau)))


def kit_plateau():
    out = []
    rock = mat("PlateauRock", "#7d68b8")
    for k, col in enumerate(["#c9a7ff", "#8fe3f0", "#ff9fd0"]):
        ob = obj(f"Plateau_Crystals{k}", [rock, mat(f"CrystalGlow{k}", col, 0.2)])
        bm = bmesh.new()
        blob(bm, (0, 0, 0.1), (1.1, 0.9, 0.45), 0, 1, 0.15)
        crystal(bm, Vector((0, 0, 0)), 0.38, R(1.6, 2.4), (0, 0), 1)
        for _ in range(6):
            a = R(0, math.tau)
            crystal(bm, Vector((math.cos(a) * 0.45, math.sin(a) * 0.45, 0)), R(0.15, 0.28), R(0.5, 1.3),
                    (math.cos(a + math.pi / 2) * R(0.3, 0.6), math.sin(a + math.pi / 2) * R(0.3, 0.6)), 1)
        out.append(finish(ob, bm))

    stone = mat("RuinStone", "#bdb2e2", 0.8)
    for k in range(2):
        ob = obj(f"Plateau_Column{k}", [stone])
        bm = bmesh.new()
        box(bm, (0, 0, 0.2), (1.4, 1.4, 0.4), 0)
        h = R(3.2, 4.2) if k == 0 else R(1.4, 2.2)
        jitter(cyl(bm, 0.5, 0.47, h, 10, 0, (0, 0, 0.4)), 0.03)
        if k == 0:
            box(bm, (0, 0, h + 0.55), (1.2, 1.2, 0.3), 0)
        else:
            v = cyl(bm, 0.48, 0.46, 1.4, 10, 0, (1.4, 0.3, 0.45), (math.pi / 2, 0, R(0, math.tau)))
            jitter(v, 0.04)
        out.append(finish(ob, bm, auto=True))

    glow = mat("SpireCrystalGlow", "#c9a7ff", 0.2)

    def spire(bm, p):
        h = R(10, 26)
        v = cyl(bm, R(2.5, 4.5), R(0.3, 0.8), h, 5, 2, p)
        jitter(v, 0.6)
        if random.random() < 0.6:
            crystal(bm, p + Vector((R(-1, 1), R(-1, 1), h * R(0.3, 0.6))), R(0.6, 1.1), R(2, 4), (R(-0.5, 0.5), R(-0.5, 0.5)), 3)

    out.append(backdrop("Plateau_Backdrop", [mat("PlateauNear", "#6f5aa8"), mat("PlateauFar", "#8c7cc4"), mat("SpireRock", "#5d4a96"), glow],
                        [-1, 9, 15, 21, 27, 30, 22], spire, 70))
    return out


# ---------- crater: basalt columns, glowing vents, wax stalagmites, volcano peaks ----------

def kit_crater():
    out = []
    basalt, basalt_top, lava = mat("Basalt", "#3b2b2b"), mat("BasaltTop", "#5a4038"), mat("LavaGlow", "#ff8a2a", 0.4)
    for k in range(3):
        ob = obj(f"Crater_Basalt{k}", [basalt, basalt_top])
        bm = bmesh.new()
        for i in range(random.randint(4, 7)):
            a, r = R(0, math.tau), R(0, 0.7)
            h = R(0.6, 2.6)
            v = cyl(bm, R(0.32, 0.45), R(0.3, 0.42), h, 6, 0, (math.cos(a) * r, math.sin(a) * r, -0.2))
            for f in {f for vv in v for f in vv.link_faces}:
                if f.normal.z > 0.9:
                    f.material_index = 1
        out.append(finish(ob, bm))

    ob = obj("Crater_Vent", [basalt, lava])
    bm = bmesh.new()
    jitter(cyl(bm, 1.3, 0.55, 0.9, 10, 0, (0, 0, -0.1)), 0.08)
    blob(bm, (0, 0, 0.78), (0.48, 0.48, 0.08), 1, 1, 0.02)
    out.append(finish(ob, bm))

    ob = obj("Crater_Stalagmite", [mat("BurntWax", "#8a4f34"), lava])
    bm = bmesh.new()
    for i in range(3):
        a = R(0, math.tau)
        h = R(1.4, 2.8)
        p = Vector((math.cos(a) * R(0, 0.5), math.sin(a) * R(0, 0.5), -0.1))
        jitter(cyl(bm, R(0.35, 0.55), 0.06, h, 7, 0, p), 0.05)
        blob(bm, p + Vector((0, 0, h * 0.92)), (0.1, 0.1, 0.16), 1, 1, 0.02)
    out.append(finish(ob, bm))

    def volcano(bm, p):
        h, r = R(12, 30), R(7, 13)
        jitter(cyl(bm, r, r * 0.22, h, 9, 2, p), 0.9)
        blob(bm, p + Vector((0, 0, h - 0.2)), (r * 0.2, r * 0.2, 0.4), 3, 1, 0.05)

    out.append(backdrop("Crater_Backdrop", [mat("AshNear", "#4a3030"), mat("AshFar", "#5e3a32"), mat("VolcanoRock", "#3a2626"), lava],
                        [-1, 9, 15, 20, 24, 22, 16], volcano, 26, 55, 75))
    return out


# ---------- export ----------

for kit in WANTED:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    random.seed(kit)
    objects = globals()["kit_" + kit]()
    out_dir = os.path.join(ENV_ROOT, KITS[kit], "Models")
    os.makedirs(out_dir, exist_ok=True)
    for ob in objects:
        bpy.ops.object.select_all(action="DESELECT")
        ob.select_set(True)
        bpy.context.view_layer.objects.active = ob
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(out_dir, ob.name + ".fbx"),
            use_selection=True, object_types={"MESH"},
            apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
            axis_forward="-Z", axis_up="Y", bake_space_transform=True,
            mesh_smooth_type="FACE", add_leaf_bones=False)
        print("exported", ob.name, len(ob.data.polygons), "faces")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, kit.capitalize() + "Kit.blend"))
