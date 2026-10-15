using System.Linq;
using UnityEditor;
using UnityEngine;
namespace AvH.Editor {
 public static partial class ConfigureOwnedAssets {
  const string Adventure="Synty/PolygonAdventure/Prefabs/";
  static readonly Color Sand=new Color(.73f,.66f,.48f),Stone=new Color(.66f,.61f,.49f),Plaster=new Color(.86f,.78f,.59f),Wood=new Color(.28f,.21f,.14f),Teal=new Color(.12f,.39f,.42f),Clay=new Color(.63f,.29f,.16f);
  /// <summary>True when the owned village prefab exists but predates the pushable prop container.</summary>
  public static bool VillageNeedsRebuild() {
   var catalog=AssetDatabase.LoadAssetAtPath<OwnedAssetCatalog>(Root+"Resources/OwnedAssetCatalog.asset");
   return catalog!=null&&catalog.Village!=null&&catalog.Village.transform.Find(PushProps.Container)==null;
  }
  static GameObject BuildVillage() {
   var map=new GameObject("Sunwash market town");
   Solid(map,"Town ground",new Vector3(0,-.6f,0),new Vector3(88,1.2f,88),Sand);
   // A central market and two intersecting loops. Travel space stays 6–9m wide.
   Road(map,"Market spine",new Vector3(0,0,-39),new Vector3(0,0,39),8);
   Road(map,"West east street",new Vector3(-40,0,0),new Vector3(40,0,0),7);
   Road(map,"North loop",new Vector3(-30,0,14),new Vector3(32,0,14),6);
   Road(map,"South loop",new Vector3(-30,0,-15),new Vector3(33,0,-15),6);
   Road(map,"West passage",new Vector3(-30,0,-15),new Vector3(-30,0,29),6);
   Road(map,"East passage",new Vector3(33,0,-29),new Vector3(33,0,29),6);
   Road(map,"Roof approach",new Vector3(-20,0,0),TownLayout.RoofApproach,5);
   Surface(map,"Market stone square",new Vector3(0,.049f,0),new Vector3(21,.025f,19),new Color(.46f,.43f,.34f));
   // Courtyard edges and surface seams give readable scale at shoulder-camera height.
   Paving(map);
   BuildRoof(map);BuildCourtyard(map);BuildWorkshop(map);
   // Homes form street fronts and short alleys instead of isolated props on an empty plane.
   var homes=new[]{new Vector3(-14,0,-11),new Vector3(-23,0,-10),new Vector3(-35,0,-8),new Vector3(-35,0,6),new Vector3(-35,0,24),new Vector3(-10,0,24),new Vector3(2,0,31),new Vector3(12,0,31),new Vector3(20,0,34),new Vector3(36,0,36),new Vector3(37,0,8),new Vector3(17,0,6),new Vector3(13,0,-9),new Vector3(-16,0,-30),new Vector3(-28,0,-29),new Vector3(0,0,-29)};
   for(int i=0;i<homes.Length;i++){float yaw=i<5?90:i<10?180:i<13?270:0;var house=Place(map,$"Buildings/SM_Bld_Village_{i%4+1:00}.prefab",homes[i],yaw,1.15f+(i%3)*.13f);DressHouse(map,house,i);}
   foreach(var p in new[]{new Vector3(-9,0,-7),new Vector3(8,0,7),new Vector3(9,0,-7),new Vector3(-9,0,7)}) {
    Place(map,"Buildings/SM_Bld_Stall_01.prefab",p,p.x<0?90:270,1.05f);
    Supply(map,p+new Vector3(p.x<0?-2.9f:2.9f,0,-.6f));
   }
   Place(map,"Buildings/SM_Bld_Well_01.prefab",new Vector3(-7,0,13),0,1);
   Place(map,"Props/SM_Prop_Cart_01.prefab",new Vector3(14,0,-19),20,1.1f);
   Place(map,"Props/SM_Prop_Cart_01.prefab",new Vector3(-25,0,-20),-15,1);
   Supply(map,new Vector3(27,0,-29));Supply(map,new Vector3(16,0,-30));Supply(map,new Vector3(29,0,27));
   foreach(var p in new[]{new Vector3(-38,0,-30),new Vector3(-33,0,35),new Vector3(-16,0,36),new Vector3(-7,0,34),new Vector3(9,0,22),new Vector3(37,0,20),new Vector3(39,0,-15),new Vector3(34,0,-35),new Vector3(10,0,-35),new Vector3(-8,0,-20)}) {
    Place(map,"Environments/SM_Env_Tree_05.prefab",p,35,1.1f);
    Solid(map,"Tree bed",p+Vector3.up*.12f,new Vector3(3,.24f,2.8f),Stone);
    Place(map,"Environments/SM_Env_Bush_02.prefab",p+new Vector3(1.1f,.24f,.5f),20,.6f,false);
    Place(map,"Environments/SM_Env_Grass_01.prefab",p+new Vector3(-1,.24f,.6f),0,1,false);
   }
   for(int i=0;i<16;i++){float a=i*Mathf.PI*2/16;var p=new Vector3(Mathf.Cos(a)*65,-5,Mathf.Sin(a)*65);Place(map,i%2==0?"Environments/SM_Env_Rock_03.prefab":"Environments/SM_Env_Rock_07.prefab",p,i*32,4+(i%3),false);}
   // Visible cliff strata below the play boundary, with open edges for knock-off play.
   for(int i=0;i<20;i++){float a=i*Mathf.PI*2/20;Rock(map,new Vector3(Mathf.Cos(a)*37,-18,Mathf.Sin(a)*37),new Vector3(12,14,12),i*23);}
   for(int z=-32;z<=32;z+=16){Lamp(map,new Vector3(5,0,z));Lamp(map,new Vector3(-5,0,z+5));}
   Bunting(map,new Vector3(-10,5,2),new Vector3(10,5,2));Bunting(map,new Vector3(-11,4.8f,-8),new Vector3(11,4.8f,-8));
   return map;
  }
  static GameObject Solid(GameObject map,string name,Vector3 p,Vector3 size,Color color,bool collision=true) {
   var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(map.transform);go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=VillageMaterial("Town_"+ColorUtility.ToHtmlStringRGB(color),color);
   if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
  }
  static void Road(GameObject map,string name,Vector3 a,Vector3 b,float width) {
   var d=b-a;float height=.008f+map.transform.childCount*.00005f;var go=Solid(map,name,(a+b)*.5f+Vector3.up*height,new Vector3(width,.008f,d.magnitude),new Color(.62f,.56f,.43f),false);go.transform.rotation=Quaternion.LookRotation(d);
   for(float t=0;t<d.magnitude;t+=2){var p=a+d.normalized*t;var right=Vector3.Cross(Vector3.up,d.normalized);foreach(float side in new[]{-1f,1f}){var curb=Solid(map,"Road edge stones",p+right*(width*.5f)*side+Vector3.up*.045f,new Vector3(.22f,.09f,1.85f),Stone,false);curb.transform.rotation=go.transform.rotation;}}
  }
  static void BuildRoof(GameObject map) {
   var p=TownLayout.Roof;
   Solid(map,"Watch house",new Vector3(p.x,1.9f,p.z),new Vector3(8,3.8f,7),Plaster);
   Solid(map,"Roof coping",p-new Vector3(0,.1f,0),new Vector3(8.4f,.2f,7.4f),Stone);
   for(int i=0;i<24;i++)Solid(map,"Watch house stair",new Vector3(p.x,(i+1)*4f/48,p.z-12.7f+i*.4f),new Vector3(2.8f,(i+1)*4f/24,.42f),Stone);
   // Narrow parapet cover, two side gaps allow emergency drops.
   Solid(map,"Roof back parapet",p+new Vector3(0,.42f,3.3f),new Vector3(8,.84f,.35f),Plaster);
   foreach(float x in new[]{-3.8f,3.8f})Solid(map,"Roof side parapet",p+new Vector3(x,.42f,1.7f),new Vector3(.35f,.84f,3),Plaster);
   for(int i=-1;i<=1;i++){Solid(map,"Watch house teal shutter",new Vector3(p.x+i*2.4f,2,p.z-3.52f),new Vector3(.9f,1.25f,.12f),Teal,false);Solid(map,"Window lintel",new Vector3(p.x+i*2.4f,2.72f,p.z-3.62f),new Vector3(1.2f,.14f,.3f),Stone,false);}
   for(int i=0;i<6;i++)Solid(map,"Roof beam",p+new Vector3(-3.4f+i*1.3f,-.45f,0),new Vector3(.2f,.25f,7.6f),Wood,false);
   Banner(map,p+new Vector3(-3.4f,1.5f,2.8f),Teal);
  }
  static void BuildCourtyard(GameObject map) {
   var p=TownLayout.Courtyard;
   Surface(map,"Courtyard tiles",p+Vector3.up*.03f,new Vector3(11,.05f,10),Stone);
   Solid(map,"Court rear wall",p+new Vector3(0,1.4f,4.5f),new Vector3(11,2.8f,.5f),Plaster);
   Solid(map,"Court east wall",p+new Vector3(5.2f,1.4f,1),new Vector3(.5f,2.8f,7),Plaster);
   foreach(float x in new[]{-4.7f,4.7f}){Solid(map,"Court gate pier",p+new Vector3(x,1.8f,-3.9f),new Vector3(.65f,3.6f,.65f),Stone);}
   Solid(map,"Court gate timber",p+new Vector3(0,3.55f,-3.9f),new Vector3(10.1f,.25f,.55f),Wood);
   for(int i=0;i<5;i++){Solid(map,"Court wall buttress",p+new Vector3(-4.5f+i*2.2f,1.6f,4.15f),new Vector3(.45f,3.2f,.9f),Stone);}
   Banner(map,p+new Vector3(4.7f,3.5f,4),new Color(.76f,.35f,.17f));
  }
  static void BuildWorkshop(GameObject map) {
   var p=TownLayout.Workshop;
   Solid(map,"Workshop rear wall",p+new Vector3(0,1.5f,-3.8f),new Vector3(10,3,.45f),Plaster);
   Solid(map,"Workshop side wall",p+new Vector3(4.8f,1.5f,0),new Vector3(.45f,3,8),Plaster);
   foreach(float x in new[]{-4.6f,4.6f})foreach(float z in new[]{-3.6f,3.6f})Solid(map,"Workshop column",p+new Vector3(x,1.6f,z),new Vector3(.36f,3.2f,.36f),Wood);
   Solid(map,"Workshop ceiling",p+new Vector3(0,3.25f,0),new Vector3(10.6f,.24f,8.7f),Wood);
   foreach(float side in new[]{-1f,1f}) {var roof=Solid(map,"Terracotta roof",p+new Vector3(side*2.6f,3.85f,0),new Vector3(5.5f,.25f,9),Clay);roof.transform.rotation=Quaternion.Euler(0,0,-side*13);}
   for(int i=-4;i<=4;i++)Solid(map,"Roof ridge tile",p+new Vector3(0,4.48f,i),new Vector3(.42f,.15f,.95f),Clay,false);
   foreach(float z in new[]{-3.54f,3.54f})Solid(map,"Workshop crossbeam",p+new Vector3(0,2.95f,z),new Vector3(9.8f,.2f,.25f),Wood,false);
   for(int i=-4;i<=4;i+=2)Solid(map,"Workshop wall frame",p+new Vector3(i,1.6f,-3.52f),new Vector3(.15f,2.8f,.13f),Wood,false);
   Solid(map,"Workshop wall base",p+new Vector3(0,.25f,-3.51f),new Vector3(9.7f,.5f,.15f),Stone,false);
   foreach(float x in new[]{-3.6f,3.6f}){
    Solid(map,"Workbench",p+new Vector3(x,.8f,-2.8f),new Vector3(1.6f,.15f,.8f),Wood);
    Place(map,"Props/SM_Prop_Pot_01.prefab",p+new Vector3(x,.88f,-2.8f),0,.7f);
    Place(map,"Items/SM_Item_Lantern_01.prefab",p+new Vector3(x,2.3f,-3.3f),0,1,false);
   }
   Banner(map,p+new Vector3(-4.6f,3.8f,3.6f),new Color(.72f,.49f,.18f));
  }
  static void DressHouse(GameObject map,GameObject house,int index) {
   var b=Bounds(house);var p=new Vector3(b.center.x,0,b.min.z-.2f);
   var awning=Solid(map,"Street awning",p+new Vector3(0,2.15f,-.65f),new Vector3(2.5f,.12f,1.7f),index%2==0?Teal:Clay,false);awning.transform.rotation=Quaternion.Euler(7,0,0);
   foreach(float x in new[]{-1.1f,1.1f})Solid(map,"Awning strut",p+new Vector3(x,1,-1.35f),new Vector3(.08f,2,.08f),Wood,false);
   if(index%2==0)Supply(map,new Vector3(b.max.x+.65f,0,b.center.z));
   Pushable(map,"Props/SM_Prop_Pot_01.prefab",p+new Vector3(1.6f,0,-.25f),15,.8f);
   Place(map,"Environments/SM_Env_Bush_02.prefab",new Vector3(b.min.x-.6f,0,b.max.z-.3f),index*26,.65f,false);
  }
  static void Supply(GameObject map,Vector3 p) {Pushable(map,"Props/SM_Prop_Crate_01.prefab",p,8,.8f);Pushable(map,"Props/SM_Prop_Barrel_01.prefab",p+new Vector3(1.1f,0,.2f),0,.85f);}
  // Crates, barrels and pots: light enough for any body or bubble to shove (host-judged, synced, back home each round).
  // Inside a shelter or at its entrance they stay fixed props so nothing is ever pushed out of or wedged into cover by design.
  static void Pushable(GameObject map,string path,Vector3 floor,float yaw,float scale) {
   if(TownLayout.NearShelter(floor)){Place(map,path,floor,yaw,scale);return;}
   var container=map.transform.Find(PushProps.Container);
   if(container==null){container=new GameObject(PushProps.Container).transform;container.SetParent(map.transform,false);}
   Place(map,path,floor,yaw,scale,false).transform.SetParent(container,true);
  }
  static void Rock(GameObject map,Vector3 p,Vector3 size,float angle) {
   var go=new GameObject("Limestone outcrop");go.transform.SetParent(map.transform);go.transform.position=p;go.transform.localScale=size;go.transform.rotation=Quaternion.Euler(0,angle,0);
   var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Generated+"Outcrop.asset");
   if(mesh==null){mesh=new Mesh();var v=new System.Collections.Generic.List<Vector3>();var t=new System.Collections.Generic.List<int>();
    for(int level=0;level<3;level++)for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7,b=(i+1)*Mathf.PI*2/7;float r=level==0?.62f:level==1?.55f:.35f,r2=level==0?.55f:level==1?.35f:.06f;float y=level*.36f,y2=(level+1)*.36f;
     Vector3 p0=new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r),p1=new Vector3(Mathf.Cos(b)*r,y,Mathf.Sin(b)*r),p2=new Vector3(Mathf.Cos(a+.13f)*r2,y2+(i%2)*.08f,Mathf.Sin(a+.13f)*r2),p3=new Vector3(Mathf.Cos(b+.13f)*r2,y2+((i+1)%2)*.08f,Mathf.Sin(b+.13f)*r2);
     int n=v.Count;v.AddRange(new[]{p0,p2,p1,p1,p2,p3});for(int j=0;j<6;j++)t.Add(n+j);
    }mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,Generated+"Outcrop.asset");}
   go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=VillageMaterial("Limestone",new Color(.55f,.52f,.39f));
  }
  static void Lamp(GameObject map,Vector3 p) {Solid(map,"Lantern pole",p+Vector3.up*1.6f,new Vector3(.12f,3.2f,.12f),Wood,false);Solid(map,"Lantern arm",p+new Vector3(.2f,3.12f,0),new Vector3(.6f,.12f,.12f),Wood,false);Place(map,"Items/SM_Item_Lantern_01.prefab",p+new Vector3(.4f,2.6f,0),0,1.2f,false);}
  static void Banner(GameObject map,Vector3 p,Color color) {Solid(map,"Banner pole",p,new Vector3(.09f,3,.09f),Wood,false);Solid(map,"District pennant",p+new Vector3(.48f,.8f,0),new Vector3(.85f,1.15f,.035f),color,false);}
  static void Bunting(GameObject map,Vector3 a,Vector3 b) {
   foreach(var p in new[]{a,b})Solid(map,"Bunting mast",p*.5f,new Vector3(.1f,p.y,.1f),Wood,false).transform.position=new Vector3(p.x,p.y*.5f,p.z);
   Vector3 previous=a;
   for(int i=1;i<=24;i++){float t=i/24f;var p=Vector3.Lerp(a,b,t)-Vector3.up*Mathf.Sin(t*Mathf.PI)*.8f;var rope=Solid(map,"Bunting cord",(p+previous)*.5f,new Vector3(.025f,.025f,(p-previous).magnitude),Wood,false);rope.transform.rotation=Quaternion.LookRotation(p-previous);
    if(i%2==0){var flag=Solid(map,"Market ribbon",p-Vector3.up*.23f,new Vector3(.38f,.46f,.025f),i%4==0?Teal:Clay,false);flag.transform.rotation=Quaternion.Euler(0,0,6*Mathf.Sin(i));}previous=p;
   }
  }
  static void Paving(GameObject map) {
   var mesh=new Mesh{name="Market paving"};var v=new System.Collections.Generic.List<Vector3>();var t=new System.Collections.Generic.List<int>();
   for(int x=-14;x<14;x++)for(int z=-12;z<12;z++){float px=x*.72f+(z%2)*.18f,pz=z*.76f;int n=v.Count;v.AddRange(new[]{new Vector3(px,.065f,pz),new Vector3(px,.065f,pz+.73f),new Vector3(px+.69f,.065f,pz+.73f),new Vector3(px+.69f,.065f,pz)});t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
   mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();var path=Generated+"MarketPaving.asset";var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(stored==null){AssetDatabase.CreateAsset(mesh,path);stored=mesh;}else{EditorUtility.CopySerialized(mesh,stored);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(stored);}
   var go=new GameObject("Market paving");go.transform.SetParent(map.transform);go.AddComponent<MeshFilter>().sharedMesh=stored;go.AddComponent<MeshRenderer>().sharedMaterial=VillageMaterial("Paving",new Color(.68f,.63f,.51f));
  }
  static Material VillageMaterial(string name,Color color) {
   var path=Generated+"Village_"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}mat.color=color;mat.SetFloat("_Glossiness",0);EditorUtility.SetDirty(mat);return mat;
  }
  static void Surface(GameObject map,string name,Vector3 center,Vector3 size,Color color) {
   var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(map.transform);go.transform.position=center;go.transform.localScale=size;
   Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=VillageMaterial("Surface_"+name,color);
  }
  static GameObject Place(GameObject map,string path,Vector3 floor,float yaw,float scale,bool solid=true) {
   var go=Object.Instantiate(Load(Adventure+path),map.transform);Clean(go);go.transform.localScale*=scale;go.transform.rotation=Quaternion.Euler(0,yaw,0);
   var bounds=Bounds(go);go.transform.position+=floor-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
   foreach(var collider in go.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
   if(solid) {
    var b=Bounds(go);var obstacle=new GameObject(go.name+" collision");obstacle.transform.SetParent(map.transform);
    if(path.Contains("Tree")) {
     // Foliage is visual; only the visible trunk blocks movement.
     obstacle.transform.position=new Vector3(b.center.x,b.min.y+b.size.y*.22f,b.center.z);
     var trunk=obstacle.AddComponent<CapsuleCollider>();trunk.height=b.size.y*.44f;trunk.radius=Mathf.Min(b.size.x,b.size.z)*.055f;
    } else if(path.Contains("SM_Bld_Village_")) {obstacle.transform.position=b.center;obstacle.AddComponent<BoxCollider>().size=b.size;}
    else {
     // One convex silhouette closes decorative cavities without an invisible flat AABB top.
     var parts=go.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh!=null).Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray();
     var mesh=new Mesh();mesh.CombineMeshes(parts,true,true);
     var meshPath=Generated+"Collision_"+map.transform.childCount+".asset";
     var stored=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
     if(stored==null){AssetDatabase.CreateAsset(mesh,meshPath);stored=mesh;}else {EditorUtility.CopySerialized(mesh,stored);Object.DestroyImmediate(mesh);}
     EditorUtility.SetDirty(stored);
     var hull=obstacle.AddComponent<MeshCollider>();hull.sharedMesh=stored;hull.convex=true;
    }
    if(path.Contains("SM_Bld_Village_")) {
     // These modular houses have open parapets. A visible flat deck closes the same volume as collision.
     Surface(map,go.name+" roof deck",new Vector3(b.center.x,b.max.y,b.center.z),new Vector3(b.size.x,.025f,b.size.z),new Color(.51f,.36f,.23f));
    }
   }
   return go;
  }
 }
}
