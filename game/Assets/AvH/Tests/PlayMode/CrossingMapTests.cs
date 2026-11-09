using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 /// <summary>The sand crossing: modules, spawns, the three lanes, the bridge over the courtyard and its drop gaps.</summary>
 public class CrossingMapTests {
  static string TempSettings()=>System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml");
  static Vector3 V(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);
  static UnityPlaytestSession Crossing(GameObject root) {
   var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.BotAutomationEnabled=false;
   game.StartSolo("교차로",77,TempSettings(),GameMaps.Crossing);return game;
  }
  static bool Drops(List<Vector3> path,Vector3 from){var last=from;foreach(var p in path){if(last.y-p.y>.6f)return true;last=p;}return false;}
  static string Show(List<Vector3> path)=>string.Join(" ",path.Select(p=>p.ToString("F1")));
  static bool In(Rect r,Vector3 p,float margin=0)=>p.x>r.xMin-margin&&p.x<r.xMax+margin&&p.z>r.yMin-margin&&p.z<r.yMax+margin;

  [Test] public void BlenderModulesStandOnTheirOrigin() {
   foreach(var (name,height) in new[]{("SD_Floor",.01f),("SD_Wall",6f),("SD_Block",6f),("SD_Platform",3.5f),("SD_Stair",3.55f),("SD_Ramp",3.5f),("SD_Railing",1f),("SD_Bridge",.02f),("SD_Pillar",3.1f),("SD_Roof",3.6f),("SD_LowWall",1.2f)}) {
    var prefab=Resources.Load<GameObject>("Crossing/"+name);Assert.IsNotNull(prefab,name+" is imported from Blender");
    var b=new Bounds();bool first=true;
    foreach(var f in prefab.GetComponentsInChildren<MeshFilter>()){var m=f.sharedMesh.bounds;foreach(var c in new[]{m.min,m.max}){var w=prefab.transform.InverseTransformPoint(f.transform.TransformPoint(c));if(first){b=new Bounds(w,Vector3.zero);first=false;}else b.Encapsulate(w);}}
    Assert.AreEqual(height,b.max.y,.12f,name+" top");Assert.AreEqual(0,(b.min.x+b.max.x)/2,.1f,name+" centred on x");Assert.AreEqual(0,(b.min.z+b.max.z)/2,.15f,name+" centred on z");
   }
   Assert.IsNotNull(Resources.Load<GameObject>("Crossing/SD_Awning"));Assert.IsNotNull(Resources.Load<GameObject>("Crossing/SD_Palm"));
  }
  [Test] public void BlenderStairAndRampRiseTowardPlusZ() {
   foreach(var name in new[]{"SD_Stair","SD_Ramp"}) {
    var prefab=Resources.Load<GameObject>("Crossing/"+name);var filter=prefab.GetComponentInChildren<MeshFilter>();var t=filter.transform;
    var vertices=filter.sharedMesh.vertices.Select(v=>prefab.transform.InverseTransformPoint(t.TransformPoint(v))).ToArray();
    float top=vertices.Max(v=>v.y);var high=vertices.Where(v=>v.y>top-.3f).Average(v=>v.z);var low=vertices.Where(v=>v.y<.3f).Average(v=>v.z);
    Assert.Greater(high,low+2,name+" climbs toward +Z like its collider");
   }
  }

  [Test] public void CrossingIsTheThirdMapAndMuchWiderThanTheOthers() {
   Assert.Greater(GameMaps.Count,GameMaps.Crossing);Assert.AreEqual("모래 교차로",GameMaps.Name(GameMaps.Crossing));Assert.AreEqual(GameMaps.Crossing,GameMaps.Clamp(GameMaps.Crossing));
   Assert.GreaterOrEqual(CrossingLayout.HalfX*2*CrossingLayout.HalfZ*2,(WarehouseLayout.YardHalfX*2*WarehouseLayout.YardHalfZ*2)*2.2f,"at least twice the warehouse yard");
   var g=CrossingLayout.Grid;
   Assert.LessOrEqual(g.OriginX,-CrossingLayout.HalfX);Assert.GreaterOrEqual(g.OriginX+(g.SizeX-1)*g.Cell,CrossingLayout.HalfX);
   Assert.LessOrEqual(g.OriginZ,-CrossingLayout.HalfZ);Assert.GreaterOrEqual(g.OriginZ+(g.SizeZ-1)*g.Cell,CrossingLayout.HalfZ);
   Assert.AreEqual(2,g.Layers,"the bridge and the courtyard under it");
   var s=new PlaytestSession(1,TempSettings());s.StartSolo("a",map:GameMaps.Crossing);
   Assert.IsTrue(s.Observe().Players.All(p=>p.Position.Z<-20&&Mathf.Abs(p.Position.X)<10),"everyone starts in the south plaza");
  }

  [UnityTest] public IEnumerator CrossingBuildsWithClearPlazaSpawns() {
   var root=new GameObject("crossing spawns");
   try {
    var game=Crossing(root);yield return null;
    Assert.AreEqual(GameMaps.Crossing,game.BuiltMap);Assert.IsNotNull(root.transform.Find(CrossingMap.RootName));Assert.IsNull(root.transform.Find("Village"));
    Assert.AreEqual(3,game.ShelterPoints.Count);
    for(int i=0;i<50;i++)game.Step(.02f);
    foreach(var p in game.Observe().Players) {
     var at=V(p.Position);
     Assert.AreEqual(0,at.y,.15f,$"Slot {p.Slot} stands on the ground: {at}");
     Assert.IsFalse(Physics.OverlapCapsule(at+Vector3.up*.45f,at+Vector3.up*1.4f,.3f).Any(c=>!(c is CharacterController)),$"Slot {p.Slot} spawns clear of geometry");
    }
    Assert.AreEqual(12,game.ObserveProps().Length,"light crates are pushable props");
    foreach(var c in CrossingMap.CoverPositions())Assert.IsFalse(CrossingLayout.Blocks.Any(b=>b.Contains(new Vector2(c.x,c.z))),"cover stands in a street: "+c);
   } finally {Object.Destroy(root);}
   yield return null;
  }

  [UnityTest] public IEnumerator ThreeLanesLeadNorthAndEverySiteIsReachable() {
   var root=new GameObject("crossing lanes");
   try {
    var game=Crossing(root);yield return null;
    var spawn=new Vector3(0,0,-28);
    foreach(var shelter in game.ShelterPoints){var path=game.PlanBotRoute(spawn,shelter,out bool ok);Assert.IsTrue(ok,"reachable: "+shelter);Assert.IsFalse(Drops(path,spawn),"no jump needed to reach "+shelter);}
    // Each lane on its own joins the plaza to the courtyard.
    foreach(var (lane,from,to) in new[]{(CrossingLayout.LongLane,new Vector3(-46,0,-19),new Vector3(-46,0,19)),(CrossingLayout.MiddleAlley,new Vector3(2,0,-19),new Vector3(14,0,19)),(CrossingLayout.Passage,new Vector3(40,0,-19),new Vector3(40,0,19))}) {
     var path=game.PlanBotRoute(from,to,out bool ok);Assert.IsTrue(ok,"lane open: "+from);
     Assert.IsTrue(path.All(p=>In(lane,p,.5f)),"the lane is one corridor, not a way round: "+Show(path));
    }
    // A held tower stair: the route round it climbs a site and crosses the bridge.
    var avoid=CrossingLayout.TowerStair+new Vector3(2,0,0);
    var around=game.PlanBotRoute(new Vector3(14,0,21),CrossingLayout.ShelterTower,out bool reached,false,avoid);Assert.IsTrue(reached);
    Assert.IsFalse(around.Any(p=>p.x>5&&p.x<12&&Mathf.Abs(p.z-26)<1.3f&&p.y>.5f&&p.y<3),"not up the held stair: "+Show(around));
    Assert.IsTrue(around.Any(p=>CrossingLayout.OnBridge(p)),"over the bridge instead: "+Show(around));
   } finally {Object.Destroy(root);}
   yield return null;
  }

  [UnityTest] public IEnumerator BridgeAndCourtyardAreBothWalkableAndGapsDropBetweenThem() {
   var root=new GameObject("crossing bridge");
   try {
    var game=Crossing(root);yield return null;
    var high=game.PlanBotRoute(CrossingLayout.ShelterA,CrossingLayout.ShelterB,out bool across);Assert.IsTrue(across);
    Assert.IsTrue(high.All(p=>p.y>CrossingLayout.Upper-.3f),"site to site stays on the bridge: "+Show(high));
    var under=game.PlanBotRoute(new Vector3(-12,0,27),new Vector3(-12,0,35),out bool below);Assert.IsTrue(below);
    Assert.IsTrue(under.All(p=>p.y<.5f),"the courtyard passes under the bridge: "+Show(under));
    // Off the bridge: the shortest way down drops through a parapet gap, never through the deck or over a parapet.
    var from=new Vector3(-12,CrossingLayout.Upper,CrossingLayout.BridgeZ);
    var down=game.PlanBotRoute(from,new Vector3(-12,0,24),out bool fell,true);Assert.IsTrue(fell);
    var last=from;bool gap=false;
    foreach(var p in down){if(last.y>3&&p.y<1){Assert.IsTrue(CrossingLayout.BridgeGaps.Any(g=>Mathf.Abs(last.x-g.x)<2.2f&&Mathf.Abs(last.z-g.z)<1.8f),"drops only at a gap: "+last+" "+Show(down));gap=true;}last=p;}
    Assert.IsTrue(gap,"the route jumps off: "+Show(down));
   } finally {Object.Destroy(root);}
   yield return null;
  }

  static Vector3 Walk(UnityPlaytestSession game,int slot,List<Vector3> path,int maxSteps) {
   int cursor=0;
   for(int i=0;i<maxSteps&&cursor<path.Count;i++) {
    var p=V(game.Observe().Players[slot].Position);var d=path[cursor]-p;d.y=0;
    if(d.magnitude<.4f){cursor++;continue;}
    game.SubmitInput(slot,new PlayerInput{Forward=1,Yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg});game.Step(.02f);
   }
   game.SubmitInput(slot,new PlayerInput());for(int i=0;i<20;i++)game.Step(.02f);
   return V(game.Observe().Players[slot].Position);
  }

  [UnityTest] public IEnumerator HumanClimbsASiteCrossesTheBridgeAndFallsThroughAGap() {
   var root=new GameObject("crossing walk");
   try {
    var game=Crossing(root);yield return null;
    var start=V(game.Observe().Players[0].Position);
    var up=Walk(game,0,game.PlanBotRoute(start,CrossingLayout.ShelterA,out _),4000);
    Assert.Greater(up.y,CrossingLayout.Upper-.2f,"the long lane stair carries a body onto site A: "+up);
    var east=Walk(game,0,game.PlanBotRoute(up,CrossingLayout.ShelterB,out _),4000);
    Assert.Greater(east.y,CrossingLayout.Upper-.2f,"across the bridge to site B: "+east);Assert.Greater(east.x,30);
    // The parapet holds a body walking into it.
    var mid=Walk(game,0,game.PlanBotRoute(east,new Vector3(10,CrossingLayout.Upper,CrossingLayout.BridgeZ),out _),2000);
    for(int i=0;i<80;i++){game.SubmitInput(0,new PlayerInput{Forward=1,Yaw=180});game.Step(.02f);}
    Assert.Greater(game.Observe().Players[0].Position.Y,CrossingLayout.Upper-.2f,"walking into the parapet does not fall off: "+mid);
    // Through the gap at x 16: a real fall into the courtyard.
    var edge=Walk(game,0,game.PlanBotRoute(V(game.Observe().Players[0].Position),new Vector3(16,CrossingLayout.Upper,31.2f),out _),2000);float lowest=10;
    Assert.Greater(edge.y,CrossingLayout.Upper-.2f,"at the gap, still on the bridge: "+edge);
    for(int i=0;i<60;i++){game.SubmitInput(0,new PlayerInput{Forward=1,Yaw=180});game.Step(.02f);}
    game.SubmitInput(0,new PlayerInput());for(int i=0;i<60;i++){game.Step(.02f);lowest=Mathf.Min(lowest,game.Observe().Players[0].Position.Y);}
    Assert.Less(lowest,.3f,"dropped through the gap");
    var lane=Walk(game,0,game.PlanBotRoute(V(game.Observe().Players[0].Position),new Vector3(40,0,0),out _),4000);
    Assert.Less(Vector3.Distance(lane,new Vector3(40,0,0)),1.5f,"and runs off down the roofed passage: "+lane);
   } finally {Object.Destroy(root);}
   yield return null;
  }

  // With the owned Synty assets configured the generated dressing replaces every Blender module; colliders do not change.
  [UnityTest] public IEnumerator OwnedDressingReplacesTheBlenderModules() {
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
   Assume.That(catalog!=null&&catalog.Crossing!=null,"owned assets not configured in this checkout");
   var root=new GameObject("crossing dressed");
   try {
    var game=Crossing(root);yield return null;
    var map=root.transform.Find(CrossingMap.RootName);Assert.IsNotNull(map.Find(CrossingMap.DressingName),"the Synty dressing is placed");
    Assert.IsFalse(map.GetComponentsInChildren<Renderer>().Any(r=>r.name.StartsWith("SD_")),"no Blender module is drawn under it");
    Assert.IsTrue(map.GetComponentsInChildren<MeshFilter>().Any(f=>f.name.StartsWith("Crossing ")),"static scenery is merged into chunk meshes");
    Assert.IsFalse(map.Find(CrossingMap.DressingName).GetComponentsInChildren<Collider>().Any(),"the dressing is visual only");
    Assert.AreEqual(12,game.ObserveProps().Length);
   } finally {Object.Destroy(root);}
   yield return null;
  }

  // Humans run north during preparation; the first animals then leave from the south plaza and must come up the lanes.
  [UnityTest] public IEnumerator FirstAnimalsStartInThePlazaWhileHumansStayPut() {
   var root=new GameObject("crossing release");
   try {
    var game=Crossing(root);yield return null;
    game.Session.BeginSettingsEdit(0);var edit=game.Session.ObserveSettings().Edit;edit.PreparationSeconds=2;edit.InitialAnimals=2;
    game.Session.UpdateSettingsEdit(0,edit);Assert.IsTrue(game.Session.ApplySettingsNow(0));
    // Everyone walks off north a little first.
    for(int i=0;i<12;i++)game.SubmitInput(i,new PlayerInput{Forward=1,Yaw=0});
    var before=game.Observe();int guard=0;
    while(game.Observe().Phase==RoundPhase.Preparation&&guard++<400){before=game.Observe();for(int i=0;i<12;i++)game.SubmitInput(i,new PlayerInput{Forward=1,Yaw=0,RoundId=before.Round});game.Step(.02f);}
    var after=game.Observe();Assert.AreEqual(RoundPhase.Chase,after.Phase);
    var animals=after.Players.Where(p=>p.Faction==Faction.Animal).ToArray();Assert.AreEqual(2,animals.Length);
    foreach(var a in animals){var body=game.PlayerTransform(a.Slot).position;Assert.Less(body.z,-31,"the animal body starts in the plaza's south row: "+body);Assert.Less(Mathf.Abs(V(a.Position).z-body.z),.5f,"session and body agree");}
    foreach(var h in after.Players.Where(p=>p.Faction==Faction.Human))Assert.Less(Vector3.Distance(V(h.Position),V(before.Players[h.Slot].Position)),.5f,"humans are not moved");
    var transform=after.Events.Where(e=>e.Kind==FeelEventKind.Transform).ToArray();Assert.AreEqual(2,transform.Length);Assert.IsTrue(transform.All(e=>e.Position.Z<-31),"the transformation shows where the animal starts");
    Assert.IsFalse(GameMaps.AnimalsStartApart(GameMaps.Town));Assert.IsFalse(GameMaps.AnimalsStartApart(GameMaps.Warehouse));
   } finally {Object.Destroy(root);}
   yield return null;
  }

  [UnityTest] public IEnumerator HostSwitchesFromTheCrossingBackToTheVillage() {
   var root=new GameObject("crossing switch");
   try {
    var game=Crossing(root);yield return null;
    game.Session.BeginSettingsEdit(0);var edit=game.Session.ObserveSettings().Edit;edit.Map=GameMaps.Town;edit.PreparationSeconds=1;edit.RoundSeconds=1;edit.ResultSeconds=1;
    game.Session.UpdateSettingsEdit(0,edit);Assert.IsTrue(game.Session.ApplySettingsNow(0));
    int round=game.Observe().Round;for(int i=0;i<300&&game.Observe().Round==round;i++)game.Step(.02f);
    Assert.AreEqual(GameMaps.Town,game.BuiltMap);yield return null;
    Assert.IsNull(root.transform.Find(CrossingMap.RootName),"the crossing is gone");Assert.IsNotNull(root.transform.Find("Village"));
    // Bots plan on the village grid again.
    Assert.IsTrue(game.PlanBotRoute(new Vector3(-12,0,-6),new Vector3(12,0,0),out bool ok).Count>0&&ok);
   } finally {Object.Destroy(root);}
   yield return null;
  }
 }
}
