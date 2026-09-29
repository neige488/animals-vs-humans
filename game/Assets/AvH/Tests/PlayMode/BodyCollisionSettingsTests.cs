using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace AvH.Tests {
 public class BodyCollisionSettingsTests {
  [UnityTest] public IEnumerator FriendlyBodiesBlockOrPassAccordingToTheirOwnSwitch() {
   yield return Exercise(false,false);yield return Exercise(false,true);
  }
  [UnityTest] public IEnumerator EnemyBodiesBlockOrPassAndResetToFriendlyRulesNextRound() {
   yield return Exercise(true,false);yield return Exercise(true,true);
  }
  static IEnumerator Exercise(bool enemy,bool enabled) {
   string path=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+".xml");
   var root=new GameObject("Body collision settings");
   try {
    // Save through the public settings boundary, then load the same profile as gameplay.
    var profile=new PlaytestSession(1,path);profile.StartSolo("host");profile.BeginSettingsEdit(0);
    var rules=profile.ObserveSettings().Edit;
    rules.FriendlyCollision=enemy?!enabled:enabled;
    rules.EnemyCollision=enemy?enabled:!enabled;
    profile.UpdateSettingsEdit(0,rules);Assert.That(profile.ApplySettings(0),Is.True);
    int seed=0;
    for(;seed<1000;seed++) {
     var probe=new PlaytestSession(seed);probe.StartSolo("seed");probe.Advance(20);
     if(probe.Observe().Players[0].Faction==Faction.Animal&&probe.Observe().Players[4].Faction==Faction.Human)break;
    }
    Assert.That(seed,Is.LessThan(1000));
    var world=root.AddComponent<UnityPlaytestSession>();world.AutomaticStep=false;world.BotAutomationEnabled=false;
    world.StartSolo("host",seed,path);yield return null;
    if(enemy)world.Step(20);
    Assert.That(world.Observe().Players[0].Faction,Is.EqualTo(enemy?Faction.Animal:Faction.Human));
    Assert.That(world.Observe().Players[4].Faction,Is.EqualTo(Faction.Human));
    DriveIntoStationaryPlayer(world,enabled);
    // The next round restores human factions and positions. The friendly switch must now win,
    // even if this exact pair previously had the opposite enemy-collision rule.
    world.SubmitInput(0,new PlayerInput());
    var state=world.Observe();world.Step((float)state.SecondsRemaining+(state.Phase==RoundPhase.Preparation?180:0)+5);
    Assert.That(world.Observe().Round,Is.EqualTo(2));
    Assert.That(world.Observe().Players[0].Faction,Is.EqualTo(Faction.Human));
    DriveIntoStationaryPlayer(world,rules.FriendlyCollision);
   } finally {
    Object.Destroy(root);
    if(File.Exists(path))File.Delete(path);
    if(File.Exists(path+".tmp"))File.Delete(path+".tmp");
   }
   yield return null;
  }
  static void DriveIntoStationaryPlayer(UnityPlaytestSession world,bool shouldBlock) {
   float stationaryZ=world.Observe().Players[4].Position.Z;
   for(int i=0;i<35;i++){world.SubmitInput(0,new PlayerInput{Forward=1,Yaw=0});world.Step(.02f);}
   var state=world.Observe();
   if(shouldBlock)Assert.That(state.Players[0].Position.Z,Is.LessThan(stationaryZ-.5f),"Enabled body collision blocks passage");
   else Assert.That(state.Players[0].Position.Z,Is.GreaterThan(stationaryZ+.2f),"Disabled body collision allows actual passage");
   Assert.That(state.Players[4].Faction,Is.EqualTo(Faction.Human),"Walking into another body must not transform it");
   Assert.That(state.Players[4].Position.Z,Is.EqualTo(stationaryZ).Within(.08f));
  }
 }
}
