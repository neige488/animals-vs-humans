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
    var threatened=new System.Collections.Generic.Dictionary<int,Vector3>();int observedRound=start.Round;
    for(int i=0;i<600;i++){game.Step(.02f);if(i%50==0)yield return null;}
    Assert.Greater(game.Observe().Players.Count(p=>p.IsBot && Vector3.Distance(V(p.Position),V(start.Players[p.Slot].Position))>6),8,"Bots must leave spawn toward shelters: "+string.Join(";",game.Observe().Players.Select(p=>$"{p.Slot}:{V(p.Position)}")));
    for(int i=0;i<5000;i++) {
     game.Step(.02f);var state=game.Observe();
     if(state.Round!=observedRound){threatened.Clear();observedRound=state.Round;}
     fired|=game.ObserveBubbles().Length>0;reloaded|=state.Players.Any(p=>p.ReloadRemaining>0);
     transformed|=state.Players.Count(p=>p.Faction==Faction.Animal)>2;
     foreach(var human in state.Players.Where(p=>p.IsBot&&p.Faction==Faction.Human)) {
      if(!threatened.ContainsKey(human.Slot)&&state.Players.Any(a=>a.Faction==Faction.Animal&&Vector3.Distance(V(a.Position),V(human.Position))<4))threatened[human.Slot]=V(human.Position);
      if(threatened.TryGetValue(human.Slot,out var danger))fled|=state.Phase==RoundPhase.Chase&&Vector3.Distance(danger,V(human.Position))>6;
     }
     if(i%50==0)yield return null;
    }
    Assert.IsTrue(fired,"Human bots fire real bubbles");Assert.IsTrue(reloaded,"Human bots reload through player input");
    Assert.IsTrue(transformed,"Animal bots reach and hit humans using physical melee");
    Assert.IsTrue(fled,"A threatened human bot must relocate before being caught");
   } finally {Object.Destroy(root);}
   yield return null;
  }
  [UnityTest] public IEnumerator ThreeSheltersStayReachableWhenAnEntranceIsBlockedMidApproach() {
   var root=new GameObject("blocked entrance");
   try {
    var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;
    game.StartSolo("observer",123,System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml"));yield return null;
    for(int i=0;i<75;i++)game.Step(.02f);
    var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.SetParent(root.transform);
    blocker.name="Representative blocked corner entrance";blocker.transform.position=new Vector3(13,1.5f,12);blocker.transform.localScale=new Vector3(8,3,.5f);Physics.SyncTransforms();
    for(int i=0;i<800;i++){game.Step(.02f);if(i%50==0)yield return null;}
    foreach(var shelter in new[]{new Vector3(-12,4,10),new Vector3(14,0,15),new Vector3(9,0,-14)})
     Assert.GreaterOrEqual(game.Observe().Players.Count(p=>p.IsBot&&Vector3.Distance(V(p.Position),shelter)<3),2,
      "Every shelter needs a usable route after blockage: "+shelter+" positions="+string.Join(";",game.Observe().Players.Select(p=>$"{p.Slot}:{V(p.Position)}")));
   } finally {Object.Destroy(root);}
   yield return null;
  }
  static Vector3 V(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);
 }
}
