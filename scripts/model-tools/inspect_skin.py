import bpy, numpy as np, os, json, sys
from pathlib import Path
root=Path(__file__).resolve().parents[2]
if '--fbx' in sys.argv:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=str(root/'Assets/Momo/Resources/ImportedCat/Tabby.fbx'))
 print([(o.name,o.type,list(o.scale)) for o in bpy.context.scene.objects],flush=True)
 print([a.name for a in bpy.data.actions],flush=True)
 rig=bpy.data.objects['TabbyRig'];rig.animation_data_create()
 action=next(a for a in bpy.data.actions if a.name.endswith('Walk'))
 rig.animation_data.action=action
 if action.slots:rig.animation_data.action_slot=action.slots[0]
 print("ACTION CHANNELS",len(list(action.fcurves)) if hasattr(action,"fcurves") else "layered",flush=True)
 for frame in [1,4,7,10,13,16,19,22,25,28,31,34,37]:
  bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
  foot=rig.pose.bones['ForePaw.L'];print('WALK',frame,'head',list(foot.head),'world',list((rig.matrix_world@foot.head)),flush=True)

 bpy.ops.export_scene.gltf(filepath=str(root/'artifacts/imported-cat/FBX-roundtrip.glb'),export_format='GLB',export_animations=True,export_animation_mode='ACTIONS')
 os._exit(0)
bpy.ops.wm.open_mainfile(filepath=str(root/'artifacts/imported-cat/Tabby-editable.blend'))
arm=bpy.data.objects['TabbyRig'];mesh=bpy.data.objects['TabbyMesh']
coords=np.array([v.co[:] for v in mesh.data.vertices]);edges=np.array([e.vertices[:] for e in mesh.data.edges]);length=np.linalg.norm(coords[edges[:,0]]-coords[edges[:,1]],axis=1)
for name in ['Idle','Sit','Sleep']:
 arm.animation_data.action=bpy.data.actions[name];bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
 ev=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());data=ev.to_mesh();pos=np.array([v.co[:] for v in data.vertices]);ev.to_mesh_clear()
 ratio=np.linalg.norm(pos[edges[:,0]]-pos[edges[:,1]],axis=1)/np.maximum(length,.00001)
 print('POSE',name,'deform',np.quantile(ratio,[.5,.9,.99,.999,1]),flush=True)
 for bn in ['Pelvis','Spine','Chest','Head','Tail1','Tail5','HindUpper.L','HindLower.L']:
  pb=arm.pose.bones[bn];print(bn,'head',list(pb.head),'tail',list(pb.tail),'scale',list(pb.scale),flush=True)
 for idx in np.argsort(ratio)[-5:]:
  vi=edges[idx,0];v=mesh.data.vertices[vi];print('EDGE',float(ratio[idx]),list(coords[vi]),[(mesh.vertex_groups[g.group].name,round(g.weight,3))for g in v.groups],flush=True)
 bpy.ops.export_scene.gltf(filepath=str(root/'artifacts/imported-cat'/('Check-'+name+'.glb')),export_format='GLB',export_animations=False,export_skins=True)
os._exit(0)
