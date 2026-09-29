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
    var start=game.Observe();bool fired=false,reloaded=false,transformed=false;
    for(int i=0;i<600;i++){game.Step(.02f);if(i%50==0)yield return null;}
    Assert.Greater(game.Observe().Players.Count(p=>p.IsBot && Vector3.Distance(V(p.Position),V(start.Players[p.Slot].Position))>6),8,"Bots must leave spawn toward shelters: "+string.Join(";",game.Observe().Players.Select(p=>$"{p.Slot}:{V(p.Position)}")));
    for(int i=0;i<5000;i++) {
     game.Step(.02f);var state=game.Observe();
     fired|=game.ObserveBubbles().Length>0;reloaded|=state.Players.Any(p=>p.ReloadRemaining>0);
     transformed|=state.Players.Count(p=>p.Faction==Faction.Animal)>2;
     if(i%50==0)yield return null;
    }
    Assert.IsTrue(fired,"Human bots fire real bubbles");Assert.IsTrue(reloaded,"Human bots reload through player input");
    Assert.IsTrue(transformed,"Animal bots reach and hit humans using physical melee");
   } finally {Object.Destroy(root);}
   yield return null;
  }
  static Vector3 V(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);
 }
}
