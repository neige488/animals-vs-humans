using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 /// <summary>Bots of one side take different streets and close in from different sides instead of walking single file.</summary>
 public class BotRouteSpreadTests {
  static string TempSettings()=>System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml");
  static Vector3 V(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);

  /// <summary>Share of route cells next to a teammate's route, and share of moving bot samples spent right behind a teammate heading the same way.</summary>
  struct Spread {public float Shared,Trailing;public override string ToString()=>$"shared {Shared:P0}, trailing {Trailing:P0}";}

  static IEnumerator Run(int map,float penalty,Spread[] result) {
   var keep=BotDirector.SharePenalty;BotDirector.SharePenalty=penalty;
   var root=new GameObject("spread "+map);
   try {
    var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;
    game.StartSolo("관찰",11,TempSettings(),map);yield return null;
    var last=game.Observe().Players.Select(p=>V(p.Position)).ToArray();
    int moving=0,trailing=0;
    for(int i=1;i<=4000;i++) {
     game.Step(.02f);if(i%100==0)yield return null;
     if(i%10!=0)continue;
     var state=game.Observe();if(state.Phase==RoundPhase.Results)break;
     var now=state.Players.Select(p=>V(p.Position)).ToArray();
     foreach(var a in state.Players.Where(p=>p.IsBot)) {
      var step=now[a.Slot]-last[a.Slot];step.y=0;if(step.magnitude<.3f)continue;moving++;var heading=step.normalized;
      bool behind=state.Players.Any(b=>b.Slot!=a.Slot&&b.IsBot&&b.Faction==a.Faction&&Mathf.Abs(now[b.Slot].y-now[a.Slot].y)<1&&Within(now[a.Slot],now[b.Slot],heading,last[b.Slot]));
      if(behind)trailing++;
     }
     last=now;
    }
    var nav=game.ObserveBotNavigation();
    result[0]=new Spread{Shared=nav.PlannedCells==0?0:(float)nav.SharedCells/nav.PlannedCells,Trailing=moving==0?0:(float)trailing/moving};
   } finally {BotDirector.SharePenalty=keep;Object.Destroy(root);}
   yield return null;
  }
  // b is 0.8..4 m ahead of a, nearly on a's line, and moving the same way.
  static bool Within(Vector3 a,Vector3 b,Vector3 heading,Vector3 bBefore) {
   var gap=b-a;gap.y=0;float along=Vector3.Dot(gap,heading);if(along<.8f||along>4)return false;
   if((gap-heading*along).magnitude>1)return false;
   var step=b-bBefore;step.y=0;return step.magnitude>.3f&&Vector3.Dot(step.normalized,heading)>.85f;
  }

  [UnityTest] public IEnumerator TeammatesSpreadOverDifferentRoutes() {
   foreach(var map in new[]{GameMaps.Crossing,GameMaps.Town}) {
    var off=new Spread[1];var on=new Spread[1];
    yield return Run(map,0,off);yield return Run(map,BotDirector.SharePenalty,on);
    Debug.Log($"[route spread] {GameMaps.Name(map)}: off {off[0]} / on {on[0]}");
    Assert.Less(on[0].Shared,off[0].Shared*.8f,$"{GameMaps.Name(map)}: teammates plan round each other's routes ({off[0]} → {on[0]})");
    Assert.Less(on[0].Trailing,.1f,$"{GameMaps.Name(map)}: few bots walk single file ({on[0]})");
    Assert.LessOrEqual(on[0].Trailing,off[0].Trailing,$"{GameMaps.Name(map)}: fewer bots walk in a teammate's footsteps ({off[0]} → {on[0]})");
   }
  }
 }
}
