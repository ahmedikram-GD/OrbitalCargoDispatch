import bpy, math, os, random
import numpy as np
from mathutils import Vector

folder='D:/Projects/OrbitalCargoDispatch/ArtSource/DockKit'
os.makedirs(folder,exist_ok=True)
scene=bpy.data.scenes.new('DockKit_Authored')
bpy.context.window.scene=scene
scene.world=bpy.data.worlds.new('DockKit_World');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.15,.19,.25,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.4
scene.render.engine='CYCLES';scene.cycles.samples=8;scene.render.bake.margin=12
rng=np.random.default_rng(54);N=1024
noise=rng.normal(0,.003,(N,N)).astype(np.float32)
coarse=np.repeat(np.repeat(rng.normal(0,.008,(32,32)),32,axis=0),32,axis=1)
for i in range(12):coarse=(coarse+np.roll(coarse,1,0)+np.roll(coarse,-1,0)+np.roll(coarse,1,1)+np.roll(coarse,-1,1))/5
noise+=coarse.astype(np.float32)
scuff=np.zeros((N,N),np.float32)
for i in range(350):
    x,y=rng.integers(2,N-120,size=2);length=int(rng.integers(3,40))
    scuff[y,x:x+length]=float(rng.uniform(.007,.02))
def image(name,pixels,noncolor=False,size=N):
    im=bpy.data.images.new(name,width=size,height=size,alpha=True)
    if noncolor:im.colorspace_settings.name='Non-Color'
    im.pixels.foreach_set(pixels.ravel());im.filepath_raw=folder+'/'+name+'.png';im.file_format='PNG';im.save();im.pack();return im
def material(name,col,metal=.3,rough=.5,texture=False,emission=0):
    m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*col,1)
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*col,1)
    p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    if texture:
        a=np.ones((N,N,4),np.float32);a[:,:,:3]=np.clip(np.array(col)[None,None,:]+noise[:,:,None]+scuff[:,:,None],0,1)
        n=m.node_tree.nodes.new('ShaderNodeTexImage');n.image=image(name+'_paint',a);m.node_tree.links.new(n.outputs['Color'],p.inputs['Base Color'])
        a[:,:,:3]=np.clip(rough+noise[:,:,None]*4+scuff[:,:,None]*2,.2,.9)
        n=m.node_tree.nodes.new('ShaderNodeTexImage');n.image=image(name+'_rough',a,True);m.node_tree.links.new(n.outputs['Color'],p.inputs['Roughness'])
    if emission:p.inputs['Emission Color'].default_value=(*col,1);p.inputs['Emission Strength'].default_value=emission
    return m
floor=material('Deck_enamel',(.30,.32,.33),.18,.38,True)
wall=material('Wall_navy',(.14,.20,.27),.25,.55,True)
inset=material('Panel_blue',(.19,.27,.34),.25,.53,True)
frame=material('Frame_coating',(.07,.10,.13),.35,.48,True)
steel=material('Brushed_steel',(.38,.43,.46),.8,.32)
rubber=material('Gasket',(.018,.025,.032),0,.8)
amber=material('Safety_ochre',(.85,.47,.09),.1,.53)
lamp=material('Warm_diffuser',(1,.62,.23),0,.35,False,3)
modules={};parts=[]
def box(name,pos,size,mat,bevel=.015):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name;o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mat)
    if bevel:
        mod=o.modifiers.new('Edge bevel','BEVEL');mod.width=bevel;mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in o.data.polygons:p.use_smooth=True
    mod=o.modifiers.new('Face normals','WEIGHTED_NORMAL');mod.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    parts.append(o);return o
def bolt(pos,r=.018):
    bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=r,depth=.008,location=pos)
    o=bpy.context.object;o.name='Recessed_fastener';o.data.materials.append(steel);parts.append(o)
def finish(name,origin):
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
    group=o.vertex_groups.new(name=name);group.add(list(range(len(o.data.vertices))),1,'REPLACE')
    modules[name]=(o,Vector(origin));parts.clear()

# Floor: 2 m module, top at zero; inset seams and thin steel perimeter.
box('Deck_subframe',(0,0,-.065),(2,2,.10),frame,.012)
box('Deck_plate',(0,0,-.019),(1.997,1.997,.038),floor,.002)
finish('Dock_Floor_2m',(0,0,0))

# Wall: 2 m wide, 3.8 m tall. Front is Blender -Y, back sits on y=0.
X=4
box('Back_structure',(X,.07,1.9),(2,.14,3.8),frame,.015)
box('Main_recess',(X,-.018,2.02),(1.72,.05,2.85),rubber,.02)
for x in [-.405,.405]:
    box('Wall_panel',(X+x,-.055,2.04),(.78,.035,2.70),wall,.04)
    box('Inset_service_panel',(X+x,-.082,1.53),(.60,.018,.75),inset,.025)
    for z in [1.22,1.34,1.46]:box('Vent_slot',(X+x,-.095,z),(.39,.012,.025),rubber,.003)
for s in [-1,1]:
    box('Structural_column',(X+s*.94,-.065,1.9),(.12,.25,3.8),frame,.018)
    box('Column_insert',(X+s*.94,-.199,2.12),(.055,.022,1.15),steel,.006)
    for z in [.20,.5]:box('Impact_bumper',(X,-.14,z),(1.76,.18,.14),frame,.025)
box('Top_cap',(X,-.065,3.67),(1.8,.22,.16),frame,.018)
box('Header_ochre',(X,-.18,3.67),(.45,.015,.045),amber,.004)
box('Maintenance_plate',(X+.48,-.10,2.64),(.22,.015,.15),steel,.005)
finish('Dock_Wall_2m',(X,0,0))

# Ceiling luminaire: 1.6 m housing, recessed diffuser and protective crossbars.
X=8
box('Mounting_spine',(X,0,-.045),(1.50,.16,.09),steel,.01)
box('Luminaire_body',(X,0,-.15),(1.66,.56,.22),frame,.035)
box('Reflector',(X,0,-.268),(1.48,.44,.025),steel,.015)
box('Diffuser_gasket',(X,0,-.285),(1.36,.35,.025),rubber,.01)
box('Warm_diffuser',(X,0,-.30),(1.29,.28,.018),lamp,.008)
for x in [-.47,0,.47]:box('Diffuser_guard',(X+x,0,-.319),(.022,.37,.025),frame,.004)
for x in [-.75,.75]:
    box('End_cap',(X+x,0,-.17),(.11,.59,.20),steel,.015)
    for y in [-.13,0,.13]:box('Cooling_slot',(X+x,y,-.276),(.065,.035,.009),rubber,.003)
finish('Dock_Light_160cm',(X,0,0))

# Join only for a shared UV atlas and bake. Vertex groups retain each module identity.
bpy.ops.object.select_all(action='DESELECT')
for o,origin in modules.values():o.select_set(True)
bpy.context.view_layer.objects.active=next(iter(modules.values()))[0];bpy.ops.object.join();master=bpy.context.object;master.name='DockKit_AtlasMaster'
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.008);bpy.ops.object.mode_set(mode='OBJECT')
mats=list(master.data.materials);targets=[]
for m in mats:
    n=m.node_tree.nodes.new('ShaderNodeTexImage');targets.append(n)
def bake(name,kind,noncolor=False):
    im=bpy.data.images.new(name,width=2048,height=2048,alpha=True)
    if noncolor:im.colorspace_settings.name='Non-Color'
    for m,n in zip(mats,targets):n.image=im;m.node_tree.nodes.active=n
    args={'type':kind,'use_clear':True}
    if kind=='DIFFUSE':args['pass_filter']={'COLOR'}
    bpy.ops.object.bake(**args);im.filepath_raw=folder+'/'+name+'.png';im.file_format='PNG';im.save();im.pack();return im
color=bake('DockKit_Color','DIFFUSE');rough=bake('DockKit_Roughness','ROUGHNESS',True)
emissive=bake('DockKit_Emission','EMIT');ao=bake('DockKit_AO','AO',True)
restore=[]
for m in mats:
    nt=m.node_tree;p=nt.nodes.get('Principled BSDF');out=next(n for n in nt.nodes if n.type=='OUTPUT_MATERIAL');original=out.inputs[0].links[0].from_socket
    node=nt.nodes.new('ShaderNodeEmission');node.inputs[0].default_value=(p.inputs['Metallic'].default_value,)*3+(1,)
    nt.links.new(node.outputs[0],out.inputs[0]);restore.append((nt,out,original,node))
metal=bake('DockKit_Metallic','EMIT',True)
for nt,out,original,node in restore:nt.links.new(original,out.inputs[0]);nt.nodes.remove(node)
a=np.array(metal.pixels[:],np.float32).reshape(-1,4);r=np.array(rough.pixels[:],np.float32).reshape(-1,4)
a[:,3]=1-np.clip(r[:,0],0,1);image('DockKit_MetalSmooth',a,True,2048)
atlasmat=bpy.data.materials.new('DockKit_Atlas');atlasmat.use_nodes=True
n=atlasmat.node_tree.nodes.new('ShaderNodeTexImage');n.image=color;atlasmat.node_tree.links.new(n.outputs['Color'],atlasmat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
report={}
for name,(_,origin) in modules.items():
    o=master.copy();o.data=master.data.copy();scene.collection.objects.link(o);o.name=name
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    o.vertex_groups.active_index=o.vertex_groups[name].index
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='DESELECT');bpy.ops.object.vertex_group_select();bpy.ops.mesh.select_all(action='INVERT');bpy.ops.mesh.delete(type='VERT');bpy.ops.object.mode_set(mode='OBJECT')
    scene.cursor.location=origin;bpy.ops.object.origin_set(type='ORIGIN_CURSOR');o.location=(0,0,0)
    o.data.materials.clear();o.data.materials.append(atlasmat)
    for p in o.data.polygons:p.material_index=0
    bpy.ops.export_scene.fbx(filepath=folder+'/'+name+'.fbx',use_selection=True,object_types={'MESH'},bake_anim=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y')
    o.data.calc_loop_triangles();report[name]=len(o.data.loop_triangles)
    o.location=origin
master.hide_render=True;master.hide_set(True)
scene.cursor.location=(0,0,0)
bpy.data.libraries.write(folder+'/DockKit_Authored.blend',{scene},fake_user=True,compress=True)
print('Module triangle counts:',report)
