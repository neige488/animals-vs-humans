using System;
using System.Collections.Generic;
using UnityEngine;
namespace AvH {
 /// <summary>
 /// Sand crossing, metres: a wide walled desert town. Its layout is original; it borrows only the general idea of
 /// team shooter maps (three lanes, held sites with two ways in).
 /// Everyone starts in the south plaza. Three lanes lead north to the courtyard: a long open street (west),
 /// a narrow bent alley (middle) and a short roofed passage (east). Humans hold two raised sites and a tower,
 /// joined by a bridge; animals come up the stairs or round by the ramps, and a push off the bridge drops a body into the courtyard.
 /// <code>
 ///  z=36 ┌[ site A y3.5 ]──────[tower]──────[ site B y3.5 ]┐
 ///       │ A x-48..-28 ══bridge z30..33══ B x28..48        │
 ///  z=24 │ ▲stair x-44   ramp◄  stair◄   ►ramp   stair x40▲│
 ///  z=20 │long│   block W2       │alley│  block E2  │roof│  │
 ///       │lane│                  └bend─┐            │pass│  │
 ///       │    │   block W1        alley│  block E1  │age │  │
 ///  z=-20│    └──────────────── ─┘    └────────────┘    │  │
 ///       │            south plaza (spawn z -28)            │
 ///  z=-36└─────────────────────────────────────────────────┘
 ///      x=-52                                            x=52
 /// </code>
 /// </summary>
 public static class CrossingLayout {
  public const float HalfX=52,HalfZ=36,WallHeight=6,WallThickness=.4f,BlockHeight=6,Upper=3.5f;
  public const float StairWidth=2,StairLength=8.4f,RampWidth=3,RampLength=12,BridgeWidth=3;
  /// <summary>Solid town blocks (xmin, xmax, zmin, zmax): the streets are what is left between them.</summary>
  public static readonly Rect[] Blocks={R(-40,0,-20,4),R(4,36,-20,-4),R(-40,12,4,20),R(16,36,-4,20),R(44,52,-20,20)};
  /// <summary>Raised sites (top at <see cref="Upper"/>): A west, B east, the tower in the middle.</summary>
  public static readonly Rect SiteA=R(-48,-28,24,36),SiteB=R(28,48,24,36),Tower=R(-4,4,24,36);
  public const float BridgeZ=31.5f;
  /// <summary>Lanes (for logs and tests): long street, bent alley, roofed passage.</summary>
  public static readonly Rect LongLane=R(-52,-40,-20,20),MiddleAlley=R(0,16,-20,20),Passage=R(36,44,-20,20);
  public static readonly Rect RoofSpan=R(36,44,-12,12);
  // Stairs climb +Z from the lanes (A, B) and -X onto the tower; ramps climb sideways onto the sites.
  public static readonly Vector3 StairA=new Vector3(-44,0,24-StairLength/2),StairB=new Vector3(40,0,24-StairLength/2),TowerStair=new Vector3(4+StairLength/2,0,26);
  public static readonly Vector3 RampA=new Vector3(-28+RampLength/2,0,26),RampB=new Vector3(28-RampLength/2,0,26);
  public static Vector3 StairAFoot=>new Vector3(-44,0,24-StairLength-.8f);
  public static Vector3 StairBFoot=>new Vector3(40,0,24-StairLength-.8f);
  public static Vector3 RampAFoot=>new Vector3(-28+RampLength+.8f,0,26);
  public static Vector3 RampBFoot=>new Vector3(28-RampLength-.8f,0,26);
  public static readonly Vector3 ShelterA=new Vector3(-38,Upper,31),ShelterB=new Vector3(38,Upper,31),ShelterTower=new Vector3(0,Upper,32);
  public static readonly Vector3[] Recovery={new Vector3(0,1,-28),new Vector3(-46,1,0),new Vector3(40,1,0),new Vector3(0,1,21.5f),new Vector3(8,1,0)};
  /// <summary>Jump-down gaps in the bridge railings (x, side z).</summary>
  public static readonly (float x,float z)[] BridgeGaps={(-16,30),(16,30),(-22,33),(22,33)};
  /// <summary>The bot grid covering the whole town, with a second layer for the bridge over the courtyard.</summary>
  public static readonly RouteGrid Grid=new RouteGrid(-HalfX-1,-HalfZ-1,.75f,(int)((HalfX+1)*2/.75f)+1,(int)((HalfZ+1)*2/.75f)+1,2);
  public static bool OnDeck(Vector3 p)=>p.y>Upper-.4f&&p.z>23.7f&&p.z<36&&(SiteA.Contains(new Vector2(p.x,p.z))||SiteB.Contains(new Vector2(p.x,p.z))||Tower.Contains(new Vector2(p.x,p.z))||Mathf.Abs(p.z-BridgeZ)<BridgeWidth/2+.2f);
  public static bool OnBridge(Vector3 p)=>p.y>Upper-.4f&&Mathf.Abs(p.z-BridgeZ)<BridgeWidth/2+.2f&&Mathf.Abs(p.x)>4&&Mathf.Abs(p.x)<28;
  /// <summary>On a stair or ramp slope (not ground, not deck).</summary>
  public static bool OnClimb(Vector3 p) {
   if(p.y<.4f||p.y>Upper-.3f)return false;
   bool stair=Mathf.Abs(p.x-StairA.x)<1.3f&&p.z<24||Mathf.Abs(p.x-StairB.x)<1.3f&&p.z<24||Mathf.Abs(p.z-TowerStair.z)<1.3f&&p.x>4&&p.x<13;
   bool ramp=Mathf.Abs(p.z-26)<1.8f&&(p.x>-28&&p.x<-15||p.x>15&&p.x<28);
   return stair||ramp;
  }
  /// <summary>Lane name an animal is in, or null.</summary>
  public static string Lane(Vector3 p) {
   var q=new Vector2(p.x,p.z);
   return LongLane.Contains(q)?"긴 대로":MiddleAlley.Contains(q)&&!InBlock(q)?"중앙 골목":Passage.Contains(q)?"지붕 통로":null;
  }
  static bool InBlock(Vector2 q){foreach(var b in Blocks)if(b.Contains(q))return true;return false;}
  static Rect R(float x0,float x1,float z0,float z1)=>Rect.MinMaxRect(x0,z0,x1,z1);
 }

 /// <summary>Builds the sand crossing from the Blender modules (Resources/Crossing). Colliders are plain boxes and wedges.</summary>
 public static class CrossingMap {
  public const string RootName="Crossing";
  public static IReadOnlyList<Vector3> Shelters {get;}=Array.AsReadOnly(new[]{CrossingLayout.ShelterA,CrossingLayout.ShelterB,CrossingLayout.ShelterTower});
  static readonly Color Sand=new Color(.84f,.72f,.5f),Roof=new Color(.72f,.4f,.26f);

  public static IReadOnlyList<Vector3> Build(Transform parent) {
   var root=new GameObject(RootName).transform;root.SetParent(parent,false);
   MapKit.Solid(root,"Ground",new Vector3(0,-.5f,0),new Vector3(CrossingLayout.HalfX*2+2,1,CrossingLayout.HalfZ*2+2),Sand);
   for(float x=-CrossingLayout.HalfX+2;x<CrossingLayout.HalfX;x+=4)for(float z=-CrossingLayout.HalfZ+2;z<-20;z+=4)Module(root,"SD_Floor",new Vector3(x,.02f,z),0);
   Walls(root);foreach(var b in CrossingLayout.Blocks)Block(root,b);
   Raised(root,"Site A",CrossingLayout.SiteA);Raised(root,"Site B",CrossingLayout.SiteB);Raised(root,"Tower",CrossingLayout.Tower);
   Bridge(root);Climbs(root);Railings(root);Passage(root);Cover(root);Decor(root);LightCrates(root);
   return Shelters;
  }

  static void Walls(Transform root) {
   float t=CrossingLayout.WallThickness,h=CrossingLayout.WallHeight,hx=CrossingLayout.HalfX,hz=CrossingLayout.HalfZ;
   for(float x=-hx+2;x<hx;x+=4){Panel(new Vector3(x,0,-hz-t/2),180);Panel(new Vector3(x,0,hz+t/2),0);}
   for(float z=-hz+2;z<hz;z+=4){Panel(new Vector3(-hx-t/2,0,z),-90);Panel(new Vector3(hx+t/2,0,z),90);}
   void Panel(Vector3 foot,float yaw){var panel=Module(root,"SD_Wall",foot,yaw);MapKit.Collider(panel.gameObject,new Vector3(0,h/2,0),new Vector3(4,h,t));}
  }

  // A town block: one solid collider, house modules only along its outside, one flat roof over the inside.
  static void Block(Transform root,Rect r) {
   var block=new GameObject("Block").transform;block.SetParent(root,false);
   MapKit.Invisible(block,"Block collider",new Vector3(r.center.x,CrossingLayout.BlockHeight/2,r.center.y),new Vector3(r.width,CrossingLayout.BlockHeight,r.height));
   for(float x=r.xMin+2;x<r.xMax;x+=4)for(float z=r.yMin+2;z<r.yMax;z+=4)
    if(x<r.xMin+3||x>r.xMax-3||z<r.yMin+3||z>r.yMax-3)Module(block,"SD_Block",new Vector3(x,0,z),((int)(x+z)&3)*90);
   if(r.width>8&&r.height>8)Visual(block,"Block roof",new Vector3(r.center.x,CrossingLayout.BlockHeight-.05f,r.center.y),new Vector3(r.width-8,.1f,r.height-8),Roof);
  }

  // A raised site: solid stone base (nothing walkable under it), floor tiles on top.
  static void Raised(Transform root,string name,Rect r) {
   var site=new GameObject(name).transform;site.SetParent(root,false);
   MapKit.Invisible(site,name+" collider",new Vector3(r.center.x,CrossingLayout.Upper/2,r.center.y),new Vector3(r.width,CrossingLayout.Upper,r.height));
   for(float x=r.xMin+2;x<r.xMax;x+=4)for(float z=r.yMin+2;z<r.yMax;z+=4){Module(site,"SD_Platform",new Vector3(x,0,z),0);Module(site,"SD_Floor",new Vector3(x,CrossingLayout.Upper+.02f,z),0);}
  }

  // Two spans over the courtyard, site to tower to site. The courtyard stays open underneath (the bot grid's second layer).
  static void Bridge(Transform root) {
   var bridge=new GameObject("Bridge").transform;bridge.SetParent(root,false);
   foreach(var (from,to) in new[]{(CrossingLayout.SiteA.xMax,CrossingLayout.Tower.xMin),(CrossingLayout.Tower.xMax,CrossingLayout.SiteB.xMin)}) {
    MapKit.Invisible(bridge,"Bridge deck",new Vector3((from+to)/2,CrossingLayout.Upper-.2f,CrossingLayout.BridgeZ),new Vector3(to-from,.4f,CrossingLayout.BridgeWidth));
    for(float x=from+2;x<to;x+=4)Module(bridge,"SD_Bridge",new Vector3(x,CrossingLayout.Upper,CrossingLayout.BridgeZ),0);
   }
   foreach(var x in new[]{-20f,-10f,10f,20f}){var pillar=Module(bridge,"SD_Pillar",new Vector3(x,0,CrossingLayout.BridgeZ),0);MapKit.Collider(pillar.gameObject,new Vector3(0,1.55f,0),new Vector3(.8f,3.1f,.8f));}
  }

  static void Climbs(Transform root) {
   Stair(root,CrossingLayout.StairA,0);Stair(root,CrossingLayout.StairB,0);Stair(root,CrossingLayout.TowerStair,-90);
   Ramp(root,CrossingLayout.RampA,-90);Ramp(root,CrossingLayout.RampB,90);
  }
  static void Stair(Transform root,Vector3 centre,float yaw){var stair=Module(root,"SD_Stair",centre,yaw);MapKit.StairColliders(stair,CrossingLayout.StairLength,CrossingLayout.StairWidth,CrossingLayout.Upper,21);}
  static void Ramp(Transform root,Vector3 centre,float yaw){var ramp=Module(root,"SD_Ramp",centre,yaw);MapKit.RampWedge(ramp,CrossingLayout.RampLength,CrossingLayout.RampWidth,CrossingLayout.Upper);}

  // 1 m parapets on every open deck edge, with openings at stair and ramp heads, the bridge ends and the jump-down gaps.
  static void Railings(Transform root) {
   var rails=new GameObject("Railings").transform;rails.SetParent(root,false);
   float half=CrossingLayout.StairWidth/2+.2f,ramp=CrossingLayout.RampWidth/2+.2f,bridge=CrossingLayout.BridgeWidth/2;
   (float,float) Open(float c,float h)=>(c-h,c+h);
   var a=CrossingLayout.SiteA;var b=CrossingLayout.SiteB;var t=CrossingLayout.Tower;
   // Site fronts (south), with the stair head and one jump-down gap.
   Run(rails,a.xMin,a.xMax,a.yMin+.15f,true,Open(-44,half),Open(-34,2));
   Run(rails,b.xMin,b.xMax,b.yMin+.15f,true,Open(40,half),Open(34,2));
   Run(rails,t.xMin,t.xMax,t.yMin+.15f,true,Open(-2,1));
   // Inner sides face the courtyard: ramp head and bridge.
   Run(rails,a.yMin,a.yMax,a.xMax-.15f,false,Open(26,ramp),Open(CrossingLayout.BridgeZ,bridge));
   Run(rails,b.yMin,b.yMax,b.xMin+.15f,false,Open(26,ramp),Open(CrossingLayout.BridgeZ,bridge));
   Run(rails,t.yMin,t.yMax,t.xMax-.15f,false,Open(26,half),Open(CrossingLayout.BridgeZ,bridge));
   Run(rails,t.yMin,t.yMax,t.xMin+.15f,false,Open(CrossingLayout.BridgeZ,bridge));
   // Outer sides drop into the strip behind each site.
   Run(rails,a.yMin,a.yMax,a.xMin+.15f,false,Open(30,2));
   Run(rails,b.yMin,b.yMax,b.xMax-.15f,false,Open(30,2));
   // Both bridge edges, each span with a gap.
   float south=CrossingLayout.BridgeZ-bridge+.15f,north=CrossingLayout.BridgeZ+bridge-.15f;
   Run(rails,a.xMax,t.xMin,south,true,Open(-16,2));Run(rails,t.xMax,b.xMin,south,true,Open(16,2));
   Run(rails,a.xMax,t.xMin,north,true,Open(-22,2));Run(rails,t.xMax,b.xMin,north,true,Open(22,2));
  }
  static void Run(Transform parent,float from,float to,float at,bool alongX,params (float a,float b)[] openings) {
   var gaps=new List<(float a,float b)>(openings);gaps.Sort((p,q)=>p.a.CompareTo(q.a));
   float start=from;
   foreach(var (a,b) in gaps){Rail(parent,start,Mathf.Min(a,to),at,alongX);start=Mathf.Max(start,b);}
   Rail(parent,start,to,at,alongX);
  }
  static void Rail(Transform parent,float from,float to,float at,bool alongX) {
   if(to-from<.3f)return;float length=to-from,mid=(from+to)/2;
   var foot=alongX?new Vector3(mid,CrossingLayout.Upper,at):new Vector3(at,CrossingLayout.Upper,mid);
   var rail=Module(parent,"SD_Railing",foot,alongX?0:90);rail.localScale=new Vector3(length/4,1,1);
   MapKit.Collider(rail.gameObject,new Vector3(0,.5f,0),new Vector3(4,1,.3f));
  }

  // The east passage is roofed (shade only: nobody stands up there and shots pass the tiles).
  static void Passage(Transform root) {
   var r=CrossingLayout.RoofSpan;
   for(float z=r.yMin+2;z<r.yMax;z+=4)Module(root,"SD_Roof",new Vector3(r.center.x,0,z),0);
  }

  // Sandbag walls and crate stacks: cover in the plaza, the lanes and the courtyard; never on a stair, ramp, gap or the spawn.
  static readonly (Vector3 p,float yaw)[] LowWalls={
   (new Vector3(-12,0,-24),0),(new Vector3(12,0,-24),0),(new Vector3(-46,0,-8),0),(new Vector3(-48,0,8),0),
   (new Vector3(8,0,0),0),(new Vector3(40,0,-16),0),(new Vector3(-22,0,21.5f),0),(new Vector3(22,0,21.5f),0)};
  static readonly (Vector3 p,int high)[] CrateStacks={
   (new Vector3(-44,0,-15),2),(new Vector3(-50,0,13),1),(new Vector3(-26,0,-31),2),(new Vector3(28,0,-33),1),(new Vector3(30,0,-25),2),
   (new Vector3(13,0,-2),1),(new Vector3(-8,0,22.5f),1),(new Vector3(-40,CrossingLayout.Upper,34),1),(new Vector3(42,CrossingLayout.Upper,34),2)};
  public static IEnumerable<Vector3> CoverPositions(){foreach(var w in LowWalls)yield return w.p;foreach(var s in CrateStacks)yield return s.p;}
  static void Cover(Transform root) {
   var cover=new GameObject("Cover").transform;cover.SetParent(root,false);
   foreach(var (p,yaw) in LowWalls){var wall=Module(cover,"SD_LowWall",p,yaw);MapKit.Collider(wall.gameObject,new Vector3(0,.6f,0),new Vector3(4,1.2f,.6f));}
   foreach(var (p,high) in CrateStacks)for(int i=0;i<high;i++)for(int j=0;j<2;j++) {
    var crate=MapKit.Module(cover,"Warehouse/WH_Crate",p+new Vector3(j*1.05f-.525f,i*1.0f,0),(i*17+j*9)%12-6);
    MapKit.Collider(crate.gameObject,new Vector3(0,.5f,0),Vector3.one);
   }
  }

  static void Decor(Transform root) {
   var decor=new GameObject("Decor").transform;decor.SetParent(root,false);
   foreach(var p in new[]{new Vector3(-36,0,-30),new Vector3(36,0,-30),new Vector3(-20,0,-23),new Vector3(20,0,-23)}){var palm=Module(decor,"SD_Palm",p,p.x*7);MapKit.Collider(palm.gameObject,new Vector3(0,2.5f,0),new Vector3(.5f,5,.5f));}
   // Shop awnings over the lanes, high enough to walk under.
   foreach(var (p,yaw) in new[]{(new Vector3(0,2.9f,-12),90f),(new Vector3(4,2.9f,-8),-90f),(new Vector3(-40,2.9f,-10),-90f),(new Vector3(-40,2.9f,12),-90f),(new Vector3(-20,2.9f,-20),180f),(new Vector3(20,2.9f,-20),180f),(new Vector3(16,2.9f,10),-90f),(new Vector3(36,2.9f,16),90f)})
    Module(decor,"SD_Awning",p,yaw);
  }

  // Light crates are pushable props (no collider): bodies and shots knock them, and they tumble off the decks and the bridge.
  static readonly Vector3[] Light={
   new Vector3(-34,CrossingLayout.Upper,27),new Vector3(-41,CrossingLayout.Upper,26.5f),new Vector3(34,CrossingLayout.Upper,27),new Vector3(43,CrossingLayout.Upper,26.5f),
   new Vector3(-12,CrossingLayout.Upper,CrossingLayout.BridgeZ),new Vector3(12,CrossingLayout.Upper,CrossingLayout.BridgeZ),new Vector3(1.5f,CrossingLayout.Upper,30),
   new Vector3(-46,0,2),new Vector3(2,0,-12),new Vector3(40,0,5),new Vector3(-6,0,28),new Vector3(18,0,-29)};
  public static IEnumerable<Vector3> LightCratePositions()=>Light;
  static void LightCrates(Transform root) {
   var container=new GameObject(PushProps.Container).transform;container.SetParent(root,false);
   for(int i=0;i<Light.Length;i++){var crate=MapKit.Module(container,"Warehouse/WH_Crate",Light[i],i*23%90);crate.name="Light crate "+i;crate.localScale=Vector3.one*.7f;}
  }

  static Transform Module(Transform parent,string name,Vector3 foot,float yaw)=>MapKit.Module(parent,"Crossing/"+name,foot,yaw);
  static void Visual(Transform parent,string name,Vector3 center,Vector3 size,Color color) {
   var box=MapKit.Solid(parent,name,center,size,color);UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
  }
 }
}
