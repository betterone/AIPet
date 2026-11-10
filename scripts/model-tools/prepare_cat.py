"""Local, reproducible processing of the user's standing cat. Requires bpy 4.5.3.
No provider, generated motion service, or purchased animation data is used.
All clips below are authored procedural prototypes, not motion capture.
"""
import bpy, bmesh, math, json, hashlib, sys, os
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Momo/Resources/ImportedCat'
ART=ROOT/'artifacts/imported-cat'
SOURCE=Path(sys.argv[1] if len(sys.argv)>1 else '/Users/zak/Downloads/580743ed2307e41a576a307c50143168.glb')
OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
bpy.context.view_layer.objects.active=mesh;mesh.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
mesh.name='TabbyMesh';mesh.data.name='TabbySkin'
print('IMPORTED',len(mesh.data.polygons), 'faces',flush=True)
# Preserve authored UVs and original texture appearance. This is decimation, not quad retopology.
mod=mesh.modifiers.new('Mobile reduction','DECIMATE');mod.ratio=40000/len(mesh.data.polygons);mod.use_collapse_triangulate=True
bpy.ops.object.modifier_apply(modifier=mod.name)
bm=bmesh.new();bm.from_mesh(mesh.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=0.000005);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh.data);bm.free()
for p in mesh.data.polygons:p.use_smooth=True
print('DECIMATED',len(mesh.data.polygons),len(mesh.data.vertices),flush=True)
# Export resized textures separately for deterministic Unity material setup.
mat=mesh.data.materials[0];mat.name='TabbyMaterial'
texpaths={}
for node in mat.node_tree.nodes:
 if node.type!='TEX_IMAGE' or node.image is None:continue
 im=node.image
 name='Normal' if 'normal' in im.name.lower() else 'MetalRough' if 'rough' in im.name.lower() or 'metallic' in im.name.lower() else 'BaseColor'
 im.scale(2048,2048);im.filepath_raw=str(OUT/(name+'.png'));im.file_format='PNG';im.save();im.pack();texpaths[name]=im.filepath_raw
# Material is matte fur; avoid source's overbright specular multiplier.
bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
bs.inputs['Specular IOR Level'].default_value=.25
if 'Specular Tint' in bs.inputs:bs.inputs['Specular Tint'].default_value=(1,1,1,1)
# Bones use Blender coordinates: X left/right, -Y forward, Z up.
bones={}
def add(name,a,b,parent=None):bones[name]=(Vector(a),Vector(b),parent)
add('Root',(0,0,0),(0,0,.12))
add('Pelvis',(0,.21,.36),(0,.035,.38),'Root')
add('Spine',(0,.035,.38),(0,-.14,.395),'Pelvis')
add('Chest',(0,-.14,.395),(0,-.255,.43),'Spine')
add('Neck',(0,-.255,.43),(0,-.325,.525),'Chest')
add('Head',(0,-.325,.525),(0,-.42,.55),'Neck')
for side,sgn in [('L',1),('R',-1)]:
 add('Ear.'+side,(sgn*.074,-.305,.635),(sgn*.096,-.30,.712),'Head')
 x=sgn*.092
 add('ForeUpper.'+side,(x,-.20,.39),(x,-.195,.215),'Chest')
 add('ForeLower.'+side,(x,-.195,.215),(x,-.268,.05),'ForeUpper.'+side)
 add('ForePaw.'+side,(x,-.268,.05),(x,-.335,.029),'ForeLower.'+side)
 add('HindUpper.'+side,(x,.205,.36),(x,.115,.245),'Pelvis')
 add('HindLower.'+side,(x,.115,.245),(x,.29,.11),'HindUpper.'+side)
 add('HindAnkle.'+side,(x,.29,.11),(x,.22,.046),'HindLower.'+side)
 add('HindPaw.'+side,(x,.22,.046),(x,.157,.025),'HindAnkle.'+side)
points=[(0,.31,.395),(0,.43,.415),(0,.53,.452),(0,.574,.525),(0,.573,.599),(0,.545,.635)]
for i in range(5):add('Tail'+str(i+1),points[i],points[i+1],'Pelvis' if i==0 else 'Tail'+str(i))
armdata=bpy.data.armatures.new('TabbySkeleton');arm=bpy.data.objects.new('TabbyRig',armdata);bpy.context.collection.objects.link(arm)
bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);bpy.context.view_layer.objects.active=arm;bpy.ops.object.mode_set(mode='EDIT')
for name,(a,b,parent) in bones.items():
 eb=armdata.edit_bones.new(name);eb.head=a;eb.tail=b
 if parent:eb.parent=armdata.edit_bones[parent]
 eb.use_deform=name!='Root'
bpy.ops.object.mode_set(mode='OBJECT')
mesh.parent=arm
m=mesh.modifiers.new('Skin','ARMATURE');m.object=arm
# Solve skinning on a watertight voxel proxy, then transfer to the UV-preserving mesh.
from scipy.spatial import cKDTree
bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);bpy.context.view_layer.objects.active=mesh
proxy=mesh.copy();proxy.data=mesh.data.copy();proxy.name='SkinningProxy';bpy.context.collection.objects.link(proxy)
proxy.modifiers.clear();proxy.vertex_groups.clear();proxy.parent=None
bpy.ops.object.select_all(action='DESELECT');proxy.select_set(True);bpy.context.view_layer.objects.active=proxy
proxy.data.remesh_voxel_size=.009;proxy.data.use_remesh_preserve_volume=True
bpy.ops.object.voxel_remesh()
arm.select_set(True);bpy.context.view_layer.objects.active=arm
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
coords=np.array([v.co[:] for v in mesh.data.vertices]);names=[n for n in bones if n!='Root']
pcoords=np.array([v.co[:] for v in proxy.data.vertices]);pw=np.zeros((len(pcoords),len(names)))
for v in proxy.data.vertices:
 for g in v.groups:
  name=proxy.vertex_groups[g.group].name
  if name in names:pw[v.index,names.index(name)]=g.weight
unbound=int(np.sum(pw.sum(axis=1)<.001));print('PROXY',len(pcoords),'unbound',unbound,flush=True)
if unbound:raise RuntimeError('Proxy automatic skinning failed; do not publish broken weights')
distance,indices=cKDTree(pcoords).query(coords,k=3)
w=np.sum(pw[indices]/np.maximum(distance[:,:,None],.00001)**2,axis=1);w/=w.sum(axis=1,keepdims=True)
mesh.vertex_groups.clear()
for name in names:mesh.vertex_groups.new(name=name)
missing=0
for i in range(len(coords)):
 ids=np.argsort(w[i])[-4:];total=w[i,ids].sum()
 for j in ids:mesh.vertex_groups[names[j]].add([i],float(w[i,j]/total),'REPLACE')
bpy.data.objects.remove(proxy,do_unlink=True)
bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);bpy.context.view_layer.objects.active=mesh
print('WEIGHTS fallback vertices',missing,flush=True)
bpy.context.view_layer.objects.active=mesh
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL',limit=4)
bpy.ops.object.vertex_group_normalize_all(lock_active=False)
# Baking world-space joint poses gives real articulated legs and curved spine.
rest={n:armdata.bones[n].matrix_local.copy() for n in bones}
scene=bpy.context.scene;scene.render.fps=30

posed={}
def set_world(n,mat):
 parent=bones[n][2]
 arm.pose.bones[n].matrix_basis=rest[n].inverted()@rest[parent]@posed[parent].inverted()@mat if parent else rest[n].inverted()@mat
 posed[n]=mat

def set_segment(n,a,b,twist=0):
 a,b=Vector(a),Vector(b);q=(bones[n][1]-bones[n][0]).rotation_difference(b-a)
 mat=q.to_matrix().to_4x4()@rest[n];mat.translation=a
 set_world(n,mat)

def transform_child(n,parent_mat):
 pm=parent_mat@rest[bones[n][2]].inverted();m=pm@rest[n];set_world(n,m)

def two_bone(a,c,l1,l2,pole):
 d=c-a;length=min(max(d.length,abs(l1-l2)+1e-5),l1+l2-1e-5);axis=d.normalized();cos=(l1*l1+length*length-l2*l2)/(2*l1*length)
 along=l1*cos;h=math.sqrt(max(0,l1*l1-along*along));perp=Vector(pole)-axis*Vector(pole).dot(axis)
 return a+axis*along+perp.normalized()*h

def pose(kind,t):
 for pb in arm.pose.bones:pb.matrix_basis=Matrix.Identity(4)
 sit=kind=='Sit';lie=kind=='Sleep';eat=kind=='Eat';stretch=kind=='Stretch';walk=kind=='Walk'
 # Pause-rich micro motion with small amplitudes; no global body rocking.
 cycle=t if kind in ['Walk','Paw','Groom'] else t/({'Eat':2,'Stretch':3}.get(kind,4))
 breath=math.sin(cycle*math.tau)*.0018
 pelvis=Vector((0,.21,.36));spine=Vector((0,.035,.38));chest=Vector((0,-.14,.395));neck=Vector((0,-.255,.43));head=Vector((0,-.325,.525));nose=Vector((0,-.42,.55))
 if sit:
  pelvis+=Vector((0,-.01,-.19));spine+=Vector((0,0,-.10));chest.z-=.025
 if lie:
  for p in [pelvis,spine,chest,neck,head,nose]:p.z-=.195
  head.z-=.03;nose.z-=.04
 if eat:
  chest.z-=.035;neck+=Vector((0,-.015,-.09));head+=Vector((0,-.005,-.195));nose+=Vector((0,.015,-.255))
 if stretch:
  chest+=Vector((0,-.075,-.13));neck+=Vector((0,-.09,-.15));head+=Vector((0,-.11,-.17));nose+=Vector((0,-.10,-.19))
 if walk:
  bob=.003*math.cos(t*math.tau*2);pelvis.z+=bob;spine.z+=bob;chest.z+=bob;neck.z+=bob;head.z+=bob;nose.z+=bob
 spine.z+=breath;chest.z+=breath
 if kind=='Groom':
  head+=Vector((.015,-.01,-.075));nose+=Vector((.04,.03,-.115))
 set_segment('Root',(0,0,0),(0,0,.12));set_segment('Pelvis',pelvis,spine);set_segment('Spine',spine,chest);set_segment('Chest',chest,neck);set_segment('Neck',neck,head);set_segment('Head',head,nose)
 for side in ['L','R']:
  transform_child('Ear.'+side,posed['Head'])
  # Ground contact is stationary during stance; swing lifts toe on a smooth arc.
  sgn=1 if side=='L' else -1;x=sgn*.092
  for front in [True,False]:
   name=('Fore' if front else 'Hind');parent='Chest' if front else 'Pelvis';local=bones[name+'Upper.'+side][0]
   start=posed[parent]@rest[parent].inverted()@local
   target=Vector((x,-.268 if front else .22,.05 if front else .046))
   if walk:
    offset=(0 if side=='L' else .5)+(0 if not front else .25);phase=(t+offset)%1;stance=.65;stride=.22
    if phase<stance:target.y+=(-.5+phase/stance)*stride
    else:
     u=(phase-stance)/(1-stance);smooth=u*u*(3-2*u);target.y+=(.5-smooth)*stride;target.z+=.042*math.sin(math.pi*u)**1.4
   if sit and not front:target.y=.24;target.x=sgn*.12
   if lie:target.y+=-.08 if front else .05
   if stretch and front:target.y-=.12
   if kind in ['Paw','Groom'] and front and side=='L':
    amp=math.sin(math.pi*(t%1))**2
    target+=Vector((-.018,-.10*amp,.13*amp))
   # Use actual rest segment lengths; preserve paw orientation at contact.
   l1=(bones[name+'Upper.'+side][1]-bones[name+'Upper.'+side][0]).length
   l2=(bones[name+'Lower.'+side][1]-bones[name+'Lower.'+side][0]).length
   end=target if front else target+Vector((0,.07,.064))
   knee=two_bone(start,end,l1,l2,(0,1 if front else -1,0))
   set_segment(name+'Upper.'+side,start,knee);set_segment(name+'Lower.'+side,knee,end)
   if not front:set_segment('HindAnkle.'+side,end,target)
   toe=target+Vector((0,-.067,-.021));set_segment(name+'Paw.'+side,target,toe)
 # Tail follows hips, then adds small distributed bends.
 for i in range(1,6):
  n='Tail'+str(i);parent=bones[n][2];transform_child(n,posed[parent])
  if not lie:
   m=posed[n].copy();rot=Quaternion((0,0,1),.025*math.sin(cycle*math.tau-i*.45)).to_matrix().to_4x4();m=m@rot;set_world(n,m)

clips=[('Idle',4),('Walk',1.2),('Sit',4),('Sleep',4),('Eat',2),('Paw',1.6),('Groom',2),('Stretch',3)]
arm.animation_data_create()
for name,duration in clips:
 action=bpy.data.actions.new(name);arm.animation_data.action=action
 steps=round(duration*30)
 for f in range(steps+1):
  scene.frame_set(f+1);t=f/steps if name in ['Walk','Paw','Groom'] else f/30
  pose(name,t)
  for pb in arm.pose.bones:
   pb.rotation_mode='QUATERNION';pb.keyframe_insert('location',frame=f+1,group=pb.name);pb.keyframe_insert('rotation_quaternion',frame=f+1,group=pb.name);pb.keyframe_insert('scale',frame=f+1,group=pb.name)
 action.use_fake_user=True
 print('CLIP',name,steps+1,flush=True)
arm.animation_data.action=None;pose('Idle',0);scene.frame_set(1)
# Keep all source and generated assets editable; export only the selected mesh + rig.
bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);arm.select_set(True);bpy.context.view_layer.objects.active=arm
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'Tabby-editable.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT/'Tabby.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='STRIP',use_mesh_modifiers=True,apply_scale_options='FBX_SCALE_ALL')
bpy.ops.export_scene.gltf(filepath=str(ART/'Tabby-rigged.glb'),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='ACTIONS',export_skins=True,export_all_influences=False)
report={'source':str(SOURCE),'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'triangles':len(mesh.data.polygons),'vertices':len(mesh.data.vertices),'bones':len(bones),'max_weights':4,'fallback_vertices':missing,'skinning':'Heat weights on watertight voxel proxy, transferred to UV-preserving reduced mesh','textures':list(texpaths),'texture_resolution':2048,'clips':dict(clips),'motion_source':'Locally authored procedural prototype; no mocap or third-party animation','limitations':['Decimated triangle mesh, not production quad retopology','No eyelid or mouth blend shapes','Phone performance and subjective naturalness require device review']}
(ART/'processing-report.json').write_text(json.dumps(report,indent=2))
print('PROCESSING COMPLETE',json.dumps(report),flush=True)
# bpy on macOS can crash during interpreter teardown; files and report are already flushed.
os._exit(0)
