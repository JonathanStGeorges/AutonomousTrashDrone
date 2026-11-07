import bpy,json,pathlib
from mathutils import Vector
project=pathlib.Path(__file__).resolve().parents[2]
paths=list((project/'Assets/TrashSailDemo/Imported').glob('*/*.fbx'))+[project/'Assets/Models/marus_boat/marus_boat_model.fbx']
report=[]
for path in paths:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=str(path))
 meshes=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
 corners=[obj.matrix_world@Vector(c) for obj in meshes for c in obj.bound_box]
 low=[min(c[i] for c in corners) for i in range(3)]
 high=[max(c[i] for c in corners) for i in range(3)]
 report.append({'asset':path.stem,'path':str(path.relative_to(project)),'blenderZupMin':low,'blenderZupMax':high,'dimensions':[high[i]-low[i] for i in range(3)],'vertices':sum(len(obj.data.vertices) for obj in meshes),'polygons':sum(len(obj.data.polygons) for obj in meshes)})
(project/'Assets/TrashSailDemo/Imported/source-bounds.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
