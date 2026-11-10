"""모래 교차로 모듈 생성기 (Blender 5.x).

실행: blender --background --python game/Tools/Blender/crossing_modules.py -- <출력 폴더>

창고 모듈과 같은 규칙: 바닥 중앙이 원점, Unity 기준 +Z가 '앞'(계단·경사로가 올라가는 방향).
충돌은 Unity가 단순 박스·쐐기로 따로 만든다. 여기서 만든 메시는 보이는 모양만 담당한다.
나무 상자는 창고의 WH_Crate를 같이 쓴다.
"""
import math
import os
import sys

import bmesh
import bpy

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.join(os.getcwd(), "crossing")
os.makedirs(OUT, exist_ok=True)

PALETTE = {
    "sand": (0.86, 0.74, 0.52),
    "sandstone": (0.80, 0.66, 0.45),
    "sandstone_dark": (0.60, 0.47, 0.31),
    "plaster": (0.93, 0.87, 0.73),
    "terracotta": (0.74, 0.39, 0.24),
    "teal": (0.16, 0.47, 0.52),
    "wood": (0.58, 0.40, 0.24),
    "wood_dark": (0.38, 0.26, 0.15),
    "cloth_red": (0.78, 0.22, 0.18),
    "cloth_cream": (0.95, 0.90, 0.78),
    "leaf": (0.30, 0.55, 0.22),
    "trunk": (0.50, 0.38, 0.25),
    "shadow": (0.20, 0.16, 0.12),
    "sandbag": (0.70, 0.62, 0.44),
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
            bsdf.inputs["Roughness"].default_value = 0.9
    return mat


def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for block in (bpy.data.meshes, bpy.data.materials):
        for item in list(block):
            block.remove(item)


def box(parts, mat, center, size, rot_x=0.0, rot_y=0.0):
    """center/size는 Unity 축(x 오른쪽, y 위, z 앞) 미터 단위. rot_y는 Unity 세로축 회전."""
    x, y, z = center
    sx, sy, sz = size
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, z, y))
    obj = bpy.context.active_object
    obj.scale = (sx, sz, sy)
    obj.rotation_euler = (rot_x, 0, rot_y)
    obj.data.materials.append(material(mat))
    parts.append(obj)
    return obj


def cylinder(parts, mat, center, radius, height, vertices=8):
    x, y, z = center
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=height, location=(x, z, y))
    obj = bpy.context.active_object
    obj.data.materials.append(material(mat))
    parts.append(obj)
    return obj


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
    """4m 모래 바닥 타일. 위로 1cm만 올라온다."""
    p = []
    box(p, "sand", (0, -0.1, 0), (4, 0.2, 4))
    box(p, "sandstone", (0, 0.004, 1.95), (4, 0.01, 0.1))
    box(p, "sandstone", (1.95, 0.004, 0), (0.1, 0.01, 4))
    box(p, "sandstone_dark", (-0.8, 0.005, 0.6), (0.9, 0.01, 0.7))
    export("SD_Floor", p)


def wall():
    """바깥 성벽 4m x 6m. 위에 톱니 모양 여장."""
    p = []
    box(p, "sandstone", (0, 2.8, 0), (4, 5.6, 0.4))
    box(p, "sandstone_dark", (0, 0.35, 0.04), (4, 0.7, 0.5))
    box(p, "plaster", (0, 3.2, 0.04), (4, 0.12, 0.46))
    for x in (-1.5, -0.5, 0.5, 1.5):
        box(p, "sandstone", (x, 5.8, 0), (0.6, 0.4, 0.4))
    export("SD_Wall", p)


def block():
    """흙벽 건물 한 칸 4m x 6m x 4m. 네 면에 창과 덧문이 있어 어느 쪽을 바깥으로 놓아도 된다."""
    p = []
    box(p, "plaster", (0, 3, 0), (4, 6, 4))
    box(p, "sandstone_dark", (0, 0.3, 0), (4.04, 0.6, 4.04))
    box(p, "terracotta", (0, 5.95, 0), (4.1, 0.1, 4.1))
    for yaw in range(4):
        a = yaw * math.pi / 2
        dx, dz = math.sin(a), math.cos(a)
        for floor_y in (1.8, 4.3):
            cx, cz = dx * 2.02, dz * 2.02
            # 창: 어두운 구멍 + 청록 덧문 두 짝
            box(p, "shadow", (cx, floor_y, cz), (0.9 if dz else 0.06, 1.1, 0.06 if dz else 0.9))
            for side in (-1, 1):
                ox, oz = (side * 0.7, 0) if dz else (0, side * 0.7)
                box(p, "teal", (cx + ox, floor_y, cz + oz), (0.45 if dz else 0.07, 1.1, 0.07 if dz else 0.45))
            box(p, "sandstone", (cx, floor_y - 0.65, cz), (1.3 if dz else 0.12, 0.12, 0.12 if dz else 1.3))
    export("SD_Block", p)


def platform():
    """거점 받침 4m x 3.5m x 4m 돌덩어리. 위에는 SD_Floor를 깐다."""
    p = []
    box(p, "sandstone", (0, 1.75, 0), (4, 3.5, 4))
    for row in range(5):
        y = 0.35 + row * 0.7
        off = 0.5 if row % 2 else 0.0
        for i in range(4):
            x = -1.5 + i + off
            if x > 2:
                continue
            for a in range(4):
                ang = a * math.pi / 2
                dx, dz = math.sin(ang), math.cos(ang)
                # 돌 이음선: 얕은 홈
                box(p, "sandstone_dark", (dx * 2.01 + (x if dz else 0), y, dz * 2.01 + (0 if dz else x)), (0.04 if dz else 0.03, 0.66, 0.03 if dz else 0.04))
        for a in range(4):
            ang = a * math.pi / 2
            dx, dz = math.sin(ang), math.cos(ang)
            box(p, "sandstone_dark", (dx * 2.01, y + 0.35, dz * 2.01), (4.02 if dz else 0.03, 0.04, 0.03 if dz else 4.02))
    export("SD_Platform", p)


def stair():
    """2m 폭, 3.5m 높이, 21칸 돌계단. 앞(+Z)으로 올라간다. 아래는 막힌 돌덩어리."""
    p = []
    steps, rise, run, width = 21, 3.5 / 21, 0.4, 2.0
    length = steps * run
    for i in range(steps):
        h = (i + 1) * rise
        z = -length / 2 + run * (i + 0.5)
        box(p, "sandstone", (0, h / 2, z), (width, h, run))
        box(p, "sandstone_dark", (0, h - 0.004, z + run / 2 - 0.03), (width - 0.04, 0.01, 0.05))
    for side in (-1, 1):
        box(p, "plaster", (side * (width / 2 + 0.06), 1.75, 0), (0.12, 0.14, math.hypot(length, 3.5)), rot_x=math.atan2(3.5, length))
    export("SD_Stair", p)


def ramp():
    """3m 폭, 12m 길이, 3.5m 높이 흙 경사로. 앞(+Z)으로 올라가고 옆면이 막혀 있다."""
    p = []
    length, rise, width = 12.0, 3.5, 3.0
    angle = math.atan2(rise, length)
    slope = math.hypot(length, rise)
    box(p, "sand", (0, rise / 2 - 0.1, 0), (width, 0.2, slope), rot_x=angle)
    for i in range(10):
        t = (i + 0.5) / 10
        box(p, "wood", (0, rise * t + 0.01, -length / 2 + length * t), (width - 0.2, 0.03, 0.16), rot_x=angle)
    for side in (-1, 1):
        x = side * (width / 2 - 0.05)
        mesh = bpy.data.meshes.new("side")
        bm = bmesh.new()
        verts = [bm.verts.new(v) for v in ((x, -length / 2, 0), (x, length / 2, 0), (x, length / 2, rise - 0.05))]
        bm.faces.new(verts)
        bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.1)
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new("side", mesh)
        bpy.context.collection.objects.link(obj)
        obj.data.materials.append(material("sandstone"))
        p.append(obj)
    export("SD_Ramp", p)


def railing():
    """4m 길이, 1m 높이 낮은 흙벽 난간."""
    p = []
    box(p, "plaster", (0, 0.45, 0), (4, 0.9, 0.3))
    box(p, "sandstone", (0, 0.95, 0), (4.04, 0.1, 0.36))
    export("SD_Railing", p)


def bridge():
    """다리 한 칸: x로 4m, z로 3m 폭. 원점은 상판 윗면 중앙이다(위로 2cm 판자)."""
    p = []
    box(p, "sandstone", (0, -0.2, 0), (4, 0.4, 3))
    for i in range(8):
        box(p, "wood", (-1.75 + i * 0.5, 0.01, 0), (0.42, 0.02, 2.9))
    box(p, "sandstone_dark", (0, -0.45, 0), (4, 0.1, 2.6))
    export("SD_Bridge", p)


def pillar():
    """다리 기둥 0.8m 각, 높이 3.1m (다리 상판 아래까지)."""
    p = []
    box(p, "sandstone", (0, 1.55, 0), (0.8, 3.1, 0.8))
    box(p, "sandstone_dark", (0, 0.15, 0), (1.0, 0.3, 1.0))
    box(p, "sandstone_dark", (0, 2.95, 0), (1.0, 0.3, 1.0))
    export("SD_Pillar", p)


def roof():
    """덮인 통로 지붕 한 칸: x로 8m, z로 4m. 들보 아래가 3.2m, 윗면 3.6m."""
    p = []
    box(p, "terracotta", (0, 3.5, 0), (8.4, 0.2, 4))
    for x in (-3, -1, 1, 3):
        box(p, "wood_dark", (x, 3.3, 0), (0.25, 0.2, 4))
    box(p, "wood_dark", (0, 3.3, 1.9), (8.4, 0.2, 0.2))
    export("SD_Roof", p)


def low_wall():
    """모래주머니 엄폐벽: 4m 길이, 1.2m 높이."""
    p = []
    for row in range(3):
        off = 0.25 if row % 2 else 0
        for i in range(8):
            x = -1.75 + i * 0.5 + off
            if x > 1.9:
                continue
            box(p, "sandbag", (x, 0.2 + row * 0.4, 0), (0.48, 0.38, 0.6))
    export("SD_LowWall", p)


def awning():
    """가게 차양: 3m 폭, 앞(+Z)으로 1.2m 나온다. 벽면에 붙이는 장식이다."""
    p = []
    for i in range(6):
        box(p, "cloth_red" if i % 2 == 0 else "cloth_cream", (-1.25 + i * 0.5, 0, 0.6), (0.5, 0.05, 1.3), rot_x=-0.35)
    box(p, "wood_dark", (0, -0.2, 1.2), (3.1, 0.06, 0.06))
    export("SD_Awning", p)


def palm():
    """야자수: 줄기 5m, 잎 6장."""
    p = []
    for i in range(6):
        cylinder(p, "trunk", (0.04 * i, 0.45 + i * 0.85, 0), 0.22 - i * 0.015, 0.9)
    for i in range(6):
        a = i * math.pi / 3
        leaf = box(p, "leaf", (math.sin(a) * 1.1, 5.1, math.cos(a) * 1.1), (0.5, 0.06, 2.2), rot_x=-0.3, rot_y=-a)
    export("SD_Palm", p)


clear()
for build in (floor_tile, wall, block, platform, stair, ramp, railing, bridge, pillar, roof, low_wall, awning, palm):
    build()
print("done")
