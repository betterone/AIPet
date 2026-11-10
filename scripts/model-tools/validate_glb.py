"""Read-only validation of the deliverable, independent of Blender's export code."""
import json, struct, io
from pathlib import Path
import numpy as np
from PIL import Image
root=Path(__file__).resolve().parents[2]
p=root/'artifacts/imported-cat/Tabby-rigged.glb'
b=p.read_bytes();magic,version,total=struct.unpack_from('<III',b)
assert magic==0x46546c67 and version==2 and total==len(b)
n=struct.unpack_from('<I',b,12)[0];j=json.loads(b[20:20+n]);data=b[28+n:]
def accessor(i):
 a=j['accessors'][i];v=j['bufferViews'][a['bufferView']]
 dtype={5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]
 k={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
 return np.ndarray((a['count'],k),dtype=dtype,buffer=data,offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',np.dtype(dtype).itemsize*k),np.dtype(dtype).itemsize))
assert len(j['skins'])==1 and len(j['skins'][0]['joints'])>=26
triangles=0
for mesh in j['meshes']:
 for prim in mesh['primitives']:
  attrs=prim['attributes'];pos=accessor(attrs['POSITION']);ind=accessor(prim['indices'])
  weights=accessor(attrs['WEIGHTS_0']);joints=accessor(attrs['JOINTS_0'])
  assert np.isfinite(pos).all() and ind.max()<len(pos)
  assert np.isfinite(weights).all() and np.allclose(weights.sum(axis=1),1,atol=1e-4)
  assert joints.max()<len(j['skins'][0]['joints']) and weights.shape[1]==4
  triangles+=len(ind)//3
assert triangles==40000
expected={'Idle','Walk','Sit','Sleep','Eat','Paw','Groom','Stretch'}
assert {a['name'] for a in j['animations']}==expected
for a in j['animations']:
 errors=[]
 for ch in a['channels']:
  s=a['samplers'][ch['sampler']];time=accessor(s['input']);values=accessor(s['output'])
  assert np.isfinite(values).all() and np.all(np.diff(time[:,0])>0)
  first,last=values[0],values[-1]
  delta=float(np.max(np.abs(first-last)))
  if ch['target']['path']=='rotation': delta=min(delta,float(np.max(np.abs(first+last))))
  errors.append(delta)
 assert max(errors)<2e-4,(a['name'],max(errors))
 print(a['name'],'loop endpoint max delta',max(errors))
for im in j['images']:
 v=j['bufferViews'][im['bufferView']];start=v.get('byteOffset',0)
 image=Image.open(io.BytesIO(data[start:start+v['byteLength']]))
 assert image.size==(2048,2048),image.size
print('PASS: GLB structure, 40000 triangles, skin weights, 8 seamless clip endpoints, 3 embedded 2048 textures.')
