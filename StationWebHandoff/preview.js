(async function(){
  'use strict';
  const status=document.getElementById('status');
  try{
    const scene=new THREE.Scene();scene.background=new THREE.Color(0x19212a);
    const renderer=new THREE.WebGLRenderer({canvas:document.getElementById('view'),antialias:true,powerPreference:'default'});
    renderer.setPixelRatio(Math.min(devicePixelRatio,1.5));renderer.setSize(innerWidth,innerHeight);
    renderer.outputEncoding=THREE.sRGBEncoding;renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=.95;
    renderer.physicallyCorrectLights=true;renderer.shadowMap.enabled=false;renderer.shadowMap.type=THREE.PCFSoftShadowMap;
    const camera=new THREE.PerspectiveCamera(40,innerWidth/innerHeight,.05,100);
    const controls=new THREE.OrbitControls(camera,renderer.domElement);controls.enableDamping=true;controls.minDistance=.8;controls.maxDistance=18;controls.maxPolarAngle=Math.PI*.49;
    const studio=new THREE.Scene();studio.add(new THREE.Mesh(new THREE.BoxGeometry(20,12,20),new THREE.MeshBasicMaterial({color:0x303a47,side:THREE.BackSide})));
    function panel(pos,rot,size,col){const o=new THREE.Mesh(new THREE.PlaneGeometry(...size),new THREE.MeshBasicMaterial({color:col,side:THREE.DoubleSide}));o.position.set(...pos);o.rotation.set(...rot);studio.add(o);}
    panel([0,5,0],[Math.PI/2,0,0],[8,5],new THREE.Color(3.5,2.8,2.0));
    panel([-5,2,0],[0,Math.PI/2,0],[7,4],new THREE.Color(1.4,1.8,2.5));
    panel([3,2,-4],[0,0,0],[4,3],new THREE.Color(2,1.7,1.3));
    const pmrem=new THREE.PMREMGenerator(renderer);scene.environment=pmrem.fromScene(studio,.025).texture;pmrem.dispose();
    studio.traverse(o=>{if(o.isMesh){o.geometry.dispose();o.material.dispose();}});
    scene.add(new THREE.HemisphereLight(0xc8d8ed,0x554b3d,.30));
    const key=new THREE.DirectionalLight(0xffdfa8,1.7);key.position.set(-3,6,5);key.castShadow=true;key.shadow.mapSize.set(1024,1024);key.shadow.camera.left=-5;key.shadow.camera.right=5;key.shadow.camera.top=5;key.shadow.camera.bottom=-5;key.shadow.bias=-.0003;key.shadow.normalBias=.02;scene.add(key);
    const fill=new THREE.DirectionalLight(0x9bcdf1,.5);fill.position.set(4,3,2);scene.add(fill);
    const groundMat=new THREE.MeshStandardMaterial({color:0x222326,roughness:.73,metalness:.12,envMapIntensity:.12});
    const ground=new THREE.Mesh(new THREE.PlaneGeometry(80,80),groundMat);ground.rotation.x=-Math.PI/2;ground.position.y=-.012;ground.receiveShadow=true;scene.add(ground);
    const holeGround=new THREE.Mesh(new THREE.RingGeometry(1.005,40,96),groundMat);holeGround.rotation.x=-Math.PI/2;holeGround.position.y=-.012;holeGround.receiveShadow=true;scene.add(holeGround);
    const assets=await OrbitalStations.load('./');const models={};const checks={};
    for(const a of assets.manifest.assets){
      const root=assets.create(a.id);models[a.id]=root;scene.add(root);root.visible=false;
      const bounds=new THREE.Box3().setFromObject(root),size=bounds.getSize(new THREE.Vector3());let triangles=0,draws=0;
      root.traverse(o=>{if(o.isMesh){triangles+=o.geometry.index?o.geometry.index.count/3:o.geometry.attributes.position.count/3;draws++;}});
      if(triangles!==a.triangles)throw new Error(a.id+' triangle mismatch');
      if(size.toArray().some((v,i)=>Math.abs(v-a.dimensionsXYZ[i])>.02))throw new Error(a.id+' dimensions mismatch');
      checks[a.id]={triangles,meshPrimitives:draws,dimensions:size.toArray()};
    }
    const beam=models.scanner.getObjectByName('Scanner_ScanBeam');const belt=models.conveyor.getObjectByName('Conveyor_Belt');
    if(!beam||!belt||!models.scanner.getObjectByName('Scanner_CargoSlot')||!models['disposal-chute'].getObjectByName('Disposal_DropTarget'))throw new Error('Missing integration node');
    console.info('STATION_CHECKS '+JSON.stringify(checks));
    const select=document.getElementById('focus');
    function focus(){
      const id=select.value;Object.keys(models).forEach(k=>models[k].visible=k===id);ground.visible=id!=='disposal-chute';holeGround.visible=id==='disposal-chute';
      const box=new THREE.Box3().setFromObject(models[id]),center=box.getCenter(new THREE.Vector3());
      const radius=id==='conveyor'?4.6:id==='scanner'?3.9:3.3;
      controls.target.set(0,id==='scanner'?1.35:id==='conveyor'?.5:.25);
      camera.position.copy(controls.target).add(new THREE.Vector3(radius*.95,radius*.67,radius*1.40));controls.update();
      const a=assets.manifest.assets.find(a=>a.id===id);status.textContent=a.triangles.toLocaleString()+' triangles · '+a.meshPrimitives+' mesh draws · '+Math.round(a.bytes/1024)+' KB GLB';
      document.getElementById('detail').textContent=id==='conveyor'?'Moving belt · separate frame and optional intake guard':id==='scanner'?'Open front · cyan glass sides · moving scan beam':'Open throat · floor-level mounting ring · safety railing';
    }
    select.addEventListener('change',focus);document.getElementById('reset').addEventListener('click',focus);
    document.getElementById('shadows').addEventListener('change',e=>{renderer.shadowMap.enabled=e.target.checked;scene.traverse(o=>{if(o.isMesh)o.material.needsUpdate=true;});});
    focus();status.dataset.ready='true';
    const clock=new THREE.Clock();renderer.setAnimationLoop(()=>{
      const time=clock.getElapsedTime();if(document.getElementById('motion').checked){belt.material.map.offset.y=-time*.22;beam.position.y=1.3+Math.sin(time*1.8)*.73;}
      controls.update();renderer.render(scene,camera);
      document.getElementById('metrics').textContent=renderer.info.render.calls+' frame draws · '+renderer.info.render.triangles.toLocaleString()+' rendered triangles';
    });
    addEventListener('resize',()=>{camera.aspect=innerWidth/innerHeight;camera.updateProjectionMatrix();renderer.setSize(innerWidth,innerHeight);});
  }catch(error){status.textContent='Preview failed: '+error.message;console.error(error);}
})();
