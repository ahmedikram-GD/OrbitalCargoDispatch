using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OrbitalCargo.Room.EditorTools
{
    /// <summary>
    /// Authored low-poly cargo tug and docking surround. Lofted hull sections and turned engine
    /// profiles create the silhouette; generated assets stay editable and reuse the existing launcher.
    /// All dimensions are asset-authoring dimensions, not runtime gameplay tuning.
    /// </summary>
    public static class HeroDockArt
    {
        private const string Folder = "Assets/OrbitalCargo/Art/Dock";
        private static Material _hull, _dark, _metal, _accent, _glass, _light, _rubber;
        private static readonly List<Mesh> TemporaryMeshes = new List<Mesh>();

        public static void Apply(Transform room, Transform root)
        {
            var bay = room.Find("Bay_A");
            if (bay == null) return;
            _hull = Material("TugIvory", new Color(.72f, .77f, .76f), .25f, .12f);
            _dark = Material("TugNavy", new Color(.095f, .16f, .21f), .27f, .3f);
            _metal = Material("TugMetal", new Color(.28f, .37f, .40f), .4f, .5f);
            _accent = Material("TugOchre", new Color(.93f, .48f, .10f), .23f, .12f);
            _glass = Material("TugGlass", new Color(.035f, .20f, .28f), .78f, .25f, .12f);
            _light = Material("TugCyan", new Color(.15f, .75f, .85f), .3f, .1f, 1.1f);
            _rubber = Material("TugRecess", new Color(.025f, .04f, .055f), .1f, 0f);
            var art = Group("BayA", root);
            art.SetPositionAndRotation(bay.position, bay.rotation);
            Surround(art);
            var board = bay.Find("Backboard");
            if (board != null) board.GetComponent<Renderer>().enabled = false;
            var pad = bay.Find("Pad");
            if (pad != null) pad.GetComponent<Renderer>().sharedMaterial = _dark;
            foreach (var r in bay.Find("Slots").GetComponentsInChildren<Renderer>()) r.sharedMaterial = _metal;
            var shuttle = bay.Find("Shuttle");
            if (shuttle != null)
            {
                var old = shuttle.Find("HeroTug");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                foreach (var r in shuttle.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                var tug = Group("HeroTug", shuttle);
                Tug(tug);
                DockArtPass.Combine(tug, "HeroTug");
                foreach (var t in tug.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;
                PrefabUtility.SaveAsPrefabAsset(tug.gameObject, Folder + "/CargoTug.prefab");
            }
            var pedestal = bay.Find("LaunchPad/Pedestal");
            if (pedestal != null)
            {
                pedestal.GetComponent<Renderer>().enabled = false;
                var console = Group("LaunchConsole", art);
                console.position = pedestal.parent.position;
                Loft("ConsoleFoot", console, new Vector3(0f,.13f,0f),
                    new[] { new Vector4(-.3f,.31f,.11f,0f),new Vector4(.3f,.31f,.11f,0f) }, _dark);
                Beam("ConsoleStem", console, new Vector3(0f,.15f,-.1f), new Vector3(0f,.95f,.04f), .27f, .25f, _metal);
                var head = Group("SlopingControl", console);
                head.localPosition = new Vector3(0f,1.05f,0f);
                head.localRotation = Quaternion.Euler(-22f,0f,0f);
                Box("ConsoleBody",head,Vector3.zero,new Vector3(.56f,.20f,.43f),_dark);
                Box("Screen",head,new Vector3(-.07f,.108f,0f),new Vector3(.26f,.015f,.26f),_glass);
                for(int i=0;i<3;i++) Box("Readout",head,new Vector3(-.07f,.119f,-.07f+i*.06f),new Vector3(.17f,.008f,.013f),_light);
                var lamp = pedestal.parent.Find("Lamp");
                // Keep the renderer referenced by LaunchButton, including its pulse property block.
                if(lamp != null) { lamp.localScale = new Vector3(.12f,.04f,.12f); lamp.localPosition = new Vector3(.18f,1.17f,.02f); }
            }
            DockArtPass.Combine(art, "HeroBay");
            DockArtPass.FinishStationStyling(AssetDatabase.LoadAssetAtPath<OrbitalCargo.Config.RoomLayoutSO>(RoomBuilder.DefaultLayoutPath),room);
            foreach (var mesh in TemporaryMeshes) Object.DestroyImmediate(mesh);
            TemporaryMeshes.Clear();
        }

        private static void Tug(Transform p)
        {
            // z, half-width, half-height, vertical offset: an asymmetric wedge, not a scaled cube.
            Loft("LowerHull", p,new Vector3(0f,2.45f,0f),new[] {
                new Vector4(-1.25f,.73f,.12f,0f),new Vector4(-.9f,1.1f,.20f,0f),
                new Vector4(.55f,1.08f,.19f,0f),new Vector4(1.33f,.65f,.10f,.10f),
                new Vector4(1.52f,.43f,.065f,.14f)},_dark);
            Loft("MainHull",p,new Vector3(0f,2.7f,0f),new[] {
                new Vector4(-1.23f,.72f,.20f,0f),new Vector4(-.85f,1.04f,.35f,.04f),
                new Vector4(.35f,.98f,.34f,.05f),new Vector4(1.18f,.64f,.19f,-.10f),
                new Vector4(1.48f,.43f,.075f,-.12f)},_hull);
            Loft("CockpitSeal",p,new Vector3(0f,3.0f,0f),new[] {
                new Vector4(-.48f,.61f,.12f,0f),new Vector4(.12f,.62f,.17f,0f),
                new Vector4(1.36f,.45f,.14f,-.18f)},_rubber);
            Loft("CockpitGlass",p,new Vector3(0f,3.025f,0f),new[] {
                new Vector4(-.42f,.55f,.105f,0f),new Vector4(.10f,.56f,.14f,0f),
                new Vector4(1.38f,.40f,.115f,-.18f)},_glass);
            Beam("CanopySpine",p,new Vector3(0f,3.19f,.08f),new Vector3(0f,2.97f,1.39f),.045f,.035f,_hull);
            Beam("WindshieldDivider",p,new Vector3(0f,2.72f,1.40f),new Vector3(0f,2.97f,1.40f),.035f,.025f,_hull);
            Loft("RoofArmor",p,new Vector3(0f,3.08f,-.70f),new[] {
                new Vector4(-.35f,.57f,.06f,0f),new Vector4(.20f,.62f,.09f,0f)},_accent);
            foreach(float s in new[]{-1f,1f})
            {
                var engine=Group("Engine",p); engine.localPosition=new Vector3(s*1.46f,2.65f,-.12f);
                Lathe("Nacelle",engine,new[]{new Vector2(-1.15f,.25f),new Vector2(-.95f,.39f),new Vector2(.72f,.39f),new Vector2(1.02f,.28f)},_hull);
                Lathe("FrontCollar",engine,new[]{new Vector2(.68f,.40f),new Vector2(.86f,.42f),new Vector2(1.04f,.31f),new Vector2(1.05f,.23f),new Vector2(.82f,.20f)},_metal);
                Lathe("IntakeInterior",engine,new[]{new Vector2(.81f,.20f),new Vector2(.58f,.13f),new Vector2(.57f,0f)},_rubber);
                Lathe("IntakeRing",engine,new[]{new Vector2(.84f,.215f),new Vector2(.85f,.19f)},_light);
                Lathe("RearBand",engine,new[]{new Vector2(-.86f,.405f),new Vector2(-.64f,.405f)},_accent);
                Lathe("Exhaust",engine,new[]{new Vector2(-1.30f,.18f),new Vector2(-1.12f,.26f),new Vector2(-.97f,.26f)},_metal);
                Beam("EnginePylon",p,new Vector3(s*.8f,2.63f,-.25f),new Vector3(s*1.45f,2.6f,-.25f),.22f,.75f,_dark);
                // Folded lifting arms leave all six cargo positions unobstructed.
                Beam("RearArm",p,new Vector3(s*1.67f,.32f,-1.12f),new Vector3(s*1.58f,2.4f,-.70f),.12f,.18f,_metal);
                Beam("CargoSki",p,new Vector3(s*1.72f,.22f,-1.17f),new Vector3(s*1.72f,.22f,1.05f),.16f,.22f,_dark);
                Beam("SkiNose",p,new Vector3(s*1.72f,.22f,1.05f),new Vector3(s*1.72f,.40f,1.30f),.16f,.18f,_metal);
                Box("RunningLamp",p,new Vector3(s*.31f,2.61f,1.54f),new Vector3(.14f,.045f,.025f),_light);
                for(int i=0;i<5;i++) Box("CoolingVent",p,new Vector3(s*.82f,3.015f,-.53f+i*.12f),new Vector3(.15f,.025f,.048f),_rubber);
            }
            DockArtPass.Label("TugId",p,"OC / 07",new Vector3(0f,2.56f,1.55f),.065f,.42f);
        }

        private static void Surround(Transform p)
        {
            Box("RearRecess",p,new Vector3(0f,1.7f,-1.48f),new Vector3(4.1f,3.4f,.1f),_rubber);
            for(int i=-2;i<=2;i++)
            {
                Box("RearPanel",p,new Vector3(i*.78f,1.65f,-1.39f),new Vector3(.73f,2.95f,.07f),_dark);
                for(int j=0;j<3;j++) Box("RearVent",p,new Vector3(i*.78f,.42f+j*.11f,-1.34f),new Vector3(.53f,.035f,.035f),_rubber);
            }
            foreach(float s in new[]{-1f,1f})
            {
                Beam("DockUpright",p,new Vector3(s*2.02f,.08f,-.85f),new Vector3(s*2.02f,2.9f,-.85f),.27f,.42f,_metal);
                Beam("DockShoulder",p,new Vector3(s*2.02f,2.9f,-.85f),new Vector3(s*1.42f,3.65f,-.85f),.30f,.42f,_metal);
                Beam("ShoulderInset",p,new Vector3(s*1.98f,2.95f,-.60f),new Vector3(s*1.48f,3.58f,-.60f),.10f,.025f,_accent);
                Box("UprightLamp",p,new Vector3(s*2.02f,2.16f,-.61f),new Vector3(.055f,.7f,.025f),_light);
                Box("Foot",p,new Vector3(s*2.02f,.16f,-.85f),new Vector3(.5f,.32f,.65f),_dark);
                Beam("SafetyRail",p,new Vector3(s*1.98f,.7f,-.6f),new Vector3(s*1.98f,.7f,.8f),.065f,.065f,_accent);
                foreach(float z in new[]{-.6f,.8f}) Beam("RailPost",p,new Vector3(s*1.98f,.12f,z),new Vector3(s*1.98f,.7f,z),.065f,.065f,_metal);
                Box("DockEdge",p,new Vector3(s*1.9f,.09f,.1f),new Vector3(.065f,.08f,2.6f),_accent);
            }
            Box("DockLintel",p,new Vector3(0f,3.66f,-.85f),new Vector3(2.9f,.24f,.42f),_metal);
            Box("SignBack",p,new Vector3(0f,3.64f,-.59f),new Vector3(2.6f,.33f,.08f),_dark);
            DockArtPass.Label("BaySign",p,"A  /  IRONFALL",new Vector3(0f,3.65f,-.535f),.20f,2.4f);
            DockArtPass.Label("CargoSign",p,"STANDARD FREIGHT",new Vector3(0f,1.53f,-1.30f),.105f,2.5f);
            for(int i=0;i<10;i++)
            {
                var stripe=Group("ThresholdStripe",p);
                stripe.localPosition=new Vector3(-1.5f+i*.33f,.104f,1.15f);
                stripe.localRotation=Quaternion.Euler(0f,-30f,0f);
                Box("Paint",stripe,Vector3.zero,new Vector3(.13f,.006f,.23f),_accent);
            }
        }

        private static Transform Group(string name,Transform parent)
        { var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform; }
        private static void Box(string n,Transform p,Vector3 pos,Vector3 size,Material m)
        { DockArtPass.Box(n,p,pos,size,m); }
        private static void Beam(string n,Transform p,Vector3 a,Vector3 b,float width,float depth,Material m)
        {
            var g=Group(n,p);g.localPosition=(a+b)*.5f;g.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
            Box(n,g,Vector3.zero,new Vector3(width,(b-a).magnitude,depth),m);
        }
        private static Material Material(string name,Color color,float smooth,float metal,float emission=0f)
        {
            string path=Folder+"/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metal);
            if(emission>0f){m.SetColor("_EmissionColor",color*emission);m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;m.EnableKeyword("_EMISSION");}
            EditorUtility.SetDirty(m);return m;
        }
        private static void Loft(string name,Transform parent,Vector3 pos,Vector4[] sections,Material mat)
        {
            var rings=new List<Vector3[]>();
            foreach(var s in sections)
            {
                float w=s.y,h=s.z,b=Mathf.Min(w,h)*.55f;
                rings.Add(new[]{new Vector3(-w+b,-h+s.w,s.x),new Vector3(w-b,-h+s.w,s.x),
                    new Vector3(w,-h+b+s.w,s.x),new Vector3(w,h-b+s.w,s.x),new Vector3(w-b,h+s.w,s.x),
                    new Vector3(-w+b,h+s.w,s.x),new Vector3(-w,h-b+s.w,s.x),new Vector3(-w,-h+b+s.w,s.x)});
            }
            MeshPart(name,parent,pos,rings,true,mat);
        }
        private static void Lathe(string name,Transform parent,Vector2[] profile,Material mat)
        {
            var rings=new List<Vector3[]>();
            foreach(var s in profile)
            {
                var ring=new Vector3[16];
                for(int i=0;i<16;i++){float a=i*Mathf.PI*2f/16f;ring[i]=new Vector3(Mathf.Cos(a)*s.y,Mathf.Sin(a)*s.y,s.x);}
                rings.Add(ring);
            }
            MeshPart(name,parent,Vector3.zero,rings,false,mat);
        }
        private static void MeshPart(string name,Transform parent,Vector3 pos,List<Vector3[]> rings,bool caps,Material mat)
        {
            var v=new List<Vector3>();var t=new List<int>();
            for(int r=0;r<rings.Count-1;r++)
            for(int i=0;i<rings[r].Length;i++)
            { int j=(i+1)%rings[r].Length;Face(v,t,new[]{rings[r][i],rings[r][j],rings[r+1][j],rings[r+1][i]}); }
            if(caps){var first=(Vector3[])rings[0].Clone();System.Array.Reverse(first);Face(v,t,first);Face(v,t,rings[rings.Count-1]);}
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            TemporaryMeshes.Add(mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=pos;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;
        }
        private static void Face(List<Vector3> v,List<int> t,Vector3[] poly)
        { int start=v.Count;v.AddRange(poly);for(int i=1;i<poly.Length-1;i++){t.Add(start);t.Add(start+i);t.Add(start+i+1);} }
    }
}
