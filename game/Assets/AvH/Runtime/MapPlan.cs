using System;
using System.Collections.Generic;
using UnityEngine;
namespace AvH {
 /// <summary>What a planned box is for: decides its fallback colour and how the editor dresses it.</summary>
 public enum PlanKind { Ground, Rock, Terrace, Wall, House, Platform, Deck, Rubble, Tree }
 /// <summary>A solid box (centre, full size, yaw in degrees). A tree keeps only a trunk collider.</summary>
 public struct PlanBlock { public PlanKind Kind; public Vector3 Center, Size; public float Yaw; }
 /// <summary>A stair or ramp: <see cref="Center"/> is the middle of its footprint on its base floor; it climbs toward its local +Z.</summary>
 public struct PlanClimb { public Vector3 Center; public float Yaw, Rise, Width, Length; public bool Ramp; }
 /// <summary>A 1 m parapet from <see cref="From"/> to <see cref="To"/> standing on the floor at their height.</summary>
 public struct PlanRail { public Vector3 From, To; }
 /// <summary>
 /// One map as data: solid boxes, climbs, parapets, cover, pushable crates and the points bots use. The game builds its
 /// colliders (and plain fallback shapes) from it; the editor dresses the same plan with owned Synty models, so the
 /// look always sits exactly on the collision.
 /// </summary>
 public sealed class MapPlan {
  public string Root, Theme;
  public readonly List<PlanBlock> Blocks=new List<PlanBlock>();
  public readonly List<PlanClimb> Climbs=new List<PlanClimb>();
  public readonly List<PlanRail> Rails=new List<PlanRail>();
  public readonly List<(Vector3 p,int high)> Crates=new List<(Vector3,int)>();
  public readonly List<Vector3> LightCrates=new List<Vector3>();
  public Vector3[] Shelters, Recovery;
  public RouteGrid Grid=RouteGrid.Default;
  public void Box(PlanKind kind,Vector3 center,Vector3 size,float yaw=0)=>Blocks.Add(new PlanBlock{Kind=kind,Center=center,Size=size,Yaw=yaw});
  /// <summary>A box given by its floor rectangle (x0..x1, z0..z1) from <paramref name="bottom"/> up to <paramref name="top"/>.</summary>
  public void Slab(PlanKind kind,float x0,float x1,float z0,float z1,float bottom,float top)=>Box(kind,new Vector3((x0+x1)/2,(bottom+top)/2,(z0+z1)/2),new Vector3(x1-x0,top-bottom,z1-z0));
  public void Stair(Vector3 center,float yaw,float rise,float width=2)=>Climbs.Add(new PlanClimb{Center=center,Yaw=yaw,Rise=rise,Width=width,Length=StairLength(rise)});
  public void Ramp(Vector3 center,float yaw,float rise,float length,float width=3)=>Climbs.Add(new PlanClimb{Center=center,Yaw=yaw,Rise=rise,Width=width,Length=length,Ramp=true});
  public void Rail(Vector3 from,Vector3 to)=>Rails.Add(new PlanRail{From=from,To=to});
  /// <summary>Steps of 1/6 m rise and 0.4 m run: both factions climb them without jumping.</summary>
  public static int Steps(float rise)=>Mathf.Max(1,Mathf.RoundToInt(rise*6));
  public static float StairLength(float rise)=>Steps(rise)*.4f;
 }

 /// <summary>Builds a <see cref="MapPlan"/>: colliders always, plain shapes only when no generated dressing exists.</summary>
 public static class PlannedMap {
  public const string DressingName="Synty dressing";
  public static IReadOnlyList<Vector3> Build(Transform parent,MapPlan plan,GameObject dressing,GameObject crate) {
   var root=new GameObject(plan.Root).transform;root.SetParent(parent,false);
   bool dressed=dressing!=null;
   if(dressed){var d=UnityEngine.Object.Instantiate(dressing,root);d.name=DressingName;TownDressing.Dress(d.transform);}
   var solids=new GameObject("Solids").transform;solids.SetParent(root,false);
   foreach(var b in plan.Blocks) {
    if(b.Kind==PlanKind.Tree) {
     var tree=new GameObject("Tree").transform;tree.SetParent(solids,false);tree.localPosition=b.Center;
     MapKit.Collider(tree.gameObject,new Vector3(0,b.Size.y/2,0),new Vector3(b.Size.x,b.Size.y,b.Size.z));
     if(!dressed){Plain(tree,"Trunk",new Vector3(0,b.Size.y/2,0),new Vector3(b.Size.x,b.Size.y,b.Size.z),Colour(plan.Theme,b.Kind));Plain(tree,"Canopy",new Vector3(0,b.Size.y+1.2f,0),new Vector3(3,2.4f,3),new Color(.3f,.5f,.25f));}
     continue;
    }
    var box=MapKit.Solid(solids,b.Kind.ToString(),b.Center,b.Size,Colour(plan.Theme,b.Kind));box.localRotation=Quaternion.Euler(0,b.Yaw,0);
    if(dressed)box.GetComponent<Renderer>().enabled=false;
   }
   foreach(var c in plan.Climbs) {
    var climb=new GameObject(c.Ramp?"Ramp":"Stair").transform;climb.SetParent(solids,false);climb.localPosition=c.Center;climb.localRotation=Quaternion.Euler(0,c.Yaw,0);
    if(c.Ramp) {
     MapKit.RampWedge(climb,c.Length,c.Width,c.Rise);
     if(!dressed){var wedge=climb.GetComponentInChildren<MeshCollider>();wedge.gameObject.AddComponent<MeshFilter>().sharedMesh=wedge.sharedMesh;wedge.gameObject.AddComponent<MeshRenderer>().sharedMaterial=PrototypeVillage.Material(Colour(plan.Theme,PlanKind.Platform));}
    } else {
     MapKit.StairColliders(climb,c.Length,c.Width,c.Rise,MapPlan.Steps(c.Rise));
     if(!dressed){int steps=MapPlan.Steps(c.Rise);float run=c.Length/steps;for(int i=0;i<steps;i++){float h=(i+1)*c.Rise/steps;Plain(climb,"Step",new Vector3(0,h/2,-c.Length/2+run*(i+.5f)),new Vector3(c.Width,h,run),Colour(plan.Theme,PlanKind.Platform));}}
    }
   }
   foreach(var r in plan.Rails) {
    var d=r.To-r.From;d.y=0;var rail=new GameObject("Rail").transform;rail.SetParent(solids,false);rail.localPosition=(r.From+r.To)/2;rail.localRotation=Quaternion.LookRotation(d.normalized);
    MapKit.Collider(rail.gameObject,new Vector3(0,.5f,0),new Vector3(.3f,1,d.magnitude));
    if(!dressed)Plain(rail,"Parapet",new Vector3(0,.5f,0),new Vector3(.3f,1,d.magnitude),Colour(plan.Theme,PlanKind.Wall));
   }
   foreach(var (p,high) in plan.Crates)for(int i=0;i<high;i++)for(int j=0;j<2;j++) {
    var at=p+new Vector3(j*1.05f-.525f,i,0);
    var holder=dressed?new GameObject("Crate").transform:MapKit.Module(solids,"Warehouse/WH_Crate",at,(i*17+j*9)%12-6);
    if(dressed){holder.SetParent(solids,false);holder.localPosition=at;}
    MapKit.Collider(holder.gameObject,new Vector3(0,.5f,0),Vector3.one);
   }
   var container=new GameObject(PushProps.Container).transform;container.SetParent(root,false);
   for(int i=0;i<plan.LightCrates.Count;i++) {
    Transform c;
    if(crate!=null){c=UnityEngine.Object.Instantiate(crate,container).transform;c.localPosition=plan.LightCrates[i];c.localRotation=Quaternion.Euler(0,i*23%90,0);}
    else c=MapKit.Module(container,"Warehouse/WH_Crate",plan.LightCrates[i],i*23%90);
    c.name="Light crate "+i;c.localScale=Vector3.one*.7f;
   }
   return Array.AsReadOnly(plan.Shelters);
  }
  static void Plain(Transform parent,string name,Vector3 local,Vector3 size,Color color) {
   var go=GameObject.CreatePrimitive(PrimitiveType.Cube);UnityEngine.Object.Destroy(go.GetComponent<Collider>());go.name=name;go.transform.SetParent(parent,false);
   go.transform.localPosition=local;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=PrototypeVillage.Material(color);
  }
  /// <summary>Plain fallback colours by theme and kind.</summary>
  public static Color Colour(string theme,PlanKind kind) {
   bool snow=theme=="snow",sky=theme=="sky";
   switch(kind) {
    case PlanKind.Ground:case PlanKind.Terrace:return snow?new Color(.9f,.92f,.95f):sky?new Color(.45f,.62f,.35f):new Color(.38f,.5f,.3f);
    case PlanKind.Rock:return new Color(.5f,.47f,.42f);
    case PlanKind.Wall:return snow?new Color(.6f,.6f,.62f):new Color(.55f,.52f,.46f);
    case PlanKind.House:return snow?new Color(.55f,.4f,.3f):new Color(.7f,.62f,.5f);
    case PlanKind.Deck:return new Color(.55f,.4f,.26f);
    case PlanKind.Rubble:return new Color(.5f,.48f,.44f);
    case PlanKind.Tree:return new Color(.4f,.3f,.2f);
    default:return new Color(.62f,.58f,.5f);
   }
  }
 }
}
