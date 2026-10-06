using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 // Bots move like people: smoothed paths instead of 45-degree grid zigzags, easing into where they stop.
 public class BotMotionTests {
  static Vector3 V(WorldPosition p)=>new Vector3(p.X,p.Y,p.Z);
  // Every human-bot bubble against the exact line to its nearest animal when the bot chose its input.
  static IEnumerator AimErrors(float delay,float error,List<float> errors) {
   var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml");
   var root=new GameObject("bot aim");
   try {
    var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;game.StartSolo("observer",123,path);yield return null;
    game.Session.BeginSettingsEdit(0);var rules=game.Session.ObserveSettings().Edit;rules.BotAimDelaySeconds=delay;rules.BotAimErrorDegrees=error;game.Session.UpdateSettingsEdit(0,rules);Assert.IsTrue(game.Session.ApplySettingsNow(0));
    var seen=new HashSet<int>();
    for(int step=0;step<2500;step++) {
     var before=game.Observe();game.Step(.02f);
     foreach(var bubble in game.ObserveBubbles()) {
      if(!seen.Add(bubble.Id)||step<1000)continue;
      var shooter=before.Players[bubble.OwnerSlot];if(!shooter.IsBot)continue;
      // The bot chose its target after this step's rules update; skip the step where factions just changed.
      var target=before.Players.Where(p=>p.Faction!=shooter.Faction).OrderBy(p=>(V(p.Position)-V(shooter.Position)).sqrMagnitude).FirstOrDefault();
      if(target==null||game.Observe().Players.Count(p=>p.Faction==Faction.Animal)!=before.Players.Count(p=>p.Faction==Faction.Animal))continue;
      errors.Add(Vector3.Angle(V(target.Position)-V(shooter.Position),V(bubble.Direction)));
     }
     if(step%100==0)yield return null;
    }
   } finally {Object.Destroy(root);if(System.IO.File.Exists(path))System.IO.File.Delete(path);}
  }
  [UnityTest] public IEnumerator ZeroBotAimSettingsShootExactlyAndDefaultsAimLikeAPerson() {
   var exact=new List<float>();yield return AimErrors(0,0,exact);
   var human=new List<float>();yield return AimErrors(.2f,3,human);
   Debug.Log($"BOT_AIM zero shots={exact.Count} maxError={exact.DefaultIfEmpty().Max():F3} default shots={human.Count} meanError={human.DefaultIfEmpty().Average():F2} maxError={human.DefaultIfEmpty().Max():F2}");
   Assert.Greater(exact.Count,5);Assert.Greater(human.Count,5,"Default bots still defend with bubbles");
   Assert.Less(exact.Max(),.05f,"0 delay and 0 error is the original exact aim");
   Assert.Greater(human.Average(),.5f,"Default bots aim slightly off and late");
  }
  [UnityTest] public IEnumerator HumanBotsWalkSmoothedPathsAndEaseIntoTheirShelter() {
   var root=new GameObject("bot motion");
   try {
    var game=root.AddComponent<UnityPlaytestSession>();game.AutomaticStep=false;
    game.StartSolo("observer",123,System.IO.Path.Combine(System.IO.Path.GetTempPath(),System.Guid.NewGuid()+".xml"));yield return null;
    float top=game.Session.ObserveSettings().Current.HumanSpeed;
    // Preparation only: every bot is a human heading for its shelter, nobody flees yet.
    var track=new List<Vector3>[12];for(int i=0;i<12;i++)track[i]=new List<Vector3>();
    for(int step=0;step<950;step++){game.Step(.02f);if(step%5==0)foreach(var p in game.Observe().Players)track[p.Slot].Add(V(p.Position));if(step%100==0)yield return null;}
    Assert.AreEqual(RoundPhase.Preparation,game.Observe().Phase);
    float turning=0,travelled=0,zigzag=0;var approach=new List<float>();
    for(int slot=1;slot<12;slot++) {
     var points=track[slot];var rest=points[points.Count-1];
     int lastTurn=-99;float lastSign=0;
     for(int i=2;i<points.Count;i++) {
      var a=points[i-1]-points[i-2];var b=points[i]-points[i-1];a.y=b.y=0;
      // Cruising headings only: a stop, a start or a shove is not a zigzag.
      if(a.magnitude<=top*.1f*.6f||b.magnitude<=top*.1f*.6f)continue;
      float turn=Vector3.SignedAngle(a,b,Vector3.up);travelled+=b.magnitude;turning+=Mathf.Abs(turn);
      // A zigzag turns one way then straight back within a few tenths of a second; a corner keeps turning the same way.
      if(Mathf.Abs(turn)>10){if(i-lastTurn<=4&&Mathf.Sign(turn)!=lastSign)zigzag+=Mathf.Abs(turn);lastTurn=i;lastSign=Mathf.Sign(turn);}
     }
     // Speed while closing the last stretch to where this bot finally stands.
     bool settled=points.Skip(points.Count-20).All(p=>Vector3.Distance(p,rest)<.3f);
     if(!settled||game.ShelterPoints.All(s=>Vector3.Distance(s,rest)>3))continue;
     for(int i=1;i<points.Count;i++){float d=Vector3.Distance(points[i],rest);if(d>.6f&&d<1.4f){var delta=points[i]-points[i-1];delta.y=0;approach.Add(delta.magnitude/.1f);}}
    }
    float perMetre=turning/Mathf.Max(1,travelled),zigzagPerMetre=zigzag/Mathf.Max(1,travelled),arrival=approach.Count==0?float.NaN:approach.Average();
    Debug.Log($"BOT_MOTION zigzagDegreesPerMetre={zigzagPerMetre:F2} turningDegreesPerMetre={perMetre:F1} travelled={travelled:F0} approachSpeed={arrival:F2} samples={approach.Count} top={top}");
    Assert.Greater(travelled,100,"Bots actually walked to their shelters");
    // Grid-cell following measured 4.2 deg/m; what remains after smoothing is jostling at spawn and shelter doors.
    Assert.Less(zigzagPerMetre,2,"Smoothed paths: no back-and-forth 45-degree zigzag between grid cells");
    Assert.Greater(approach.Count,10);Assert.Less(arrival,top*.75f,"Bots ease into where they stop instead of braking at full speed");
   } finally {Object.Destroy(root);}
   yield return null;
  }
 }
}
