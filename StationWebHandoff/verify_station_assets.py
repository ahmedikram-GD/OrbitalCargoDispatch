"""Validate GLB structure, actual index counts, finite geometry and texture budgets."""
import json,struct,hashlib,math
from pathlib import Path
ROOT=Path(__file__).resolve().parent
if not (ROOT/'manifest.json').exists():ROOT=Path('D:/Projects/OrbitalCargoDispatch/StationWebHandoff')
manifest=json.loads((ROOT/'manifest.json').read_text())
anchors={'conveyor':['Conveyor_Belt','Conveyor_IntakeGuard','Conveyor_SurfaceStart','Conveyor_SurfaceEnd'],'scanner':['Scanner_Glass','Scanner_ScanBeam','Scanner_CargoSlot','Scanner_ControlTarget'],'disposal-chute':['Disposal_DropTarget','Disposal_ThroatBottom']}
for asset in manifest['assets']:
    raw=(ROOT/asset['url']).read_bytes();magic,version,total=struct.unpack_from('<III',raw)
    assert magic==0x46546c67 and version==2 and total==len(raw)
    assert hashlib.sha256(raw).hexdigest()==asset['sha256'] and len(raw)==asset['bytes']
    jl=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+jl]);bl,bt=struct.unpack_from('<II',raw,20+jl);binary=raw[28+jl:]
    assert bt==0x004e4942 and len(binary)==bl
    assert not doc.get('extensionsRequired'),doc.get('extensionsRequired')
    names={n.get('name') for n in doc['nodes']};assert set(anchors[asset['id']])<=names
    for accessor in doc['accessors']:
        if accessor['componentType']==5126:
            view=doc['bufferViews'][accessor['bufferView']];offset=view.get('byteOffset',0)+accessor.get('byteOffset',0)
            width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[accessor['type']]
            stride=view.get('byteStride',width*4)
            for i in range(accessor['count']):assert all(math.isfinite(v) for v in struct.unpack_from('<'+'f'*width,binary,offset+i*stride))
    triangles=draws=0
    for mesh in doc['meshes']:
        for primitive in mesh['primitives']:
            draws+=1;assert primitive.get('mode',4)==4
            idx=doc['accessors'][primitive['indices']];assert idx['count']%3==0;triangles+=idx['count']//3
            view=doc['bufferViews'][idx['bufferView']];offset=view.get('byteOffset',0)+idx.get('byteOffset',0)
            fmt={5121:'B',5123:'H',5125:'I'}[idx['componentType']]
            indices=struct.unpack_from('<'+fmt*idx['count'],binary,offset)
            assert max(indices)<doc['accessors'][primitive['attributes']['POSITION']]['count']
            assert 'NORMAL' in primitive['attributes'] and 'TEXCOORD_0' in primitive['attributes']
    assert triangles==asset['triangles'] and triangles<=asset['triangleBudget'] and draws==asset['meshPrimitives']
    for im in doc['images']:
        view=doc['bufferViews'][im['bufferView']];png=binary[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
        assert png.startswith(b'\x89PNG\r\n\x1a\n');w,h=struct.unpack_from('>II',png,16);assert max(w,h)<=1024
    if asset['id']=='scanner':
        materials=[m for m in doc['materials'] if m.get('alphaMode')=='BLEND'];assert len(materials)==1 and not materials[0].get('doubleSided',False)
    print(asset['id'],triangles,'triangles;',draws,'mesh primitives;',len(raw),'bytes: PASS')
assert sum(a['triangles'] for a in manifest['assets'])==manifest['trianglesTotal']<=15000
print('All station exports passed.')
