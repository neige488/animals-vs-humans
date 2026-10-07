using System;
using System.Collections.Generic;
using NUnit.Framework;
namespace AvH.Tests {
 // Remote bodies play host frames back on a slightly delayed, steadily advancing clock.
 public class InterpolationTests {
  const double Send=.05,Speed=5;
  // A host frame every 50 ms (position = 5 m/s × host time); TCP keeps order but arrivals bunch and stall.
  static List<(double arrival,double host)> Jittered(int seed,double seconds,double jitter) {
   var random=new Random(seed);var result=new List<(double,double)>();double last=0;
   for(double host=0;host<seconds;host+=Send){double arrival=Math.Max(last,host+.02+random.NextDouble()*jitter);result.Add((arrival,host));last=arrival;}
   return result;
  }
  static double Position(InterpolationTimeline<double[]> timeline,double now) {
   Assert.IsTrue(timeline.Sample(now,out var from,out var to,out float t));
   return from[0]+(to[0]-from[0])*t;
  }
  [Test] public void JitteredArrivalsPlayBackSmoothlyAboutOneFrameIntervalBehind() {
   var timeline=new InterpolationTimeline<double[]>();var frames=Jittered(7,8,.045);int next=0;
   double previous=double.NaN,dt=1/60.0,maxLag=0,minLag=double.MaxValue;
   for(double now=0;now<8;now+=dt) {
    while(next<frames.Count&&frames[next].arrival<=now){timeline.Add(frames[next].host,now,new[]{frames[next].host*Speed});next++;}
    if(next==0)continue;
    double position=Position(timeline,now);
    if(now>1) {
     double step=position-previous;
     Assert.GreaterOrEqual(step,-1e-9,"Never moves backwards at "+now);
     Assert.LessOrEqual(step,Speed*dt*1.3,"No catch-up jump at "+now);
     double lag=now-position/Speed;maxLag=Math.Max(maxLag,lag);minLag=Math.Min(minLag,lag);
    }
    previous=position;
   }
   Assert.Less(maxLag,.25,"Playback stays close to the host");Assert.Greater(minLag,.05,"Keeps a small buffer to absorb jitter");
  }
  [Test] public void StarvedTimelineHoldsTheNewestFrameThenResumesWithoutJumping() {
   var timeline=new InterpolationTimeline<double[]>();double now=0,dt=1/60.0,host=0,previous=0;
   for(;now<1;now+=dt){while(host<=now-.02){timeline.Add(host,now,new[]{host*Speed});host+=Send;}if(timeline.Count>0)previous=Position(timeline,now);}
   double held=double.NaN;
   for(double stall=now+.4;now<stall;now+=dt){double position=Position(timeline,now);Assert.GreaterOrEqual(position,previous-1e-9);Assert.LessOrEqual(position,host*Speed);previous=position;held=position;}
   Assert.AreEqual((host-Send)*Speed,held,1e-6,"A stalled stream holds the newest host frame instead of guessing");
   for(double end=now+1;now<end;now+=dt) {
    while(host<=now-.02){timeline.Add(host,now,new[]{host*Speed});host+=Send;}
    double position=Position(timeline,now);
    Assert.GreaterOrEqual(position,previous-1e-9);Assert.LessOrEqual(position-previous,Speed*dt*1.3+1e-9,"Resumes on the same clock instead of jumping");previous=position;
   }
  }
  [Test] public void OldOrDuplicateFramesAreIgnoredAndARestartedHostResynchronises() {
   var timeline=new InterpolationTimeline<double[]>();
   timeline.Add(10,0,new[]{1.0});timeline.Add(10.05,.05,new[]{2.0});timeline.Add(10.05,.06,new[]{99.0});timeline.Add(10.02,.07,new[]{99.0});
   Assert.AreEqual(2.0,timeline.Latest[0]);
   timeline.Add(0,.1,new[]{7.0});
   Assert.AreEqual(7.0,timeline.Latest[0],"A host clock that went far back is a new session");
   Assert.AreEqual(7.0,Position(timeline,.1));
  }
  static NetworkBodyMotion Body(float x,float yaw,LocomotionState motion)=>NetworkBodyMotion.Encode(new WorldPosition(x,1,0),yaw,0,motion);
  [Test] public void BlendedBodyFollowsTheShortTurnAndSnapsAcrossATeleport() {
   var a=Body(0,170,new LocomotionState{VelocityX=2,Grounded=true});var b=Body(1,-170,new LocomotionState{VelocityX=4,Grounded=false});
   var mid=NetworkBodyMotion.Blend(a,b,.5f);
   Assert.AreEqual(.5,mid.Position.X,.01);Assert.AreEqual(180,Math.Abs(mid.Yaw),.01,"Turns through 180, not back through 0");Assert.AreEqual(3,mid.Motion.VelocityX,.01);
   Assert.IsFalse(NetworkBodyMotion.Blend(a,b,.6f).Motion.Grounded);Assert.IsTrue(NetworkBodyMotion.Blend(a,b,.4f).Motion.Grounded);
   var far=NetworkBodyMotion.Blend(a,Body(NetworkBodyMotion.TeleportDistance+1,-170,default),.1f);
   Assert.AreEqual(NetworkBodyMotion.TeleportDistance+1,far.Position.X,.01,"A warp is shown as a warp, never a slide through walls");
  }
  [Test] public void BlendedActionAdvancesWithinAPhaseAndChangesOnlyAtTheNextFrame() {
   var windup=Body(0,0,new LocomotionState{Action=ActionPhase.Windup,ActionProgress=.2f});var later=Body(0,0,new LocomotionState{Action=ActionPhase.Windup,ActionProgress=.6f});
   var swing=Body(0,0,new LocomotionState{Action=ActionPhase.Swing,ActionProgress=.1f});
   var within=NetworkBodyMotion.Blend(windup,later,.5f).Motion;Assert.AreEqual(ActionPhase.Windup,within.Action);Assert.AreEqual(.4,within.ActionProgress,.01);
   var across=NetworkBodyMotion.Blend(later,swing,.9f).Motion;Assert.AreEqual(ActionPhase.Windup,across.Action);Assert.AreEqual(.6,across.ActionProgress,.01);
   Assert.AreEqual(ActionPhase.Swing,NetworkBodyMotion.Blend(later,swing,1).Motion.Action);
  }
 }
}
