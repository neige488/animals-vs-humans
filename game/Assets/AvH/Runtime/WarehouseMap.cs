using System;
using System.Collections.Generic;
using UnityEngine;
namespace AvH {
 /// <summary>
 /// Two-storey warehouse, metres. Shared by geometry, bots and tests.
 /// Humans hold the west staircase from the mezzanine; animals that cannot climb it walk round to the east ramp.
 /// When the deck falls, humans jump down through a railing gap and leave by a door into the yard.
 /// <code>
 ///  z=14 ┌──────────────── north wall ────────────────┐
 ///       │   [ mezzanine deck  y=3.5, x -20..20 ]      │
 ///  z=6  │ ▲stair  ══rail══ gap ══rail══ gap ══ ramp▲  │
 ///       │ │x=-16                             x=17│    │
 ///       │      spawn (1F)                             │
 ///  z=-14└──door x=-14──────────────door x=10─────────┘
 ///                yard (fenced, x ±34, z -24)
 /// </code>
 /// </summary>
 public static class WarehouseLayout {
  public const float HalfX=24,HalfZ=14,WallHeight=6,WallThickness=.3f,Upper=3.5f;
  public const float YardHalfX=34,YardHalfZ=24,FenceHeight=3;
  public const float DeckHalfX=20,DeckFront=6,DeckBack=14;
  public const float StairX=-16,StairWidth=2,StairLength=8.4f;
  public const float RampX=17,RampWidth=3,RampLength=12;
  public const float DoorWidth=4;
  /// <summary>Centre x of the 4 m railing gaps humans jump down through.</summary>
  public static readonly float[] DropGaps={-6,6};
  /// <summary>South wall door centres (x) and side wall door centre (z).</summary>
  public static readonly float[] SouthDoors={-14,10};
  public const float SideDoorZ=-8;
  public static Vector3 StairFoot=>new Vector3(StairX,0,DeckFront-StairLength-.6f);
  public static Vector3 StairHead=>new Vector3(StairX,Upper,DeckFront+.8f);
  public static Vector3 RampFoot=>new Vector3(RampX,0,DeckFront-RampLength-.6f);
  public static Vector3 RampHead=>new Vector3(RampX,Upper,DeckFront+.8f);
  /// <summary>Bot shelters: west deck crowding the stair head (defenders stand in each other's line of fire), east deck near the ramp head, and the yard past the east door.</summary>
  public static readonly Vector3 StairShelter=new Vector3(-14.5f,Upper,8), RampShelter=new Vector3(12,Upper,10), YardShelter=new Vector3(10,0,-19);
  public static readonly Vector3[] Recovery={new Vector3(-8,1,-8),new Vector3(8,1,-8),new Vector3(0,1,-11),new Vector3(0,1,-2)};
 }

 /// <summary>Builds the warehouse from the Blender modules (Resources/Warehouse). Colliders are plain boxes and wedges.</summary>
 public static class WarehouseMap {
  public const string RootName="Warehouse";
  public static IReadOnlyList<Vector3> Shelters {get;}=Array.AsReadOnly(new[]{WarehouseLayout.StairShelter,WarehouseLayout.RampShelter,WarehouseLayout.YardShelter});
  static readonly Color Asphalt=new Color(.36f,.37f,.36f),Fallback=new Color(.55f,.55f,.52f);

  public static IReadOnlyList<Vector3> Build(Transform parent) {
   var root=new GameObject(RootName).transform;root.SetParent(parent,false);
   // Ground: one collider for the whole yard, the warehouse floor tiles sit 2 cm above it (visual only).
   Solid(root,"Ground",new Vector3(0,-.5f,0),new Vector3(WarehouseLayout.YardHalfX*2+2,1,WarehouseLayout.YardHalfZ*2+2),Asphalt);
   for(float x=-WarehouseLayout.HalfX+2;x<WarehouseLayout.HalfX;x+=4)for(float z=-WarehouseLayout.HalfZ+2;z<WarehouseLayout.HalfZ;z+=4)
    if(z<WarehouseLayout.DeckFront||Mathf.Abs(x)>WarehouseLayout.DeckHalfX)Module(root,"WH_Floor",new Vector3(x,.02f,z),0);
   Walls(root);Fence(root);Deck(root);Stair(root);Ramp(root);Railings(root);Cover(root);LightCrates(root);
   return Shelters;
  }

  static void Walls(Transform root) {
   float t=WarehouseLayout.WallThickness,h=WarehouseLayout.WallHeight,hx=WarehouseLayout.HalfX,hz=WarehouseLayout.HalfZ;
   for(float x=-hx+2;x<hx;x+=4) {
    bool door=Array.IndexOf(WarehouseLayout.SouthDoors,x)>=0;
    if(!door)Panel(root,new Vector3(x,0,-hz-t/2),180);
    Panel(root,new Vector3(x,0,hz+t/2),0);
   }
   for(float z=-hz+2;z<hz;z+=4) {
    bool door=Mathf.Approximately(z,WarehouseLayout.SideDoorZ);
    if(!door){Panel(root,new Vector3(-hx-t/2,0,z),-90);Panel(root,new Vector3(hx+t/2,0,z),90);}
   }
   void Panel(Transform parent,Vector3 foot,float yaw){var panel=Module(parent,"WH_Wall",foot,yaw);Collider(panel.gameObject,new Vector3(0,h/2,0),new Vector3(4,h,t));}
  }

  static void Fence(Transform root) {
   float hx=WarehouseLayout.YardHalfX,hz=WarehouseLayout.YardHalfZ,h=WarehouseLayout.FenceHeight;
   foreach(var (center,size) in new[]{(new Vector3(0,h/2,-hz),new Vector3(hx*2,h,.4f)),(new Vector3(0,h/2,hz),new Vector3(hx*2,h,.4f)),(new Vector3(-hx,h/2,0),new Vector3(.4f,h,hz*2)),(new Vector3(hx,h/2,0),new Vector3(.4f,h,hz*2))})
    Solid(root,"Yard fence",center,size,new Color(.42f,.5f,.44f));
  }

  // The mezzanine is a row of solid rack blocks: nothing walkable hides under the deck, so the bot grid sees one floor per cell.
  static void Deck(Transform root) {
   for(float x=-WarehouseLayout.DeckHalfX+2;x<WarehouseLayout.DeckHalfX;x+=4)for(float z=WarehouseLayout.DeckFront+2;z<WarehouseLayout.DeckBack;z+=4) {
    var block=Module(root,"WH_RackBlock",new Vector3(x,0,z),z<WarehouseLayout.DeckFront+4?180:0);
    Collider(block.gameObject,new Vector3(0,WarehouseLayout.Upper/2,0),new Vector3(4,WarehouseLayout.Upper,4));
    Module(root,"WH_Floor",new Vector3(x,WarehouseLayout.Upper+.02f,z),0);
   }
  }

  // 21 steps of 1/6 m: both factions climb without jumping; each step is a solid column like the village stair.
  static void Stair(Transform root) {
   float run=WarehouseLayout.StairLength/21,rise=WarehouseLayout.Upper/21,start=WarehouseLayout.DeckFront-WarehouseLayout.StairLength;
   var stair=Module(root,"WH_Stair",new Vector3(WarehouseLayout.StairX,0,start+WarehouseLayout.StairLength/2),0);
   for(int i=0;i<21;i++){float h=(i+1)*rise;Collider(stair.gameObject,new Vector3(0,h/2,-WarehouseLayout.StairLength/2+run*(i+.5f)),new Vector3(WarehouseLayout.StairWidth,h,run));}
  }

  // A solid wedge, so the slope is the only floor the bot grid sees there.
  static void Ramp(Transform root) {
   float l=WarehouseLayout.RampLength,w=WarehouseLayout.RampWidth,h=WarehouseLayout.Upper;
   var ramp=Module(root,"WH_Ramp",new Vector3(WarehouseLayout.RampX,0,WarehouseLayout.DeckFront-l/2),0);
   var mesh=new Mesh{name="Ramp wedge"};
   mesh.vertices=new[]{new Vector3(-w/2,0,-l/2),new Vector3(w/2,0,-l/2),new Vector3(-w/2,0,l/2),new Vector3(w/2,0,l/2),new Vector3(-w/2,h,l/2),new Vector3(w/2,h,l/2)};
   mesh.triangles=new[]{0,2,1, 1,2,3, 0,1,4, 1,5,4, 2,4,3, 3,4,5, 0,4,2, 1,3,5};
   mesh.RecalculateNormals();mesh.RecalculateBounds();
   var wedge=new GameObject("Ramp collider");wedge.transform.SetParent(ramp,false);var collider=wedge.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=true;
  }

  // 1 m railing along the deck edge with openings at the stair head, the ramp head and the two jump-down gaps.
  static void Railings(Transform root) {
   float edge=WarehouseLayout.DeckFront-.05f,half=WarehouseLayout.DeckHalfX;
   var openings=new List<(float a,float b)>{(WarehouseLayout.StairX-WarehouseLayout.StairWidth/2-.2f,WarehouseLayout.StairX+WarehouseLayout.StairWidth/2+.2f),(WarehouseLayout.RampX-WarehouseLayout.RampWidth/2-.2f,WarehouseLayout.RampX+WarehouseLayout.RampWidth/2+.2f)};
   foreach(var gap in WarehouseLayout.DropGaps)openings.Add((gap-2,gap+2));
   openings.Sort((p,q)=>p.a.CompareTo(q.a));
   float from=-half;
   foreach(var (a,b) in openings){Rail(root,from,a,edge);from=b;}
   Rail(root,from,half,edge);
   // Side edges of the deck stay open: another (longer) way down.
  }
  static void Rail(Transform root,float from,float to,float z) {
   if(to-from<.3f)return;float length=to-from;
   var rail=Module(root,"WH_Railing",new Vector3((from+to)/2,WarehouseLayout.Upper,z),0);rail.localScale=new Vector3(length/4,1,1);
   Collider(rail.gameObject,new Vector3(0,.5f,0),new Vector3(4,1,.12f));
  }

  // Fixed crate stacks: cover on the ground floor and on the deck. Never on the stair, ramp, gaps or spawn.
  static readonly (Vector3 p,int high)[] CoverStacks={
   (new Vector3(-9,0,-2),2),(new Vector3(8.5f,0,-8),1),(new Vector3(-3,0,-10.5f),2),(new Vector3(12,0,1),1),(new Vector3(-20,0,-10),2),
   (new Vector3(-1,WarehouseLayout.Upper,11),1),(new Vector3(17,WarehouseLayout.Upper,12),2),(new Vector3(-18,WarehouseLayout.Upper,12.5f),1)};
  public static IEnumerable<Vector3> CoverPositions(){foreach(var s in CoverStacks)yield return s.p;}
  static void Cover(Transform root) {
   foreach(var (p,high) in CoverStacks)for(int i=0;i<high;i++)for(int j=0;j<2;j++) {
    var crate=Module(root,"WH_Crate",p+new Vector3(j*1.05f-.525f,i*1.0f,0),(i*17+j*9)%12-6);
    Collider(crate.gameObject,new Vector3(0,.5f,0),Vector3.one);
   }
  }

  // Light crates are pushable props (no collider): bodies and shots knock them, and they tumble off the deck.
  static readonly Vector3[] Light={
   new Vector3(-13,WarehouseLayout.Upper,7.4f),new Vector3(-10.5f,WarehouseLayout.Upper,7.2f),new Vector3(-6,WarehouseLayout.Upper,8.5f),new Vector3(4.5f,WarehouseLayout.Upper,8),new Vector3(13.5f,WarehouseLayout.Upper,8),
   new Vector3(-12,0,-6),new Vector3(2,0,-11),new Vector3(11,0,-11),new Vector3(-14,0,-19),new Vector3(16,0,-20)};
  public static IEnumerable<Vector3> LightCratePositions()=>Light;
  static void LightCrates(Transform root) {
   var container=new GameObject(PushProps.Container).transform;container.SetParent(root,false);
   for(int i=0;i<Light.Length;i++){var crate=Module(container,"WH_Crate",Light[i],i*23%90);crate.name="Light crate "+i;crate.localScale=Vector3.one*.7f;}
  }

  // One Blender module (visual only). Without the imported model a plain box of the same footprint stands in.
  static Transform Module(Transform parent,string name,Vector3 foot,float yaw) {
   var prefab=Resources.Load<GameObject>("Warehouse/"+name);
   GameObject go;
   if(prefab!=null){go=UnityEngine.Object.Instantiate(prefab,parent);foreach(var c in go.GetComponentsInChildren<Collider>())UnityEngine.Object.Destroy(c);}
   else{go=GameObject.CreatePrimitive(PrimitiveType.Cube);UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.transform.SetParent(parent,false);go.GetComponent<Renderer>().sharedMaterial=PrototypeVillage.Material(Fallback);}
   go.name=name;go.transform.localPosition=foot;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
   return go.transform;
  }
  static void Collider(GameObject owner,Vector3 center,Vector3 size){var box=owner.AddComponent<BoxCollider>();box.center=center;box.size=size;}
  static void Solid(Transform parent,string name,Vector3 center,Vector3 size,Color color) {
   var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(parent,false);
   box.transform.localPosition=center;box.transform.localScale=size;box.GetComponent<Renderer>().sharedMaterial=PrototypeVillage.Material(color);
  }
 }
}
