using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 /// <summary>Sky islands, snowy hill village and forest ruins: built from their plans, reachable, and the sky fall rule.</summary>
 public class PlannedMapTests {
  static string TempSettings()=>System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml");
  static Vector3 V(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);
  static UnityPlaytestSession World(GameObject root,int map) {
   var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.BotAutomationEnabled=false;
   game.StartSolo("계획",41,TempSettings(),map);return game;
  }
  static bool Drops(List<Vector3> path,Vector3 from){var last=from;foreach(var p in path){if(last.y-p.y>.6f)return true;last=p;}return false;}
  static IEnumerable<int> Maps=>new[]{GameMaps.Sky,GameMaps.Snow,GameMaps.Ruins};

  [Test] public void SixMapsWithNamesAndRules() {
   Assert.AreEqual(6,GameMaps.Count);
   Assert.AreEqual("하늘 섬",GameMaps.Name(GameMaps.Sky));Assert.AreEqual("눈 덮인 산골",GameMaps.Name(GameMaps.Snow));Assert.AreEqual("숲속 성터",GameMaps.Name(GameMaps.Ruins));
   Assert.IsTrue(GameMaps.FallTurnsAnimal(GameMaps.Sky));Assert.IsFalse(Maps.Concat(new[]{GameMaps.Town,GameMaps.Warehouse,GameMaps.Crossing}).Any(m=>m!=GameMaps.Sky&&GameMaps.FallTurnsAnimal(m)));
   foreach(var m in Maps){Assert.IsTrue(GameMaps.AnimalsStartApart(m));Assert.IsNotNull(MapPlans.For(m));Assert.AreEqual(3,MapPlans.For(m).Shelters.Length);}
  }

  [UnityTest] public IEnumerator EachMapBuildsWithClearSpawnsAndReachableShelters() {
   foreach(var map in Maps) {
    var root=new GameObject("planned "+map);
    try {
     var game=World(root,map);yield return null;
     var plan=MapPlans.For(map);
     Assert.AreEqual(map,game.BuiltMap);Assert.IsNotNull(root.transform.Find(plan.Root));
     for(int i=0;i<50;i++)game.Step(.02f);
     foreach(var p in game.Observe().Players) {
      var at=V(p.Position);Assert.AreEqual(0,at.y,.2f,$"{plan.Root}: slot {p.Slot} stands on the ground: {at}");
      Assert.IsFalse(Physics.OverlapCapsule(at+Vector3.up*.45f,at+Vector3.up*1.4f,.3f).Any(c=>!(c is CharacterController)),$"{plan.Root}: slot {p.Slot} spawns clear");
     }
     Assert.AreEqual(plan.LightCrates.Count,game.ObserveProps().Length,plan.Root+" light crates");
     var catalog=Resources.Load<OwnedAssetCatalog>("OwnedAssetCatalog");
     if(catalog!=null&&catalog.Dressing(map)!=null){var dressing=root.transform.Find(plan.Root).Find(PlannedMap.DressingName);Assert.IsNotNull(dressing,plan.Root+" is dressed");Assert.IsFalse(dressing.GetComponentsInChildren<Collider>().Any(c=>c.gameObject.layer!=Layers.CameraOnly),plan.Root+" dressing only blocks the camera");}
     var spawn=V(game.Observe().Players[0].Position);var animal=V(GameMaps.AnimalStart(map,0));animal.y=0;
     foreach(var shelter in game.ShelterPoints) {
      var path=game.PlanBotRoute(spawn,shelter,out bool ok);Assert.IsTrue(ok,$"{plan.Root}: shelter {shelter} reachable from the spawn");Assert.IsFalse(Drops(path,spawn),$"{plan.Root}: no jump needed to reach {shelter}");
      game.PlanBotRoute(animal,shelter,out bool hunt);Assert.IsTrue(hunt,$"{plan.Root}: animals reach {shelter} from their start");
     }
    } finally {Object.Destroy(root);}
    yield return null;
   }
  }

  static Vector3 Walk(UnityPlaytestSession game,int slot,List<Vector3> path,int maxSteps) {
   int cursor=0;
   for(int i=0;i<maxSteps&&cursor<path.Count;i++) {
    var p=V(game.Observe().Players[slot].Position);var d=path[cursor]-p;d.y=0;
    if(d.magnitude<.4f){cursor++;continue;}
    game.SubmitInput(slot,new PlayerInput{Forward=1,Yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,RoundId=game.Observe().Round});game.Step(.02f);
   }
   game.SubmitInput(slot,new PlayerInput());for(int i=0;i<20;i++)game.Step(.02f);
   return V(game.Observe().Players[slot].Position);
  }

  [UnityTest] public IEnumerator SnowTerracesClimbToTheSummitOnFoot() {
   var root=new GameObject("snow climb");
   try {
    var game=World(root,GameMaps.Snow);yield return null;
    var top=Walk(game,0,game.PlanBotRoute(V(game.Observe().Players[0].Position),MapPlans.Snow.Shelters[0],out _),6000);
    Assert.Greater(top.y,8.7f,"three terraces up to the summit: "+top);
   } finally {Object.Destroy(root);}
   yield return null;
  }

  static void StartChase(UnityPlaytestSession game) {
   game.Session.BeginSettingsEdit(0);var edit=game.Session.ObserveSettings().Edit;edit.PreparationSeconds=1;edit.InitialAnimals=1;
   game.Session.UpdateSettingsEdit(0,edit);Assert.IsTrue(game.Session.ApplySettingsNow(0));
   for(int i=0;i<200&&game.Observe().Phase!=RoundPhase.Chase;i++)game.Step(.02f);
   Assert.AreEqual(RoundPhase.Chase,game.Observe().Phase);
  }

  // Off the narrow bridge to the animals' island: a shoved human falls, turns animal and restarts on that island, credited to the shover.
  [UnityTest] public IEnumerator SkyFallTurnsAHumanIntoAnAnimalCreditedToTheShover() {
   var root=new GameObject("sky fall");
   try {
    var game=World(root,GameMaps.Sky);yield return null;StartChase(game);
    int human=game.Observe().Players.First(p=>p.Faction==Faction.Human&&p.Slot!=0).Slot;
    Assert.IsTrue(game.Session.RecordFriendlyShove(human,0),"a friend's cork shoves them");
    // Walk the shoved human off the side of the north bridge.
    var body=game.PlayerTransform(human).GetComponent<CharacterController>();body.enabled=false;body.transform.position=new Vector3(0,.1f,24);body.enabled=true;
    FeelEvent fell=null;
    for(int i=0;i<200&&fell==null;i++){game.SubmitInput(human,new PlayerInput{Forward=1,Yaw=90,RoundId=game.Observe().Round});game.Step(.02f);fell=game.Observe().Events.FirstOrDefault(e=>e.Kind==FeelEventKind.FellOff);}
    Assert.IsNotNull(fell,"the fall is judged");Assert.AreEqual(human,fell.Target);Assert.AreEqual(0,fell.Actor,"credited to the shover");
    var after=game.Observe().Players[human];Assert.AreEqual(Faction.Animal,after.Faction,"a fallen human is an animal");
    for(int i=0;i<10;i++)game.Step(.02f);
    var at=game.PlayerTransform(human).position;Assert.Greater(at.z,32,"restarts on the animals' island: "+at);Assert.Greater(at.y,-1);
    StringAssert.Contains("추락",HitFeed.Fell("준호","민지",ToolKind.Glove));
    // An animal that falls just restarts on its island, still an animal.
    var animalBody=game.PlayerTransform(human).GetComponent<CharacterController>();animalBody.enabled=false;animalBody.transform.position=new Vector3(20,-20,0);animalBody.enabled=true;
    game.Step(.02f);Assert.Greater(game.PlayerTransform(human).position.z,32);Assert.AreEqual(Faction.Animal,game.Observe().Players[human].Faction);
   } finally {Object.Destroy(root);}
   yield return null;
  }

  // Bots walk the narrow sky bridges without stepping off on their own: every fall in a bot-only chase is someone's shove.
  [UnityTest] public IEnumerator SkyBotsDoNotWalkOffTheBridgesOnTheirOwn() {
   var root=new GameObject("sky bots");
   try {
    var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;
    game.StartSolo("관찰",7,TempSettings(),GameMaps.Sky);yield return null;
    int unshoved=0,lastId=0;var fallers=new List<string>();
    for(int i=0;i<6000;i++) {
     game.Step(.02f);if(i%50==0)yield return null;
     foreach(var e in game.Observe().Events.Where(e=>e.Id>lastId)){lastId=e.Id;if(e.Kind==FeelEventKind.FellOff&&e.Actor<0){unshoved++;fallers.Add($"slot {e.Target} @ {V(e.Position)}");}}
     if(game.Observe().Phase==RoundPhase.Results)break;
    }
    Assert.AreEqual(0,unshoved,"humans fell without a shove: "+string.Join(", ",fallers));
   } finally {Object.Destroy(root);}
   yield return null;
  }

  [UnityTest] public IEnumerator FallsElsewhereOnlyReturnTheBody() {
   var root=new GameObject("ruins fall");
   try {
    var game=World(root,GameMaps.Ruins);yield return null;StartChase(game);
    int human=game.Observe().Players.First(p=>p.Faction==Faction.Human).Slot;
    var body=game.PlayerTransform(human).GetComponent<CharacterController>();body.enabled=false;body.transform.position=new Vector3(10,-1,-5);body.enabled=true;
    for(int i=0;i<150;i++)game.Step(.02f);
    Assert.AreEqual(Faction.Human,game.Observe().Players[human].Faction,"the chasm is not the sky");
    Assert.Greater(game.PlayerTransform(human).position.y,-1,"back on a recovery point");
    // On the sky islands a fall during preparation only returns the human to the spawn island.
    var sky=new GameObject("sky prep");
    try {
     var g=World(sky,GameMaps.Sky);yield return null;
     var b=g.PlayerTransform(3).GetComponent<CharacterController>();b.enabled=false;b.transform.position=new Vector3(20,-20,-20);b.enabled=true;g.Step(.02f);
     Assert.AreEqual(Faction.Human,g.Observe().Players[3].Faction);Assert.Less(g.PlayerTransform(3).position.z,-26,"back on the spawn island");
    } finally {Object.Destroy(sky);}
   } finally {Object.Destroy(root);}
   yield return null;
  }
 }
}
