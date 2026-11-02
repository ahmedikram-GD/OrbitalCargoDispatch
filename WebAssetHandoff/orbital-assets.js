/* Three.js r128 adapter. Load three.min.js and GLTFLoader.js before this file. */
(function (global) {
  'use strict';
  const files = {
    'crate-standard': 'models/crate-standard.glb',
    'floor-2m': 'models/floor-2m.glb',
    'wall-2m': 'models/wall-2m.glb',
    'fixture-160cm': 'models/fixture-160cm.glb'
  };

  async function load(baseURL, onProgress) {
    const THREE = global.THREE;
    if (!THREE || !THREE.GLTFLoader) throw new Error('Load Three.js and the matching GLTFLoader first.');
    const base = new URL(baseURL || './', document.baseURI);
    if (!base.pathname.endsWith('/')) base.pathname += '/';
    const loader = new THREE.GLTFLoader();
    const templates = {};
    await Promise.all(Object.keys(files).map(async id => {
      const url = new URL(files[id], base).href;
      const gltf = await new Promise((resolve, reject) => {
        loader.load(url, resolve, event => {
          if (onProgress) onProgress({ id, loaded: event.loaded, total: event.total });
        }, error => reject(new Error('Could not load ' + url + ': ' + (error.message || 'check HTTP/CORS access'))));
      });
      templates[id] = gltf.scene;
    }));

    // All kit GLBs embed the same atlas. Reuse one material/texture set after loading.
    let kitMaterial;
    templates['floor-2m'].traverse(object => { if (object.isMesh && !kitMaterial) kitMaterial = object.material; });
    const discardedMaterials = new Set();
    const discardedTextures = new Set();
    ['wall-2m', 'fixture-160cm'].forEach(id => templates[id].traverse(object => {
      if (!object.isMesh) return;
      const original = object.material;
      if (original !== kitMaterial) {
        discardedMaterials.add(original);
        ['map', 'metalnessMap', 'roughnessMap', 'aoMap', 'emissiveMap', 'normalMap'].forEach(key => {
          if (original[key]) discardedTextures.add(original[key]);
        });
      }
      object.material = kitMaterial;
    }));
    discardedTextures.forEach(texture => texture.dispose());
    discardedMaterials.forEach(material => material.dispose());

    function create(id, options) {
      if (!templates[id]) throw new Error('Unknown asset: ' + id);
      options = options || {};
      const container = new THREE.Group();
      container.name = 'Visual:' + id;
      const model = templates[id].clone(true);
      container.add(model);
      const bounds = new THREE.Box3().setFromObject(model);
      const size = bounds.getSize(new THREE.Vector3());
      if (options.size != null) {
        if (!(options.size > 0)) throw new Error('Asset size must be greater than zero.');
        model.scale.multiplyScalar(options.size / Math.max(size.x, size.y, size.z));
      }
      if (options.pivot === 'bottom' || options.pivot === 'center') {
        bounds.setFromObject(model);
        const center = bounds.getCenter(new THREE.Vector3());
        model.position.x -= center.x;
        model.position.z -= center.z;
        model.position.y -= options.pivot === 'bottom' ? bounds.min.y : center.y;
      }
      container.traverse(object => {
        if (!object.isMesh) return;
        object.castShadow = options.castShadow !== false;
        object.receiveShadow = options.receiveShadow !== false;
      });
      container.userData.orbitalAssetId = id;
      return container;
    }

    function dispose() {
      const geometries = new Set(), materials = new Set(), textures = new Set();
      Object.values(templates).forEach(root => root.traverse(object => {
        if (!object.isMesh) return;
        geometries.add(object.geometry);
        materials.add(object.material);
      }));
      materials.forEach(material => {
        Object.values(material).forEach(value => { if (value && value.isTexture) textures.add(value); });
        material.dispose();
      });
      textures.forEach(texture => texture.dispose());
      geometries.forEach(geometry => geometry.dispose());
    }
    return { create, dispose, ids: Object.keys(files) };
  }
  global.OrbitalAssets = { load };
})(window);
