"""Dependency-free checks for the four self-contained exported GLB files."""
import hashlib
import json
from pathlib import Path
import struct

root = Path(__file__).resolve().parent
manifest = json.loads((root / 'manifest.json').read_text(encoding='utf-8'))
for item in manifest['assets']:
    path = root / item['url']
    data = path.read_bytes()
    magic, version, length = struct.unpack_from('<III', data)
    assert magic == 0x46546C67 and version == 2 and length == len(data), path.name
    json_length, json_type = struct.unpack_from('<II', data, 12)
    assert json_type == 0x4E4F534A
    document = json.loads(data[20:20 + json_length])
    assert len(document['meshes']) == 1 and len(document['materials']) == 1
    primitives = document['meshes'][0]['primitives']
    assert len(primitives) == 1 and primitives[0].get('mode', 4) == 4
    attributes = primitives[0]['attributes']
    assert all(name in attributes for name in ['POSITION', 'NORMAL', 'TEXCOORD_0'])
    assert all('uri' not in buffer for buffer in document['buffers'])
    assert all('bufferView' in image and image['mimeType'] == 'image/png' for image in document['images'])
    assert not document.get('extensionsRequired'), 'A decoder/extension was introduced'
    material = document['materials'][0]
    pbr = material['pbrMetallicRoughness']
    assert 'baseColorTexture' in pbr and 'metallicRoughnessTexture' in pbr
    assert 'emissiveTexture' in material
    if item['id'] != 'crate-standard':
        assert material['occlusionTexture']['index'] == pbr['metallicRoughnessTexture']['index']
    count = document['accessors'][primitives[0]['indices']]['count'] // 3
    assert count == item['triangles']
    binary_offset = 20 + json_length + 8
    for image in document['images']:
        view = document['bufferViews'][image['bufferView']]
        start = binary_offset + view.get('byteOffset', 0)
        png = data[start:start + view['byteLength']]
        assert png[:8] == b'\x89PNG\r\n\x1a\n'
        width, height = struct.unpack_from('>II', png, 16)
        assert width <= 1024 and height <= 1024
    digest = hashlib.sha256(data).hexdigest()
    if 'sha256' in item:
        assert digest == item['sha256']
    item['sha256'] = digest
    item['bytes'] = len(data)
    print(f"PASS {item['id']}: {count:,} triangles, 1 material, embedded textures, {len(data):,} bytes")
(root / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print('All four GLBs passed. Browser scale/material checks are in preview.js.')
