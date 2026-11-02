(async function () {
  'use strict';
  const status = document.getElementById('status');
  try {
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x101923);
    const camera = new THREE.PerspectiveCamera(45, innerWidth / innerHeight, 0.05, 100);
    const renderer = new THREE.WebGLRenderer({ canvas: document.getElementById('view'), antialias: true, powerPreference: 'high-performance' });
    renderer.setPixelRatio(Math.min(devicePixelRatio, 1.75));
    renderer.setSize(innerWidth, innerHeight);
    renderer.outputEncoding = THREE.sRGBEncoding;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.0;
    renderer.physicallyCorrectLights = true;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    const controls = new THREE.OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.minDistance = 0.6;
    controls.maxDistance = 18;
    controls.maxPolarAngle = Math.PI * 0.49;

    // A local image-based lighting environment: no external HDR file or network service.
    const studio = new THREE.Scene();
    studio.add(new THREE.Mesh(new THREE.BoxGeometry(20, 12, 20), new THREE.MeshBasicMaterial({ color: 0x273646, side: THREE.BackSide })));
    function environmentPanel(position, rotation, size, color) {
      const panel = new THREE.Mesh(new THREE.PlaneGeometry(size[0], size[1]), new THREE.MeshBasicMaterial({ color, side: THREE.DoubleSide }));
      panel.position.set(...position); panel.rotation.set(...rotation); studio.add(panel);
    }
    environmentPanel([0, 5, 0], [Math.PI / 2, 0, 0], [8, 5], new THREE.Color(3.5, 2.8, 2.0));
    environmentPanel([-5, 2, 0], [0, Math.PI / 2, 0], [7, 4], new THREE.Color(1.3, 1.8, 2.6));
    environmentPanel([3, 2, -4], [0, 0, 0], [4, 3], new THREE.Color(2.0, 1.6, 1.0));
    const pmrem = new THREE.PMREMGenerator(renderer);
    const environment = pmrem.fromScene(studio, 0.025);
    scene.environment = environment.texture;
    pmrem.dispose();
    studio.traverse(object => { if (object.isMesh) { object.geometry.dispose(); object.material.dispose(); } });
    scene.add(new THREE.HemisphereLight(0xb4c7df, 0x39322b, 0.35));
    const key = new THREE.DirectionalLight(0xffe1b2, 2.1);
    key.position.set(3, 6, 4); scene.add(key);
    const fill = new THREE.DirectionalLight(0x9dc6e9, 0.7);
    fill.position.set(-5, 3, 1); scene.add(fill);
    const spot = new THREE.SpotLight(0xffd39a, 95, 16, Math.PI / 3, 0.6, 2);
    spot.position.set(-1, 3.5, 1.2);
    spot.target.position.set(0, 0, -0.5);
    spot.castShadow = true;
    spot.shadow.mapSize.set(1024, 1024);
    spot.shadow.camera.near = 0.2;
    spot.shadow.camera.far = 20;
    spot.shadow.bias = -0.0003;
    spot.shadow.normalBias = 0.02;
    scene.add(spot, spot.target);

    const assets = await OrbitalAssets.load('./');
    const room = new THREE.Group(); scene.add(room);
    for (const x of [-2, 0, 2]) for (const z of [-2, 0, 2]) {
      const floor = assets.create('floor-2m', { castShadow: false });
      floor.position.set(x, 0, z); room.add(floor);
    }
    for (const x of [-2, 0, 2]) {
      const wall = assets.create('wall-2m', { castShadow: false });
      wall.position.set(x, 0, -3); room.add(wall);
    }
    for (const z of [-2, 0, 2]) {
      const wall = assets.create('wall-2m', { castShadow: false });
      wall.position.set(3, 0, z); wall.rotation.y = -Math.PI / 2; room.add(wall);
    }
    for (const x of [-1.5, 1.5]) {
      const fixture = assets.create('fixture-160cm');
      fixture.position.set(x, 3.8, -0.5); room.add(fixture);
    }
    const crates = [];
    for (const p of [[-1.1, 0.012, 0.2], [0.4, 0.012, -0.7], [1.65, 0.012, -1.7]]) {
      const crate = assets.create('crate-standard', { size: 0.8, pivot: 'bottom' });
      crate.position.set(...p); crate.rotation.y = Math.PI / 10; room.add(crate); crates.push(crate);
    }
    const paint = new THREE.MeshStandardMaterial({ color: 0xc88a29, roughness: 0.65, metalness: 0.05 });
    for (const x of [-1.75, 2.5]) {
      const stripe = new THREE.Mesh(new THREE.PlaneGeometry(0.075, 5.5), paint);
      stripe.rotation.x = -Math.PI / 2; stripe.position.set(x, 0.006, 0); room.add(stripe);
    }

    const inspection = new THREE.Group(); scene.add(inspection);
    inspection.visible = false;
    const checks = {};
    for (const id of assets.ids) {
      const test = assets.create(id);
      const box = new THREE.Box3().setFromObject(test);
      const dimensions = box.getSize(new THREE.Vector3());
      let materials = new Set(), triangles = 0, maps = true, meshCount = 0;
      test.traverse(object => {
        if (!object.isMesh) return;
        meshCount++; materials.add(object.material);
        triangles += object.geometry.index ? object.geometry.index.count / 3 : object.geometry.attributes.position.count / 3;
        maps = maps && !!(object.material.map && object.material.roughnessMap && object.material.metalnessMap && object.material.emissiveMap);
      });
      if (meshCount !== 1 || materials.size !== 1 || !maps) throw new Error('Asset material/mesh validation failed: ' + id);
      if (id === 'floor-2m' && (Math.abs(dimensions.x - 2) > 0.02 || Math.abs(dimensions.z - 2) > 0.02)) throw new Error('Floor scale is incorrect.');
      if (id === 'floor-2m' && Math.abs(box.max.y) > 0.01) throw new Error('Deck pivot is incorrect.');
      if (id === 'wall-2m' && Math.abs(dimensions.y - 3.8) > 0.02) throw new Error('Wall orientation is incorrect.');
      if (id === 'fixture-160cm' && Math.abs(box.max.y) > 0.01) throw new Error('Ceiling-mount pivot is incorrect.');
      checks[id] = { dimensions: dimensions.toArray(), triangles, meshCount, materialCount: materials.size, maps };
    }
    console.info('ORBITAL_ASSET_CHECKS ' + JSON.stringify(checks));
    const fittedCrate = assets.create('crate-standard', { size: 0.8, pivot: 'bottom' });
    const fittedBounds = new THREE.Box3().setFromObject(fittedCrate);
    const fittedSize = fittedBounds.getSize(new THREE.Vector3());
    if (Math.abs(fittedBounds.min.y) > 0.0001 || Math.abs(Math.max(fittedSize.x, fittedSize.y, fittedSize.z) - 0.8) > 0.0001)
      throw new Error('Gameplay crate size/pivot validation failed.');

    function focus(id) {
      room.visible = id === 'room'; inspection.visible = id !== 'room';
      controls.maxPolarAngle = id === 'fixture-160cm' ? Math.PI * 0.90 : Math.PI * 0.49;
      if (id === 'room') {
        camera.position.set(-5.8, 3.6, 7.0); controls.target.set(-0.1, 1.1, -0.6);
      } else {
        inspection.clear();
        const model = assets.create(id, id === 'crate-standard' ? { size: 0.8, pivot: 'bottom' } : {});
        inspection.add(model);
        const box = new THREE.Box3().setFromObject(model), center = box.getCenter(new THREE.Vector3());
        const size = box.getSize(new THREE.Vector3()), radius = Math.max(size.x, size.y, size.z);
        controls.target.copy(center);
        camera.position.copy(center).add(new THREE.Vector3(radius * 1.25, id === 'fixture-160cm' ? -radius * 0.9 : radius * 0.65 + 0.3, radius * 1.65));
      }
      controls.update();
    }
    const select = document.getElementById('focus');
    select.addEventListener('change', () => focus(select.value));
    document.getElementById('reset').addEventListener('click', () => focus(select.value));
    focus('room');
    status.textContent = 'Four assets ready · Browser preview';
    status.dataset.ready = 'true';
    addEventListener('resize', () => {
      camera.aspect = innerWidth / innerHeight; camera.updateProjectionMatrix(); renderer.setSize(innerWidth, innerHeight);
    });
    renderer.setAnimationLoop(() => { controls.update(); renderer.render(scene, camera); });
  } catch (error) {
    status.textContent = 'Preview failed: ' + error.message;
    status.style.color = '#ff9c8b';
    console.error(error);
  }
})();
