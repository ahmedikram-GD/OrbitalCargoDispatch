import bpy, numpy as np
folder='D:/Projects/OrbitalCargoDispatch/ArtSource/CargoCrate/'
im=bpy.data.images.load(folder+'Crate_Atlas_Color.png',check_existing=False)
pixels=np.array(im.pixels[:],dtype=np.float32).reshape(-1,4)
pixels[:,:3]=np.power(np.maximum(pixels[:,:3],0),.70)
im.pixels.foreach_set(pixels.ravel())
im.filepath_raw=folder+'Crate_Atlas_Color_URP.png'
im.file_format='PNG'
im.save()
print('Saved brighter navy palette for the existing Gamma-space dock')
