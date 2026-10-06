using System.Linq;
using NUnit.Framework;
namespace AvH.Tests {
public class MotionTests {
 static PlaytestSession Session(System.Action<PlaytestValues> tune=null) {
  var s=new PlaytestSession(1);s.StartSolo("host");
  if(tune!=null){s.BeginSettingsEdit(0);var v=s.ObserveSettings().Edit;tune(v);s.UpdateSettingsEdit(0,v);Assert.IsTrue(s.ApplySettingsNow(0));}
  return s;
 }
 static MotionProfile Profile(PlaytestSession s,string id,Faction faction)=>CharacterMotion.Profile(s.ObserveSettings().Current,new PlayerState{Faction=faction,CharacterId=id});
 [Test] public void ZeroInertiaReachesFullSpeedAndStopsWithinOneStep() {
  var s=Session(v=>v.InertiaSeconds=0);
  foreach(var faction in new[]{Faction.Human,Faction.Animal})foreach(var id in faction==Faction.Human?new[]{"human-default"}:Enumerable.Range(0,AnimalBalance.Count).Select(AnimalBalance.Id).ToArray()) {
   var profile=Profile(s,id,faction);
   foreach(bool grounded in new[]{true,false}) {
    var moving=CharacterMotion.Next(new MotionState(),new MotionIntent{DirectionX=1,Grounded=grounded},profile,.02);
    Assert.AreEqual(profile.MaxSpeed,moving.VelocityX,1e-5,id);Assert.AreEqual(0,moving.VelocityZ,1e-5);
    var reversed=CharacterMotion.Next(moving,new MotionIntent{DirectionZ=-.5f,Grounded=grounded},profile,.02);
    Assert.AreEqual(0,reversed.VelocityX,1e-5);Assert.AreEqual(-.5f*profile.MaxSpeed,reversed.VelocityZ,1e-5);
    var stopped=CharacterMotion.Next(moving,new MotionIntent{Grounded=grounded},profile,.02);
    Assert.AreEqual(0,stopped.VelocityX,1e-5);Assert.AreEqual(0,stopped.VelocityZ,1e-5);
   }
  }
 }
}}
