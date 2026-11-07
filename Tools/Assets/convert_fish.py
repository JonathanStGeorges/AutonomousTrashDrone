import bpy, json, pathlib, struct, sys
root=pathlib.Path(__file__).resolve().parents[2]
project=root
asset=project/'Assets/TrashSailDemo/Imported/BarramundiFish'
source=asset/'BarramundiFish.glb'
data=source.read_bytes()
json_length,json_type=struct.unpack_from('<II',data,12)
manifest=json.loads(data[20:20+json_length])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
materials=[]
for material in bpy.data.materials:
    if not material.use_nodes: continue
    for node in material.node_tree.nodes:
        if node.type!='BSDF_PRINCIPLED': continue
        for socket,role in [('Base Color','BaseColor'),('Normal','Normal'),('Metallic','MetallicRoughness')]:
            inp=node.inputs.get(socket)
            if not inp or not inp.is_linked: continue
            upstream=inp.links[0].from_node
            candidates=[upstream]
            if upstream.type in ('NORMAL_MAP','SEPARATE_COLOR','SEPRGB'):
                candidates=[link.from_node for sock in upstream.inputs for link in sock.links]
            for candidate in candidates:
                if candidate.type=='TEX_IMAGE' and candidate.image:
                    image=candidate.image.copy()
                    if max(image.size)>1024:
                        factor=1024/max(image.size);image.scale(int(image.size[0]*factor),int(image.size[1]*factor))
                    image.filepath_raw=str(asset/(role+'.png'));image.file_format='PNG';image.save();candidate.image=image
                    materials.append({'material':material.name,'role':role,'image':role+'.png'})
meshes=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
from mathutils import Vector
corners=[obj.matrix_world@Vector(v) for obj in meshes for v in obj.bound_box]
minimum=[min(v[i] for v in corners) for i in range(3)]
maximum=[max(v[i] for v in corners) for i in range(3)]
summary={'source':'BarramundiFish.glb','animations':len(manifest.get('animations',[])),'skins':len(manifest.get('skins',[])),'nodes':len(manifest.get('nodes',[])),'blenderBoundsMin':minimum,'blenderBoundsMax':maximum,'vertices':sum(len(obj.data.vertices) for obj in meshes),'polygons':sum(len(obj.data.polygons) for obj in meshes),'materials':materials,'conversion':'Blender4.5.9 glTF import; FBX axis -Z forward/Y up; uniform scale1; textures max1024px'}
bpy.ops.export_scene.fbx(filepath=str(asset/'BarramundiFish.fbx'),use_selection=False,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=True,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
(asset/'conversion.json').write_text(json.dumps(summary,indent=2))
print(json.dumps(summary,indent=2))


