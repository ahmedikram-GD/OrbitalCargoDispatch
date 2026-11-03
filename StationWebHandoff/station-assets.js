/* Three.js r128. Load matching THREE and GLTFLoader before this adapter. */
(function(global){
  'use strict';
  async function load(baseURL){
    const base=new URL(baseURL||'./',document.baseURI);if(!base.pathname.endsWith('/'))base.pathname+='/';
    const manifest=await fetch(new URL('manifest.json',base)).then(r=>{if(!r.ok)throw new Error('Manifest: HTTP '+r.status);return r.json();});
    const loader=new THREE.GLTFLoader(),templates={};
    await Promise.all(manifest.assets.map(async a=>{
      const gltf=await new Promise((resolve,reject)=>loader.load(new URL(a.url,base).href,resolve,undefined,reject));templates[a.id]=gltf.scene;
    }));
    // All stations embed the identical atlas. Deduplicate its GPU material and maps.
    let atlas;const discardedMaterials=new Set(),discardedTextures=new Set();
    Object.values(templates).forEach(root=>root.traverse(o=>{
      if(!o.isMesh)return;
      if(o.material.name.startsWith('Stations_SharedPBR')){
        if(!atlas)atlas=o.material;
        if(o.material!==atlas){
          discardedMaterials.add(o.material);
          for(const k of ['map','metalnessMap','roughnessMap','aoMap','emissiveMap'])if(o.material[k])discardedTextures.add(o.material[k]);
          o.material=atlas;
        }
      }
      if(o.material.transparent){o.material.depthWrite=false;o.castShadow=false;}
    }));
    discardedTextures.forEach(t=>t.dispose());discardedMaterials.forEach(m=>m.dispose());
    function create(id){
      if(!templates[id])throw new Error('Unknown station: '+id);
      const root=templates[id].clone(true);root.name='Station:'+id;
      root.traverse(o=>{if(o.isMesh){o.castShadow=!o.material.transparent&&!o.name.includes('ScanBeam');o.receiveShadow=!o.material.transparent;}});
      return root;
    }
    function dispose(){
      const geometries=new Set(),materials=new Set(),textures=new Set();
      Object.values(templates).forEach(root=>root.traverse(o=>{if(o.isMesh){geometries.add(o.geometry);materials.add(o.material);}}));
      materials.forEach(m=>{Object.values(m).forEach(v=>{if(v&&v.isTexture)textures.add(v);});m.dispose();});
      textures.forEach(t=>t.dispose());geometries.forEach(g=>g.dispose());
    }
    return {create,dispose,manifest,ids:manifest.assets.map(a=>a.id)};
  }
  global.OrbitalStations={load};
})(window);
