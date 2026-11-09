"""2층 창고 모듈 생성기 (Blender 5.x).

실행: blender --background --python game/Tools/Blender/warehouse_modules.py -- <출력 폴더>

각 모듈은 바닥 중앙이 원점이고, Unity 기준 +Z가 '앞'(계단·경사로가 올라가는 방향)이다.
충돌은 Unity가 단순 박스로 따로 만든다. 여기서 만든 메시는 보이는 모양만 담당한다.
"""
import math
import os
import sys

import bpy

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.join(os.getcwd(), "warehouse")
os.makedirs(OUT, exist_ok=True)

PALETTE = {
    "concrete": (0.55, 0.55, 0.52),
    "line": (0.95, 0.78, 0.18),
    "metal": (0.36, 0.45, 0.52),
    "metal_dark": (0.24, 0.30, 0.36),
    "wood": (0.66, 0.46, 0.27),
    "wood_dark": (0.45, 0.30, 0.17),
    "safety": (0.98, 0.72, 0.10),
    "rubber": (0.18, 0.18, 0.20),
    "rack": (0.85, 0.36, 0.16),
    "cardboard": (0.72, 0.56, 0.36),
}


def material(name):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (*PALETTE[name], 1)
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (*PALETTE[name], 1)
            bsdf.inputs["Roughness"].default_value = 0.85
    return mat


def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for block in (bpy.data.meshes, bpy.data.materials):
        for item in list(block):
            block.remove(item)


def box(parts, mat, center, size, rot_x=0.0):
    """center/size는 Unity 축(x 오른쪽, y 위, z 앞) 미터 단위."""
    x, y, z = center
    sx, sy, sz = size
    # Unity (x, y, z) -> Blender (x, z, y) 로 바꿔 놓고, 내보낼 때 축 변환으로 되돌린다.
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, z, y))
    obj = bpy.context.active_object
    obj.scale = (sx, sz, sy)
    obj.rotation_euler = (rot_x, 0, 0)
    obj.data.materials.append(material(mat))
    parts.append(obj)
    return obj


def cylinder(parts, mat, center, radius, height):
    x, y, z = center
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=radius, depth=height, location=(x, z, y))
    obj = bpy.context.active_object
    obj.data.materials.append(material(mat))
    parts.append(obj)


def export(name, parts):
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.shade_flat()
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="Z",
        axis_up="Y",
        bake_space_transform=True,
        object_types={"MESH"},
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
    )
    print("exported", path)
    bpy.ops.object.delete()


def floor_tile():
    p = []
    box(p, "concrete", (0, -0.1, 0), (4, 0.2, 4))
    # 바닥 이음매와 안전선: 위로 1cm만 올라온 얇은 띠라 걸려 넘어지지 않는다.
    box(p, "metal_dark", (0, 0.002, 1.99), (4, 0.01, 0.03))
    box(p, "metal_dark", (1.99, 0.002, 0), (0.03, 0.01, 4))
    box(p, "line", (-1.6, 0.003, 0), (0.12, 0.01, 3.2))
    export("WH_Floor", p)


def wall_panel():
    p = []
    box(p, "metal", (0, 3, 0), (4, 6, 0.2))
    # 골판 무늬
    for i in range(10):
        box(p, "metal_dark", (-1.8 + i * 0.4, 3, 0.12), (0.08, 5.6, 0.06))
    box(p, "concrete", (0, 0.4, 0.05), (4, 0.8, 0.32))
    box(p, "metal_dark", (0, 5.9, 0.05), (4, 0.2, 0.3))
    export("WH_Wall", p)


def stair():
    """2m 폭, 3.5m 높이, 21칸(칸 높이 1/6m, 깊이 0.4m). 앞(+Z)으로 올라간다."""
    p = []
    steps, rise, run, width = 21, 3.5 / 21, 0.4, 2.0
    length = steps * run
    for i in range(steps):
        h = (i + 1) * rise
        z = -length / 2 + run * (i + 0.5)
        box(p, "wood", (0, h - 0.03, z), (width - 0.1, 0.06, run - 0.02))
        box(p, "safety", (0, h - 0.005, z + run / 2 - 0.04), (width - 0.12, 0.012, 0.06))
        box(p, "wood_dark", (0, (h - 0.06) / 2, z), (width - 0.14, max(0.02, h - 0.06), run - 0.06))
    # 옆판(stringer)
    angle = math.atan2(3.5, length)
    for side in (-1, 1):
        box(p, "metal_dark", (side * (width / 2 - 0.03), 1.75, 0), (0.06, 0.25, math.hypot(length, 3.5)), rot_x=angle)
    export("WH_Stair", p)


def ramp():
    """3m 폭, 12m 길이, 3.5m 높이 경사로. 앞(+Z)으로 올라간다."""
    p = []
    length, rise, width = 12.0, 3.5, 3.0
    angle = math.atan2(rise, length)
    slope = math.hypot(length, rise)
    box(p, "metal", (0, rise / 2 - 0.1, 0), (width, 0.2, slope), rot_x=angle)
    # 미끄럼 방지 줄무늬
    for i in range(12):
        t = (i + 0.5) / 12
        box(p, "rubber", (0, rise * t + 0.005, -length / 2 + length * t), (width - 0.3, 0.02, 0.12), rot_x=angle)
    # 아래가 막힌 옆판: 충돌도 꽉 찬 쐐기라 봇 경로 격자가 경사면을 바닥 하나로 본다.
    import bmesh
    for side in (-1, 1):
        x = side * (width / 2 - 0.04)
        mesh = bpy.data.meshes.new("skirt")
        bm = bmesh.new()
        # Blender 좌표 (x, unity z, unity y)
        verts = [bm.verts.new(v) for v in ((x, -length / 2, 0), (x, length / 2, 0), (x, length / 2, rise - 0.05))]
        bm.faces.new(verts)
        bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.08)
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new("skirt", mesh)
        bpy.context.collection.objects.link(obj)
        obj.data.materials.append(material("metal_dark"))
        p.append(obj)
    for side in (-1, 1):
        box(p, "safety", (side * (width / 2 - 0.05), rise / 2 + 0.08, 0), (0.1, 0.16, slope), rot_x=angle)
    export("WH_Ramp", p)


def railing():
    """4m 길이, 1m 높이 안전 난간."""
    p = []
    for x in (-1.95, -0.65, 0.65, 1.95):
        box(p, "safety", (x, 0.5, 0), (0.07, 1.0, 0.07))
    box(p, "safety", (0, 1.0, 0), (4, 0.07, 0.08))
    box(p, "safety", (0, 0.55, 0), (4, 0.05, 0.05))
    box(p, "metal_dark", (0, 0.06, 0), (4, 0.12, 0.05))
    export("WH_Railing", p)


def crate():
    """1m 나무 상자."""
    p = []
    box(p, "wood", (0, 0.5, 0), (0.94, 0.94, 0.94))
    for y in (0.05, 0.95):
        for z in (-0.47, 0.47):
            box(p, "wood_dark", (0, y, z), (1.0, 0.1, 0.06))
        for x in (-0.47, 0.47):
            box(p, "wood_dark", (x, y, 0), (0.06, 0.1, 1.0))
    for x in (-0.47, 0.47):
        for z in (-0.47, 0.47):
            box(p, "wood_dark", (x, 0.5, z), (0.08, 1.0, 0.08))
    box(p, "wood_dark", (0, 0.5, 0.48), (1.25, 0.08, 0.04)).rotation_euler[1] = math.radians(45)
    export("WH_Crate", p)


def rack_block():
    """2층 바닥을 받치는 4m x 4m x 3.5m 화물칸 덩어리. 앞면은 팔레트 선반처럼 보인다."""
    p = []
    box(p, "metal_dark", (0, 1.75, 0), (4, 3.5, 4))
    for x in (-1.95, 0, 1.95):
        box(p, "rack", (x, 1.75, 2.02), (0.12, 3.5, 0.08))
    for y in (0.1, 1.25, 2.4):
        box(p, "rack", (0, y, 2.02), (4, 0.1, 0.1))
        for x in (-1.0, 1.0):
            box(p, "cardboard", (x + 0.1, y + 0.45, 1.7), (1.4, 0.8, 0.6))
    box(p, "safety", (0, 3.45, 2.02), (4, 0.1, 0.1))
    export("WH_RackBlock", p)


clear()
for build in (floor_tile, wall_panel, stair, ramp, railing, crate, rack_block):
    build()
print("done")
