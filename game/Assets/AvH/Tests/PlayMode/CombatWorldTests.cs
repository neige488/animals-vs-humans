using System.Collections;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class CombatWorldTests {
  readonly System.Collections.Generic.List<string> profiles=new System.Collections.Generic.List<string>();
  string Profile(){var path=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+".xml");profiles.Add(path);return path;}
  [TearDown] public void CleanupProfiles(){foreach(var p in profiles){if(File.Exists(p))File.Delete(p);if(File.Exists(p+".bak"))File.Delete(p+".bak");}profiles.Clear();}
  [UnityTest] public IEnumerator BubbleAimUsesInputWhileBodyIsStillTurning() {
   var root=new GameObject("aim during turn");var session=root.AddComponent<UnityPlaytestSession>();session.AutomaticStep=false;session.BotAutomationEnabled=false;session.StartSolo("aim",123,Profile());yield return null;
   session.SubmitInput(0,new PlayerInput{Forward=1,Yaw=123,Attack=true});session.Step(.02f);
   Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(session.PlayerTransform(0).eulerAngles.y,123)),90);
   var direction=session.ObserveBubbles().Single().Direction;var expected=Quaternion.Euler(0,123,0)*Vector3.forward;
   Assert.Less(Vector3.Distance(new Vector3(direction.X,direction.Y,direction.Z),expected),.001f);
   Object.Destroy(root);yield return null;
  }
  [UnityTest] public IEnumerator BubblesBurstOnTerrainAndExpireByRangeOrLifetime() {
   for(int mode=0;mode<3;mode++) {
    var root=new GameObject("bubble limits");var session=root.AddComponent<UnityPlaytestSession>();session.AutomaticStep=false; session.BotAutomationEnabled=false;session.StartSolo("tester",123,Profile());yield return null;
    if(mode>0){session.Session.BeginSettingsEdit(0);var rules=session.Session.ObserveSettings().Edit;if(mode==1)rules.BubbleRange=1;else rules.BubbleLifetime=.05f;session.Session.UpdateSettingsEdit(0,rules);session.Session.ApplySettings(0);session.Step(205);}
    session.SubmitInput(0,new PlayerInput{Attack=true,Yaw=mode==0?180:270});session.Step(.02f);Assert.AreEqual(1,session.ObserveBubbles().Length);session.SubmitInput(0,new PlayerInput());
    for(int i=0;i<(mode==0?25:5);i++)session.Step(.02f);
    Assert.AreEqual(0,session.ObserveBubbles().Length,"Terrain, range and life each retire the projectile");
    Object.Destroy(root);yield return null;
   }
  }

  [UnityTest] public IEnumerator FriendlyBubblePassesThroughWhileBodyCollisionRemainsEnabled() {
   var root=new GameObject("friendly pass");var session=root.AddComponent<UnityPlaytestSession>();session.AutomaticStep=false; session.BotAutomationEnabled=false;
   session.StartSolo("tester",123,Profile());yield return null;
   session.Session.BeginSettingsEdit(0);var rules=session.Session.ObserveSettings().Edit;rules.FriendlyPush=false;
   session.Session.UpdateSettingsEdit(0,rules);session.Session.ApplySettings(0);session.Step(205);
   var before=session.Observe().Players[4].Position;
   session.SubmitInput(0,new PlayerInput{Attack=true});session.Step(.02f);session.SubmitInput(0,new PlayerInput());
   for(int i=0;i<15;i++)session.Step(.02f);
   Assert.AreEqual(before.Z,session.Observe().Players[4].Position.Z,.01f);
   Assert.Greater(session.ObserveBubbles().Single().Position.Z,1);
   Assert.IsFalse(Physics.GetIgnoreCollision(session.PlayerTransform(0).GetComponent<CharacterController>(),session.PlayerTransform(4).GetComponent<CharacterController>()));
   for(int i=0;i<110;i++)session.Step(.02f);Assert.AreEqual(0,session.ObserveBubbles().Length);
   Object.Destroy(root);yield return null;
  }
  [UnityTest] public IEnumerator ContactDoesNotTransformButAnimalAttackDoesAfterGrace() {
   int seed=0;
   for(;seed<1000;seed++){var probe=new PlaytestSession(seed);probe.StartSolo("seed");probe.Advance(20);var s=probe.Observe();if(s.Players[0].Faction==Faction.Animal&&s.Players[4].Faction==Faction.Human)break;}
   var root=new GameObject("melee");var session=root.AddComponent<UnityPlaytestSession>();session.AutomaticStep=false; session.BotAutomationEnabled=false;
   session.StartSolo("tester",seed,Profile());yield return null;
   for(int i=0;i<1001;i++)session.Step(.02f);
   for(int i=0;i<40;i++){session.SubmitInput(0,new PlayerInput{Forward=1,Attack=true});session.Step(.02f);}
   Assert.AreEqual(Faction.Human,session.Observe().Players[4].Faction,"Initial grace prevents attacks even while moving into contact");
   session.SubmitInput(0,new PlayerInput());for(int i=0;i<100;i++)session.Step(.02f);
   Assert.AreEqual(Faction.Human,session.Observe().Players[4].Faction,"Contact alone cannot transform");
   session.SubmitInput(0,new PlayerInput{Forward=-1});session.Step(.1f);
   Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(session.PlayerTransform(0).eulerAngles.y,0)),45,"Body is still facing away from aim");
   session.SubmitInput(0,new PlayerInput{Attack=true,Yaw=0});session.Step(.02f);
   Assert.AreEqual(Faction.Animal,session.Observe().Players[4].Faction);
   Assert.Greater(session.Observe().Players[4].AttackGraceRemaining,0);
   Object.Destroy(root);yield return null;
  }

  [UnityTest] public IEnumerator BubbleConsumesAmmoTravelsHitsAndPushesWithoutChangingFaction() {
   var root=new GameObject("combat");var session=root.AddComponent<UnityPlaytestSession>();session.AutomaticStep=false; session.BotAutomationEnabled=false;session.StartSolo("tester",123,Profile());
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
