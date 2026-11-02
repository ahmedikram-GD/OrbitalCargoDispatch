"""Export the authored assets without changing or saving the user's open Blender scene."""
import bpy, os, json
import numpy as np
from mathutils import Vector

project='D:/Projects/OrbitalCargoDispatch'
destination=project+'/WebAssetHandoff'
os.makedirs(destination+'/models',exist_ok=True)
previous_scene=bpy.context.window.scene
if previous_scene.name.startswith('WebAsset_Export'):
    previous_scene=next(s for s in bpy.data.scenes if s.name.startswith('DockKit_Authored') and len(s.objects)>0)
    bpy.context.window.scene=previous_scene
for old in list(bpy.data.scenes):
    if old.name.startswith('WebAsset_Export') and len(old.objects)==0:bpy.data.scenes.remove(old)
scene=bpy.data.scenes.new('WebAsset_Export')
bpy.context.window.scene=scene
created=[]

def load_image(path,noncolor=False):
    im=bpy.data.images.load(path,check_existing=False)
    if noncolor:im.colorspace_settings.name='Non-Color'
    if max(im.size)>1024:im.scale(1024,1024)
    return im

def make_material(prefix,source,color_file,emission_tint,has_ao):
    color=load_image(source+'/'+color_file)
    rough=load_image(source+'/'+prefix+'_Roughness.png',True)
    metal=load_image(source+'/'+prefix+'_Metallic.png',True)
    ao=load_image(source+'/'+prefix+'_AO.png',True) if has_ao else None
    emission=load_image(source+'/'+prefix+'_Emission.png')
    e=np.array(emission.pixels[:],np.float32).reshape(-1,4)
    e[:,:3]*=np.array(emission_tint)[None,:]
    emission.pixels.foreach_set(e.ravel());emission.pack()
    r=np.array(rough.pixels[:],np.float32).reshape(-1,4)
    m=np.array(metal.pixels[:],np.float32).reshape(-1,4)
    orm_pixels=np.ones_like(r)
    orm_pixels[:,0]=np.array(ao.pixels[:],np.float32).reshape(-1,4)[:,0] if ao else 1
    orm_pixels[:,1]=r[:,0];orm_pixels[:,2]=m[:,0]
    orm=bpy.data.images.new(prefix+'_Web_ORM',width=1024,height=1024,alpha=True)
    orm.colorspace_settings.name='Non-Color';orm.pixels.foreach_set(orm_pixels.ravel());orm.pack()
    mat=bpy.data.materials.new(prefix+'_WebPBR');mat.use_nodes=True
    nt=mat.node_tree;bsdf=nt.nodes.get('Principled BSDF')
    bsdf.inputs['Metallic'].default_value=1
    bsdf.inputs['Roughness'].default_value=1
    bsdf.inputs['Emission Strength'].default_value=1
    def texture(im):
        n=nt.nodes.new('ShaderNodeTexImage');n.image=im;return n
    nt.links.new(texture(color).outputs['Color'],bsdf.inputs['Base Color'])
    nt.links.new(texture(emission).outputs['Color'],bsdf.inputs['Emission Color'])
    orm_node=texture(orm);separate=nt.nodes.new('ShaderNodeSeparateColor');separate.mode='RGB'
    nt.links.new(orm_node.outputs['Color'],separate.inputs[0])
    nt.links.new(separate.outputs['Green'],bsdf.inputs['Roughness'])
    nt.links.new(separate.outputs['Blue'],bsdf.inputs['Metallic'])
    if has_ao:
        group=bpy.data.node_groups.get('glTF Material Output')
        if group is None:
            group=bpy.data.node_groups.new('glTF Material Output','ShaderNodeTree')
            group.interface.new_socket(name='Occlusion',in_out='INPUT',socket_type='NodeSocketFloat')
        output=nt.nodes.new('ShaderNodeGroup');output.node_tree=group
        nt.links.new(separate.outputs['Red'],output.inputs['Occlusion'])
    return mat

crate_folder=project+'/ArtSource/CargoCrate'
kit_folder=project+'/ArtSource/DockKit'
crate_material=make_material('Crate_Atlas',crate_folder,'Crate_Atlas_Color.png',(1,1,1),False)
kit_material=make_material('DockKit',kit_folder,'DockKit_Color.png',(1,.70,.25),True)
specs=[
    ('crate-standard',crate_folder+'/CargoCrate_URP.fbx','CargoCrate_URP',crate_material,'bottom'),
    ('floor-2m',kit_folder+'/Dock_Floor_2m.fbx','Dock_Floor_2m',kit_material,'deck-top'),
    ('wall-2m',kit_folder+'/Dock_Wall_2m.fbx','Dock_Wall_2m',kit_material,'bottom'),
    ('fixture-160cm',kit_folder+'/Dock_Light_160cm.fbx','Dock_Light_160cm',kit_material,'ceiling-mount'),
]
manifest={'formatVersion':1,'units':'metres','coordinates':'glTF right-handed, +Y up','threeVersionTested':'0.128.0','assets':[]}
try:
    for asset_id,fbx,prefix,material,pivot in specs:
        before=set(scene.objects)
        bpy.ops.import_scene.fbx(filepath=fbx)
        imported=[o for o in scene.objects if o not in before];created.extend(imported)
        meshes=[o for o in imported if o.type=='MESH']
        if len(meshes)!=1:raise RuntimeError('Expected one authored mesh in '+fbx)
        obj=meshes[0]
        obj.hide_render=False;obj.hide_set(False)
        world=obj.matrix_world.copy();obj.parent=None;obj.matrix_world=world;obj.location=(0,0,0)
        # The crate's mesh origin is inherited from a lid detail; centre-bottom is the game pivot.
        if asset_id=='crate-standard':
            points=[obj.matrix_world @ Vector(p) for p in obj.bound_box]
            center=Vector(((min(p.x for p in points)+max(p.x for p in points))/2,
                           (min(p.y for p in points)+max(p.y for p in points))/2,
                           min(p.z for p in points)))
            obj.location-=center
        obj.data=obj.data.copy();obj.data.materials.clear();obj.data.materials.append(material)
        for p in obj.data.polygons:p.material_index=0
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        filename='models/'+asset_id+'.glb'
        bpy.ops.export_scene.gltf(filepath=destination+'/'+filename,export_format='GLB',use_selection=True,use_active_scene=True,
            export_yup=True,export_animations=False,export_cameras=False,export_lights=False)
        obj.data.calc_loop_triangles()
        manifest['assets'].append({'id':asset_id,'url':filename,'pivot':pivot,'triangles':len(obj.data.loop_triangles),'materialCount':1,'textureMaxSize':1024,'bytes':os.path.getsize(destination+'/'+filename)})
    with open(destination+'/manifest.json','w') as f:json.dump(manifest,f,indent=2)
    print(json.dumps(manifest))
finally:
    bpy.context.window.scene=previous_scene
    for obj in created:
        bpy.data.objects.remove(obj,do_unlink=True)
    bpy.data.scenes.remove(scene)
