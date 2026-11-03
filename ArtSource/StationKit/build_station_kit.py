"""Reference-led, low-poly web station kit. Execute in the connected Blender."""
import bpy, math, os, json, hashlib, struct, re
from mathutils import Vector
ROOT='D:/Projects/OrbitalCargoDispatch/ArtSource/StationKit'
OUT='D:/Projects/OrbitalCargoDispatch/StationWebHandoff'
os.makedirs(OUT+'/models',exist_ok=True)
previous=bpy.context.window.scene
scene=bpy.data.scenes.new('Stations_Authored')
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'

def image(name,linear=False):
    im=bpy.data.images.load(ROOT+'/textures/'+name+'.png',check_existing=False)
    if linear:im.colorspace_settings.name='Non-Color'
    im.pack();return im

def material(name,color,orm,emission=None):
    m=bpy.data.materials.new(name);m.use_nodes=True;nt=m.node_tree;p=nt.nodes.get('Principled BSDF')
    p.inputs['Metallic'].default_value=1;p.inputs['Roughness'].default_value=1
    def tex(im):
        n=nt.nodes.new('ShaderNodeTexImage');n.image=im;return n
    nt.links.new(tex(color).outputs['Color'],p.inputs['Base Color'])
    t=tex(orm);sep=nt.nodes.new('ShaderNodeSeparateColor');nt.links.new(t.outputs['Color'],sep.inputs[0])
    nt.links.new(sep.outputs['Green'],p.inputs['Roughness']);nt.links.new(sep.outputs['Blue'],p.inputs['Metallic'])
    group=bpy.data.node_groups.get('glTF Material Output')
    if group is None:
        group=bpy.data.node_groups.new('glTF Material Output','ShaderNodeTree');group.interface.new_socket(name='Occlusion',in_out='INPUT',socket_type='NodeSocketFloat')
    oc=nt.nodes.new('ShaderNodeGroup');oc.node_tree=group;nt.links.new(sep.outputs['Red'],oc.inputs['Occlusion'])
    if emission:
        nt.links.new(tex(emission).outputs['Color'],p.inputs['Emission Color']);p.inputs['Emission Strength'].default_value=1
    return m

atlas=material('Stations_SharedPBR_1024',image('Stations_BaseColor'),image('Stations_ORM',True),image('Stations_Emission'))
beltmat=material('Conveyor_RepeatBelt_256',image('Belt_BaseColor'),image('Belt_ORM',True))
glass=bpy.data.materials.new('Scanner_Glass_LowOverdraw');glass.use_nodes=True
p=glass.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(.12,.52,.59,1);p.inputs['Roughness'].default_value=.22;p.inputs['Metallic'].default_value=.05;p.inputs['Alpha'].default_value=.16
glass.surface_render_method='BLENDED';glass.use_backface_culling=True

parts=[];models={};assets=[]
def uv_tile(obj,tile):
    mesh=obj.data;uv=mesh.uv_layers.new(name='UVMap') if not mesh.uv_layers else mesh.uv_layers[0]
    verts=[v.co for v in mesh.vertices];bounds=[(min(v[i] for v in verts),max(v[i] for v in verts)) for i in range(3)]
    for face in mesh.polygons:
        axis=max(range(3),key=lambda i:abs(face.normal[i]));a,b=((1,2) if axis==0 else (0,2) if axis==1 else (0,1))
        for li in face.loop_indices:
            co=mesh.vertices[mesh.loops[li].vertex_index].co
            u=(co[a]-bounds[a][0])/max(.000001,bounds[a][1]-bounds[a][0]);v=(co[b]-bounds[b][0])/max(.000001,bounds[b][1]-bounds[b][0])
            if tile==8:v=.3125+.375*v
            uv.data[li].uv=((tile%4+(3+250*u)/256)/4,(3-tile//4+(3+250*v)/256)/4)

def mesh_obj(name,verts,faces,tile,mat=None,smooth=False):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj);mesh.materials.append(mat or atlas)
    uv_tile(obj,tile)
    for p in mesh.polygons:p.use_smooth=smooth
    parts.append(obj);return obj

def box(name,pos,size,tile=0,bevel=.018,rot=None):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name;o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Silhouette bevel','BEVEL');mod.width=min(bevel,min(size)*.3);mod.segments=1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    uv_tile(o,tile);o.data.materials.append(atlas)
    for p in o.data.polygons:p.use_smooth=True
    mod=o.modifiers.new('Weighted face normals','WEIGHTED_NORMAL');mod.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    if rot:o.rotation_euler=rot
    parts.append(o);return o

def cylinder(name,pos,r,depth,tile=3,n=8,rotation=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=n,radius=r,depth=depth,location=pos)
    o=bpy.context.object;o.name=name;uv_tile(o,tile);o.data.materials.append(atlas)
    if rotation:o.rotation_euler=rotation
    for p in o.data.polygons:p.use_smooth=len(p.vertices)==4
    parts.append(o);return o

def front(name,x,y,z,w,h,tile,rotation=0):
    o=mesh_obj(name,[(-w/2,0,-h/2),(w/2,0,-h/2),(w/2,0,h/2),(-w/2,0,h/2)],[(0,1,2,3)],tile)
    o.location=(x,y,z);o.rotation_euler.x=rotation;return o

def pipe(name,path,r=.035,tile=4,sides=8):
    pts=[Vector(p) for p in path];verts=[]
    for i,p in enumerate(pts):
        tangent=(pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]).normalized()
        ref=Vector((0,1,0)) if abs(tangent.y)<.9 else Vector((1,0,0))
        a=tangent.cross(ref).normalized();b=tangent.cross(a).normalized()
        for j in range(sides):verts.append(p+r*(math.cos(j*math.tau/sides)*a+math.sin(j*math.tau/sides)*b))
    faces=[]
    for i in range(len(pts)-1):
        for j in range(sides):faces.append((i*sides+j,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,(i+1)*sides+j))
    faces.extend([tuple(reversed(range(sides))),tuple((len(pts)-1)*sides+j for j in range(sides))])
    return mesh_obj(name,verts,faces,tile,smooth=True)

def empty(name,pos,parent):
    o=bpy.data.objects.new(name,None);scene.collection.objects.link(o);o.parent=parent;o.location=pos;return o

def join(name,selected=None):
    selected=list(parts if selected is None else selected)
    bpy.ops.object.select_all(action='DESELECT')
    for o in selected:o.select_set(True)
    bpy.context.view_layer.objects.active=selected[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
    scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    for item in selected:
        if item in parts:parts.remove(item)
    return o

def root(name):
    o=bpy.data.objects.new(name,None);scene.collection.objects.link(o);return o

def register(asset_id,r,filename,budget,notes):
    meshes=[o for o in r.children_recursive if o.type=='MESH'];triangles=0
    for o in meshes:o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles)
    if triangles>budget:raise RuntimeError(asset_id+' over budget: '+str(triangles))
    bpy.ops.object.select_all(action='DESELECT');r.select_set(True)
    for o in r.children_recursive:o.select_set(True)
    bpy.context.view_layer.objects.active=r
    path=OUT+'/models/'+filename
    bpy.ops.export_scene.gltf(filepath=path,export_format='GLB',use_selection=True,use_active_scene=True,export_yup=True,export_animations=False,export_cameras=False,export_lights=False)
    # Blender's global datablock suffixes must not leak into the stable web API.
    raw=open(path,'rb').read();jsonlen=struct.unpack_from('<I',raw,12)[0]
    doc=json.loads(raw[20:20+jsonlen]);tail=raw[20+jsonlen:]
    for node in doc.get('nodes',[]):
        if 'name' in node:node['name']=re.sub(r'\.\d{3}$','',node['name'])
    encoded=json.dumps(doc,separators=(',',':')).encode();encoded+=b' '*((-len(encoded))%4)
    with open(path,'wb') as f:f.write(struct.pack('<III',0x46546C67,2,20+len(encoded)+len(tail))+struct.pack('<II',len(encoded),0x4E4F534A)+encoded+tail)
    bounds=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
    dims=[max(p[i] for p in bounds)-min(p[i] for p in bounds) for i in range(3)]
    entry={'id':asset_id,'url':'models/'+filename,'triangles':triangles,'triangleBudget':budget,'meshPrimitives':sum(len(o.data.materials) for o in meshes),'dimensionsXYZ':[round(dims[0],4),round(dims[2],4),round(dims[1],4)],'bytes':os.path.getsize(path),'sha256':hashlib.sha256(open(path,'rb').read()).hexdigest(),'notes':notes}
    assets.append(entry);models[asset_id]=r;print(json.dumps(entry))

# CONVEYOR. A 5.6 m single draw opaque frame plus separately scrollable belt.
conveyor=root('Conveyor_Station')
for y in [-.51,.51]:
    box('Chassis_spine',(0,y,.65),(5.6,.12,.34),2,.035)
    box('Steel_top_edge',(0,y,.862),(5.52,.08,.035),3,.006)
    box('Underframe',(0,y,.25),(5.20,.09,.10),2,.01)
    for x in [-2.5,-.83,.83,2.5]:
        box('Support_leg',(x,y,.38),(.15,.20,.72),2,.022)
        box('Bolted_foot',(x,y,.035),(.34,.34,.07),1,.015)
        for dx in [-.115,.115]:cylinder('Foot_fastener',(x+dx,y-.10,.077),.018,.012,3,6)
        box('Recessed_lamp_gasket',(x,math.copysign(.625,y),.50),(.09,.024,.30),5,.012)
        box('Amber_leg_practical',(x,math.copysign(.644,y),.50),(.047,.012,.235),6,.004)
    for i in range(6):
        x=-2.29+i*.916
        box('Chassis_removable_panel',(x,y*1.10,.73),(.89,.035,.235),0,.018)
        for dx in [-.37,.37]:
            cylinder('Side_fastener',(x+dx,y*1.142,.79),.016,.010,3,6,(math.pi/2,0,0))
for x in [-2.67,2.67]:
    cylinder('Belt_return_roller',(x,0,.76),.105,.94,2,12,(math.pi/2,0,0))
    for y in [-.54,.54]:box('End_bearing_guard',(x,y,.75),(.28,.13,.31),1,.035)
# Rear guard behind the intake edge; a named separate node can be hidden at integration.
guardparts=[]
before=set(parts)
pipe('Yellow_end_guard',[(2.85,-.50,0),(2.85,-.50,.98),(2.85,-.47,1.05),(2.85,-.40,1.08),(2.85,.40,1.08),(2.85,.47,1.05),(2.85,.50,.98),(2.85,.50,0)],.035)
pipe('Guard_crossbar',[(2.85,-.50,.50),(2.85,.50,.50)],.027)
guard=join('Conveyor_IntakeGuard',[o for o in parts if o not in before]);guard.parent=conveyor
frame=join('Conveyor_Frame');frame.parent=conveyor
# Slight wrap at each roller. UV repeated along X, rib direction across the belt.
verts=[];xs=[(-2.77,.745),(-2.75,.805),(-2.69,.85),(2.69,.85),(2.75,.805),(2.77,.745)]
for x,z in xs:verts.extend([(x,-.455,z),(x,.455,z)])
belt=mesh_obj('Conveyor_Belt',verts,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(5)],5,beltmat)
for poly in belt.data.polygons:
    for li in poly.loop_indices:
        co=belt.data.vertices[belt.data.loops[li].vertex_index].co;belt.data.uv_layers[0].data[li].uv=((co.y+.455)/.91,(co.x+2.77)*1.5)
belt.parent=conveyor;parts.remove(belt)
empty('Conveyor_SurfaceStart',(-2.5,0,.85),conveyor);empty('Conveyor_SurfaceEnd',(2.5,0,.85),conveyor)
register('conveyor',conveyor,'conveyor.glb',5500,'Metres; floor pivot; cargo surface Y=.85; travel +X. Belt and intake guard are named separate nodes. Belt UV V runs along travel.')

# SCANNER. Beveled header, open accessible front, bounded glass on three sides.
scanner=root('Scanner_Station')
box('Foundation',(0,0,.10),(1.98,1.72,.20),2,.065)
box('Hazard_step',(0,-.86,.14),(1.85,.12,.16),2,.025)
box('Deck_inset',(0,0,.215),(1.57,1.38,.04),1,.025)
box('Deck_scan_plate',(0,0,.243),(1.25,1.10,.02),0,.025)
for x in [-.88,.88]:
    for y in [-.73,.73]:
        box('Armored_upright',(x,y,1.38),(.18,.19,2.48),2,.032)
        box('Column_foot',(x,y,.31),(.26,.27,.30),0,.04)
        box('Column_cap',(x,y,2.40),(.24,.25,.24),1,.03)
        if y<0:
            box('Status_recess',(x,y-.105,1.27),(.075,.027,.38),5,.01)
            box('Status_amber',(x,y-.124,1.27),(.039,.012,.29),6,.005)
    box('Glass_sill',(x,0,.28),(.10,1.35,.09),3,.01)
    box('Upper_frame',(x,0,2.43),(.10,1.35,.12),0,.02)
    box('Emitter_recess',(x*.86,.61,1.28),(.14,.10,1.55),1,.018)
    box('Emitter_strip',(x*.86,.548,1.28),(.045,.018,1.35),7,.006)
box('Header_armor',(0,0,2.63),(2.02,1.80,.40),0,.085)
box('Header_left_cap',(-.91,0,2.63),(.16,1.81,.37),1,.028)
box('Header_right_cap',(.91,0,2.63),(.16,1.81,.37),1,.028)
front('SCAN_header_label',0,-.906,2.63,1.35,.34,8)
front('Header_side_vent',-.93,-.915,2.61,.10,.20,12)
box('Diffuser_gasket',(0,0,2.415),(1.63,1.30,.035),5,.012)
box('Cyan_ceiling_diffuser',(0,0,2.39),(1.40,1.10,.02),7,.02)
box('Back_crossbar',(0,.75,.35),(1.58,.10,.13),0,.02)
# Real geometry only where it changes the silhouette or gives useful depth.
for x in [-.58,.58]:
    box('Deck_luminous_guide',(x,0,.258),(.018,.94,.008),7,.002)
for y in [-.47,.47]:box('Deck_guide_return',(0,y,.258),(1.17,.018,.008),7,.002)
for x in [-.71,.71]:
    for z in [.65,1.95]:
        box('Emitter_mount',(x,.61,z),(.23,.16,.14),0,.022)
for x in [-.85,.85]:
    for z in [.36,2.43,2.69]:cylinder('Scanner_front_lock',(x,-.842 if z<2.5 else -.91,z),.019,.012,3,6,(math.pi/2,0,0))
for side in [-1,1]:
    # Louver inserts on the header's side, using the same atlas instead of slats.
    o=front('Header_cooling_grille',side*1.015,0,2.64,1.12,.23,12)
    o.rotation_euler.z=side*math.pi/2
# Control pedestal follows the original asymmetric silhouette.
box('Terminal_foot',(1.20,-.47,.065),(.57,.62,.13),2,.04)
box('Terminal_pedestal',(1.20,-.47,.62),(.48,.52,1.14),1,.045)
front('Terminal_service_vent',1.20,-.735,.45,.32,.39,12)
box('Terminal_lamp_recess',(1.20,-.742,.97),(.34,.025,.10),2,.015)
box('Terminal_ready_lamp',(1.20,-.762,.97),(.26,.015,.045),6,.006)
angle=math.radians(-17)
box('Angled_display_housing',(1.20,-.41,1.38),(.62,.17,.63),2,.045,(angle,0,0))
screen=front('Control_display',1.20,-.505,1.405,.49,.49,10,angle)
for x in [-.74,.74]:
    for y in [-.61,.61]:cylinder('Deck_lock',(x,y,.246),.027,.01,3,8)
frame=join('Scanner_Frame');frame.parent=scanner
# Replace the stretched hazard UV on the wide front riser with ten clean stripes.
for i in range(12):
    x0=-.89+i*.148
    mesh_obj('Step_safety_stripe',[(x0,-.924,.065),(x0+.148,-.924,.065),(x0+.224,-.924,.215),(x0+.076,-.924,.215)],[(0,1,2,3)],4 if i%2==0 else 2)
stripes=join('Step_paint');stripes.parent=scanner
# Merge static paint back into the opaque frame to retain one static draw.
bpy.ops.object.select_all(action='DESELECT');frame.select_set(True);stripes.select_set(True);bpy.context.view_layer.objects.active=frame;bpy.ops.object.join()
# Three single-sided panels: no front pane to obscure placing/scanning cargo.
gv=[(-.87,-.62,.34),(-.87,.62,.34),(-.87,.62,2.36),(-.87,-.62,2.36),(.87,-.62,.34),(.87,-.62,2.36),(.87,.62,2.36),(.87,.62,.34),(-.76,.73,.34),(.76,.73,.34),(.76,.73,2.36),(-.76,.73,2.36)]
g=mesh_obj('Scanner_Glass',gv,[(3,2,1,0),(7,6,5,4),(11,10,9,8)],7,glass);g.parent=scanner;parts.remove(g)
beam=box('Scanner_ScanBeam',(0,0,1.30),(1.51,.024,.012),7,.002);parts.remove(beam);beam.parent=scanner
empty('Scanner_CargoSlot',(0,0,.255),scanner);empty('Scanner_ControlTarget',(1.20,-.75,1.3),scanner)
register('scanner',scanner,'scanner.glb',5500,'Front is +Z; floor pivot; cargo slot Y=.255. Three single-sided glass panes, open front. ScanBeam node translates vertically. No transmission extension or real-time lights.')

# DISPOSAL CHUTE. 32-sided open throat, no hidden bottom cap, segmented rim.
chute=root('Disposal_Station')
def annulus(name,profile,n=32,tile=2,hazard=False):
    verts=[]
    for r,z in profile:
        for j in range(n):a=j*math.tau/n;verts.append((r*math.cos(a),r*math.sin(a),z))
    faces=[]
    for k in range(len(profile)-1):
        for j in range(n):faces.append((k*n+j,k*n+(j+1)%n,(k+1)*n+(j+1)%n,(k+1)*n+j))
    o=mesh_obj(name,verts,faces,tile)
    if hazard:
        # Each rim sector is independently painted using the shared trim sheet.
        uv=o.data.uv_layers[0]
        for p in o.data.polygons:
            t=4 if (p.index%n)//2%2==0 else 2
            for j,li in enumerate(p.loop_indices):uv.data[li].uv=((t%4+(.08 if j in [0,3] else .92))/4,(3-t//4+(.08 if j in [0,1] else .92))/4)
    return o
annulus('Outer_mounting_flange',[(1.40,-.05),(1.40,-.005),(1.35,.018),(1.20,.018)],32,1)
annulus('Segmented_hazard_rim',[(1.24,.018),(1.22,.055),(1.07,.055),(1.035,.015)],32,2,True)
annulus('Steel_inner_lip',[(1.038,.02),(1.01,.005),(.99,-.035)],32,3)
annulus('Open_throat',[(.99,-.035),(.99,-.90)],32,2)
# Vertical inner panels, sparsely modeled seams and fasteners.
for j in range(16):
    a=j*math.tau/16;x,y=.984*math.cos(a),.984*math.sin(a)
    o=box('Throat_panel_seam',(x,y,-.46),(.014,.018,.82),0,.002);o.rotation_euler.z=a
for j in range(12):
    a=j*math.tau/12;cylinder('Flange_bolt',(1.30*math.cos(a),1.30*math.sin(a),.027),.024,.012,3,6)
# Curved rear rail, 16 arc samples with eight-sided tube cross sections.
angles=[math.radians(12+i*156/16) for i in range(17)]
for height in [.56,1.05]:pipe('Rear_safety_rail',[(1.27*math.cos(a),1.27*math.sin(a),height) for a in angles],.035,4,8)
for a in [angles[0],angles[4],angles[12],angles[-1]]:
    x,y=1.27*math.cos(a),1.27*math.sin(a)
    pipe('Rail_post',[(x,y,.035),(x,y,1.05)],.036,4,8)
    box('Rail_mount',(x,y,.055),(.17,.17,.07),4,.015)
box('Disposal_sign_housing',(0,1.10,.84),(.77,.12,.82),4,.05)
front('TO_DISPOSAL_sign',0,1.032,.84,.69,.72,9)
front('Inner_down_arrow',0,.977,-.35,.35,.50,15)
# Beacon is optional emission only; no point light or actual flashing animation.
box('Beacon_mount',(-1.0,.75,1.08),(.19,.19,.09),2,.02)
cylinder('Amber_beacon',(-1.0,.75,1.21),.07,.18,6,12)
cylinder('Beacon_cap',(-1.0,.75,1.31),.074,.025,4,12)
frame=join('Disposal_Frame');frame.parent=chute
empty('Disposal_DropTarget',(0,0,0),chute)
empty('Disposal_ThroatBottom',(0,0,-.9),chute)
register('disposal-chute',chute,'disposal-chute.glb',4000,'Deck-plane pivot Y=0. Open throat extends down .9 m; floor must have an opening. Hole diameter 1.98 m; outer flange diameter 2.8 m. Front is +Z.')

manifest={'formatVersion':1,'units':'metres','coordinates':'glTF +Y up, fronts +Z','textures':{'atlas':1024,'belt':256,'opaquePBR':'baseColor + packed ORM + emissive','glass':'alpha BLEND; single-sided; no transmission'},'triangleBudgetTotal':15000,'trianglesTotal':sum(a['triangles'] for a in assets),'assets':assets}
with open(OUT+'/manifest.json','w') as f:json.dump(manifest,f,indent=2)
# Store the isolated authoring scene, without replacing the user's original scene/file.
scene.world=bpy.data.worlds.new('Stations_StudioWorld');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.16,.22,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.4
# Spread the authored stations for convenient Blender inspection AFTER metric exports.
conveyor.location=(-4,0,0);scanner.location=(2.3,0,0);chute.location=(6.5,0,0)
bpy.data.libraries.write(ROOT+'/Stations_Authored.blend',{scene},fake_user=True,compress=True)
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_distance=15;area.spaces.active.region_3d.view_location=(0,0,1)
        area.spaces.active.shading.type='MATERIAL'
bpy.context.window.scene=previous
print('STATIONS_COMPLETE '+json.dumps(manifest))
