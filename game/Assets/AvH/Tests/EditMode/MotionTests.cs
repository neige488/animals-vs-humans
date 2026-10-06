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
 static (int steps,MotionState state) Run(MotionState from,MotionIntent intent,MotionProfile profile,System.Func<MotionState,bool> done,int max=200) {
  var state=from;for(int i=1;i<=max;i++){state=CharacterMotion.Next(state,intent,profile,.01);if(done(state))return (i,state);}return (max+1,state);
 }
 static float Speed(MotionState s)=>(float)System.Math.Sqrt(s.VelocityX*s.VelocityX+s.VelocityZ*s.VelocityZ);
 [Test] public void InertiaRampsToFullSpeedAndBackToRestOverTheConfiguredTime() {
  var s=Session(v=>{v.InertiaSeconds=.2f;v.AirControl=.5f;});var profile=Profile(s,"human-default",Faction.Human);
  var start=Run(new MotionState(),new MotionIntent{DirectionX=1,Grounded=true},profile,m=>Speed(m)>=profile.MaxSpeed-1e-4f);
  Assert.That(start.steps,Is.InRange(19,21),"Ground start reaches max speed after InertiaSeconds");
  var half=CharacterMotion.Next(new MotionState(),new MotionIntent{DirectionX=1,Grounded=true},profile,.1);
  Assert.AreEqual(profile.MaxSpeed*.5f,half.VelocityX,.01f,"Ramp is linear in time");
  var stop=Run(start.state,new MotionIntent{Grounded=true},profile,m=>Speed(m)<1e-4f);
  Assert.That(stop.steps,Is.InRange(19,21),"Ground stop takes InertiaSeconds");
  var air=Run(new MotionState(),new MotionIntent{DirectionX=1},profile,m=>Speed(m)>=profile.MaxSpeed-1e-4f);
  Assert.That(air.steps,Is.InRange(39,41),"Half air control doubles the time to change speed in the air");
  var neverOvershoot=CharacterMotion.Next(start.state,new MotionIntent{DirectionX=1,Grounded=true},profile,5);
  Assert.AreEqual(profile.MaxSpeed,Speed(neverOvershoot),1e-4f);
 }
 [Test] public void ReversingDirectionIsNotSlowerThanStopping() {
  var s=Session(v=>v.InertiaSeconds=.2f);var profile=Profile(s,"human-default",Faction.Human);
  var full=new MotionState{VelocityX=profile.MaxSpeed};
  var stop=Run(full,new MotionIntent{Grounded=true},profile,m=>Speed(m)<1e-4f);
  var turn=Run(full,new MotionIntent{DirectionX=-1,Grounded=true},profile,m=>m.VelocityX<=0);
  Assert.LessOrEqual(turn.steps,stop.steps,"A sharp turn sheds speed at least as fast as a stop");
  var side=Run(full,new MotionIntent{DirectionZ=1,Grounded=true},profile,m=>m.VelocityZ>=profile.MaxSpeed*.9f);
  Assert.Less(side.steps,stop.steps+5,"A 90 degree turn reaches the new heading without a long drift");
 }
}}
