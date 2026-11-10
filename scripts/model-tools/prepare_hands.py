"""Adapt para/MakeHuman CC0 arms into a right hand + continuous full arm.
Requires bpy 4.5.3. Source and licence are recorded beside the exported asset.
No original FPS animation or arm-control constraints are used.
"""
from pathlib import Path
import bpy, bmesh, math, json, hashlib, sys
from mathutils import Vector, Matrix
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Momo/Resources/CompanionHand'
OUT.mkdir(parents=True,exist_ok=True)
MODEL=ROOT/'Assets/Momo/Models/CompanionHand'
MODEL.mkdir(parents=True,exist_ok=True)
SRC=Path(sys.argv[1] if len(sys.argv)>1 else '/private/tmp/momo-hand-source')
bpy.ops.wm.open_mainfile(filepath=str(SRC/'FPS ARMS RIG 1.blend'))
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
source=next(o for o in bpy.context.scene.objects if o.type=='MESH')
old=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
# Source rig-local -> canonical Blender: X across palm, Y towards fingertips, Z palm normal.
wrist=old.data.bones['hand.R'].head_local
forward=(old.data.bones['f_middle.03.R'].tail_local-wrist).normalized()
across=old.data.bones['f_pinky.01.R'].head_local-old.data.bones['f_index.01.R'].head_local
across=(across-forward*across.dot(forward)).normalized()
normal=across.cross(forward).normalized()
scale=.305/(old.data.bones['f_middle.03.R'].tail_local-wrist).length
basis=Matrix((across,forward,normal))
def point(p):
 v=basis@(p-wrist)*scale;v.y-=.115;return v
print('SCALE',scale,'BASIS',basis,'ORIGINAL MESH MATRIX',source.matrix_world)
# Remove the other arm and the shoulder/torso beyond the upper arm, before subdivision.
mesh=source.data.copy();obj=bpy.data.objects.new('CompanionHandSkin',mesh);bpy.context.collection.objects.link(obj)
source_to_rig=old.matrix_world.inverted()@source.matrix_world
for v in mesh.vertices:v.co=source_to_rig@v.co
bm=bmesh.new();bm.from_mesh(mesh)
dead=[v for v in bm.verts if v.co.x> -1.65]
bmesh.ops.delete(bm,geom=dead,context='VERTS');bm.to_mesh(mesh);bm.free()
# Vertex group order is preserved by copying the source groups.
for g in source.vertex_groups:obj.vertex_groups.new(name=g.name)
for v in mesh.vertices:v.co=point(v.co)
for p in mesh.polygons:p.use_smooth=True
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
sub=obj.modifiers.new('Smooth hand silhouette','SUBSURF');sub.levels=1
bpy.ops.object.modifier_apply(modifier=sub.name)
# A compact deform-only hierarchy. Forearm points back from wrist and is independent
# of the finger hierarchy, so the runtime can aim it towards an offscreen elbow.
rigdata=bpy.data.armatures.new('CompanionHandSkeleton');rig=bpy.data.objects.new('CompanionHandRig',rigdata);bpy.context.collection.objects.link(rig)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
root=rigdata.edit_bones.new('HandRoot');root.head=(0,-.115,0);root.tail=(0,-.065,0)
for b in old.data.bones:
 if not b.name.endswith('.R') or b.name.startswith(('clavicle','deltoid','upper_arm','forearm')):continue
 n=rigdata.edit_bones.new(b.name);n.head=point(b.head_local);n.tail=point(b.tail_local)
 n.parent=rigdata.edit_bones.get(b.parent.name) if b.parent else root
 if n.parent is None:n.parent=root
 n.align_roll(normal)
fore=rigdata.edit_bones.new('forearm.R');fore.head=point(wrist);fore.tail=point(old.data.bones['forearm.R'].head_local);fore.parent=root
upper=rigdata.edit_bones.new('upper_arm.R');upper.head=fore.tail;upper.tail=point(old.data.bones['upper_arm.R'].head_local);upper.parent=fore
end=rigdata.edit_bones.new('ElbowEnd');end.head=fore.tail;end.tail=fore.tail+(fore.tail-fore.head).normalized()*.03;end.parent=fore
shoulder=rigdata.edit_bones.new('ShoulderEnd');shoulder.head=upper.tail;shoulder.tail=upper.tail+(upper.tail-upper.head).normalized()*.03;shoulder.parent=upper
bpy.ops.object.mode_set(mode='OBJECT')
# Cut vertices near the shoulder may still carry deltoid/clavicle weight.
valid={b.name for b in rig.data.bones}
for v in obj.data.vertices:
 weights={obj.vertex_groups[g.group].name:g.weight for g in v.groups}
 upper=sum(w for n,w in weights.items() if n not in valid)
 if upper>0:obj.vertex_groups['upper_arm.R'].add([v.index],upper,'ADD')
for g in list(obj.vertex_groups):
 if g.name not in valid:obj.vertex_groups.remove(g)
obj.parent=rig;mod=obj.modifiers.new('Hand skin','ARMATURE');mod.object=rig
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL',limit=4)
bpy.ops.object.vertex_group_normalize_all(lock_active=False)
# Preserve the authored UV skin information.
image=bpy.data.images.load(str(SRC/'new_diff.png'),check_existing=False)
# Copy original texture without a Blender image write (old packed image metadata).
import shutil
shutil.copyfile(SRC/'new_diff.png',OUT/'Skin.png')
mat=bpy.data.materials.new('Companion skin');mat.use_nodes=True
bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.75
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
obj.data.materials.clear();obj.data.materials.append(mat)
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(MODEL/'HandSource.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',use_armature_deform_only=False)
bpy.ops.wm.save_as_mainfile(filepath='/private/tmp/momo-hand-adapted.blend')
print('EXPORTED',len(obj.data.vertices),'vertices',len(obj.data.polygons),'faces',len(rig.data.bones),'bones')
