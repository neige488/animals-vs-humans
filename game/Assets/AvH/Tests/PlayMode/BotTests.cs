using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class BotTests {
  [UnityTest] public IEnumerator SoloBotsReachSheltersAndCreateCombatWithoutHitOverrides() {
   var root=new GameObject("bot match");
   try {
    var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;
    game.StartSolo("observer",123,System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml"));
    yield return null;
    var start=game.Observe();bool fired=false,reloaded=false,transformed=false,fled=false;
    var threatened=new System.Collections.Generic.Dictionary<int,Vector3>();int observedRound=start.Round;var departure=new System.Collections.Generic.Dictionary<int,int>();var visits=new int[12];int maxVisits=0;var lastShelter=System.Linq.Enumerable.Repeat(-1,12).ToArray();
    for(int i=0;i<600;i++){game.Step(.02f);if(i%50==0)yield return null;}
    Assert.AreEqual(11,game.Observe().Players.Count(p=>p.IsBot && Vector3.Distance(V(p.Position),V(start.Players[p.Slot].Position))>6),"Bots must leave spawn toward shelters: "+string.Join(";",game.Observe().Players.Select(p=>$"{p.Slot}:{V(p.Position)}")));
    var window=game.Observe();var last=window;var travel=new float[12];var productive=new bool[12];int windowSteps=0;
    for(int i=0;i<10000;i++) {
     game.Step(.02f);var state=game.Observe();
     if(state.Round!=observedRound){threatened.Clear();departure.Clear();System.Array.Clear(visits,0,12);for(int slot=0;slot<12;slot++)lastShelter[slot]=-1;observedRound=state.Round;window=state;last=state;System.Array.Clear(travel,0,12);System.Array.Clear(productive,0,12);windowSteps=0;}
     foreach(var bot in state.Players.Where(p=>p.IsBot)) {
      travel[bot.Slot]+=Vector3.Distance(V(bot.Position),V(last.Players[bot.Slot].Position));
      productive[bot.Slot]|=bot.Ammo<last.Players[bot.Slot].Ammo||bot.ReloadRemaining>0||
       (bot.Faction==Faction.Human&&game.ShelterPoints.Any(point=>Vector3.Distance(point,V(bot.Position))<3))||
       (bot.Faction==Faction.Animal&&state.Players.Any(h=>h.Faction==Faction.Human&&Vector3.Distance(V(h.Position),V(bot.Position))<CombatRules.MeleeDistance+.3f));
     }
     if(++windowSteps>=500) {
      foreach(var bot in state.Players.Where(p=>p.IsBot)) {
       float net=Vector3.Distance(V(bot.Position),V(window.Players[bot.Slot].Position));
       Assert.IsTrue(net>1||productive[bot.Slot],$"Unproductive stop/oscillation slot {bot.Slot}: path={travel[bot.Slot]:F2}, net={net:F2}, role={bot.Faction}, position={V(bot.Position)}, round={state.Round}, phase={state.Phase}, remaining={state.SecondsRemaining:F2}, peers="+string.Join(";",state.Players.Select(p=>$"{p.Slot}:{p.Faction}:{V(p.Position)}")));
      }
      window=state;windowSteps=0;System.Array.Clear(travel,0,12);System.Array.Clear(productive,0,12);
     }
     last=state;
     fired|=game.ObserveBubbles().Length>0;reloaded|=state.Players.Any(p=>p.ReloadRemaining>0);
     transformed|=state.Players.Count(p=>p.Faction==Faction.Animal)>2;
     foreach(var human in state.Players.Where(p=>p.IsBot&&p.Faction==Faction.Human)) {
      int shelter=-1;for(int n=0;n<game.ShelterPoints.Count;n++)if(Vector3.Distance(game.ShelterPoints[n],V(human.Position))<3)shelter=n;
      if(shelter>=0&&lastShelter[human.Slot]!=shelter){if(lastShelter[human.Slot]>=0)visits[human.Slot]++;lastShelter[human.Slot]=shelter;}
      maxVisits=System.Math.Max(maxVisits,visits[human.Slot]);
      Assert.LessOrEqual(visits[human.Slot],6,"Repeated shelter oscillation slot "+human.Slot);
      if(shelter>=0&&!departure.ContainsKey(human.Slot)&&state.Players.Any(a=>a.Faction==Faction.Animal&&Vector3.Distance(V(a.Position),V(human.Position))<4))departure[human.Slot]=shelter;
      if(departure.TryGetValue(human.Slot,out int origin))fled|=state.Phase==RoundPhase.Chase&&shelter>=0&&shelter!=origin;
     }
     if(i%50==0)yield return null;
    }
    Assert.IsTrue(fired,"Human bots fire real bubbles");Assert.IsTrue(reloaded,"Human bots reload through player input");
    Assert.IsTrue(transformed,"Animal bots reach and hit humans using physical melee");
    Assert.IsTrue(fled,"A threatened human bot must arrive at another shelter before being caught");
    Debug.Log("BOT_OBSERVATION simulatedSeconds=212 allBots=11 stagnantOrUnproductiveOscillation=0 maxShelterChangesPerObservedRound="+maxVisits);
   } finally {Object.Destroy(root);}
   yield return null;
  }
  [UnityTest] public IEnumerator ThreeSheltersStayReachableWhenAnEntranceIsBlockedMidApproach() {
   var root=new GameObject("blocked entrance");
   try {
    var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;
    game.StartSolo("observer",123,System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml"));yield return null;
    double beforeMs=0,afterMs=0;
    for(int i=0;i<75;i++){var clock=System.Diagnostics.Stopwatch.StartNew();game.Step(.02f);beforeMs=System.Math.Max(beforeMs,clock.Elapsed.TotalMilliseconds);}
    var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.SetParent(root.transform);
    blocker.name="Representative blocked corner entrance";blocker.transform.position=new Vector3(13,1.5f,12);blocker.transform.localScale=new Vector3(8,3,.5f);Physics.SyncTransforms();
    for(int i=0;i<800;i++){var clock=System.Diagnostics.Stopwatch.StartNew();game.Step(.02f);afterMs=System.Math.Max(afterMs,clock.Elapsed.TotalMilliseconds);if(i%50==0)yield return null;}
    var navigation=game.ObserveBotNavigation();Assert.LessOrEqual(navigation.MaxGridBuildsPerStep,1);
    Debug.Log($"NAV_CPU_STEP_MS beforeMax={beforeMs:F3} afterMax={afterMs:F3} builds={navigation.GridBuilds} maxBuildsPerStep={navigation.MaxGridBuildsPerStep} platform={Application.platform}");
    foreach(var shelter in game.ShelterPoints)
     Assert.GreaterOrEqual(game.Observe().Players.Count(p=>p.IsBot&&Vector3.Distance(V(p.Position),shelter)<3),2,
      "Every shelter needs a usable route after blockage: "+shelter+" positions="+string.Join(";",game.Observe().Players.Select(p=>$"{p.Slot}:{V(p.Position)}")));
   } finally {Object.Destroy(root);}
   yield return null;
  }
  [UnityTest] public IEnumerator UnreachableShelterDoesNotLeaveAnyBotFrozen() {
   var root=new GameObject("unreachable shelter");
   try {
    var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;
    game.StartSolo("observer",123,System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml"));yield return null;
    Barrier(root,new Vector3(13.5f,2,12),new Vector3(8,4,1));
    Barrier(root,new Vector3(10,2,15),new Vector3(1,4,7));Physics.SyncTransforms();
    var initial=game.Observe();
    for(int i=0;i<150;i++){game.Step(.02f);if(i%50==0)yield return null;}
    foreach(var bot in game.Observe().Players.Where(p=>p.IsBot))
     Assert.Greater(Vector3.Distance(V(bot.Position),V(initial.Players[bot.Slot].Position)),1,"Blocked target froze slot "+bot.Slot);
   } finally {Object.Destroy(root);}yield return null;
  }
  [UnityTest] public IEnumerator AnimalDetoursAroundNewWallAndPhysicallyHitsHuman() {
   string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml");
   var root=new GameObject("animal blocked pursuit");
   try {
    var profile=new PlaytestSession(1,path);profile.BeginSettingsEdit(0);var rules=profile.ObserveSettings().Edit;rules.InitialAnimals=1;rules.BubbleRange=.1f;
    profile.UpdateSettingsEdit(0,rules);Assert.IsTrue(profile.ApplySettings(0));
    int seed=0;for(;seed<1000;seed++){var probe=new PlaytestSession(seed,path);probe.StartSolo("probe");probe.Advance(20);if(probe.Observe().Players[4].Faction==Faction.Animal)break;}
    Assert.Less(seed,1000);
    var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.BotAutomationEnabled=false;game.StartSolo("target",seed,path);yield return null;
    // Move other humans away through ordinary inputs to isolate this pursuit; no faction or hit injection.
    for(int i=0;i<200;i++){for(int slot=1;slot<12;slot++)if(slot!=4)game.SubmitInput(slot,new PlayerInput{Forward=1});game.Step(.02f);}
    for(int slot=1;slot<12;slot++)game.SubmitInput(slot,new PlayerInput());
    for(int i=0;i<910;i++)game.Step(.02f);
    Assert.AreEqual(Faction.Animal,game.Observe().Players[4].Faction);Assert.AreEqual(Faction.Human,game.Observe().Players[0].Faction);
    game.BotAutomationEnabled=true;game.Step(.02f); // Cache the initially clear route before representative blockage.
    Barrier(root,new Vector3(-4.5f,1.5f,-1.5f),new Vector3(3,3,.5f));Physics.SyncTransforms();
    bool detoured=false,hit=false;var previous=V(game.Observe().Players[4].Position);
    for(int i=0;i<1000;i++) {
     game.Step(.02f);var state=game.Observe();var position=V(state.Players[4].Position);
     Assert.Less(Vector3.Distance(previous,position),1,"Pursuit must use continuous movement, never teleport: "+previous+" -> "+position);previous=position;
     detoured|=Mathf.Abs(position.x+4.5f)>1.8f;
     if(state.Players[0].Faction==Faction.Animal){hit=true;Assert.LessOrEqual(Vector3.Distance(position,V(state.Players[0].Position)),CombatRules.MeleeDistance);break;}
     if(i%50==0)yield return null;
    }
    Assert.IsTrue(detoured,"Animal must go around the wall endpoint");Assert.IsTrue(hit,"Physical melee must reach the target after detour");
   } finally {Object.Destroy(root);if(System.IO.File.Exists(path))System.IO.File.Delete(path);}yield return null;
  }
  [UnityTest] public IEnumerator MapDefinedSheltersHaveStandingClearance() {
   var root=new GameObject("shelter clearance");
   try {
    var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.BotAutomationEnabled=false;
    game.StartSolo("observer",123,System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml"));yield return null;Physics.SyncTransforms();
    foreach(var point in game.ShelterPoints) {
     Assert.IsTrue(Physics.RaycastAll(point+Vector3.up*.2f,Vector3.down,.5f).Any(h=>!(h.collider is CharacterController)&&h.normal.y>.7f),"Shelter needs supporting floor: "+point);
     Assert.IsFalse(Physics.OverlapCapsule(point+Vector3.up*.45f,point+Vector3.up*1.47f,.38f).Any(c=>c.GetComponentInParent<CharacterController>()==null),"Shelter standing capsule blocked: "+point);
    }
   } finally {Object.Destroy(root);}yield return null;
  }
  static void Barrier(GameObject root,Vector3 position,Vector3 size) {
   var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform);wall.transform.position=position;wall.transform.localScale=size;
  }
  static Vector3 V(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);
 }
}
