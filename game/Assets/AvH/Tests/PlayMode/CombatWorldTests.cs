using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class CombatWorldTests {
  [UnityTest] public IEnumerator BubbleConsumesAmmoTravelsHitsAndPushesWithoutChangingFaction() {
   var root=new GameObject("combat");var session=root.AddComponent<UnityPlaytestSession>();session.AutomaticStep=false;session.StartSolo("tester",123);
   yield return null;
   var before=session.Observe().Players[4];
   session.SubmitInput(0,new PlayerInput{Attack=true,Yaw=0});session.Step(.02f);
   Assert.AreEqual(11,session.Observe().Players[0].Ammo);Assert.AreEqual(1,session.ObserveBubbles().Length);
   session.SubmitInput(0,new PlayerInput());
   for(int i=0;i<30;i++)session.Step(.02f);
   Assert.Greater(session.Observe().Players[4].Position.Z,before.Position.Z+.3f);
   Assert.AreEqual(Faction.Human,session.Observe().Players[4].Faction);Assert.AreEqual(0,session.ObserveBubbles().Length);
   Object.Destroy(root);yield return null;
  }
 }
}
