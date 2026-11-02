import bpy, math, os, random
import numpy as np
from mathutils import Vector
folder='D:/Projects/OrbitalCargoDispatch/ArtSource/CargoCrate'
os.makedirs(folder,exist_ok=True)
scene=bpy.data.scenes.new('CargoCrate_Benchmark')
bpy.context.window.scene=scene
world=bpy.data.worlds.new('CrateStudio');scene.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.16,.21,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.35
root=bpy.data.objects.new('CargoCrate_Standard',None);scene.collection.objects.link(root)
rng=np.random.default_rng(12)
size=1024
grain=rng.normal(0,.012,(size,size)).astype(np.float32)
# Fine finish variation and sparse shallow scratches; image textures survive model export.
scuff=np.zeros((size,size),dtype=np.float32)
for i in range(420):
    x,y=rng.integers(3,size-80,size=2);length=int(rng.integers(2,65))
    scuff[y,x:x+length]=float(rng.uniform(.015,.07))
def image(name,arr,noncolor=False):
    im=bpy.data.images.new(name,width=size,height=size,alpha=True)
    if noncolor:im.colorspace_settings.name='Non-Color'
    im.pixels.foreach_set(arr.ravel());im.filepath_raw=folder+'/'+name+'.png';im.file_format='PNG';im.save();im.pack();return im
rough=np.ones((size,size,4),np.float32);rough[:,:,:3]=np.clip(.43+grain[:,:,None]*3+scuff[:,:,None],.2,.75)
roughimg=image('Steel_Roughness',rough,True)
def mat(name,color,metal=.25,roughness=.43,textured=False,emit=0):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    n=m.node_tree.nodes;p=n.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=roughness
    if textured:
        a=np.ones((size,size,4),np.float32)
        a[:,:,:3]=np.clip(np.array(color)[None,None,:]+grain[:,:,None]+scuff[:,:,None],0,1)
        tex=n.new('ShaderNodeTexImage');tex.image=image(name+'_Color',a);m.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color'])
        r=n.new('ShaderNodeTexImage');r.image=roughimg;m.node_tree.links.new(r.outputs['Color'],p.inputs['Roughness'])
    if emit:
        p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emit
    return m
navy=mat('Painted_Navy',(.095,.16,.235),.35,textured=True)
edge=mat('Reinforced_Frame',(.035,.052,.075),.45,textured=True)
panel=mat('Inset_Paint',(.15,.22,.29),.3,textured=True)
rubber=mat('Rubber_Seal',(.013,.019,.025),0,.78)
steel=mat('Exposed_Steel',(.32,.37,.40),.8,.28)
cyan=mat('Status_Cyan',(.035,.60,.75),.1,.25,emit=1.5)
ivory=mat('Shipping_Label',(.72,.73,.67),0,.72)
ink=mat('Printed_Ink',(.018,.025,.033),0,.7)
def box(name,loc,scale,material,bevel=.012):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    o=bpy.context.object;o.name=name;o.dimensions=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.parent=root;o.data.materials.append(material)
    if bevel:
        mod=o.modifiers.new('Manufactured edge','BEVEL');mod.width=bevel;mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for f in o.data.polygons:f.use_smooth=True
    mod=o.modifiers.new('Face normals','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=50
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return o
box('Sealed inner shell',(0,0,.49),(.89,.89,.86),navy,.035)
box('Lid gasket',(0,0,.89),(.91,.91,.025),rubber,.012)
box('Armoured lid',(0,0,.935),(.95,.95,.08),navy,.025)
box('Lid inset',(0,0,.980),(.65,.69,.012),panel,.018)
for x in [-1,1]:
    for y in [-1,1]:
        box('Corner spine',(x*.425,y*.425,.50),(.13,.13,.69),edge,.022)
        for z in [.11,.91]:
            box('Impact guard',(x*.407,y*.407,z),(.21,.21,.17),edge,.025)
            box('Guard steel insert',(x*.407,y*.407,z+.087),(.10,.10,.012),steel,.008)
for s in [-1,1]:
    box('Panel gasket',(0,s*.451,.49),(.65,.016,.51),rubber,.016)
    box('Side inset',(0,s*.463,.49),(.61,.018,.47),panel,.018)
    box('Recessed handle',(0,s*.479,.69),(.31,.018,.095),rubber,.012)
    box('Handle bar',(0,s*.497,.71),(.23,.026,.028),steel,.007)
    box('Label plate',(0,s*.482,.41),(.31,.008,.17),ivory,.004)
    random.seed(13)
    x=-.125
    while x<.125:
        width=random.choice([.003,.005,.008])
        box('Barcode',(x,s*.488,.415),(width,.003,.095),ink,0);x+=width+random.choice([.003,.006])
    for x in [-.32,.32]:
        box('Light socket',(x,s*.471,.55),(.06,.024,.24),edge,.008)
        box('Status strip',(x,s*.488,.55),(.025,.008,.18),cyan,.004)
    # Latches visibly bridge the lid joint.
    for x in [-.27,.27]:
        box('Latch backing',(x,s*.481,.88),(.085,.022,.14),steel,.008)
        box('Latch clasp',(x,s*.502,.88),(.053,.022,.09),edge,.008)
    box('End inset',(s*.454,0,.49),(.022,.62,.47),panel,.018)
    box('End handle recess',(s*.469,0,.68),(.012,.30,.085),rubber,.008)
    box('End handle',(s*.484,0,.69),(.025,.23,.026),steel,.006)
for x in [-.33,.33]:box('Lid rib',(x,0,.991),(.055,.75,.035),edge,.01)
# Combine by material: one model, a small stable material set.
parts=[o for o in root.children if o.type=='MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join()
mesh=bpy.context.object;mesh.name='CargoCrate_Mesh'
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')
def area(name,loc,power,color,scale):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.color=color;data.shape='DISK';data.size=scale
    o=bpy.data.objects.new(name,data);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,.5))-o.location).to_track_quat('-Z','Y').to_euler()
area('Warm key',(2,-3,4),450,(1,.82,.63),3)
area('Cool fill',(-3,-1,2),250,(.52,.75,1),3)
area('Rim',(1,3,3),550,(.7,.86,1),2)
floor=box('Studio floor',(0,0,-.04),(200,200,.05),mat('Studio floor',(.065,.08,.1),.1,.65),0);floor.parent=None
data=bpy.data.cameras.new('Benchmark camera');camera=bpy.data.objects.new('Benchmark camera',data);scene.collection.objects.link(camera)
camera.location=(1.6,-2.1,1.6);camera.rotation_euler=(Vector((0,0,.51))-camera.location).to_track_quat('-Z','Y').to_euler();data.lens=55;scene.camera=camera
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.render.filepath=folder+'/crate-benchmark.png'
bpy.ops.wm.save_as_mainfile(filepath=folder+'/CargoCrate_Benchmark.blend')
deps=bpy.context.evaluated_depsgraph_get();evaluated=mesh.evaluated_get(deps);tri=evaluated.to_mesh();tri.calc_loop_triangles();print('Triangles:',len(tri.loop_triangles));evaluated.to_mesh_clear()
print('Saved benchmark source:',folder)
