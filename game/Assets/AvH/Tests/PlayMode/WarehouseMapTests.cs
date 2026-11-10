using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 /// <summary>The two-storey warehouse: modules, collision, spawns, bot routes, the jump-down escape and map switching.</summary>
 public class WarehouseMapTests {
  static string TempSettings()=>System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml");
  static Vector3 V(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);
  static UnityPlaytestSession Warehouse(GameObject root,bool bots=false) {
   var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.BotAutomationEnabled=bots;
   game.StartSolo("창고",321,TempSettings(),GameMaps.Warehouse);return game;
  }
  static bool Drops(List<Vector3> path,Vector3 from){var last=from;foreach(var p in path){if(last.y-p.y>.6f)return true;last=p;}return false;}

  // Each module sits on its own floor-centre origin, so placement in code matches the colliders.
  [Test] public void BlenderModulesStandOnTheirOrigin() {
   foreach(var (name,height) in new[]{("WH_Floor",.01f),("WH_Wall",6f),("WH_Stair",3.6f),("WH_Ramp",3.65f),("WH_Railing",1.03f),("WH_Crate",1f),("WH_RackBlock",3.5f)}) {
    var prefab=Resources.Load<GameObject>("Warehouse/"+name);Assert.IsNotNull(prefab,name);
    var b=new Bounds();bool first=true;
    foreach(var f in prefab.GetComponentsInChildren<MeshFilter>()){var m=f.sharedMesh.bounds;foreach(var c in new[]{m.min,m.max}){var w=prefab.transform.InverseTransformPoint(f.transform.TransformPoint(c));if(first){b=new Bounds(w,Vector3.zero);first=false;}else b.Encapsulate(w);}}
    Assert.AreEqual(height,b.max.y,.1f,name+" top");Assert.AreEqual(0,(b.min.x+b.max.x)/2,.1f,name+" centred on x");
   }
  }
  [Test] public void BlenderStairAndRampRiseTowardPlusZ() {
   foreach(var name in new[]{"WH_Stair","WH_Ramp"}) {
    var prefab=Resources.Load<GameObject>("Warehouse/"+name);Assert.IsNotNull(prefab,name+" is imported from Blender");
    var filter=prefab.GetComponentInChildren<MeshFilter>();var t=filter.transform;
    var vertices=filter.sharedMesh.vertices.Select(v=>prefab.transform.InverseTransformPoint(t.TransformPoint(v))).ToArray();
    float top=vertices.Max(v=>v.y);var high=vertices.Where(v=>v.y>top-.3f).Average(v=>v.z);var low=vertices.Where(v=>v.y<.3f).Average(v=>v.z);
    Assert.Greater(high,low+2,name+" must climb toward +Z so the deck collider and the model agree");
    Assert.AreEqual(WarehouseLayout.Upper,top,.4f,name+" top reaches the deck");
   }
  }

  [UnityTest] public IEnumerator WarehouseBuildsWithClearGroundFloorSpawns() {
   var root=new GameObject("warehouse spawns");
   try {
    var game=Warehouse(root);yield return null;
    Assert.AreEqual(GameMaps.Warehouse,game.BuiltMap);Assert.AreEqual(GameMaps.Warehouse,game.Observe().Map);
    Assert.IsNotNull(root.transform.Find(WarehouseMap.RootName));Assert.IsNull(root.transform.Find("Village"));
    Assert.AreEqual(3,game.ShelterPoints.Count);
    for(int i=0;i<50;i++)game.Step(.02f);
    foreach(var p in game.Observe().Players) {
     var at=V(p.Position);
     Assert.AreEqual(0,at.y,.15f,$"Slot {p.Slot} stands on the ground floor: {at}");
     Assert.Less(Mathf.Abs(at.x),WarehouseLayout.HalfX-1);Assert.Less(at.z,WarehouseLayout.DeckFront-1,"spawn is south of the deck");
     Assert.IsFalse(Physics.OverlapCapsule(at+Vector3.up*.45f,at+Vector3.up*1.4f,.3f).Any(c=>!(c is CharacterController)),$"Slot {p.Slot} spawns clear of geometry");
    }
    Assert.AreEqual(10,game.ObserveProps().Length,"light crates are pushable props");
   } finally {Object.Destroy(root);}
   yield return null;
  }

  [UnityTest] public IEnumerator BotRoutesClimbStairsAndDetourByRampWhenTheStairIsAvoided() {
   var root=new GameObject("warehouse routes");
   try {
    var game=Warehouse(root);yield return null;
    var spawn=new Vector3(-4,0,-3);
    foreach(var shelter in game.ShelterPoints){var path=game.PlanBotRoute(spawn,shelter,out bool ok);Assert.IsTrue(ok,"every shelter reachable: "+shelter);Assert.IsFalse(Drops(path,spawn),"no jump needed to reach "+shelter);}
    var deck=WarehouseLayout.StairShelter;
    var direct=game.PlanBotRoute(spawn,deck,out bool reached);Assert.IsTrue(reached);
    Assert.IsTrue(direct.Any(p=>Mathf.Abs(p.x-WarehouseLayout.StairX)<1.5f&&p.y>1&&p.y<3),"the short way up is the west staircase");
    // A held staircase: an animal marks it and plans round it.
    var detour=game.PlanBotRoute(spawn,deck,out bool around,false,WarehouseLayout.StairFoot+new Vector3(0,0,3));
    Assert.IsTrue(around);
    Assert.IsTrue(detour.Any(p=>Mathf.Abs(p.x-WarehouseLayout.RampX)<2f&&p.y>1&&p.y<3),"blocked stairs send the route up the east ramp");
    Assert.IsFalse(detour.Any(p=>Mathf.Abs(p.x-WarehouseLayout.StairX)<1.2f&&p.y>.5f&&p.y<3),"and not up the stairs");
    // A chased human on the deck: jumping through a railing gap is the way out to the yard.
    var from=new Vector3(-6,WarehouseLayout.Upper,8);
    // The bot grid already lets a body step off a ledge (up to 4.2 m), so the shortest way out jumps through a railing gap.
    var jump=game.PlanBotRoute(from,WarehouseLayout.YardShelter,out bool jumped);Assert.IsTrue(jumped);
    var last=from;bool throughGap=false;
    foreach(var p in jump){if(last.y>3&&p.y<1&&WarehouseLayout.DropGaps.Any(g=>Mathf.Abs(last.x-g)<2))throughGap=true;last=p;}
    Assert.IsTrue(throughGap,"the escape jumps down through a railing gap: "+string.Join(" ",jump.Select(p=>p.ToString("F1"))));
    // Going up there is no shortcut: a jump never climbs, so an animal must take the stair or the ramp.
    var climb=game.PlanBotRoute(new Vector3(-6,0,3),from,out bool climbed);Assert.IsTrue(climbed);
    Assert.IsTrue(climb.Any(p=>p.y>1&&p.y<3&&(Mathf.Abs(p.x-WarehouseLayout.StairX)<1.5f||Mathf.Abs(p.x-WarehouseLayout.RampX)<2)),"climbing uses the stair or the ramp");
   } finally {Object.Destroy(root);}
   yield return null;
  }
  static float Length(Vector3 from,List<Vector3> path){float d=0;var last=from;foreach(var p in path){d+=Vector3.Distance(last,p);last=p;}return d;}

  // Follows planned waypoints with ordinary input; returns the final position.
  static Vector3 Walk(UnityPlaytestSession game,int slot,List<Vector3> path,int maxSteps,bool jumpAtEdges=false) {
   int cursor=0;
   for(int i=0;i<maxSteps&&cursor<path.Count;i++) {
    var p=V(game.Observe().Players[slot].Position);var d=path[cursor]-p;d.y=0;
    if(d.magnitude<.4f){cursor++;continue;}
    float yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;
    game.SubmitInput(slot,new PlayerInput{Forward=1,Yaw=yaw});game.Step(.02f);
   }
   return V(game.Observe().Players[slot].Position);
  }

  [UnityTest] public IEnumerator HumansClimbTheStairJumpDownAGapAndLeaveByADoor() {
   var root=new GameObject("warehouse walk");
   try {
    var game=Warehouse(root);yield return null;
    var start=V(game.Observe().Players[0].Position);
    var up=Walk(game,0,game.PlanBotRoute(start,WarehouseLayout.StairShelter,out _),1500);
    Assert.Greater(up.y,WarehouseLayout.Upper-.2f,"stairs carry a body onto the deck: "+up);
    var east=Walk(game,0,game.PlanBotRoute(up,WarehouseLayout.RampShelter,out _),1500);
    Assert.Greater(east.y,WarehouseLayout.Upper-.2f,"the deck is continuous to the ramp side: "+east);
    var down=Walk(game,0,game.PlanBotRoute(east,WarehouseLayout.RampFoot,out _),1500);
    Assert.Less(down.y,.3f,"the ramp leads down: "+down);
    var back=Walk(game,0,game.PlanBotRoute(down,new Vector3(-6,WarehouseLayout.Upper,8),out _),2500);
    Assert.Greater(back.y,WarehouseLayout.Upper-.2f,"up the ramp again: "+back);
    // Off the edge through the railing gap: a real fall onto the ground floor.
    var gap=new Vector3(WarehouseLayout.DropGaps[0],0,WarehouseLayout.DeckFront-2.5f);float lowest=back.y;
    for(int i=0;i<200;i++){var p=V(game.Observe().Players[0].Position);var d=gap-p;d.y=0;if(d.magnitude<.4f)break;game.SubmitInput(0,new PlayerInput{Forward=1,Yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg});game.Step(.02f);}
    game.SubmitInput(0,new PlayerInput());for(int i=0;i<60;i++){game.Step(.02f);lowest=Mathf.Min(lowest,game.Observe().Players[0].Position.Y);}
    Assert.Less(lowest,.3f,"jumped down through the gap");
    var yard=Walk(game,0,game.PlanBotRoute(V(game.Observe().Players[0].Position),WarehouseLayout.YardShelter,out _),2500);
    Assert.Less(yard.z,-WarehouseLayout.HalfZ-1,"left by a door into the yard: "+yard);
    // The railing itself holds a walking body.
    var rail=new Vector3(0,WarehouseLayout.Upper,8);var onDeck=Walk(game,0,game.PlanBotRoute(yard,rail,out _),3000);
    for(int i=0;i<100;i++){game.SubmitInput(0,new PlayerInput{Forward=1,Yaw=180});game.Step(.02f);}
    Assert.Greater(game.Observe().Players[0].Position.Y,WarehouseLayout.Upper-.2f,"walking into the railing does not fall off: "+onDeck);
   } finally {Object.Destroy(root);}
   yield return null;
  }

  // With the owned Synty assets configured the timber-granary dressing replaces the Blender modules; colliders stay.
  [UnityTest] public IEnumerator OwnedDressingReplacesTheBlenderModules() {
   var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
   Assume.That(catalog!=null&&catalog.Warehouse!=null,"owned assets not configured in this checkout");
   var root=new GameObject("warehouse dressed");
   try {
    var game=Warehouse(root);yield return null;
    var map=root.transform.Find(WarehouseMap.RootName);var dressing=map.Find(WarehouseMap.DressingName);Assert.IsNotNull(dressing);
    Assert.IsFalse(map.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.name.StartsWith("WH_")),"no Blender module is drawn");
    Assert.IsFalse(dressing.GetComponentsInChildren<Collider>().Any(c=>c.gameObject.layer!=Layers.CameraOnly),"the dressing only blocks the camera");
    Assert.AreEqual(10,game.ObserveProps().Length);
    Assert.Greater(dressing.GetComponent<TownDressing>().Observe().LanternLights,0,"hall lanterns are lit");
   } finally {Object.Destroy(root);}
   yield return null;
  }

  [UnityTest] public IEnumerator HostMapChangeTakesEffectAtTheNextRound() {
   var root=new GameObject("map switch");
   try {
    var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.BotAutomationEnabled=false;
    game.StartSolo("전환",5,TempSettings());yield return null;
    Assert.AreEqual(GameMaps.Town,game.BuiltMap);
    game.Session.BeginSettingsEdit(0);var edit=game.Session.ObserveSettings().Edit;edit.Map=GameMaps.Warehouse;edit.PreparationSeconds=1;edit.RoundSeconds=1;edit.ResultSeconds=1;
    // Live debug apply changes the setting at once, but the map only changes when the next round starts.
    game.Session.UpdateSettingsEdit(0,edit);Assert.IsTrue(game.Session.ApplySettingsNow(0));
    int round=game.Observe().Round;game.Step(.02f);
    Assert.AreEqual(GameMaps.Town,game.BuiltMap,"the round in progress keeps its map");Assert.AreEqual(GameMaps.Town,game.Observe().Map);
    for(int i=0;i<300&&game.Observe().Round==round;i++)game.Step(.02f);
    Assert.AreEqual(round+1,game.Observe().Round);
    Assert.AreEqual(GameMaps.Warehouse,game.Observe().Map);Assert.AreEqual(GameMaps.Warehouse,game.BuiltMap);
    yield return null;
    Assert.IsNull(root.transform.Find("Village"),"the village is gone");
    for(int i=0;i<20;i++)game.Step(.02f);
    Assert.IsTrue(game.Observe().Players.All(p=>p.Position.Z<WarehouseLayout.DeckFront&&p.Position.Y<.5f),"everyone restarts on the warehouse ground floor");
   } finally {Object.Destroy(root);}
   yield return null;
  }

  [UnityTest] public IEnumerator CorkShotShovesAFriendAndSaysWhoDidIt() {
   var root=new GameObject("friendly cork");
   try {
    var game=Warehouse(root);yield return null;
    for(int i=0;i<10;i++)game.Step(.02f);
    // Slot 0 and slot 4 stand in a column (x -4.5): 0 at z -6, 4 at z -3. Shoot north.
    var before=V(game.Observe().Players[4].Position);
    game.SubmitInput(0,new PlayerInput{Attack=true,Yaw=0,Pitch=8});game.Step(.02f);game.SubmitInput(0,new PlayerInput{Yaw=0});
    FeelEvent shove=null;
    for(int i=0;i<60&&shove==null;i++){game.Step(.02f);shove=game.Observe().Events.FirstOrDefault(e=>e.Kind==FeelEventKind.Shoved);}
    Assert.IsNotNull(shove,"a friend hit by a cork is a public shove");
    Assert.AreEqual(0,shove.Actor);Assert.AreEqual(4,shove.Target);
    for(int i=0;i<20;i++)game.Step(.02f);
    var after=game.Observe().Players[4];
    Assert.Greater(after.Position.Z-before.z,.5f,"the friend is pushed along the shot");
    Assert.AreEqual(Faction.Human,after.Faction);Assert.AreEqual(ActionPhase.None,after.Action,"no stun for friends");
    Assert.AreEqual("창고의 코르크 → 봇 4 (아군!)",HitFeed.Describe("창고","봇 4","인간",true,ToolKind.Cork));
    var feed=new HitFeed();var state=game.Observe();feed.Observe(new SessionState{Players=state.Players,Events=new FeelEvent[0]},.02f);feed.Observe(new SessionState{Players=state.Players,Events=new[]{shove}},.02f);
    Assert.AreEqual(1,feed.Entries.Count);Assert.IsTrue(feed.Entries[0].Friendly);StringAssert.Contains("아군",feed.Entries[0].Text);
    Assert.IsTrue(root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Cork ricochet"),"the cork pings back off the friend");
   } finally {Object.Destroy(root);}
   yield return null;
  }
 }
}
