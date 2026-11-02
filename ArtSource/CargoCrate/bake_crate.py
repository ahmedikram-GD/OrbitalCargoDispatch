import bpy, os
import numpy as np
scene=bpy.data.scenes['CargoCrate_Benchmark'];bpy.context.window.scene=scene
obj=bpy.data.objects['CargoCrate_Mesh']
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
scene.render.engine='CYCLES';scene.cycles.samples=8
scene.render.bake.margin=8
folder='D:/Projects/OrbitalCargoDispatch/ArtSource/CargoCrate'
mats=list(obj.data.materials)
targets=[]
for m in mats:
    n=m.node_tree.nodes.new('ShaderNodeTexImage');n.label='Atlas bake target';targets.append(n)
def bake(name,kind,noncolor=False):
    im=bpy.data.images.new(name,width=1024,height=1024,alpha=True)
    if noncolor:im.colorspace_settings.name='Non-Color'
    for m,n in zip(mats,targets):n.image=im;m.node_tree.nodes.active=n
    args={'type':kind,'use_clear':True}
    if kind=='DIFFUSE':args['pass_filter']={'COLOR'}
    bpy.ops.object.bake(**args)
    im.filepath_raw=folder+'/'+name+'.png';im.file_format='PNG';im.save();im.pack()
    return im
base=bake('Crate_Atlas_Color','DIFFUSE')
rough=bake('Crate_Atlas_Roughness','ROUGHNESS',True)
emission=bake('Crate_Atlas_Emission','EMIT')
restore=[]
for m in mats:
    nt=m.node_tree;p=nt.nodes.get('Principled BSDF');out=next(n for n in nt.nodes if n.type=='OUTPUT_MATERIAL')
    original=out.inputs['Surface'].links[0].from_socket
    emit=nt.nodes.new('ShaderNodeEmission');emit.inputs['Color'].default_value=(p.inputs['Metallic'].default_value,)*3+(1,)
    nt.links.new(emit.outputs[0],out.inputs['Surface']);restore.append((nt,out,original,emit))
metal=bake('Crate_Atlas_Metallic','EMIT',True)
for nt,out,original,emit in restore:nt.links.new(original,out.inputs['Surface']);nt.nodes.remove(emit)
# URP metallic/smoothness layout: metallic in R, smoothness in A.
pix=np.array(metal.pixels[:],np.float32).reshape(-1,4)
roughpix=np.array(rough.pixels[:],np.float32).reshape(-1,4)
pix[:,3]=1-np.clip(roughpix[:,0],0,1)
packed=bpy.data.images.new('Crate_Atlas_MetalSmooth',width=1024,height=1024,alpha=True);packed.colorspace_settings.name='Non-Color';packed.pixels.foreach_set(pix.ravel());packed.filepath_raw=folder+'/Crate_Atlas_MetalSmooth.png';packed.file_format='PNG';packed.save();packed.pack()
# Export an independent single-material mesh while retaining the authored source object.
copy=obj.copy();copy.data=obj.data.copy();scene.collection.objects.link(copy);copy.name='CargoCrate_URP';copy.parent=None
mat=bpy.data.materials.new('Crate_URP_Atlas');mat.use_nodes=True;copy.data.materials.clear();copy.data.materials.append(mat)
for p in copy.data.polygons:p.material_index=0
n=mat.node_tree.nodes;bsdf=n.get('Principled BSDF');tex=n.new('ShaderNodeTexImage');tex.image=base;mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
obj.hide_render=True;obj.hide_set(True)
bpy.ops.object.select_all(action='DESELECT');copy.select_set(True);bpy.context.view_layer.objects.active=copy
bpy.ops.export_scene.fbx(filepath=folder+'/CargoCrate_URP.fbx',use_selection=True,object_types={'MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
bpy.ops.wm.save_as_mainfile(filepath=folder+'/CargoCrate_Benchmark.blend')
print('Baked colour, emission and URP metallic/smoothness atlas; single-material FBX saved')
