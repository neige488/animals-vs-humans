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
    float turning=0,travelled=0;var approach=new List<float>();
    for(int slot=1;slot<12;slot++) {
     var points=track[slot];var rest=points[points.Count-1];
     for(int i=2;i<points.Count;i++) {
      var a=points[i-1]-points[i-2];var b=points[i]-points[i-1];a.y=b.y=0;
      // Cruising headings only: a stop, a start or a shove is not a zigzag.
      if(a.magnitude>top*.1f*.6f&&b.magnitude>top*.1f*.6f){turning+=Vector3.Angle(a,b);travelled+=b.magnitude;}
     }
     // Speed while closing the last stretch to where this bot finally stands.
     bool settled=points.Skip(points.Count-20).All(p=>Vector3.Distance(p,rest)<.3f);
     if(!settled||game.ShelterPoints.All(s=>Vector3.Distance(s,rest)>3))continue;
     for(int i=1;i<points.Count;i++){float d=Vector3.Distance(points[i],rest);if(d>.6f&&d<1.4f){var delta=points[i]-points[i-1];delta.y=0;approach.Add(delta.magnitude/.1f);}}
    }
    float perMetre=turning/Mathf.Max(1,travelled),arrival=approach.Count==0?float.NaN:approach.Average();
    Debug.Log($"BOT_MOTION turningDegreesPerMetre={perMetre:F1} travelled={travelled:F0} approachSpeed={arrival:F2} samples={approach.Count} top={top}");
    Assert.Greater(travelled,100,"Bots actually walked to their shelters");
    Assert.Less(perMetre,6,"Smoothed paths: no repeated 45-degree zigzag between grid cells");
    Assert.Greater(approach.Count,10);Assert.Less(arrival,top*.75f,"Bots ease into where they stop instead of braking at full speed");
   } finally {Object.Destroy(root);}
   yield return null;
  }
 }
}
