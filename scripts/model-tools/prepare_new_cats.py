"""Reduce and skin supplied GLBs; retarget the project's prototype clips.
Run with bpy + scipy. Source files remain unchanged. No external motion data.
"""
import bpy, bmesh, json, math, hashlib, os, struct, sys
import numpy as np
from scipy.spatial import cKDTree
from pathlib import Path
from mathutils import Matrix, Vector
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'artifacts/new-cats'
ART.mkdir(parents=True,exist_ok=True)
DOWNLOADS=Path('/Users/zak/Downloads')
CONFIG=[
 ('Ragdoll',7,DOWNLOADS/'0dbeb6b2d88e8afc02e0d4255e21b8ef.glb',0,(1.10,1.03,.85),[(0,.31,.34),(0,.39,.27),(0,.48,.22),(0,.57,.20),(0,.65,.21),(0,.69,.24)]),
 ('White',4,DOWNLOADS/'347b20c634490440591f7ae022bdb77d.glb',0,(1.15,1.0,1.08),[(0,.31,.43),(0,.44,.49),(0,.53,.58),(0,.55,.69),(0,.52,.77),(0,.47,.79)]),
 ('Black',5,DOWNLOADS/'2275b94f3443280411ad1720d3c0d705.glb',-35,(1.12,.95,.97),[(-.04,.29,.38),(-.06,.40,.45),(-.09,.50,.56),(-.115,.53,.65),(-.11,.49,.71),(-.10,.44,.74)]),
 # User replacement: Earl's round, slate-gray British Blue appearance.
 ('BritishBlue',1,DOWNLOADS/'lux3d_model_exports/textured.glb',-35,(1.20,1.08,1.02),[(0,.30,.32),(0,.35,.33),(0,.42,.34),(0,.49,.36),(0,.55,.39),(0,.59,.42)]),
 # User replacement: Mochi's current calico appearance.
 ('Calico',6,DOWNLOADS/'lux3d_model_exports/img23d_33972.glb',-65,(1.22,1.03,1.13),[(0,.30,.43),(0,.38,.49),(0,.46,.58),(0,.48,.69),(0,.43,.76),(0,.35,.78)]),
]
CLIPS=['Idle','Walk','Sit','Sleep','Eat','Paw','Groom','Stretch']
def extract_glb_textures(source,out):
 # Preserve the PBR image associations straight from the GLB. Blender 5.2 can
 # import this generator's material as a bare Principled shader, which loses
 # the source image node links even though the glTF texture table is complete.
 data=source.read_bytes()
 if data[:4]!=b'glTF':raise RuntimeError('Expected GLB source: '+str(source))
 json_length=struct.unpack_from('<I',data,12)[0]
 manifest=json.loads(data[20:20+json_length])
 binary_offset=20+json_length+8
 pbr=manifest['materials'][0]['pbrMetallicRoughness']
 maps=[('BaseColor',pbr['baseColorTexture']),('MetalRough',pbr['metallicRoughnessTexture'])]
 if 'normalTexture' in manifest['materials'][0]:maps.append(('Normal',manifest['materials'][0]['normalTexture']))
 for output,reference in maps:
  texture=manifest['textures'][reference['index']]
  image=manifest['images'][texture['source']]
  view=manifest['bufferViews'][image['bufferView']]
  start=binary_offset+view.get('byteOffset',0)
  (out/(output+'.png')).write_bytes(data[start:start+view['byteLength']])
  image_data=bpy.data.images.load(str(out/(output+'.png')),check_existing=False)
  if max(image_data.size)>2048:
   image_data.scale(2048,2048);image_data.filepath_raw=str(out/(output+'.png'));image_data.file_format='PNG';image_data.save()
 return any(output=='Normal' for output,_ in maps)
def frame_matrix(matrix,scale):
 head=Vector(matrix.translation)*scale
 y=(Vector(matrix.col[1][:3])*scale).normalized()
 x=Vector(matrix.col[0][:3])*scale;x=(x-y*x.dot(y)).normalized();z=x.cross(y).normalized()
 return Matrix(((x.x,y.x,z.x,head.x),(x.y,y.y,z.y,head.y),(x.z,y.z,z.z,head.z),(0,0,0,1)))

def process(config):
 name,breed,source,yaw,factors,tail=config
 out=ROOT/'Assets/Momo/Resources/ImportedCats'/name;out.mkdir(parents=True,exist_ok=True)
 work=ART/name;work.mkdir(parents=True,exist_ok=True)
 cache=ART/(source.stem+'-reduced.blend')
 if cache.exists():bpy.ops.wm.open_mainfile(filepath=str(cache))
 else:
  bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(source))
 mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
 bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);bpy.context.view_layer.objects.active=mesh
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 if not cache.exists():
  mod=mesh.modifiers.new('Mobile reduction','DECIMATE');mod.ratio=40000/len(mesh.data.polygons);mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
  bpy.ops.wm.save_as_mainfile(filepath=str(cache))
 mesh.rotation_mode='XYZ';mesh.rotation_euler.z=math.radians(yaw);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 if source.parent.name=='lux3d_model_exports':
  # Lux exports are centered, approximately 0.3m high. Match the template's
  # grounded 0.72m rest-space before computing weights, not after skinning.
  coords=[v.co for v in mesh.data.vertices]
  height=max(v.z for v in coords)-min(v.z for v in coords)
  mesh.scale=Vector((1,1,1))*(.72/height)
  bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
  floor=min(v.co.z for v in mesh.data.vertices)
  mesh.location.z=-floor
  bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 mesh.name='TabbyMesh';mesh.data.name=name+'Skin'
 bm=bmesh.new();bm.from_mesh(mesh.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000005);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh.data);bm.free()
 for p in mesh.data.polygons:p.use_smooth=True
 # Extract the source PBR images without changing UV layout.
 mat=mesh.data.materials[0];mat.name=name+'Material'
 has_normal=extract_glb_textures(source,out)
 # Do not accidentally retain an older model's normal map when a source only
 # supplies base color and metal/roughness textures.
 if not has_normal:
  normal=bpy.data.images.new(name+'FlatNormal',1,1,alpha=True)
  normal.pixels[:]=(.5,.5,1,1);normal.filepath_raw=str(out/'Normal.png');normal.file_format='PNG';normal.save()
 for node in mat.node_tree.nodes:
  if node.type=='BSDF_PRINCIPLED':node.inputs['Specular IOR Level'].default_value=.25
 # Append the proven project rig/actions, then adapt its rest joints to this body.
 with bpy.data.libraries.load(str(ROOT/'artifacts/imported-cat/Tabby-editable.blend'),link=False)as(src,dst):
  dst.objects=['TabbyRig'];dst.actions=list(CLIPS)
 arm=dst.objects[0];bpy.context.collection.objects.link(arm);arm.name='TabbyRig'
 actions={a.name:a for a in dst.actions};oldrest={b.name:b.matrix_local.copy()for b in arm.data.bones};parents={b.name:b.parent.name if b.parent else None for b in arm.data.bones}
 scale=Vector(factors);samples={};arm.animation_data_create()
 for clip in CLIPS:
  action=actions[clip];arm.animation_data.action=action
  # Blender 4.4 action slots are required after appending datablocks.
  if len(action.slots):arm.animation_data.action_slot=action.slots[0]
  start,end=[round(f)for f in action.frame_range];frames=[]
  for f in range(start,end+1):
   bpy.context.scene.frame_set(f);bpy.context.view_layer.update();frames.append((f,{b.name:b.matrix.copy()for b in arm.pose.bones}))
  samples[clip]=frames
 arm.animation_data.action=None
 bpy.context.scene.frame_set(1)
 bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);bpy.context.view_layer.objects.active=arm;bpy.ops.object.mode_set(mode='EDIT')
 for bone in arm.data.edit_bones:
  old=oldrest[bone.name];length=bone.length
  if bone.name.startswith('Tail'):
   i=int(bone.name[4:])-1;bone.head=tail[i];bone.tail=tail[i+1]
  else:
   bone.head=Vector(old.translation)*scale;bone.tail=(old@Vector((0,length,0)))*scale
  bone.align_roll(Vector((1,0,0)))
 bpy.ops.object.mode_set(mode='OBJECT')
 newrest={b.name:b.matrix_local.copy()for b in arm.data.bones}
 # Reorient every pose from world-space joint positions. Tail uses local rest
 # deltas, preserving each source tail's own shape (horizontal vs upright).
 for clip in CLIPS:
  action=bpy.data.actions.new(clip);arm.animation_data.action=action
  for f,poses in samples[clip]:
   bpy.context.scene.frame_set(f);posed={}
   for bone in arm.pose.bones:
    n=bone.name;p=parents[n]
    if n.startswith('Tail'):
     oldlocal=oldrest[p].inverted()@oldrest[n];poselocal=poses[p].inverted()@poses[n]
     newlocal=newrest[p].inverted()@newrest[n]
     target=posed[p]@newlocal@oldlocal.inverted()@poselocal
    else:
     # Match adapted rest orientation while preserving the animated segment direction.
     oldframe=frame_matrix(oldrest[n],scale);frame=frame_matrix(poses[n],scale)
     target=frame@oldframe.inverted()@newrest[n]
    posed[n]=target
    bone.matrix_basis=newrest[n].inverted()@newrest[p]@posed[p].inverted()@target if p else newrest[n].inverted()@target
    bone.rotation_mode='QUATERNION'
    bone.keyframe_insert('location',frame=f,group=n);bone.keyframe_insert('rotation_quaternion',frame=f,group=n);bone.keyframe_insert('scale',frame=f,group=n)
  action.use_fake_user=True;print(name,'CLIP',clip,len(samples[clip]),flush=True)
 # Retire source actions so FBX exports exactly the eight adapted clips.
 adapted=[a for a in bpy.data.actions if a not in actions.values()]
 for action in actions.values():bpy.data.actions.remove(action)
 for action in bpy.data.actions:action.name=action.name.split('.')[0]
 arm.animation_data.action=None
 for pb in arm.pose.bones:pb.matrix_basis=Matrix.Identity(4)
 bpy.context.view_layer.update()
 # Automatic heat binding on watertight voxel proxy, transferred by nearest surface.
 proxy=mesh.copy();proxy.data=mesh.data.copy();proxy.name='SkinningProxy';bpy.context.collection.objects.link(proxy)
 bpy.ops.object.select_all(action='DESELECT');proxy.select_set(True);bpy.context.view_layer.objects.active=proxy
 proxy.data.remesh_voxel_size=.009;proxy.data.use_remesh_preserve_volume=True;bpy.ops.object.voxel_remesh()
 # Whiskers and isolated generated surface fragments must not enter heat solve.
 bm=bmesh.new();bm.from_mesh(proxy.data);seen=set();components=[]
 for v in bm.verts:
  if v in seen:continue
  stack=[v];seen.add(v);part=[]
  while stack:
   q=stack.pop();part.append(q)
   for edge in q.link_edges:
    other=edge.other_vert(q)
    if other not in seen:seen.add(other);stack.append(other)
  components.append(part)
 largest=max(components,key=len);keep=set(largest);bmesh.ops.delete(bm,geom=[v for v in bm.verts if v not in keep],context='VERTS');bm.to_mesh(proxy.data);bm.free()
 print(name,'PROXY COMPONENTS',len(components),'kept',len(largest),flush=True)
 arm.select_set(True);bpy.context.view_layer.objects.active=arm;bpy.ops.object.parent_set(type='ARMATURE_AUTO')
 names=[b.name for b in arm.data.bones if b.use_deform];pcoords=np.array([v.co[:]for v in proxy.data.vertices]);pw=np.zeros((len(pcoords),len(names)))
 for v in proxy.data.vertices:
  for g in v.groups:
   n=proxy.vertex_groups[g.group].name
   if n in names:pw[v.index,names.index(n)]=g.weight
 unbound=int(np.sum(pw.sum(1)<.001))
 if unbound:raise RuntimeError(name+': unbound heat proxy vertices '+str(unbound))
 coords=np.array([v.co[:]for v in mesh.data.vertices]);dist,idx=cKDTree(pcoords).query(coords,k=3)
 w=np.sum(pw[idx]/np.maximum(dist[:,:,None],.00001)**2,axis=1);w/=w.sum(1,keepdims=True)
 for n in names:mesh.vertex_groups.new(name=n)
 for i in range(len(coords)):
  ids=np.argsort(w[i])[-4:];total=w[i,ids].sum()
  for j in ids:mesh.vertex_groups[names[j]].add([i],float(w[i,j]/total),'REPLACE')
 bpy.data.objects.remove(proxy,do_unlink=True)
 mesh.parent=arm;mod=mesh.modifiers.new('Skin','ARMATURE');mod.object=arm
 bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);arm.select_set(True);bpy.context.view_layer.objects.active=arm
 # Keep idle as the reference; all export actions are baked from the adapted rig.
 arm.animation_data.action=next(a for a in bpy.data.actions if a.name.split('.')[0]=='Idle');bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
 arm.animation_data.action=None
 for pb in arm.pose.bones:pb.matrix_basis=Matrix.Identity(4)
 bpy.context.scene.render.fps=30
 bpy.ops.wm.save_as_mainfile(filepath=str(work/'editable.blend'))
 bpy.ops.export_scene.fbx(filepath=str(out/'Model.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='STRIP',apply_scale_options='FBX_SCALE_ALL')
 report={'breed_id':breed,'name':name,'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'triangles':len(mesh.data.polygons),'vertices':len(mesh.data.vertices),'bones':len(arm.data.bones),'max_weights':4,'proxy_unbound':unbound,'clips':CLIPS,'texture_size':2048,'yaw_degrees':yaw,'motion':'Retargeted existing locally authored procedural prototype; no facial blend shapes'}
 (work/'processing-report.json').write_text(json.dumps(report,indent=2));print('COMPLETE',json.dumps(report),flush=True)
for config in CONFIG:
 if len(sys.argv)>1 and config[0]not in sys.argv[1:]:continue
 process(config)
os._exit(0)
